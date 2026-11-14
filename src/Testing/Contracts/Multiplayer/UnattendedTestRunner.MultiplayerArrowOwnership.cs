using CombatSolver.Engine.InCombat.Simulation;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private async Task AssertMultiplayerArrowOwnershipAsync(CombatState combat)
    {
        Player local = LocalContext.GetMe(combat) ?? throw new InvalidOperationException("Missing local player.");
        Player peer = combat.Players.Single(player => player != local);
        Creature enemy = combat.Enemies.Single();
        foreach (Player player in combat.Players)
        {
            foreach (var relic in player.Relics.ToArray()) await RelicCmd.Remove(relic);
            foreach (var power in player.Creature.Powers.ToArray()) await PowerCmd.Remove(power);
            foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
            await ClearPlayerPilesAsync(player);
            SetEnergy(player, 99);
            await InjectCardAsync(combat, player, new() { CardId = "QUICK_SHOT", Pile = "Hand", Count = 3 });
            await InjectCardAsync(combat, player, new() { CardId = "STRIKE_FOOL", Pile = "Hand", Count = 3 });
        }
        foreach (var power in enemy.Powers.ToArray()) await PowerCmd.Remove(power);
        enemy.SetMaxHpInternal(999);
        enemy.SetCurrentHpInternal(999);
        await InjectCardAsync(combat, local, new() { CardId = "QUIVER", Pile = "Hand" });

        async Task NativePlay(CardModel card, Creature? target)
        {
            (int energy, int stars) = await card.SpendResources();
            await card.OnPlayWrapper(new ThrowingPlayerChoiceContext(), target, isAutoPlay: false,
                new ResourceInfo { EnergySpent = energy, EnergyValue = energy, StarsSpent = stars, StarValue = stars },
                skipCardPileVisuals: true);
        }

        SetStage("multiplayer_arrow_peer_setup");
        await NativePlay(FindActualHandCard(peer, "QUICK_SHOT", 0), enemy);
        var root = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        var parent = root.ForkSimulator();
        var sibling = parent.Fork();
        var child = parent.Fork();
        var shadow = (SimulatedCombatState)child.State.CombatState;
        List<string> checks = [];

        void Check(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("Multiplayer arrow contract: " + name);
            checks.Add(name);
        }
        ContinuationStamp Predicted(CombatPredictionSimulator simulator)
            => ContinuationStamp.CapturePredicted(local, simulator, root.StartTurnNumber, root.Forecast, root.StartTurnNumber);
        void Equal(string stage)
        {
            var expected = Predicted(child);
            var actual = ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true);
            if (expected.StateText != actual.StateText)
            {
                _writer.WriteGeneratedArtifact("multiplayer-arrow-difference.json", new
                {
                    stage, expected = expected.StateText, actual = actual.StateText,
                    differences = expected.DescribeDifferences(actual, maximumDifferences: 12),
                });
                throw new InvalidOperationException(stage + ": "
                    + string.Join("; ", expected.DescribeDifferences(actual, maximumDifferences: 12)));
            }
            checks.Add(stage + ":FullNativeState");
        }
        void ArrowReferences(CombatPredictionSimulator branch)
        {
            foreach (ImpaledPower power in ((SimulatedCombatState)branch.State.CombatState).EffectivePowers().OfType<ImpaledPower>())
                foreach (var arrow in power.Arrows)
                    Check(arrow.owner.Creature == power.Applier
                        && branch.State.GetPlayerCombatState(arrow.owner).Arrows.Contains(arrow), "arrow_reference_owner");
        }
        async Task Play(Player owner, string id, Creature? target)
        {
            SetStage("multiplayer_arrow_" + owner.NetId + "_" + id);
            CardModel card = FindActualHandCard(owner, id, 0);
            PlaySimulatedCard(child, shadow, child.State.FindCard(card)!, target, combat.Enemies);
            await NativePlay(card, target);
            Equal(owner.NetId + ":" + id);
            ArrowReferences(child);
        }

        Equal("peer_only_root");
        await Play(local, "QUICK_SHOT", enemy);
        Check(child.ImpaledBy(enemy, local) is { Amount: 1 } && child.ImpaledBy(enemy, peer) is { Amount: 1 },
            "distinct_applier_instances");
        var bothRoot = CombatRootSnapshot.Capture(combat, multiplayerAdvisor: true);
        var both = bothRoot.ForkSimulator();
        string bothInitial = Predicted(both).StateText;
        Check(bothInitial == ContinuationStamp.CaptureLive(combat, multiplayerAdvisor: true).StateText,
            "two_instance_root_native_state");
        ArrowReferences(both.Fork());

        await Play(local, "QUIVER", null);
        await Play(local, "QUICK_SHOT", enemy);
        Check(child.ImpaledBy(enemy, local) is { Amount: 1, Arrows.Count: 2 }
            && child.ImpaledBy(enemy, peer) is { HitCounter: 3, Arrows.Count: 1 }, "same_applier_arrows_share_instance");

        shadow.Apply<ImpaledPower>(enemy, 2, local.Creature);
        await PowerCmd.Apply<ImpaledPower>(new ThrowingPlayerChoiceContext(), enemy, 2, local.Creature, null);
        Equal("same_applier_power_stacking");
        Check(child.ImpaledBy(enemy, local) is { Amount: 3 } && child.ImpaledBy(enemy, peer) is { Amount: 1 },
            "stacking_preserves_peer_amount");
        await Play(local, "STRIKE_FOOL", enemy);
        await Play(local, "STRIKE_FOOL", enemy);
        Check(child.ImpaledBy(enemy, local) is { Arrows.Count: 1 }
            && child.ImpaledBy(enemy, peer) is { HitCounter: 3, Arrows.Count: 1 }, "automatic_retrieval_is_per_applier");
        var lastLocal = child.ImpaledBy(enemy, local)!;
        child.DislodgeArrow(lastLocal, null);
        child.RemoveEmptyImpaled(lastLocal);
        await ArrowCmd.DislodgeArrow(enemy, local, new ThrowingPlayerChoiceContext(), null);
        Equal("local_removed_peer_retained");
        Check(child.ImpaledBy(enemy, local) == null && child.ImpaledBy(enemy, peer) != null,
            "removal_preserves_other_applier");
        await Play(local, "QUICK_SHOT", enemy);
        Check(child.ImpaledBy(enemy, local) is { Amount: 1, HitCounter: 3 }, "reapplication_is_fresh_instance");
        for (int index = 0; index < 3; index++) await Play(peer, "STRIKE_FOOL", enemy);
        Check(child.ImpaledBy(enemy, peer) == null && child.ImpaledBy(enemy, local) is { HitCounter: 3 },
            "peer_retrieval_preserves_local_counter");

        var descendant = child.Fork();
        ArrowReferences(descendant);
        var childPower = child.ImpaledBy(enemy, local)!;
        var descendantPower = descendant.ImpaledBy(enemy, local)!;
        descendantPower.HitCounter--;
        Check(childPower.HitCounter == 3 && !ReferenceEquals(childPower.Arrows[0], descendantPower.Arrows[0]),
            "descendant_power_and_arrow_isolation");
        Check(Predicted(parent).StateText == root.ContinuationStamp.StateText
            && Predicted(sibling).StateText == root.ContinuationStamp.StateText
            && Predicted(both).StateText == bothInitial, "parent_sibling_and_recaptured_root_frozen");
        _writer.WriteGeneratedArtifact("multiplayer-arrow-ownership.json", new
        { passed = true, checks, nativeFullStateCompared = true, networkValidated = false });
        _completedChecks.Add("MultiplayerArrowOwnership:" + checks.Count + ":NativeState:PerApplier:Stack:Retrieve:Reapply:Fork");
    }
}
