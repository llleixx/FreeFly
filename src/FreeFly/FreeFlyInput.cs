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
    private InputAction? _controllerChordModifierAction;
    private InputAction? _controllerFlightToggleAction;
    private InputAction? _controllerTeleportMenuToggleAction;
    private InputAction? _speedUpControllerAction;
    private InputAction? _slowDownControllerAction;
    private string _controllerBindingSignature = string.Empty;

    public FreeFlyInput(ModConfig config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
    }

    public void EnsureActions()
    {
        string modifierPath = FreeFlyInputRules.NormalizeBindingPath(_config.ControllerChordModifierPath.Value);
        string flightPath = FreeFlyInputRules.NormalizeBindingPath(_config.ControllerFlightTogglePath.Value);
        string menuPath = FreeFlyInputRules.NormalizeBindingPath(_config.ControllerTeleportMenuTogglePath.Value);
        string speedUpPath = FreeFlyInputRules.NormalizeBindingPath(_config.SpeedUpControllerPath.Value);
        string slowDownPath = FreeFlyInputRules.NormalizeBindingPath(_config.SlowDownControllerPath.Value);
        string signature = $"{modifierPath}\n{flightPath}\n{menuPath}\n{speedUpPath}\n{slowDownPath}";
        if (signature == _controllerBindingSignature)
            return;

        DisposeActions();
        _controllerBindingSignature = signature;

        if (modifierPath.Length > 0)
        {
            _controllerChordModifierAction = TryCreateAction(
                "FreeFly Controller Chord Modifier",
                modifierPath,
                null,
                "modifier");
        }

        _controllerFlightToggleAction = TryCreateAction(
            "FreeFly Controller Flight Toggle",
            flightPath,
            modifierPath,
            "flight");
        _controllerTeleportMenuToggleAction = TryCreateAction(
            "FreeFly Controller Teleport Menu Toggle",
            menuPath,
            modifierPath,
            "teleport menu");

        if (speedUpPath.Length > 0)
        {
            _speedUpControllerAction = TryCreateAction(
                "FreeFly Controller Speed Up",
                speedUpPath,
                null,
                "speed-up");
        }

        if (slowDownPath.Length > 0)
        {
            _slowDownControllerAction = TryCreateAction(
                "FreeFly Controller Slow Down",
                slowDownPath,
                null,
                "slow-down");
        }
    }

    public FreeFlyInputSnapshot ReadSnapshot()
    {
        bool menuToggle = Input.GetKeyDown(_config.TeleportMenuShortcut.Value) ||
                          _controllerTeleportMenuToggleAction?.WasPressedThisFrame() == true;
        bool flightToggle = Input.GetKeyDown(_config.ToggleFlightShortcut.Value) ||
                            _controllerFlightToggleAction?.WasPressedThisFrame() == true;
        return new FreeFlyInputSnapshot(
            menuToggle,
            flightToggle,
            Input.GetKeyDown(KeyCode.Escape) || Gamepad.current?.buttonEast.wasPressedThisFrame == true,
            Input.GetKeyDown(KeyCode.Return) || Gamepad.current?.buttonSouth.wasPressedThisFrame == true,
            Input.GetKeyDown(KeyCode.UpArrow) || Gamepad.current?.dpad.up.wasPressedThisFrame == true,
            Input.GetKeyDown(KeyCode.DownArrow) || Gamepad.current?.dpad.down.wasPressedThisFrame == true);
    }

    public bool SpeedUpHeld() => IsKeyHeld(_config.SpeedUpShortcut.Value) ||
                                  (!ChordModifierHeld() && _speedUpControllerAction?.IsPressed() == true);

    public bool SlowDownHeld() => IsKeyHeld(_config.SlowDownShortcut.Value) ||
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
        DisposeActions();
        _controllerBindingSignature = string.Empty;
    }

    private InputAction? TryCreateAction(string name, string actionPath, string? modifierPath, string description)
    {
        if (actionPath.Length == 0)
            return null;

        try
        {
            InputAction action = new(name, InputActionType.Button);
            if (modifierPath == null || modifierPath.Length == 0)
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

    private void DisposeActions()
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

    private static bool IsKeyHeld(KeyCode key) => key != KeyCode.None && Input.GetKey(key);

    private static bool IsActionHeld(InputAction? action) => action?.IsPressed() == true;
}
