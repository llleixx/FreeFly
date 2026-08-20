using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using FreeFly.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeFly;

internal sealed class FreeFlyController
{
    private readonly ModConfig _config;
    private readonly ManualLogSource _logger;
    private readonly Dictionary<Rigidbody, bool> _originalGravity = new();
    private readonly List<TeleportOption> _teleportOptions = new();

    private PatchCapabilities _capabilities;
    private InputAction? _controllerChordModifierAction;
    private InputAction? _controllerFlightToggleAction;
    private InputAction? _controllerTeleportMenuToggleAction;
    private InputAction? _speedUpControllerAction;
    private InputAction? _slowDownControllerAction;
    private string _controllerBindingSignature = string.Empty;
    private GameObject? _menuInputBlockerObject;
    private FreeFlyMenuWindow? _menuInputBlockerWindow;
    private Vector2 _menuScrollPosition;
    private Transform? _cachedPeakFlareDestination;
    private Transform? _cachedPeakPortalDestination;
    private Transform? _cachedSoulPillarDestination;
    private float _nextPeakFlareSearchTime;
    private float _nextPeakPortalSearchTime;
    private float _nextSoulPillarSearchTime;
    private Character? _flightCharacter;
    private bool _flightActive;
    private bool _menuOpen;
    private int _selectedTarget;

    private readonly struct TeleportOption
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

    public FreeFlyController(ModConfig config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
    }

    public void SetCapabilities(PatchCapabilities capabilities) => _capabilities = capabilities;

    public void TickUpdate()
    {
        EnsureControllerActions();

        Character? local = Character.localCharacter;
        if (!_config.Enabled.Value || !_capabilities.FlightPatch || local == null || local.data == null)
        {
            StopFlight("feature unavailable");
            CloseMenu();
            return;
        }

        if (_flightActive && (_flightCharacter != local || local.warping))
            StopFlight("character changed or PEAK warp started");

        bool menuToggle = Input.GetKeyDown(_config.TeleportMenuShortcut.Value) ||
                          ControllerTeleportMenuPressed();
        bool flightToggle = Input.GetKeyDown(_config.ToggleFlightShortcut.Value) ||
                            ControllerFlightPressed();

        if (_menuOpen)
        {
            if (menuToggle)
            {
                CloseMenu();
                return;
            }

            if (flightToggle)
                return;

            RefreshTargets();
            HandleMenuInput();
            return;
        }

        if (menuToggle && CanUseInput())
        {
            OpenMenu();
            return;
        }

        if (flightToggle && CanUseInput())
        {
            if (_flightActive)
                StopFlight("toggle pressed");
            else
                StartFlight(local);
        }
    }

    public void ApplyFlightPhysics(CharacterMovement movement)
    {
        if (!_flightActive || _flightCharacter == null)
            return;

        if (movement != _flightCharacter.refs.movement)
            return;

        Character local = _flightCharacter;
        if (!IsUsable(local) || local.warping)
        {
            StopFlight("character became unavailable");
            return;
        }

        CharacterRagdoll ragdoll = local.refs.ragdoll;
        foreach (Bodypart part in ragdoll.partList)
        {
            if (part?.Rig == null)
                continue;
            part.Rig.useGravity = false;
        }

        Vector3 movementDirection = GetMovementDirection(local);
        float speed = FreeFlyMath.ApplySpeedModifiers(
            _config.SafeBaseSpeed,
            SpeedUpHeld(),
            SlowDownHeld(),
            _config.SafeSpeedUpMultiplier,
            _config.SafeSlowDownMultiplier);

        Vector3 delta = movementDirection * speed * Time.fixedDeltaTime;
        Vector3 velocity = movementDirection * speed;
        ragdoll.HaltBodyVelocity();
        foreach (Bodypart part in ragdoll.partList)
        {
            if (part?.Rig == null)
                continue;

            if (part.Rig.isKinematic)
            {
                if (IsFinite(delta) && delta.sqrMagnitude > 0f)
                    part.Rig.position += delta;
            }
            else
            {
                part.Rig.linearVelocity = velocity;
            }
        }

        local.data.isGrounded = true;
        local.data.sinceGrounded = 0f;
        local.data.sinceJump = 0f;
    }

