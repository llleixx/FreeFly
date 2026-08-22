using System.Collections.Generic;
using BepInEx.Logging;
using FreeFly.Core;
using UnityEngine;

namespace FreeFly;

internal enum FlightStopReason
{
    UserToggle,
    FeatureUnavailable,
    CharacterUnavailable,
    WarpStarted,
    PluginDestroyed
}

internal sealed class FlightRuntime
{
    private readonly ModConfig _config;
    private readonly ManualLogSource _logger;
    private readonly FreeFlyInput _input;
    private readonly FreeFlyNotification _notification;
    private readonly Dictionary<Rigidbody, bool> _originalGravity = new();
    private readonly Dictionary<Collider, bool> _originalColliderState = new();
    private Character? _flightCharacter;
    private bool _flightActive;

    public FlightRuntime(ModConfig config, ManualLogSource logger, FreeFlyInput input, FreeFlyNotification notification)
    {
        _config = config;
        _logger = logger;
        _input = input;
        _notification = notification;
    }

    public bool IsActive => _flightActive;
    public Character? ActiveCharacter => _flightCharacter;

    public bool TryStart(Character local)
    {
        if (!FreeFlyCharacterUtils.IsUsable(local) || local.refs.ragdoll == null)
            return false;

        _flightCharacter = local;
        _originalGravity.Clear();
        _originalColliderState.Clear();
        foreach (Bodypart part in local.refs.ragdoll.partList)
        {
            if (part?.Rig != null)
                _originalGravity[part.Rig] = part.Rig.useGravity;
        }

        foreach (Collider collider in local.refs.ragdoll.GetComponentsInChildren<Collider>(includeInactive: true))
        {
            if (collider != null)
                _originalColliderState[collider] = collider.enabled;
        }

        local.refs.ragdoll.ToggleCollision(false);
        _flightActive = true;
        _notification.Show("Free flight enabled");
        _logger.LogInfo("Free flight enabled.");
        return true;
    }

    public void Stop(FlightStopReason reason, bool notify = true)
    {
        if (!_flightActive && _flightCharacter == null)
            return;

        Character? character = _flightCharacter;
        if (character != null && character.refs.ragdoll != null)
        {
            character.refs.ragdoll.ToggleCollision(true);
            foreach (KeyValuePair<Collider, bool> entry in _originalColliderState)
            {
                if (entry.Key != null)
                    entry.Key.enabled = entry.Value;
            }

            foreach (Bodypart part in character.refs.ragdoll.partList)
            {
                if (part?.Rig != null && _originalGravity.TryGetValue(part.Rig, out bool gravity))
                    part.Rig.useGravity = gravity;
            }
            character.refs.ragdoll.HaltBodyVelocity();
        }

        _originalGravity.Clear();
        _originalColliderState.Clear();
        _flightCharacter = null;
        bool wasActive = _flightActive;
        _flightActive = false;
        if (!wasActive)
            return;

        if (notify)
            _notification.Show("Free flight disabled");
        _logger.LogDebug($"Free flight disabled: {GetReasonText(reason)}.");
    }

    public void ApplyPhysics(CharacterMovement movement, bool menuOpen)
    {
        if (!_flightActive || _flightCharacter == null)
            return;

        if (movement != _flightCharacter.refs.movement)
            return;

        Character local = _flightCharacter;
        if (!FreeFlyCharacterUtils.IsUsable(local) || local.warping)
        {
            Stop(local.warping ? FlightStopReason.WarpStarted : FlightStopReason.CharacterUnavailable);
            return;
        }

        CharacterRagdoll ragdoll = local.refs.ragdoll;
        foreach (Bodypart part in ragdoll.partList)
        {
            if (part?.Rig != null)
                part.Rig.useGravity = false;
        }

        Vector2 input = _input.GetMovementInput(local, menuOpen);
        Vector3 forward = local.data.lookDirection_Flat;
        Vector3 right = local.data.lookDirection_Right;
        Vector3 direction = forward * input.y + right * input.x;
        bool modifierHeld = _input.ChordModifierHeld();
        bool crouchHeld = _input.IsCrouchHeld(local, menuOpen) ||
                          CharacterInput.action_crouchToggle?.IsPressed() == true;
        float vertical = modifierHeld
            ? 0f
            : (_input.IsJumpHeld(local, menuOpen) ? 1f : 0f);
        vertical -= modifierHeld
            ? 0f
            : (crouchHeld ? 1f : 0f);
        direction += Vector3.up * vertical;
        direction = Vector3.ClampMagnitude(direction, 1f);

        float speed = FreeFlyMath.ApplySpeedModifiers(
            _config.SafeBaseSpeed,
            _input.SpeedUpHeld(),
            _input.SlowDownHeld(),
            _config.SafeSpeedUpMultiplier,
            _config.SafeSlowDownMultiplier);

        Vector3 delta = direction * speed * Time.fixedDeltaTime;
        Vector3 velocity = direction * speed;
        ragdoll.HaltBodyVelocity();
        foreach (Bodypart part in ragdoll.partList)
        {
            if (part?.Rig == null)
                continue;

            if (part.Rig.isKinematic)
            {
                if (FreeFlyCharacterUtils.IsFinite(delta) && delta.sqrMagnitude > 0f)
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

    private static string GetReasonText(FlightStopReason reason) => reason switch
    {
        FlightStopReason.UserToggle => "toggle pressed",
        FlightStopReason.FeatureUnavailable => "feature unavailable",
        FlightStopReason.CharacterUnavailable => "character became unavailable",
        FlightStopReason.WarpStarted => "character changed or PEAK warp started",
        FlightStopReason.PluginDestroyed => "plugin destroyed",
        _ => "unknown reason"
    };
}
