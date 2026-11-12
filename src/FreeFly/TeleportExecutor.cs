using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using FreeFly.Core;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace FreeFly;

internal sealed class TeleportExecutor
{
    private static readonly MethodInfo? LocalWarp = AccessTools.Method(typeof(Character), "WarpPlayer",
        new[] { typeof(Vector3), typeof(bool) });
    private static readonly bool HasWarpRpc = AccessTools.Method(typeof(Character), "WarpPlayerRPC") != null;
    private readonly ModConfig _config;
    private readonly ManualLogSource _logger;

    public TeleportExecutor(ModConfig config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
    }

    public static bool IsSupported => LocalWarp != null && HasWarpRpc;
    public bool CanMoveOtherPlayers => PhotonNetwork.IsMasterClient;

    public bool IsActionAvailable(TeleportMenuEntry entry, TeleportAction action) =>
        IsSupported && entry.Enabled && TeleportRules.CanExecute(TeleportRules.Resolve(entry.Kind, action), CanMoveOtherPlayers);

    public bool TryExecute(TeleportMenuEntry entry, TeleportAction action)
    {
        Character? local = Character.localCharacter;
        if (local == null || !FreeFlyCharacterUtils.IsUsable(local) || local.warping || !IsActionAvailable(entry, action))
            return false;

        try
        {
            TeleportOperation operation = TeleportRules.Resolve(entry.Kind, action);
            bool toSelf = operation == TeleportOperation.BringPlayerToSelf || operation == TeleportOperation.BringAllToSelf;
            TeleportDestination? destination = toSelf ? TeleportDestination.ForPlayer(local) : entry.Destination;
            if (destination == null || !destination.TryGetPose(out Vector3 position, out Vector3 forward) ||
                (destination.Player != null && !IsConnectedPlayer(destination.Player)))
                return false;

            position += Vector3.up * _config.SafeTeleportVerticalOffset - forward * _config.SafeTeleportBackwardOffset;
            if (!FreeFlyCharacterUtils.IsFinite(position))
                return false;

            // Freeze destination and recipients before the first warp changes the world.
            List<Character> targets = GetTargets(operation, local, entry.Destination?.Player);
            int initiated = 0;
            foreach (Character target in targets)
            {
                try
                {
                    if (!IsUsablePlayer(target) || !TeleportRules.CanMovePlayer(operation, target.data.dead, target.warping))
                        continue;
                    // Recheck authority immediately before execution (the host can change).
                    if (!TeleportRules.CanExecute(operation, CanMoveOtherPlayers))
                        break;
                    Warp(target, position);
                    initiated++;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning($"Could not teleport a player: {exception.Message}");
                }
            }
            return initiated > 0;
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Could not prepare teleport: {exception.Message}");
            return false;
        }
    }

    private static List<Character> GetTargets(TeleportOperation operation, Character local, Character? selectedPlayer)
    {
        List<Character> targets = new();
        switch (operation)
        {
            case TeleportOperation.MoveSelfToDestination:
                targets.Add(local);
                break;
            case TeleportOperation.BringPlayerToSelf:
                if (selectedPlayer != null && selectedPlayer != local)
                    targets.Add(selectedPlayer);
                break;
            case TeleportOperation.MoveAllToDestination:
            case TeleportOperation.BringAllToSelf:
                var players = PlayerHandler.GetAllPlayerCharacters();
                HashSet<Character> seen = new();
                if (players != null)
                {
                    foreach (Character player in players)
                    {
                        if (player != null && seen.Add(player) &&
                            (operation == TeleportOperation.MoveAllToDestination || player != local))
                            targets.Add(player);
                    }
                }
                if (operation == TeleportOperation.MoveAllToDestination && seen.Add(local))
                    targets.Add(local);
                break;
        }
        return targets;
    }

    private static bool IsUsablePlayer(Character player) =>
        player != null && player.data != null && player.refs != null && player.refs.ragdoll != null &&
        player.refs.view != null && IsConnectedPlayer(player);

    private static bool IsConnectedPlayer(Character player)
    {
        if (player == null || player.photonView == null)
            return false;
        if (player == Character.localCharacter)
            return true;
        var owner = player.photonView.Owner;
        return player.IsPlayerControlled && owner != null && !owner.IsInactive &&
               PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.Players.ContainsKey(owner.ActorNumber);
    }

    private static void Warp(Character target, Vector3 position)
    {
        if (PhotonNetwork.IsMasterClient)
        {
            target.photonView.RPC("WarpPlayerRPC", RpcTarget.All, position, true);
            return;
        }
        // Local movement continues to synchronize through the game's character sync.
        if (target != Character.localCharacter || !target.photonView.IsMine || LocalWarp == null)
            throw new InvalidOperationException("Only the host can teleport other players.");
        LocalWarp.Invoke(target, new object[] { position, true });
    }
}
