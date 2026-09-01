using System;
using BepInEx.Logging;
using FreeFly.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeFly;

internal readonly struct FreeFlyInputSnapshot
{
    public FreeFlyInputSnapshot(
        bool menuToggle,
        bool flightToggle,
        bool cancel,
        bool confirm,
        bool up,
        bool down)
    {
        MenuToggle = menuToggle;
        FlightToggle = flightToggle;
        Cancel = cancel;
        Confirm = confirm;
        Up = up;
        Down = down;
    }

    public bool MenuToggle { get; }
    public bool FlightToggle { get; }
    public bool Cancel { get; }
    public bool Confirm { get; }
    public bool Up { get; }
    public bool Down { get; }
}

internal sealed class FreeFlyInput : IDisposable
{
    private readonly ModConfig _config;
    private readonly ManualLogSource _logger;
    private InputAction? _flightToggleAction;
    private InputAction? _teleportMenuToggleAction;
    private InputAction? _controllerChordModifierAction;
    private InputAction? _speedUpKeyboardAction;
    private InputAction? _slowDownKeyboardAction;
    private InputAction? _speedUpControllerAction;
    private InputAction? _slowDownControllerAction;
    private InputAction? _cancelAction;
    private InputAction? _confirmAction;
    private InputAction? _upAction;
    private InputAction? _downAction;

    public FreeFlyInput(ModConfig config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
        BindNavigationActions();
        BindConfiguredActions();
        _config.ToggleFlightKeyboardPath.SettingChanged += OnBindingPathChanged;
        _config.TeleportMenuKeyboardPath.SettingChanged += OnBindingPathChanged;
        _config.ControllerChordModifierPath.SettingChanged += OnBindingPathChanged;
        _config.ControllerFlightTogglePath.SettingChanged += OnBindingPathChanged;
        _config.ControllerTeleportMenuTogglePath.SettingChanged += OnBindingPathChanged;
        _config.SpeedUpKeyboardPath.SettingChanged += OnBindingPathChanged;
        _config.SlowDownKeyboardPath.SettingChanged += OnBindingPathChanged;
        _config.SpeedUpControllerPath.SettingChanged += OnBindingPathChanged;
        _config.SlowDownControllerPath.SettingChanged += OnBindingPathChanged;
    }

    public FreeFlyInputSnapshot ReadSnapshot()
    {
        return new FreeFlyInputSnapshot(
            WasPressedThisFrame(_teleportMenuToggleAction),
            WasPressedThisFrame(_flightToggleAction),
            WasPressedThisFrame(_cancelAction),
            WasPressedThisFrame(_confirmAction),
            WasPressedThisFrame(_upAction),
            WasPressedThisFrame(_downAction));
    }

    public bool SpeedUpHeld() => IsActionHeld(_speedUpKeyboardAction) ||
                                  (!ChordModifierHeld() && _speedUpControllerAction?.IsPressed() == true);

    public bool SlowDownHeld() => IsActionHeld(_slowDownKeyboardAction) ||
                                  (!ChordModifierHeld() && _slowDownControllerAction?.IsPressed() == true);

    public bool ChordModifierHeld() => _controllerChordModifierAction?.IsPressed() == true;

