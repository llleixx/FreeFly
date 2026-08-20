using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using FreeFly.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FreeFly;

internal sealed class FreeFlyController
{
    private readonly ModConfig _config;
    private readonly ManualLogSource _logger;
    private readonly Dictionary<Rigidbody, bool> _originalGravity = new();
    private readonly List<Character> _targets = new();

    private PatchCapabilities _capabilities;
    private InputAction? _controllerChordModifierAction;
    private InputAction? _controllerFlightToggleAction;
    private InputAction? _controllerTeleportMenuToggleAction;
    private string _controllerBindingSignature = string.Empty;
    private GameObject? _menuInputBlockerObject;
    private FreeFlyMenuWindow? _menuInputBlockerWindow;
    private Vector2 _menuScrollPosition;
    private Character? _flightCharacter;
    private bool _flightActive;
    private bool _menuOpen;
    private int _selectedTarget;

    public FreeFlyController(ModConfig config, ManualLogSource logger)
    {
        _config = config;
        _logger = logger;
    }

    public void SetCapabilities(PatchCapabilities capabilities) => _capabilities = capabilities;

    public void TickUpdate()
    {
        EnsureControllerActions();

        Character? local = Character.localCharacter;
        if (!_config.Enabled.Value || !_capabilities.FlightPatch || local == null || local.data == null)
        {
            StopFlight("feature unavailable");
            CloseMenu();
            return;
        }

        if (_flightActive && (_flightCharacter != local || local.warping))
            StopFlight("character changed or PEAK warp started");

        bool menuToggle = Input.GetKeyDown(_config.TeleportMenuShortcut.Value) ||
                          ControllerTeleportMenuPressed();
        bool flightToggle = Input.GetKeyDown(_config.ToggleFlightShortcut.Value) ||
                            ControllerFlightPressed();

        if (_menuOpen)
        {
            if (menuToggle)
            {
                CloseMenu();
                return;
            }

            if (flightToggle)
                return;

            RefreshTargets();
            HandleMenuInput();
            return;
        }

        if (menuToggle && CanUseInput())
        {
            OpenMenu();
            return;
        }

        if (flightToggle && CanUseInput())
        {
            if (_flightActive)
                StopFlight("toggle pressed");
            else
                StartFlight(local);
        }
    }

    public void ApplyFlightPhysics(CharacterMovement movement)
    {
        if (!_flightActive || _flightCharacter == null)
            return;

        if (movement != _flightCharacter.refs.movement)
            return;

        Character local = _flightCharacter;
        if (!IsUsable(local) || local.warping)
        {
            StopFlight("character became unavailable");
            return;
        }

        CharacterRagdoll ragdoll = local.refs.ragdoll;
        foreach (Bodypart part in ragdoll.partList)
        {
            if (part?.Rig == null)
                continue;
            part.Rig.useGravity = false;
        }

        if (_menuOpen)
        {
            ragdoll.HaltBodyVelocity();
            return;
        }

        Vector3 movementDirection = GetMovementDirection(local);
        float speed = FreeFlyMath.ApplySpeedModifiers(
            _config.SafeBaseSpeed,
            SpeedUpHeld(),
            SlowDownHeld(),
            _config.SafeSpeedUpMultiplier,
            _config.SafeSlowDownMultiplier);

        Vector3 delta = movementDirection * speed * Time.fixedDeltaTime;
        Vector3 velocity = movementDirection * speed;
        ragdoll.HaltBodyVelocity();
        foreach (Bodypart part in ragdoll.partList)
        {
            if (part?.Rig == null)
                continue;

            if (part.Rig.isKinematic)
            {
                if (IsFinite(delta) && delta.sqrMagnitude > 0f)
                    part.Rig.position += delta;
            }
            else
            {
                part.Rig.linearVelocity = velocity;
            }
        }

        local.data.isGrounded = true;
        local.data.sinceGrounded = 0f;
        local.data.sinceJump = 0f;
    }

