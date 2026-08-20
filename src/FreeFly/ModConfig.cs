using BepInEx.Configuration;
using UnityEngine;

namespace FreeFly;

internal enum ControllerButton
{
    None,
    LeftShoulder,
    RightShoulder,
    LeftTrigger,
    RightTrigger,
    ButtonSouth,
    ButtonEast,
    ButtonWest,
    ButtonNorth,
    LeftStickButton,
    RightStickButton
}

internal sealed class ModConfig
{
    private const float DefaultBaseSpeed = 100f;
    private const float DefaultSpeedUpMultiplier = 2f;
    private const float DefaultSlowDownMultiplier = 0.35f;
    private const float DefaultTeleportVerticalOffset = 2f;
    private const float DefaultTeleportBackwardOffset = 1.5f;

    public ModConfig(ConfigFile config)
    {
        Enabled = config.Bind("General", "Enabled", true,
            "Enable local free flight and teammate teleport.");
        ToggleFlightShortcut = config.Bind("Controls", "ToggleFlightShortcut", KeyCode.F6,
            "Keyboard key used to toggle free flight.");
        TeleportMenuShortcut = config.Bind("Controls", "TeleportMenuShortcut", KeyCode.F7,
            "Keyboard key used to open the teammate teleport menu.");
        ControllerChordModifierPath = config.Bind("Controls", "ControllerChordModifierPath",
            "<Gamepad>/selectButton",
            "Controller button held as the optional modifier for the flight and teleport menu shortcuts. Leave empty for single-button mode.");
        ControllerFlightTogglePath = config.Bind("Controls", "ControllerFlightTogglePath",
            "<Gamepad>/leftShoulder",
            "Controller button pressed with the optional modifier to toggle free flight. Leave empty to disable.");
        ControllerTeleportMenuTogglePath = config.Bind("Controls", "ControllerTeleportMenuTogglePath",
            "<Gamepad>/rightShoulder",
            "Controller button pressed with the optional modifier to toggle the teleport menu. Leave empty to disable.");
        SpeedUpShortcut = config.Bind("Controls", "SpeedUpShortcut", KeyCode.LeftShift,
            "Keyboard key held to temporarily increase flight speed. Set to None to disable.");
        SlowDownShortcut = config.Bind("Controls", "SlowDownShortcut", KeyCode.LeftAlt,
            "Keyboard key held to temporarily decrease flight speed. Set to None to disable.");
        SpeedUpControllerButton = config.Bind("Controls", "SpeedUpControllerButton", ControllerButton.RightShoulder,
            "Controller button held to temporarily increase flight speed. Set to None to disable.");
        SlowDownControllerButton = config.Bind("Controls", "SlowDownControllerButton", ControllerButton.LeftShoulder,
            "Controller button held to temporarily decrease flight speed. Set to None to disable.");
        BaseSpeed = config.Bind("Movement", "BaseSpeed", DefaultBaseSpeed,
            new ConfigDescription("Base flight speed in meters per second.",
                new AcceptableValueRange<float>(1f, 1000f)));
        SpeedUpMultiplier = config.Bind("Movement", "SpeedUpMultiplier", DefaultSpeedUpMultiplier,
            new ConfigDescription("Multiplier while the configured speed-up key or controller button is held.",
                new AcceptableValueRange<float>(1f, 10f)));
        SlowDownMultiplier = config.Bind("Movement", "SlowDownMultiplier", DefaultSlowDownMultiplier,
            new ConfigDescription("Multiplier while the configured slow-down key or controller button is held.",
                new AcceptableValueRange<float>(0.05f, 1f)));
        TeleportVerticalOffset = config.Bind("Teleport", "VerticalOffset", DefaultTeleportVerticalOffset,
            new ConfigDescription("Height above the selected teammate's center when teleporting.",
                new AcceptableValueRange<float>(0f, 10f)));
        TeleportBackwardOffset = config.Bind("Teleport", "BackwardOffset", DefaultTeleportBackwardOffset,
            new ConfigDescription("Distance behind the selected teammate when teleporting.",
                new AcceptableValueRange<float>(0f, 10f)));
    }

    public ConfigEntry<bool> Enabled { get; }
    public ConfigEntry<KeyCode> ToggleFlightShortcut { get; }
    public ConfigEntry<KeyCode> TeleportMenuShortcut { get; }
    public ConfigEntry<string> ControllerChordModifierPath { get; }
    public ConfigEntry<string> ControllerFlightTogglePath { get; }
    public ConfigEntry<string> ControllerTeleportMenuTogglePath { get; }
    public ConfigEntry<KeyCode> SpeedUpShortcut { get; }
    public ConfigEntry<KeyCode> SlowDownShortcut { get; }
    public ConfigEntry<ControllerButton> SpeedUpControllerButton { get; }
    public ConfigEntry<ControllerButton> SlowDownControllerButton { get; }
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
