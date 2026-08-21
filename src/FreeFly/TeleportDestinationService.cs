using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using FreeFly.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeFly;

internal readonly struct TeleportOption
{
    public TeleportOption(string label, Vector3 position, Transform? anchor, Character? character, bool enabled = true)
    {
        Label = label;
        Position = position;
        Anchor = anchor;
        Character = character;
        Enabled = enabled;
    }

    public string Label { get; }
    public Vector3 Position { get; }
    public Transform? Anchor { get; }
    public Character? Character { get; }
    public bool Enabled { get; }
}

internal sealed class TeleportDestinationService
{
    private const float RefreshInterval = 0.5f;

    private readonly ModConfig _config;
    private readonly ManualLogSource _logger;
    private readonly List<TeleportOption> _options = new();
    private Transform? _cachedPeakFlareDestination;
    private Transform? _cachedPeakPortalDestination;
    private Transform? _cachedSoulPillarDestination;
    private float _nextPeakFlareSearchTime;
    private float _nextPeakPortalSearchTime;
    private float _nextSoulPillarSearchTime;
    private float _nextRefreshTime;
    private int _cachedSceneHandle = int.MinValue;
    private Segment? _cachedSegment;
    private bool _hasCachedSegment;
    private PatchCapabilities _capabilities;

    public TeleportDestinationService(ModConfig config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
    }

    public IReadOnlyList<TeleportOption> Options => _options;

    public void SetCapabilities(PatchCapabilities capabilities) => _capabilities = capabilities;

    public void RefreshIfNeeded(bool force = false)
    {
        ObserveWorld();
        if (!force && Time.unscaledTime < _nextRefreshTime)
            return;

        _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        RefreshTargets();
    }

    public bool TryTeleport(int selectedIndex)
    {
        Character? local = Character.localCharacter;
        if (!_capabilities.TeleportPatch || local == null || !FreeFlyCharacterUtils.IsUsable(local) || local.warping ||
            selectedIndex < 0 || selectedIndex >= _options.Count)
            return false;

        TeleportOption option = _options[selectedIndex];
        if (!option.Enabled)
            return false;

        Vector3 position = option.Position;
        Vector3 forward = option.Anchor != null ? option.Anchor.forward : Vector3.forward;
        if (option.Character != null)
        {
            Character target = option.Character;
            if (target == null || target.data == null || target.transform == null)
                return false;

            position = target.data.dead ? target.GetSpectatePosition() : target.Center;
            forward = target.transform.forward;
        }

        position += Vector3.up * _config.SafeTeleportVerticalOffset;
        position -= forward * _config.SafeTeleportBackwardOffset;
        if (!FreeFlyCharacterUtils.IsFinite(position))
            return false;

        local.photonView.RPC("WarpPlayerRPC", RpcTarget.All, position, true);
        return true;
    }

    private void ObserveWorld()
    {
        int sceneHandle = SceneManager.GetActiveScene().handle;
        bool hasSegment = false;
        Segment? segment = null;
        try
        {
            if (MapHandler.ExistsAndInitialized)
            {
                hasSegment = true;
                segment = MapHandler.CurrentSegmentNumber;
            }
        }
        catch
        {
            // PEAK can expose an incomplete map while a scene is changing.
        }

        bool segmentChanged = hasSegment != _hasCachedSegment ||
                              (hasSegment && segment != _cachedSegment);
        if (sceneHandle == _cachedSceneHandle && !segmentChanged)
            return;

        _cachedSceneHandle = sceneHandle;
        _cachedSegment = segment;
        _hasCachedSegment = hasSegment;
        _cachedPeakFlareDestination = null;
        _cachedPeakPortalDestination = null;
        _cachedSoulPillarDestination = null;
        _nextPeakFlareSearchTime = 0f;
        _nextPeakPortalSearchTime = 0f;
        _nextSoulPillarSearchTime = 0f;
        _nextRefreshTime = 0f;
    }

