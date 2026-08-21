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
    private FreeFlyNotification? _notification;

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        ModConfig config = new(Config);
        _notification = gameObject.AddComponent<FreeFlyNotification>();
        FreeFlyInput input = new(config, Logger);
        FlightRuntime flight = new(config, Logger, input, _notification);
        TeleportDestinationService destinations = new(config, Logger);
        TeleportMenuView menu = new(destinations);
        Controller = new FreeFlyController(config, input, flight, destinations, menu);
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
