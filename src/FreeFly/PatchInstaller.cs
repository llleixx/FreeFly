using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace FreeFly;

internal readonly struct PatchCapabilities
{
    public PatchCapabilities(bool flightPatch, bool teleportPatch)
    {
        FlightPatch = flightPatch;
        TeleportPatch = teleportPatch;
    }

    public bool FlightPatch { get; }
    public bool TeleportPatch { get; }
}

internal static class PatchInstaller
{
    public static PatchCapabilities Install(Harmony harmony, ManualLogSource logger)
    {
        bool flight = TryPatch(
            harmony,
            AccessTools.Method(typeof(CharacterMovement), "FixedUpdate"),
            postfix: AccessTools.Method(typeof(PatchCallbacks), nameof(PatchCallbacks.CharacterMovementFixedPostfix)),
            "CharacterMovement.FixedUpdate",
            logger);

        bool teleport = AccessTools.Method(typeof(Character), "WarpPlayerRPC") != null;
        if (!teleport)
            logger.LogError("PEAK's Character.WarpPlayerRPC was not found; teleport is disabled.");

        return new PatchCapabilities(flight, teleport);
    }

    private static bool TryPatch(
        Harmony harmony,
        MethodInfo? original,
        MethodInfo? postfix,
        string description,
        ManualLogSource logger)
    {
        if (original == null || postfix == null)
        {
            logger.LogError($"Required patch target is missing: {description}.");
            return false;
        }

        try
        {
            harmony.Patch(original, postfix: new HarmonyMethod(postfix));
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError($"Failed to patch {description}: {exception}");
            return false;
        }
    }
}

internal static class PatchCallbacks
{
    public static void CharacterMovementFixedPostfix(CharacterMovement __instance)
    {
        Plugin.Instance?.Controller.ApplyFlightPhysics(__instance);
    }
}
