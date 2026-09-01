using BepInEx.Configuration;
using UnityEngine;

namespace FreeFly;

internal sealed class ModConfig
{
    private const float DefaultBaseSpeed = 100f;
    private const float DefaultSpeedUpMultiplier = 2f;
    private const float DefaultSlowDownMultiplier = 0.2f;
    private const float DefaultTeleportVerticalOffset = 2f;
    private const float DefaultTeleportBackwardOffset = 1.5f;

    public ModConfig(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true,
            "Enable local free flight and teammate teleport.");
        ToggleFlightKeyboardPath = config.Bind("Controls", "Toggle Flight Keyboard Path", "<Keyboard>/f6",
            "Unity Input System control path used to toggle free flight with a keyboard. Leave empty to disable.");
        TeleportMenuKeyboardPath = config.Bind("Controls", "Teleport Menu Keyboard Path", "<Keyboard>/f7",
            "Unity Input System control path used to open the teammate teleport menu with a keyboard. Leave empty to disable.");
        ControllerChordModifierPath = config.Bind("Controls", "Controller Chord Modifier Path",
            "<Gamepad>/select",
            "Controller button held as the optional modifier for the flight and teleport menu shortcuts. Leave empty for single-button mode. Common paths: <Gamepad>/select = View/Share; <Gamepad>/start = Menu/Options; <Gamepad>/leftShoulder = LB/L1; <Gamepad>/rightShoulder = RB/R1; <Gamepad>/buttonSouth = A/Cross; <Gamepad>/buttonEast = B/Circle; <Gamepad>/buttonWest = X/Square; <Gamepad>/buttonNorth = Y/Triangle; <Gamepad>/leftTrigger = LT/L2; <Gamepad>/rightTrigger = RT/R2; <Gamepad>/leftStickPress and <Gamepad>/rightStickPress; <Gamepad>/dpad/up, <Gamepad>/dpad/down, <Gamepad>/dpad/left, and <Gamepad>/dpad/right. These semantic paths require a device recognized as Gamepad.");
        ControllerFlightTogglePath = config.Bind("Controls", "Controller Flight Toggle Path",
            "<Gamepad>/leftShoulder",
            "Controller button pressed with the optional modifier to toggle free flight. Default: <Gamepad>/leftShoulder (Xbox LB / PlayStation L1). Leave empty to disable.");
        ControllerTeleportMenuTogglePath = config.Bind("Controls", "Controller Teleport Menu Toggle Path",
            "<Gamepad>/rightShoulder",
            "Controller button pressed with the optional modifier to toggle the teleport menu. Default: <Gamepad>/rightShoulder (Xbox RB / PlayStation R1). Leave empty to disable.");
        SpeedUpKeyboardPath = config.Bind("Controls", "Speed Up Keyboard Path", "<Keyboard>/leftShift",
            "Unity Input System control path held to temporarily increase flight speed with a keyboard. Leave empty to disable.");
        SlowDownKeyboardPath = config.Bind("Controls", "Slow Down Keyboard Path", "<Keyboard>/leftAlt",
            "Unity Input System control path held to temporarily decrease flight speed with a keyboard. Leave empty to disable.");
        SpeedUpControllerPath = config.Bind("Controls", "Speed Up Controller Path", "<Gamepad>/rightShoulder",
            "Unity Input System path held to temporarily increase flight speed. Default: <Gamepad>/rightShoulder (Xbox RB / PlayStation R1). Leave empty to disable.");
        SlowDownControllerPath = config.Bind("Controls", "Slow Down Controller Path", "<Gamepad>/leftShoulder",
            "Unity Input System path held to temporarily decrease flight speed. Default: <Gamepad>/leftShoulder (Xbox LB / PlayStation L1). Leave empty to disable.");
        BaseSpeed = config.Bind("Movement", "Base Speed", DefaultBaseSpeed,
            new ConfigDescription("Base flight speed in meters per second.",
                new AcceptableValueRange<float>(1f, 1000f)));
        SpeedUpMultiplier = config.Bind("Movement", "Speed Up Multiplier", DefaultSpeedUpMultiplier,
            new ConfigDescription("Multiplier while the configured speed-up key or controller path is held.",
                new AcceptableValueRange<float>(1f, 10f)));
        SlowDownMultiplier = config.Bind("Movement", "Slow Down Multiplier", DefaultSlowDownMultiplier,
            new ConfigDescription("Multiplier while the configured slow-down key or controller path is held.",
                new AcceptableValueRange<float>(0.05f, 1f)));
        TeleportVerticalOffset = config.Bind("Teleport", "Vertical Offset", DefaultTeleportVerticalOffset,
            new ConfigDescription("Height above the selected teammate's center when teleporting.",
                new AcceptableValueRange<float>(0f, 10f)));
        TeleportBackwardOffset = config.Bind("Teleport", "Backward Offset", DefaultTeleportBackwardOffset,
            new ConfigDescription("Distance behind the selected teammate when teleporting.",
                new AcceptableValueRange<float>(0f, 10f)));

    }

    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<string> ToggleFlightKeyboardPath { get; }
    public ConfigEntry<string> TeleportMenuKeyboardPath { get; }
    public ConfigEntry<string> ControllerChordModifierPath { get; }
    public ConfigEntry<string> ControllerFlightTogglePath { get; }
    public ConfigEntry<string> ControllerTeleportMenuTogglePath { get; }
    public ConfigEntry<string> SpeedUpKeyboardPath { get; }
    public ConfigEntry<string> SlowDownKeyboardPath { get; }
    public ConfigEntry<string> SpeedUpControllerPath { get; }
    public ConfigEntry<string> SlowDownControllerPath { get; }
    public ConfigEntry<float> BaseSpeed { get; }
    public ConfigEntry<float> SpeedUpMultiplier { get; }
    public ConfigEntry<float> SlowDownMultiplier { get; }
    public ConfigEntry<float> TeleportVerticalOffset { get; }
    public ConfigEntry<float> TeleportBackwardOffset { get; }

    public float SafeBaseSpeed => SafeRange(BaseSpeed.Value, 1f, 1000f, DefaultBaseSpeed);
    public float SafeSpeedUpMultiplier => SafeRange(SpeedUpMultiplier.Value, 1f, 10f, DefaultSpeedUpMultiplier);
    public float SafeSlowDownMultiplier => SafeRange(SlowDownMultiplier.Value, 0.05f, 1f, DefaultSlowDownMultiplier);
    public float SafeTeleportVerticalOffset => SafeRange(TeleportVerticalOffset.Value, 0f, 10f, DefaultTeleportVerticalOffset);
    public float SafeTeleportBackwardOffset => SafeRange(TeleportBackwardOffset.Value, 0f, 10f, DefaultTeleportBackwardOffset);

    private static float SafeRange(float value, float min, float max, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        return Mathf.Clamp(value, min, max);
    }

}
