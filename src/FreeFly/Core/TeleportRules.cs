using System;

namespace FreeFly.Core;

internal enum TeleportEntryKind { Location, Player, BringAll }
internal enum TeleportAction { Primary, Secondary }
internal enum TeleportOperation { MoveSelfToDestination, BringPlayerToSelf, MoveAllToDestination, BringAllToSelf }

internal static class TeleportRules
{
    public static TeleportOperation Resolve(TeleportEntryKind kind, TeleportAction action) => kind switch
    {
        TeleportEntryKind.BringAll => TeleportOperation.BringAllToSelf,
        TeleportEntryKind.Player => action == TeleportAction.Secondary
            ? TeleportOperation.BringPlayerToSelf : TeleportOperation.MoveSelfToDestination,
        TeleportEntryKind.Location => action == TeleportAction.Secondary
            ? TeleportOperation.MoveAllToDestination : TeleportOperation.MoveSelfToDestination,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static bool CanExecute(TeleportOperation operation, bool isHost) => operation switch
    {
        TeleportOperation.MoveSelfToDestination => true,
        TeleportOperation.BringPlayerToSelf or TeleportOperation.MoveAllToDestination or TeleportOperation.BringAllToSelf => isHost,
        _ => false
    };

    public static bool CanMovePlayer(TeleportOperation operation, bool dead, bool warping) =>
        !warping && (!dead || operation == TeleportOperation.MoveSelfToDestination);
}
