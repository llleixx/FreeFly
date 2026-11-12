using FreeFly.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FreeFly;

// A menu entry may represent a destination or the explicit summon command.
internal sealed class TeleportMenuEntry
{
    private TeleportMenuEntry(string id, string label, TeleportEntryKind kind,
        TeleportDestination? destination, bool enabled)
    {
        Id = id;
        Label = label;
        Kind = kind;
        Destination = destination;
        Enabled = enabled;
    }

    public string Id { get; }
    public string Label { get; }
    public TeleportEntryKind Kind { get; }
    public TeleportDestination? Destination { get; }
    public bool Enabled { get; }

    public static TeleportMenuEntry ForLocation(string label, Vector3 position, Transform anchor, bool enabled) =>
        new($"location:{SceneManager.GetActiveScene().handle}:{anchor.GetInstanceID()}", label,
            TeleportEntryKind.Location, TeleportDestination.ForLocation(position, anchor), enabled);

    public static TeleportMenuEntry ForPlayer(Character player, string label) =>
        new($"player:{player.GetInstanceID()}", label, TeleportEntryKind.Player,
            TeleportDestination.ForPlayer(player), true);

    public static TeleportMenuEntry BringAll { get; } =
        new("bring-all", "Bring all players to me", TeleportEntryKind.BringAll, null, true);
}

// Only factories can create destinations, so a player cannot be mistaken for a missing location.
internal sealed class TeleportDestination
{
    private readonly Vector3 _position;
    private readonly Transform? _anchor;
    private readonly bool _isPlayer;

    private TeleportDestination(Vector3 position, Transform? anchor, Character? player, bool isPlayer)
    {
        _position = position;
        _anchor = anchor;
        Player = player;
        _isPlayer = isPlayer;
    }

    public Character? Player { get; }

    public static TeleportDestination ForLocation(Vector3 position, Transform anchor) => new(position, anchor, null, false);
    public static TeleportDestination ForPlayer(Character player) => new(default, null, player, true);

    public bool TryGetPose(out Vector3 position, out Vector3 forward)
    {
        position = default;
        forward = default;
        if (_isPlayer)
        {
            if (Player == null || Player.data == null || Player.transform == null)
                return false;
            position = Player.data.dead ? Player.GetSpectatePosition() : Player.Center;
            forward = Player.transform.forward;
        }
        else
        {
            if (_anchor == null)
                return false;
            position = _position;
            forward = _anchor.forward;
        }
        return FreeFlyCharacterUtils.IsFinite(position) && FreeFlyCharacterUtils.IsFinite(forward);
    }
}