    public void DrawMenu()
    {
        if (!_menuOpen)
            return;

        float width = Mathf.Min(720f, Screen.width - 40f);
        float height = Mathf.Min(620f, Screen.height - 40f);
        Rect area = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUIStyle boxStyle = new(GUI.skin.box)
        {
            fontSize = 28
        };
        GUIStyle labelStyle = new(GUI.skin.label)
        {
            fontSize = 20,
            wordWrap = true
        };
        GUIStyle buttonStyle = new(GUI.skin.button)
        {
            fontSize = 22
        };

        GUI.Box(area, "FreeFly - Teleport", boxStyle);
        GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 64f, area.width - 48f, area.height - 88f));
        GUILayout.Label("Select a destination. Dead teammates use their corpse position.", labelStyle);
        GUILayout.Space(12f);

        if (_teleportOptions.Count == 0)
        {
            GUILayout.Label("No teleport destination is available.", labelStyle);
        }
        else
        {
            _menuScrollPosition = GUILayout.BeginScrollView(_menuScrollPosition);
            for (int i = 0; i < _teleportOptions.Count; i++)
            {
                TeleportOption option = _teleportOptions[i];
                string status = option.Enabled ? string.Empty : " [Generating...]";
                string label = $"{(i == _selectedTarget ? "> " : "  ")}{option.Label}{status}";
                bool wasEnabled = GUI.enabled;
                GUI.enabled = option.Enabled;
                if (GUILayout.Button(label, buttonStyle, GUILayout.Height(52f)))
                {
                    _selectedTarget = i;
                    TeleportToSelected();
                }
                GUI.enabled = wasEnabled;
            }
            GUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Up/Down or D-pad: select    Enter/A: teleport    Escape/B: cancel", labelStyle);
        GUILayout.EndArea();
    }

    public void Shutdown()
    {
        CloseMenu();
        StopFlight("plugin destroyed");
        DisposeControllerActions();
    }

    private void EnsureControllerActions()
    {
        string modifierPath = NormalizeBindingPath(_config.ControllerChordModifierPath.Value);
        string flightPath = NormalizeBindingPath(_config.ControllerFlightTogglePath.Value);
        string menuPath = NormalizeBindingPath(_config.ControllerTeleportMenuTogglePath.Value);
        string speedUpPath = NormalizeBindingPath(_config.SpeedUpControllerPath.Value);
        string slowDownPath = NormalizeBindingPath(_config.SlowDownControllerPath.Value);
        string signature = $"{modifierPath}\n{flightPath}\n{menuPath}\n{speedUpPath}\n{slowDownPath}";
        if (signature == _controllerBindingSignature)
            return;

        DisposeControllerActions();
        _controllerBindingSignature = signature;

        if (modifierPath.Length > 0)
        {
            _controllerChordModifierAction = CreateButtonAction(
                "FreeFly Controller Chord Modifier",
                modifierPath,
                "modifier");
        }

        _controllerFlightToggleAction = CreateToggleAction(
            "FreeFly Controller Flight Toggle",
            modifierPath,
            flightPath,
            "flight");
        _controllerTeleportMenuToggleAction = CreateToggleAction(
            "FreeFly Controller Teleport Menu Toggle",
            modifierPath,
            menuPath,
            "teleport menu");

        if (speedUpPath.Length > 0)
        {
            _speedUpControllerAction = CreateButtonAction(
                "FreeFly Controller Speed Up",
                speedUpPath,
                "speed-up");
        }

        if (slowDownPath.Length > 0)
        {
            _slowDownControllerAction = CreateButtonAction(
                "FreeFly Controller Slow Down",
                slowDownPath,
                "slow-down");
        }
    }

    private InputAction? CreateToggleAction(string name, string modifierPath, string actionPath, string description)
    {
        if (actionPath.Length == 0)
            return null;

        try
        {
            InputAction action = new(name, InputActionType.Button);
            if (modifierPath.Length == 0)
            {
                action.AddBinding(actionPath);
            }
            else
            {
                action.AddCompositeBinding("ButtonWithOneModifier")
                    .With("modifier", modifierPath)
                    .With("button", actionPath);
            }

            action.Enable();
            return action;
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Controller {description} binding is invalid and has been disabled: {exception.Message}");
            return null;
        }
    }

    private InputAction? CreateButtonAction(string name, string path, string description)
    {
        try
        {
            InputAction action = new(name, InputActionType.Button);
            action.AddBinding(path);
            action.Enable();
            return action;
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Controller {description} binding is invalid and has been disabled: {exception.Message}");
            return null;
        }
    }

    private void DisposeControllerActions()
    {
        DisposeAction(ref _controllerChordModifierAction);
        DisposeAction(ref _controllerFlightToggleAction);
        DisposeAction(ref _controllerTeleportMenuToggleAction);
        DisposeAction(ref _speedUpControllerAction);
        DisposeAction(ref _slowDownControllerAction);
    }

    private static void DisposeAction(ref InputAction? action)
    {
        if (action == null)
            return;

        action.Disable();
        action.Dispose();
        action = null;
    }

    private void StartFlight(Character local)
    {
        if (!IsUsable(local) || local.refs.ragdoll == null)
            return;

        _flightCharacter = local;
        _originalGravity.Clear();
        foreach (Bodypart part in local.refs.ragdoll.partList)
        {
            if (part?.Rig != null)
                _originalGravity[part.Rig] = part.Rig.useGravity;
        }

        local.refs.ragdoll.ToggleCollision(false);
        _flightActive = true;
        _logger.LogInfo("Free flight enabled.");
    }

    private void StopFlight(string reason)
    {
        if (!_flightActive && _flightCharacter == null)
            return;

        Character? character = _flightCharacter;
        if (character != null && character.refs.ragdoll != null)
        {
            character.refs.ragdoll.ToggleCollision(true);
            foreach (Bodypart part in character.refs.ragdoll.partList)
            {
                if (part?.Rig != null && _originalGravity.TryGetValue(part.Rig, out bool gravity))
                    part.Rig.useGravity = gravity;
            }
            character.refs.ragdoll.HaltBodyVelocity();
        }

        _originalGravity.Clear();
        _flightCharacter = null;
        bool wasActive = _flightActive;
        _flightActive = false;
        if (wasActive)
            _logger.LogDebug($"Free flight disabled: {reason}.");
    }

    private bool IsUsable(Character character)
    {
        return character == Character.localCharacter && character.data != null &&
               character.refs.ragdoll != null && character.refs.view != null;
    }

    private bool CanUseInput()
    {
        GUIManager? gui = GUIManager.instance;
        return Time.timeScale > 0f && gui != null && !gui.windowBlockingInput && !gui.wheelActive;
    }

    private Vector3 GetMovementDirection(Character local)
    {
        Vector2 input = GetMovementInput(local);
        Vector3 forward = local.data.lookDirection_Flat;
        Vector3 right = local.data.lookDirection_Right;
        Vector3 up = Vector3.up;
        Vector3 direction = forward * input.y + right * input.x;
        bool chordModifierHeld = ControllerChordModifierHeld();
        bool jumpHeld = _menuOpen
            ? IsActionHeld(CharacterInput.action_jump)
            : local.input.jumpIsPressed;
        bool crouchHeld = _menuOpen
            ? IsActionHeld(CharacterInput.action_crouch)
            : local.input.crouchIsPressed;
        float vertical = chordModifierHeld
            ? 0f
            : (jumpHeld ? 1f : 0f);
        vertical -= chordModifierHeld
            ? 0f
            : (crouchHeld ? 1f : 0f);
        direction += up * vertical;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private Vector2 GetMovementInput(Character local)
    {
        if (!_menuOpen)
            return Vector2.ClampMagnitude(local.input.movementInput, 1f);

        Vector2 input = CharacterInput.action_move?.ReadValue<Vector2>() ?? Vector2.zero;
        if (IsActionHeld(CharacterInput.action_moveForward))
            input += Vector2.up;
        if (IsActionHeld(CharacterInput.action_moveBackward))
            input -= Vector2.up;
        if (IsActionHeld(CharacterInput.action_moveRight))
            input += Vector2.right;
        if (IsActionHeld(CharacterInput.action_moveLeft))
            input -= Vector2.right;
        return Vector2.ClampMagnitude(input, 1f);
    }

    private static bool IsActionHeld(InputAction? action) => action?.IsPressed() == true;

    private void OpenMenu()
    {
        RefreshTargets();
        _selectedTarget = Mathf.Clamp(_selectedTarget, 0, Mathf.Max(0, _teleportOptions.Count - 1));
        _menuScrollPosition = Vector2.zero;
        _menuOpen = true;
        CreateMenuInputBlocker();
    }

    private void CloseMenu()
    {
        _menuOpen = false;
        DestroyMenuInputBlocker();
    }

    private void CreateMenuInputBlocker()
    {
        if (_menuInputBlockerObject != null)
            return;

        _menuInputBlockerObject = new GameObject("FreeFly Teleport Menu Input Blocker");
        _menuInputBlockerWindow = _menuInputBlockerObject.AddComponent<FreeFlyMenuWindow>();
        if (!MenuWindow.AllActiveWindows.Contains(_menuInputBlockerWindow))
            MenuWindow.AllActiveWindows.Add(_menuInputBlockerWindow);
    }

    private void DestroyMenuInputBlocker()
    {
        if (_menuInputBlockerWindow != null)
            MenuWindow.AllActiveWindows.Remove(_menuInputBlockerWindow);

        if (_menuInputBlockerObject != null)
            UnityEngine.Object.Destroy(_menuInputBlockerObject);

        _menuInputBlockerWindow = null;
        _menuInputBlockerObject = null;
    }

    private void RefreshTargets()
    {
        Character? local = Character.localCharacter;
        _teleportOptions.Clear();
        if (local == null)
            return;

        AddStageDestinations();

        List<TeleportOption> teammateOptions = new();
        foreach (Character target in PlayerHandler.GetAllPlayerCharacters())
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

        teammateOptions.Sort((left, right) => string.Compare(left.Label, right.Label, StringComparison.OrdinalIgnoreCase));
        _teleportOptions.AddRange(teammateOptions);
        _selectedTarget = Mathf.Clamp(_selectedTarget, 0, Mathf.Max(0, _teleportOptions.Count - 1));
    }

    private void AddStageDestinations()
    {
        try
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
        catch (Exception exception)
        {
            _logger.LogDebug($"Stage teleport destinations are not ready: {exception.Message}");
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
        _cachedSoulPillarDestination = FindDestinationInRoots<Peak.ScoutmasterSoulPillar>(
            GetNadirSegmentRoot());
        return _cachedSoulPillarDestination;
    }

    private Transform? GetPeakPortalDestination()
    {
        if (_cachedPeakPortalDestination != null)
            return _cachedPeakPortalDestination;
        if (Time.unscaledTime < _nextPeakPortalSearchTime)
            return null;

        _nextPeakPortalSearchTime = Time.unscaledTime + 1f;
        _cachedPeakPortalDestination = FindDestinationInRoots<Peak.PeakGatePortal>(
            GetNadirSegmentRoot());
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
        if (IsFinite(position))
            _teleportOptions.Add(new TeleportOption(label, position, anchor, null, enabled));
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

    private static string GetBiomeDisplayName(Biome.BiomeType biome)
    {
        return biome switch
        {
            Biome.BiomeType.Swamp => "Gloom",
            Biome.BiomeType.Volcano => "Caldera",
            Biome.BiomeType.Peak => "PEAK",
            _ => biome.ToString()
        };
    }

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
                    if (IsFinite(position) && position.y > highestY)
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

    private void HandleMenuInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || ControllerCancelPressed())
        {
            CloseMenu();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || ControllerUpPressed())
            _selectedTarget = Mathf.Max(0, _selectedTarget - 1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || ControllerDownPressed())
            _selectedTarget = Mathf.Min(Mathf.Max(0, _teleportOptions.Count - 1), _selectedTarget + 1);
        if (Input.GetKeyDown(KeyCode.Return) || ControllerConfirmPressed())
            TeleportToSelected();
    }

    private void TeleportToSelected()
    {
        Character? local = Character.localCharacter;
        if (!_capabilities.TeleportPatch || local == null || !IsUsable(local) || local.warping ||
            _selectedTarget < 0 || _selectedTarget >= _teleportOptions.Count)
            return;

        TeleportOption option = _teleportOptions[_selectedTarget];
        if (!option.Enabled)
            return;

        Vector3 position = option.Position;
        Vector3 forward = option.Anchor != null ? option.Anchor.forward : Vector3.forward;
        if (option.Character != null)
        {
            Character target = option.Character;
            if (target.data == null)
                return;
            position = target.data.dead ? target.GetSpectatePosition() : target.Center;
            forward = target.transform.forward;
        }

        position += Vector3.up * _config.SafeTeleportVerticalOffset;
        position -= forward * _config.SafeTeleportBackwardOffset;
        if (!IsFinite(position))
            return;

        local.photonView.RPC("WarpPlayerRPC", RpcTarget.All, position, true);
        CloseMenu();
    }

    private bool SpeedUpHeld() => IsKeyHeld(_config.SpeedUpShortcut.Value) ||
                                  (!ControllerChordModifierHeld() &&
                                   _speedUpControllerAction?.IsPressed() == true);
    private bool SlowDownHeld() => IsKeyHeld(_config.SlowDownShortcut.Value) ||
                                   (!ControllerChordModifierHeld() &&
                                    _slowDownControllerAction?.IsPressed() == true);

    private static bool IsKeyHeld(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKey(key);
    }

    private bool ControllerChordModifierHeld() => _controllerChordModifierAction?.IsPressed() == true;
    private bool ControllerFlightPressed() => _controllerFlightToggleAction?.WasPressedThisFrame() == true;
    private bool ControllerTeleportMenuPressed() => _controllerTeleportMenuToggleAction?.WasPressedThisFrame() == true;
    private static bool ControllerConfirmPressed() => Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
    private static bool ControllerCancelPressed() => Gamepad.current?.buttonEast.wasPressedThisFrame == true;
    private static bool ControllerUpPressed() => Gamepad.current?.dpad.up.wasPressedThisFrame == true;
    private static bool ControllerDownPressed() => Gamepad.current?.dpad.down.wasPressedThisFrame == true;

    private static bool IsFinite(Vector3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static string NormalizeBindingPath(string? path)
    {
        string normalized = path?.Trim() ?? string.Empty;
        return string.Equals(normalized, "None", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized;
    }
}

internal sealed class FreeFlyMenuWindow : MenuWindow
{
    public override bool selectOnOpen => false;
}
