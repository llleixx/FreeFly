using BepInEx.Configuration;
using UnityEngine;

namespace FreeFly;

internal sealed class ModConfig
{
    private static readonly string[] ControllerPathOptions =
    [
        "<Gamepad>/select",
        "<Gamepad>/start",
        "<Gamepad>/leftShoulder",
        "<Gamepad>/rightShoulder",
        "<Gamepad>/buttonSouth",
        "<Gamepad>/buttonEast",
        "<Gamepad>/buttonWest",
        "<Gamepad>/buttonNorth",
        "<Gamepad>/leftTrigger",
        "<Gamepad>/rightTrigger",
        "<Gamepad>/leftStickPress",
        "<Gamepad>/rightStickPress",
        "<Gamepad>/dpad/up",
        "<Gamepad>/dpad/down",
        "<Gamepad>/dpad/left",
        "<Gamepad>/dpad/right",
        ""
    ];

    private const float DefaultBaseSpeed = 100f;
    private const float DefaultSpeedUpMultiplier = 2f;
    private const float DefaultSlowDownMultiplier = 0.2f;
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
            "<Gamepad>/select",
            new ConfigDescription("Controller button held as the optional modifier for the flight and teleport menu shortcuts. Leave empty for single-button mode. Common paths: <Gamepad>/select = View/Share; <Gamepad>/start = Menu/Options; <Gamepad>/leftShoulder = LB/L1; <Gamepad>/rightShoulder = RB/R1; <Gamepad>/buttonSouth = A/Cross; <Gamepad>/buttonEast = B/Circle; <Gamepad>/buttonWest = X/Square; <Gamepad>/buttonNorth = Y/Triangle; <Gamepad>/leftTrigger = LT/L2; <Gamepad>/rightTrigger = RT/R2; <Gamepad>/leftStickPress and <Gamepad>/rightStickPress; <Gamepad>/dpad/up, <Gamepad>/dpad/down, <Gamepad>/dpad/left, and <Gamepad>/dpad/right. These semantic paths require a device recognized as Gamepad.",
                new AcceptableValueList<string>(ControllerPathOptions)));
        ControllerFlightTogglePath = config.Bind("Controls", "ControllerFlightTogglePath",
            "<Gamepad>/leftShoulder",
            new ConfigDescription("Controller button pressed with the optional modifier to toggle free flight. Default: <Gamepad>/leftShoulder (Xbox LB / PlayStation L1). Leave empty to disable.",
                new AcceptableValueList<string>(ControllerPathOptions)));
        ControllerTeleportMenuTogglePath = config.Bind("Controls", "ControllerTeleportMenuTogglePath",
            "<Gamepad>/rightShoulder",
            new ConfigDescription("Controller button pressed with the optional modifier to toggle the teleport menu. Default: <Gamepad>/rightShoulder (Xbox RB / PlayStation R1). Leave empty to disable.",
                new AcceptableValueList<string>(ControllerPathOptions)));
        SpeedUpShortcut = config.Bind("Controls", "SpeedUpShortcut", KeyCode.LeftShift,
            "Keyboard key held to temporarily increase flight speed. Set to None to disable.");
        SlowDownShortcut = config.Bind("Controls", "SlowDownShortcut", KeyCode.LeftAlt,
            "Keyboard key held to temporarily decrease flight speed. Set to None to disable.");
        SpeedUpControllerPath = config.Bind("Controls", "SpeedUpControllerPath", "<Gamepad>/rightShoulder",
            new ConfigDescription("Unity Input System path held to temporarily increase flight speed. Default: <Gamepad>/rightShoulder (Xbox RB / PlayStation R1). Leave empty or set to None to disable.",
                new AcceptableValueList<string>(ControllerPathOptions)));
        SlowDownControllerPath = config.Bind("Controls", "SlowDownControllerPath", "<Gamepad>/leftShoulder",
            new ConfigDescription("Unity Input System path held to temporarily decrease flight speed. Default: <Gamepad>/leftShoulder (Xbox LB / PlayStation L1). Leave empty or set to None to disable.",
                new AcceptableValueList<string>(ControllerPathOptions)));
        BaseSpeed = config.Bind("Movement", "BaseSpeed", DefaultBaseSpeed,
            new ConfigDescription("Base flight speed in meters per second.",
                new AcceptableValueRange<float>(1f, 1000f)));
        SpeedUpMultiplier = config.Bind("Movement", "SpeedUpMultiplier", DefaultSpeedUpMultiplier,
            new ConfigDescription("Multiplier while the configured speed-up key or controller path is held.",
                new AcceptableValueRange<float>(1f, 10f)));
        SlowDownMultiplier = config.Bind("Movement", "SlowDownMultiplier", DefaultSlowDownMultiplier,
            new ConfigDescription("Multiplier while the configured slow-down key or controller path is held.",
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