    public void DrawMenu()
    {
        if (!_menuOpen)
            return;

        RefreshTargets();
        float width = Mathf.Min(720f, Screen.width - 40f);
        float height = Mathf.Min(620f, Screen.height - 40f);
        Rect area = new((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUIStyle boxStyle = new(GUI.skin.box)
        {
            fontSize = 28
        };
        GUIStyle labelStyle = new(GUI.skin.label)
        {
            fontSize = 20,
            wordWrap = true
        };
        GUIStyle buttonStyle = new(GUI.skin.button)
        {
            fontSize = 22
        };

        GUI.Box(area, "FreeFly - Teleport", boxStyle);
        GUILayout.BeginArea(new Rect(area.x + 24f, area.y + 64f, area.width - 48f, area.height - 88f));
        GUILayout.Label("Select a teammate. Dead teammates use their corpse position.", labelStyle);
        GUILayout.Space(12f);

        if (_targets.Count == 0)
        {
            GUILayout.Label("No teammate is available.", labelStyle);
        }
        else
        {
            _menuScrollPosition = GUILayout.BeginScrollView(_menuScrollPosition);
            for (int i = 0; i < _targets.Count; i++)
            {
                Character target = _targets[i];
                string state = target.data.dead ? "Dead" : target.data.fullyPassedOut ? "Passed out" : "Alive";
                string label = $"{(i == _selectedTarget ? "> " : "  ")}{target.characterName} [{state}]";
                if (GUILayout.Button(label, buttonStyle, GUILayout.Height(52f)))
                {
                    _selectedTarget = i;
                    TeleportToSelected();
                }
            }
            GUILayout.EndScrollView();
        }

        GUILayout.FlexibleSpace();
        GUILayout.Label("Up/Down or D-pad: select    Enter/A: teleport    Escape/B: cancel", labelStyle);
        GUILayout.EndArea();
    }

    public void Shutdown()
    {
        CloseMenu();
        StopFlight("plugin destroyed");
        DisposeControllerActions();
    }

    private void EnsureControllerActions()
    {
        string modifierPath = NormalizeBindingPath(_config.ControllerChordModifierPath.Value);
        string flightPath = NormalizeBindingPath(_config.ControllerFlightTogglePath.Value);
        string menuPath = NormalizeBindingPath(_config.ControllerTeleportMenuTogglePath.Value);
        string signature = $"{modifierPath}\n{flightPath}\n{menuPath}";
        if (signature == _controllerBindingSignature)
            return;

        DisposeControllerActions();
        _controllerBindingSignature = signature;

        if (modifierPath.Length > 0)
        {
            _controllerChordModifierAction = CreateButtonAction(
                "FreeFly Controller Chord Modifier",
                modifierPath,
                "modifier");
        }

        _controllerFlightToggleAction = CreateToggleAction(
            "FreeFly Controller Flight Toggle",
            modifierPath,
            flightPath,
            "flight");
        _controllerTeleportMenuToggleAction = CreateToggleAction(
            "FreeFly Controller Teleport Menu Toggle",
            modifierPath,
            menuPath,
            "teleport menu");
    }

    private InputAction? CreateToggleAction(string name, string modifierPath, string actionPath, string description)
    {
        if (actionPath.Length == 0)
            return null;

        try
        {
            InputAction action = new(name, InputActionType.Button);
            if (modifierPath.Length == 0)
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

    private InputAction? CreateButtonAction(string name, string path, string description)
    {
        try
        {
            InputAction action = new(name, InputActionType.Button);
            action.AddBinding(path);
            action.Enable();
            return action;
        }
        catch (Exception exception)
        {
            _logger.LogWarning($"Controller {description} binding is invalid and has been disabled: {exception.Message}");
            return null;
        }
    }

    private void DisposeControllerActions()
    {
        DisposeAction(ref _controllerChordModifierAction);
        DisposeAction(ref _controllerFlightToggleAction);
        DisposeAction(ref _controllerTeleportMenuToggleAction);
    }

    private static void DisposeAction(ref InputAction? action)
    {
        if (action == null)
            return;

        action.Disable();
        action.Dispose();
        action = null;
    }

    private void StartFlight(Character local)
    {
        if (!IsUsable(local) || local.refs.ragdoll == null)
            return;

        _flightCharacter = local;
        _originalGravity.Clear();
        foreach (Bodypart part in local.refs.ragdoll.partList)
        {
            if (part?.Rig != null)
                _originalGravity[part.Rig] = part.Rig.useGravity;
        }

        local.refs.ragdoll.ToggleCollision(false);
        _flightActive = true;
        _logger.LogInfo("Free flight enabled.");
    }

    private void StopFlight(string reason)
    {
        if (!_flightActive && _flightCharacter == null)
            return;

        Character? character = _flightCharacter;
        if (character != null && character.refs.ragdoll != null)
        {
            character.refs.ragdoll.ToggleCollision(true);
            foreach (Bodypart part in character.refs.ragdoll.partList)
            {
                if (part?.Rig != null && _originalGravity.TryGetValue(part.Rig, out bool gravity))
                    part.Rig.useGravity = gravity;
            }
            character.refs.ragdoll.HaltBodyVelocity();
        }

        _originalGravity.Clear();
        _flightCharacter = null;
        bool wasActive = _flightActive;
        _flightActive = false;
        if (wasActive)
            _logger.LogDebug($"Free flight disabled: {reason}.");
    }

    private bool IsUsable(Character character)
    {
        return character == Character.localCharacter && character.data != null &&
               character.refs.ragdoll != null && character.refs.view != null;
    }

    private bool CanUseInput()
    {
        GUIManager? gui = GUIManager.instance;
        return Time.timeScale > 0f && gui != null && !gui.windowBlockingInput && !gui.wheelActive;
    }

    private Vector3 GetMovementDirection(Character local)
    {
        Vector2 input = Vector2.ClampMagnitude(local.input.movementInput, 1f);
        Vector3 forward = local.data.lookDirection_Flat;
        Vector3 right = local.data.lookDirection_Right;
        Vector3 up = Vector3.up;
        Vector3 direction = forward * input.y + right * input.x;
        bool chordModifierHeld = ControllerChordModifierHeld();
        float vertical = chordModifierHeld
            ? 0f
            : (local.input.jumpIsPressed || ControllerAscendHeld() ? 1f : 0f);
        vertical -= chordModifierHeld
            ? 0f
            : (local.input.crouchIsPressed || ControllerDescendHeld() ? 1f : 0f);
        direction += up * vertical;
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void OpenMenu()
    {
        RefreshTargets();
        _selectedTarget = Mathf.Clamp(_selectedTarget, 0, Mathf.Max(0, _targets.Count - 1));
        _menuScrollPosition = Vector2.zero;
        _menuOpen = true;
        CreateMenuInputBlocker();
    }

    private void CloseMenu()
    {
        _menuOpen = false;
        DestroyMenuInputBlocker();
    }

    private void CreateMenuInputBlocker()
    {
        if (_menuInputBlockerObject != null)
            return;

        _menuInputBlockerObject = new GameObject("FreeFly Teleport Menu Input Blocker");
        _menuInputBlockerWindow = _menuInputBlockerObject.AddComponent<FreeFlyMenuWindow>();
        if (!MenuWindow.AllActiveWindows.Contains(_menuInputBlockerWindow))
            MenuWindow.AllActiveWindows.Add(_menuInputBlockerWindow);
    }

    private void DestroyMenuInputBlocker()
    {
        if (_menuInputBlockerWindow != null)
            MenuWindow.AllActiveWindows.Remove(_menuInputBlockerWindow);

        if (_menuInputBlockerObject != null)
            UnityEngine.Object.Destroy(_menuInputBlockerObject);

        _menuInputBlockerWindow = null;
        _menuInputBlockerObject = null;
    }

    private void RefreshTargets()
    {
        Character? local = Character.localCharacter;
        _targets.Clear();
        if (local == null)
            return;

        foreach (Character target in PlayerHandler.GetAllPlayerCharacters())
        {
            if (target == null || target == local || target.data == null || target.refs == null)
                continue;
            _targets.Add(target);
        }

        _targets.Sort((left, right) => string.Compare(left.characterName, right.characterName, StringComparison.OrdinalIgnoreCase));
        _selectedTarget = Mathf.Clamp(_selectedTarget, 0, Mathf.Max(0, _targets.Count - 1));
    }

    private void HandleMenuInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || ControllerCancelPressed())
        {
            CloseMenu();
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow) || ControllerUpPressed())
            _selectedTarget = Mathf.Max(0, _selectedTarget - 1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || ControllerDownPressed())
            _selectedTarget = Mathf.Min(Mathf.Max(0, _targets.Count - 1), _selectedTarget + 1);
        if (Input.GetKeyDown(KeyCode.Return) || ControllerConfirmPressed())
            TeleportToSelected();
    }

    private void TeleportToSelected()
    {
        Character? local = Character.localCharacter;
        if (!_capabilities.TeleportPatch || local == null || !IsUsable(local) || local.warping ||
            _selectedTarget < 0 || _selectedTarget >= _targets.Count)
            return;

        Character target = _targets[_selectedTarget];
        if (target == null || target.data == null)
            return;

        Vector3 position = target.Center + Vector3.up * _config.SafeTeleportVerticalOffset;
        position -= target.transform.forward * _config.SafeTeleportBackwardOffset;
        if (!IsFinite(position))
            return;

        local.photonView.RPC("WarpPlayerRPC", RpcTarget.All, position, true);
        CloseMenu();
    }

    private bool SpeedUpHeld() => IsKeyHeld(_config.SpeedUpShortcut.Value) ||
                                  (!ControllerChordModifierHeld() &&
                                   ControllerButtonHeld(_config.SpeedUpControllerButton.Value));
    private bool SlowDownHeld() => IsKeyHeld(_config.SlowDownShortcut.Value) ||
                                   (!ControllerChordModifierHeld() &&
                                    ControllerButtonHeld(_config.SlowDownControllerButton.Value));

    private static bool IsKeyHeld(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKey(key);
    }

    private static bool ControllerButtonHeld(ControllerButton button)
    {
        Gamepad? gamepad = Gamepad.current;
        if (gamepad == null)
            return false;

        return button switch
        {
            ControllerButton.LeftShoulder => gamepad.leftShoulder.isPressed,
            ControllerButton.RightShoulder => gamepad.rightShoulder.isPressed,
            ControllerButton.LeftTrigger => gamepad.leftTrigger.isPressed,
            ControllerButton.RightTrigger => gamepad.rightTrigger.isPressed,
            ControllerButton.ButtonSouth => gamepad.buttonSouth.isPressed,
            ControllerButton.ButtonEast => gamepad.buttonEast.isPressed,
            ControllerButton.ButtonWest => gamepad.buttonWest.isPressed,
            ControllerButton.ButtonNorth => gamepad.buttonNorth.isPressed,
            ControllerButton.LeftStickButton => gamepad.leftStickButton.isPressed,
            ControllerButton.RightStickButton => gamepad.rightStickButton.isPressed,
            _ => false
        };
    }

    private bool ControllerChordModifierHeld() => _controllerChordModifierAction?.IsPressed() == true;
    private bool ControllerFlightPressed() => _controllerFlightToggleAction?.WasPressedThisFrame() == true;
    private bool ControllerTeleportMenuPressed() => _controllerTeleportMenuToggleAction?.WasPressedThisFrame() == true;
    private static bool ControllerAscendHeld() => Gamepad.current?.buttonSouth.isPressed == true;
    private static bool ControllerDescendHeld() => Gamepad.current?.buttonEast.isPressed == true;
    private static bool ControllerConfirmPressed() => Gamepad.current?.buttonSouth.wasPressedThisFrame == true;
    private static bool ControllerCancelPressed() => Gamepad.current?.buttonEast.wasPressedThisFrame == true;
    private static bool ControllerUpPressed() => Gamepad.current?.dpad.up.wasPressedThisFrame == true;
    private static bool ControllerDownPressed() => Gamepad.current?.dpad.down.wasPressedThisFrame == true;

    private static bool IsFinite(Vector3 value) =>
        IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static string NormalizeBindingPath(string? path)
    {
        string normalized = path?.Trim() ?? string.Empty;
        return string.Equals(normalized, "None", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : normalized;
    }
}

internal sealed class FreeFlyMenuWindow : MenuWindow
{
    public override bool selectOnOpen => false;
}
