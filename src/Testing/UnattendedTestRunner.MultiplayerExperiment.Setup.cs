using System.Text.Json;
using System.Text.Json.Nodes;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;

namespace CombatSolver;

internal sealed partial class UnattendedTestRunner
{
    private sealed partial class ScenarioBuilder
    {
        private void PrepareMultiplayerExperiment()
        {
            if (runner._protocolHost.MultiplayerExperiment is not { } spec) return;
            // Native packet writers share a plain Dictionary across replay/export workers.
            // Materialize its immutable game-enum entries before this test starts a run.
            var enumGetter = typeof(MaxEnumValueCache).GetMethod(nameof(MaxEnumValueCache.Get))!;
            int warmedEnums = 0;
            foreach (Type type in typeof(Player).Assembly.GetTypes().Where(type => type.IsEnum
                && Enum.GetUnderlyingType(type) == typeof(int) && Enum.GetValues(type).Length > 0))
            {
                _ = enumGetter.MakeGenericMethod(type).Invoke(null, null);
                warmedEnums++;
            }
            runner._completedChecks.Add("NativePacketEnumWarmup:" + warmedEnums);
            JsonObject json = JsonSerializer.SerializeToNode(runner._request, UnattendedTestFiles.JsonOptions)!.AsObject();
            json["characterId"] = spec.Root.Local.CharacterId;
            json["encounterId"] = spec.Root.EncounterId;
            json["seed"] = spec.Root.Seed;
            json["ascension"] = spec.Ascension;
            json["actIndexForTest"] = spec.ActIndex;
            json["preserveNativeCombatStateForTest"] = true;
            json["fixedSearchBudget"] = true;
            json["searchBudgetOverrideMilliseconds"] = spec.SearchProfile.SoftTimeBudgetMilliseconds;
            json["searchMaxDegreeOfParallelismForTest"] = 1;
            json["headlessFastModeForTest"] = "Instant";
            runner._request = json.Deserialize<UnattendedTestRequest>(UnattendedTestFiles.JsonOptions)!;
            runner.ApplyHeadlessFastModeOverride();
            runner._writer.WriteGeneratedArtifact("multiplayer-experiment.resolved.json", spec);
        }

        private async Task PrepareMultiplayerExperimentDecks(RunState run)
        {
            if (runner._protocolHost.MultiplayerExperiment is not { } spec) return;
            Player local = LocalContext.GetMe(run) ?? throw new InvalidOperationException("Missing experiment local player.");
            if (local.NetId != 1 || run.Players.Count != 2)
                throw new InvalidDataException("Experiment local identity or party size mismatch.");
            foreach (Player player in run.Players)
            {
                var recipe = player == local ? spec.Root.Local : spec.Root.Peer;
                if (!ModelMatches(player.Character, recipe.CharacterId) || spec.CurrentHp > player.Creature.MaxHp)
                    throw new InvalidDataException("Experiment character or HP mismatch.");
                ClearRunDeck(run, player);
                foreach (var entry in recipe.Deck)
                    await InjectRunCardAsync(run, player, new() { CardId = entry.CardId, Count = entry.Count });
                foreach (var potion in player.PotionSlots.ToArray()) potion?.Discard();
                player.Creature.SetCurrentHpInternal(spec.CurrentHp);
                if (player.Deck.Cards.Count != recipe.Deck.Sum(card => card.Count))
                    throw new InvalidDataException("Complete experiment deck was not installed.");
            }
        }
    }
}