    private void RefreshTargets()
    {
        _options.Clear();
        Character? local = Character.localCharacter;
        if (local == null)
            return;

        try
        {
            AddStageDestinations();
        }
        catch (Exception exception)
        {
            _logger.LogDebug($"Stage teleport destinations are not ready: {exception.Message}");
        }

        try
        {
            List<TeleportOption> teammateOptions = new();
            var players = PlayerHandler.GetAllPlayerCharacters();
            if (players != null)
            {
                foreach (Character target in players)
                {
                    if (target == null || target == local || target.data == null || target.refs == null)
                        continue;

                    string state = target.data.dead ? "Dead" : target.data.fullyPassedOut ? "Passed out" : "Alive";
                    teammateOptions.Add(new TeleportOption(
                        $"Teammate: {target.characterName} [{state}]",
                        Vector3.zero,
                        null,
                        target));
                }
            }

            teammateOptions.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
            _options.AddRange(teammateOptions);
        }
        catch (Exception exception)
        {
            _logger.LogDebug($"Teammate teleport destinations are not ready: {exception.Message}");
        }
    }

    private void AddStageDestinations()
    {
        if (!MapHandler.ExistsAndInitialized)
            return;

        Segment segment = MapHandler.CurrentSegmentNumber;
        if (segment == Segment.Void)
        {
            AddNadirDestinations();
            return;
        }

        bool isFinalStage = segment == Segment.TheKiln || segment == Segment.Peak;
        int stageNumber = isFinalStage ? 5 : (int)segment + 1;
        bool stageReady = IsStageGenerated(segment);
        Campfire? startCampfire = MapHandler.PreviousCampfire;
        Transform? startAnchor = segment == Segment.Beach
            ? SpawnPoint.LocalSpawnPoint?.transform
            : startCampfire?.transform;

        if (startAnchor != null)
        {
            Vector3 position = startCampfire != null ? startCampfire.Center() : startAnchor.position;
            AddStageDestination($"Stage {stageNumber} start ({GetStageStartName(segment)})",
                position, startAnchor, stageReady);
        }

        if (isFinalStage)
        {
            Transform? peakAnchor = GetPeakDestination();
            if (peakAnchor != null)
                AddStageDestination("Stage 5 end (PEAK)", peakAnchor.position, peakAnchor, stageReady);
            return;
        }

        Campfire? endCampfire = MapHandler.CurrentCampfire;
        if (endCampfire != null)
        {
            AddStageDestination($"Stage {stageNumber} end ({GetStageEndName(segment)})",
                endCampfire.Center(), endCampfire.transform, stageReady);
        }
    }

    private void AddNadirDestinations()
    {
        bool soulFreed = Peak.VoidBiome.SoulFreedStatus == 1;
        if (soulFreed)
        {
            Transform? soulAnchor = GetSoulPillarDestination();
            if (soulAnchor != null)
                AddStageDestination("Nadir waypoint (Scoutmaster Soul)", soulAnchor.position, soulAnchor);

            Transform? portalAnchor = GetPeakPortalDestination();
            if (portalAnchor != null)
                AddStageDestination("Nadir end (The Gate)", portalAnchor.position, portalAnchor);
            return;
        }

        Transform? startAnchor = null;
        try
        {
            startAnchor = MapHandler.CurrentBaseCampSpawnPoint;
        }
        catch
        {
            // The local Nadir spawn point may not be assigned during the transition into the scene.
        }

        if (startAnchor != null)
            AddStageDestination("Nadir start (Spawn)", startAnchor.position, startAnchor);

        Transform? endAnchor = GetSoulPillarDestination();
        if (endAnchor != null)
            AddStageDestination("Nadir waypoint (Scoutmaster Soul)", endAnchor.position, endAnchor);
    }

    private Transform? GetSoulPillarDestination()
    {
        if (_cachedSoulPillarDestination != null)
            return _cachedSoulPillarDestination;
        if (Time.unscaledTime < _nextSoulPillarSearchTime)
            return null;

        _nextSoulPillarSearchTime = Time.unscaledTime + 1f;
        _cachedSoulPillarDestination = FindDestinationInRoots<Peak.ScoutmasterSoulPillar>(GetNadirSegmentRoot());
        return _cachedSoulPillarDestination;
    }

    private Transform? GetPeakPortalDestination()
    {
        if (_cachedPeakPortalDestination != null)
            return _cachedPeakPortalDestination;
        if (Time.unscaledTime < _nextPeakPortalSearchTime)
            return null;

        _nextPeakPortalSearchTime = Time.unscaledTime + 1f;
        _cachedPeakPortalDestination = FindDestinationInRoots<Peak.PeakGatePortal>(GetNadirSegmentRoot());
        return _cachedPeakPortalDestination;
    }

