using FreeFly.Core;
using NUnit.Framework;

namespace FreeFly.Core.Tests;

public sealed class TeleportRulesTests
{
    [Test]
    public void NonHostsRetainSelfTeleportToLocationsAndPlayers()
    {
        foreach (TeleportEntryKind kind in new[] { TeleportEntryKind.Location, TeleportEntryKind.Player })
        {
            TeleportOperation operation = TeleportRules.Resolve(kind, TeleportAction.Primary);
            Assert.That(operation, Is.EqualTo(TeleportOperation.MoveSelfToDestination));
            Assert.That(TeleportRules.CanExecute(operation, isHost: false), Is.True);
        }
    }

    [Test]
    public void SecondaryActionsHaveTheCorrectScopeAndRequireHost()
    {
        TeleportOperation player = TeleportRules.Resolve(TeleportEntryKind.Player, TeleportAction.Secondary);
        TeleportOperation location = TeleportRules.Resolve(TeleportEntryKind.Location, TeleportAction.Secondary);
        Assert.That(player, Is.EqualTo(TeleportOperation.BringPlayerToSelf));
        Assert.That(location, Is.EqualTo(TeleportOperation.MoveAllToDestination));
        foreach (TeleportOperation operation in new[] { player, location })
        {
            Assert.That(TeleportRules.CanExecute(operation, isHost: false), Is.False);
            Assert.That(TeleportRules.CanExecute(operation, isHost: true), Is.True);
        }
    }

    [Test]
    public void SummonCommandHasTheSameHostOnlyBehaviorForBothButtons()
    {
        foreach (TeleportAction action in new[] { TeleportAction.Primary, TeleportAction.Secondary })
        {
            TeleportOperation operation = TeleportRules.Resolve(TeleportEntryKind.BringAll, action);
            Assert.That(operation, Is.EqualTo(TeleportOperation.BringAllToSelf));
            Assert.That(TeleportRules.CanExecute(operation, isHost: false), Is.False);
            Assert.That(TeleportRules.CanExecute(operation, isHost: true), Is.True);
        }
    }

    [Test]
    public void DeadPlayersAreSkippedByNewActionsAndWarpingPlayersAreAlwaysSkipped()
    {
        foreach (TeleportOperation operation in Enum.GetValues<TeleportOperation>())
        {
            Assert.That(TeleportRules.CanMovePlayer(operation, dead: false, warping: false), Is.True);
            Assert.That(TeleportRules.CanMovePlayer(operation, dead: true, warping: false),
                Is.EqualTo(operation == TeleportOperation.MoveSelfToDestination));
            Assert.That(TeleportRules.CanMovePlayer(operation, dead: false, warping: true), Is.False);
            Assert.That(TeleportRules.CanMovePlayer(operation, dead: true, warping: true), Is.False);
        }
    }
}
