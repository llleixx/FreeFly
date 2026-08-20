using BepInEx;
using HarmonyLib;

namespace FreeFly;

[BepInPlugin(PluginGuid, PluginName, BuildInfo.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.github.lllei.FreeFly";
    public const string PluginName = "FreeFly";

    internal static Plugin? Instance { get; private set; }
    internal FreeFlyController Controller { get; private set; } = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        ModConfig config = new(Config);
        Controller = new FreeFlyController(config, Logger);
        _harmony = new Harmony(PluginGuid);

        PatchCapabilities capabilities = PatchInstaller.Install(_harmony, Logger);
        Controller.SetCapabilities(capabilities);
        Logger.LogInfo($"{PluginName} {BuildInfo.Version} loaded. " +
                       $"Flight patch: {capabilities.FlightPatch}; teleport: {capabilities.TeleportPatch}.");
    }

    private void Update()
    {
        Controller?.TickUpdate();
    }

    private void OnGUI()
    {
        Controller?.DrawMenu();
    }

    private void OnDestroy()
    {
        Controller?.Shutdown();
        _harmony?.UnpatchSelf();
        Instance = null;
    }
}
