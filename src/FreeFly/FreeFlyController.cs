using UnityEngine;

namespace FreeFly;

internal sealed class FreeFlyController
{
    private readonly ModConfig _config;
    private readonly FreeFlyInput _input;
    private readonly FlightRuntime _flight;
    private readonly TeleportDestinationService _destinations;
    private readonly TeleportMenuView _menu;
    private PatchCapabilities _capabilities;

    public FreeFlyController(
        ModConfig config,
        FreeFlyInput input,
        FlightRuntime flight,
        TeleportDestinationService destinations,
        TeleportMenuView menu)
    {
        _config = config;
        _input = input;
        _flight = flight;
        _destinations = destinations;
        _menu = menu;
    }

    public void SetCapabilities(PatchCapabilities capabilities)
    {
        _capabilities = capabilities;
        _destinations.SetCapabilities(capabilities);
    }

    public void TickUpdate()
    {
        Character? local = Character.localCharacter;
        if (!_config.Enabled.Value || !_capabilities.FlightPatch || local == null || local.data == null)
        {
            _flight.Stop(FlightStopReason.FeatureUnavailable);
            _menu.Close();
            return;
        }

        if (_flight.IsActive && (_flight.ActiveCharacter != local || local.warping))
        {
            _flight.Stop(local.warping
                ? FlightStopReason.WarpStarted
                : FlightStopReason.CharacterUnavailable);
        }

        FreeFlyInputSnapshot input = _input.ReadSnapshot();
        if (_menu.IsOpen)
        {
            if (input.MenuToggle)
            {
                _menu.Close();
                return;
            }

            if (input.FlightToggle)
                return;

            _menu.Tick(input);
            return;
        }

        if (input.MenuToggle && _input.CanUseInput())
        {
            _menu.Open();
            return;
        }

        if (input.FlightToggle && _input.CanUseInput())
        {
            if (_flight.IsActive)
                _flight.Stop(FlightStopReason.UserToggle);
            else
                _flight.TryStart(local);
        }
    }

    public void ApplyFlightPhysics(CharacterMovement movement) =>
        _flight.ApplyPhysics(movement, _menu.IsOpen);

    public void DrawMenu() => _menu.Draw();

    public void Shutdown()
    {
        _menu.Shutdown();
        _flight.Stop(FlightStopReason.PluginDestroyed, notify: false);
        _input.Dispose();
    }
}

internal sealed class FreeFlyMenuWindow : MenuWindow
{
    public override bool selectOnOpen => false;
}