    private static Transform? FindDestinationInRoots<T>(params GameObject?[] roots) where T : Component
    {
        Transform? fallback = null;
        foreach (GameObject? root in roots)
        {
            if (root == null)
                continue;

            foreach (T component in root.GetComponentsInChildren<T>(includeInactive: true))
            {
                if (component == null || component.transform == null)
                    continue;

                fallback ??= component.transform;
                if (component.gameObject.activeInHierarchy)
                    return component.transform;
            }
        }

        return fallback;
    }

    private static GameObject? GetNadirSegmentRoot()
    {
        try
        {
            return Peak.VoidBiome.instance?.segment?.segmentParent;
        }
        catch
        {
            return null;
        }
    }

    private static GameObject? GetPeakHandlerRoot()
    {
        try
        {
            return PeakHandler.Instance?.gameObject;
        }
        catch
        {
            return null;
        }
    }

    private void AddStageDestination(string label, Vector3 position, Transform anchor, bool enabled = true)
    {
        if (FreeFlyCharacterUtils.IsFinite(position))
            _options.Add(new TeleportOption(label, position, anchor, null, enabled));
    }

    private static bool IsStageGenerated(Segment segment)
    {
        if (segment == Segment.Beach || segment == Segment.Void)
            return true;

        try
        {
            if (!MapHandler.CurrentMapSegment.segmentParent.activeInHierarchy)
                return false;

            int segmentIndex = segment == Segment.Peak ? (int)Segment.TheKiln : (int)segment;
            MountainProgressHandler? progress = MountainProgressHandler.Instance;
            return progress != null && progress.maxProgressPointReached >= segmentIndex;
        }
        catch
        {
            return false;
        }
    }

    private static string GetStageStartName(Segment segment)
    {
        if (segment == Segment.Beach)
            return "Spawn";

        try
        {
            return GetBiomeDisplayName(MapHandler.GetBiomeForSegment((int)segment - 1));
        }
        catch
        {
            return "Previous campfire";
        }
    }

    private static string GetStageEndName(Segment segment)
    {
        try
        {
            return GetBiomeDisplayName(MapHandler.GetBiomeForSegment((int)segment));
        }
        catch
        {
            return "Current campfire";
        }
    }

    private static string GetBiomeDisplayName(Biome.BiomeType biome) => biome switch
    {
        Biome.BiomeType.Swamp => "Gloom",
        Biome.BiomeType.Volcano => "Caldera",
        Biome.BiomeType.Peak => "PEAK",
        _ => biome.ToString()
    };

    private Transform? GetPeakDestination()
    {
        try
        {
            if (_cachedPeakFlareDestination == null && Time.unscaledTime >= _nextPeakFlareSearchTime)
            {
                _nextPeakFlareSearchTime = Time.unscaledTime + 1f;
                GameObject? peakRoot = GetPeakHandlerRoot();
                Peak.EndgameFlareSpawner[] flareSpawners = peakRoot != null
                    ? peakRoot.GetComponentsInChildren<Peak.EndgameFlareSpawner>(includeInactive: true)
                    : Array.Empty<Peak.EndgameFlareSpawner>();
                float highestY = float.MinValue;
                foreach (Peak.EndgameFlareSpawner flareSpawner in flareSpawners)
                {
                    if (flareSpawner == null || flareSpawner.transform == null)
                        continue;

                    Vector3 position = flareSpawner.transform.position;
                    if (FreeFlyCharacterUtils.IsFinite(position) && position.y > highestY)
                    {
                        highestY = position.y;
                        _cachedPeakFlareDestination = flareSpawner.transform;
                    }
                }
            }

            if (_cachedPeakFlareDestination != null)
                return _cachedPeakFlareDestination;

            PeakHandler? peakHandler = PeakHandler.Instance;
            if (peakHandler != null && peakHandler.flareBox != null)
                return peakHandler.flareBox.transform;

            MountainProgressHandler? progress = MountainProgressHandler.Instance;
            if (progress == null || progress.progressPoints == null || progress.progressPoints.Length == 0)
                return null;

            return progress.progressPoints[progress.progressPoints.Length - 1]?.transform;
        }
        catch
        {
            return null;
        }
    }
}