    public Vector2 GetMovementInput(Character local, bool menuOpen)
    {
        if (!menuOpen)
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

    public bool IsJumpHeld(Character local, bool menuOpen) =>
        menuOpen ? IsActionHeld(CharacterInput.action_jump) : local.input.jumpIsPressed;

    public bool IsCrouchHeld(Character local, bool menuOpen) =>
        menuOpen ? IsActionHeld(CharacterInput.action_crouch) : local.input.crouchIsPressed;

    public bool CanUseInput()
    {
        GUIManager? gui = GUIManager.instance;
        return Time.timeScale > 0f && gui != null && !gui.windowBlockingInput && !gui.wheelActive;
    }

    public void Dispose()
    {
        _config.ToggleFlightKeyboardPath.SettingChanged -= OnBindingPathChanged;
        _config.TeleportMenuKeyboardPath.SettingChanged -= OnBindingPathChanged;
        _config.ControllerChordModifierPath.SettingChanged -= OnBindingPathChanged;
        _config.ControllerFlightTogglePath.SettingChanged -= OnBindingPathChanged;
        _config.ControllerTeleportMenuTogglePath.SettingChanged -= OnBindingPathChanged;
        _config.SpeedUpKeyboardPath.SettingChanged -= OnBindingPathChanged;
        _config.SlowDownKeyboardPath.SettingChanged -= OnBindingPathChanged;
        _config.SpeedUpControllerPath.SettingChanged -= OnBindingPathChanged;
        _config.SlowDownControllerPath.SettingChanged -= OnBindingPathChanged;
        DisposeConfiguredActions();
        DisposeAction(ref _cancelAction);
        DisposeAction(ref _confirmAction);
        DisposeAction(ref _upAction);
        DisposeAction(ref _downAction);
    }

    private void OnBindingPathChanged(object sender, EventArgs eventArgs)
    {
        BindConfiguredActions();
    }

    private void BindConfiguredActions()
    {
        DisposeConfiguredActions();

        string keyboardFlightPath = Normalize(_config.ToggleFlightKeyboardPath.Value);
        string keyboardMenuPath = Normalize(_config.TeleportMenuKeyboardPath.Value);
        string modifierPath = Normalize(_config.ControllerChordModifierPath.Value);
        string controllerFlightPath = Normalize(_config.ControllerFlightTogglePath.Value);
        string controllerMenuPath = Normalize(_config.ControllerTeleportMenuTogglePath.Value);
        string keyboardSpeedUpPath = Normalize(_config.SpeedUpKeyboardPath.Value);
        string keyboardSlowDownPath = Normalize(_config.SlowDownKeyboardPath.Value);
        string controllerSpeedUpPath = Normalize(_config.SpeedUpControllerPath.Value);
        string controllerSlowDownPath = Normalize(_config.SlowDownControllerPath.Value);

        _controllerChordModifierAction = TryCreateButtonAction(
            "FreeFly Controller Chord Modifier", "controller modifier", modifierPath);
        _flightToggleAction = TryCreateToggleAction(
            "FreeFly Flight Toggle", "flight toggle", keyboardFlightPath, controllerFlightPath, modifierPath);
        _teleportMenuToggleAction = TryCreateToggleAction(
            "FreeFly Teleport Menu Toggle", "teleport menu toggle", keyboardMenuPath, controllerMenuPath, modifierPath);
        _speedUpKeyboardAction = TryCreateButtonAction(
            "FreeFly Keyboard Speed Up", "keyboard speed-up", keyboardSpeedUpPath);
        _slowDownKeyboardAction = TryCreateButtonAction(
            "FreeFly Keyboard Slow Down", "keyboard slow-down", keyboardSlowDownPath);
        _speedUpControllerAction = TryCreateButtonAction(
            "FreeFly Controller Speed Up", "controller speed-up", controllerSpeedUpPath);
        _slowDownControllerAction = TryCreateButtonAction(
            "FreeFly Controller Slow Down", "controller slow-down", controllerSlowDownPath);
    }

    private void BindNavigationActions()
    {
        _cancelAction = TryCreateButtonAction(
            "FreeFly Menu Cancel", "menu cancel", "<Keyboard>/escape", "<Gamepad>/buttonEast");
        _confirmAction = TryCreateButtonAction(
            "FreeFly Menu Confirm", "menu confirm", "<Keyboard>/enter", "<Gamepad>/buttonSouth");
        _upAction = TryCreateButtonAction(
            "FreeFly Menu Up", "menu up", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
        _downAction = TryCreateButtonAction(
            "FreeFly Menu Down", "menu down", "<Keyboard>/downArrow", "<Gamepad>/dpad/down");
    }

    private InputAction? TryCreateToggleAction(
        string name,
        string description,
        string keyboardPath,
        string controllerPath,
        string modifierPath)
    {
        if (keyboardPath.Length == 0 && controllerPath.Length == 0)
            return null;

        InputAction? action = null;
        try
        {
            action = new InputAction(name, InputActionType.Button);
            if (keyboardPath.Length > 0)
                action.AddBinding(keyboardPath);
            if (controllerPath.Length > 0)
            {
                if (modifierPath.Length == 0)
                    action.AddBinding(controllerPath);
                else
                    action.AddCompositeBinding("OneModifier")
                        .With("modifier", modifierPath)
                        .With("binding", controllerPath);
            }

            action.Enable();
            return action;
        }
        catch (Exception exception)
        {
            action?.Dispose();
            _logger.LogWarning($"Could not bind the configured {description} paths; the action is disabled: {exception.Message}");
            return null;
        }
    }

    private InputAction? TryCreateButtonAction(string name, string description, params string[] paths)
    {
        InputAction? action = null;
        try
        {
            action = new InputAction(name, InputActionType.Button);
            bool hasBinding = false;
            foreach (string path in paths)
            {
                if (path.Length == 0)
                    continue;
                action.AddBinding(path);
                hasBinding = true;
            }

            if (!hasBinding)
            {
                action.Dispose();
                return null;
            }

            action.Enable();
            return action;
        }
        catch (Exception exception)
        {
            action?.Dispose();
            _logger.LogWarning($"Could not bind the configured {description} paths; the action is disabled: {exception.Message}");
            return null;
        }
    }

    private void DisposeConfiguredActions()
    {
        DisposeAction(ref _flightToggleAction);
        DisposeAction(ref _teleportMenuToggleAction);
        DisposeAction(ref _controllerChordModifierAction);
        DisposeAction(ref _speedUpKeyboardAction);
        DisposeAction(ref _slowDownKeyboardAction);
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

    private static string Normalize(string? path) => FreeFlyInputRules.NormalizeBindingPath(path);

    private static bool IsActionHeld(InputAction? action) => action?.IsPressed() == true;

    private static bool WasPressedThisFrame(InputAction? action) => action?.WasPressedThisFrame() == true;
}
