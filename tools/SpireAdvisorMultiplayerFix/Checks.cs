using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SpireAdvisorMultiplayerFix;

internal static class Checks
{
    private static int _assertions;

    internal static void Run(string inputPath, string gameDirectory)
    {
        byte[] patched = Patcher.Create(inputPath, gameDirectory);
        CheckPreservation(inputPath, patched);
        ModContext baselineContext = new("advisor-baseline");
        ModContext fixedContext = new("advisor-fixed");
        Assembly original = baselineContext.LoadFromAssemblyPath(inputPath);
        using MemoryStream stream = new(patched);
        Assembly updated = fixedContext.LoadFromStream(stream);
        Type originalAccess = original.GetType(Patcher.AccessType, throwOnError: true)!;
        Type access = updated.GetType(Patcher.AccessType, throwOnError: true)!;

        // These are native managed game models in a separate .NET process. No mod
        // initializer, Godot loop, Steam session, or save/load entry point is called.
        Player[] players = Enumerable.Range(0, 4).Select(index => MakePlayer(76_561_198_000_000_001UL + (ulong)index, index)).ToArray();
        RunState run = SetRun(players);
        LocalContext.NetId = players[1].NetId;
        List<object> oldDeck = [];
        originalAccess.GetMethod("ExtractDeckRecursive", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [run, 0, new HashSet<object>(), oldDeck]);
        Require(oldDeck.SequenceEqual(players[0].Deck.Cards), "The original host-deck failure was not reproduced.");
        Require(!oldDeck.SequenceEqual(players[1].Deck.Cards), "The failure fixture accidentally uses the guest deck.");
        Console.WriteLine("BASELINE_REPRODUCED local_seat=1 deck_owner=0");

        int seatCases = 0;
        foreach (int count in new[] { 1, 2, 3, 4 })
        {
            foreach (Player[] order in new[] { players.Take(count).ToArray(), players.Take(count).Reverse().ToArray() })
            {
                SetRun(order);
                for (int seat = 0; seat < count; seat++)
                {
                    LocalContext.NetId = order[seat].NetId;
                    VerifyPlayer(access, order[seat]);
                    seatCases++;
                }
            }
        }

        SetRun(players);
        LocalContext.NetId = players[1].NetId;
        access.GetField("_ownedRelicCache", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, new List<string> { "OTHER_PLAYER_STALE_RELIC" });
        VerifyPlayer(access, players[1]);

        List<CardModel> localCards = (List<CardModel>)players[1].Deck.Cards;
        localCards.Clear();
        Require(Call<List<object>>(access, "ReadDeckCards").Count == 0, "An empty local deck borrowed a teammate's deck.");
        CardModel added = MakeModel<StrikeIronclad>("CARD", "GUEST_NEW_CARD");
        localCards.Add(added);
        Require(ReferenceEquals(Call<List<object>>(access, "ReadDeckCards").Single(), added), "The deck did not refresh after a pick.");
        List<object> detached = Call<List<object>>(access, "ReadDeckCards");
        detached.Clear();
        Require(localCards.Count == 1, "A caller could mutate the real deck through the returned list.");

        List<RelicModel> localRelics = (List<RelicModel>)players[1].Relics;
        localRelics.Add(MakeModel<BurningBlood>("RELIC", "GUEST_NEW_RELIC"));
        Require(Call<List<string>>(access, "ReadOwnedRelics").Contains("GUEST_NEW_RELIC"), "A new relic was hidden by a stale cache.");
        localRelics.Clear();
        Require(Call<List<string>>(access, "ReadOwnedRelics").Count == 0, "An empty local relic list borrowed another player's relics.");
        SetField(players[1].Creature, "_currentHp", 0);
        Require(Call<(int, int)>(access, "ReadPlayerHp") == (0, players[1].Creature.MaxHp), "A dead local player borrowed a living teammate's health.");

        LocalContext.NetId = null;
        VerifyUnavailable(access);
        LocalContext.NetId = ulong.MaxValue;
        foreach (string name in Patcher.ReplacedMethods)
        {
            bool failed = false;
            try
            {
                access.GetMethod(name)!.Invoke(null, null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
            {
                failed = true;
            }
            Require(failed, name + " silently accepted a local ID absent from the roster.");
        }

        Player zeroId = MakePlayer(0, 4);
        SetRun([players[0], zeroId]);
        LocalContext.NetId = 0;
        VerifyPlayer(access, zeroId);
        Player rejoined = MakePlayer(players[1].NetId, 5);
        SetRun([players[0], rejoined]);
        LocalContext.NetId = rejoined.NetId;
        VerifyPlayer(access, rejoined);
        SetField(RunManager.Instance, "<State>k__BackingField", null);
        VerifyUnavailable(access);
        LocalContext.NetId = null;

        Console.WriteLine($"PASSED seat_cases={seatCases} assertions={_assertions} native_models=true game_started=false");
        baselineContext.Unload();
        fixedContext.Unload();
    }

    private static void VerifyPlayer(Type access, Player player)
    {
        List<object> cards = Call<List<object>>(access, "ReadDeckCards");
        Require(cards.SequenceEqual(player.Deck.Cards), "Deck order, multiplicity, or owner differs.");
        Require(Call<(int, int)>(access, "ReadPlayerHp") == (player.Creature.CurrentHp, player.Creature.MaxHp), "Health belongs to another player.");
        Require(Call<List<string>>(access, "ReadOwnedRelics").SequenceEqual(player.Relics.Select(relic => relic.Id.Entry).Distinct()), "Relics belong to another player.");
    }

    private static void VerifyUnavailable(Type access)
    {
        Require(Call<List<object>>(access, "ReadDeckCards").Count == 0, "An unavailable player resolved to another deck.");
        Require(Call<List<string>>(access, "ReadOwnedRelics").Count == 0, "An unavailable player resolved to another relic list.");
        Require(Call<(int, int)>(access, "ReadPlayerHp") == (0, 0), "An unavailable player resolved to another health pair.");
    }

    private static T Call<T>(Type type, string name) => (T)type.GetMethod(name)!.Invoke(null, null)!;

    private static Player MakePlayer(ulong id, int index)
    {
        Player player = Blank<Player>();
        Creature creature = Blank<Creature>();
        SetField(creature, "_currentHp", 10 + index);
        SetField(creature, "_maxHp", 70 + index);
        SetField(player, "<Creature>k__BackingField", creature);
        SetField(player, "<NetId>k__BackingField", id);
        CardPile deck = new(PileType.Deck);
        CardModel first = MakeModel<StrikeIronclad>("CARD", "PLAYER_" + index + "_A");
        CardModel second = MakeModel<StrikeIronclad>("CARD", "PLAYER_" + index + "_B");
        SetField(deck, "_cards", new List<CardModel> { first, second, first });
        SetField(player, "<Deck>k__BackingField", deck);
        SetField(player, "_relics", new List<RelicModel>
        {
            MakeModel<BurningBlood>("RELIC", "PLAYER_" + index),
            MakeModel<BurningBlood>("RELIC", "PLAYER_" + index)
        });
        return player;
    }

    private static T MakeModel<T>(string category, string entry) where T : AbstractModel
    {
        T model = Blank<T>();
        SetField(model, "<Id>k__BackingField", new ModelId(category, entry));
        return model;
    }

    private static T Blank<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static RunState SetRun(IEnumerable<Player> players)
    {
        RunState run = Blank<RunState>();
        SetField(run, "_players", players.ToList());
        SetField(RunManager.Instance, "<State>k__BackingField", run);
        return run;
    }

    private static void SetField(object target, string name, object? value)
    {
        for (Type? type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo? field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null)
                continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }

    private static void CheckPreservation(string inputPath, byte[] patched)
    {
        using AssemblyDefinition before = AssemblyDefinition.ReadAssembly(inputPath);
        using MemoryStream stream = new(patched);
        using AssemblyDefinition after = AssemblyDefinition.ReadAssembly(stream);
        Dictionary<string, string> oldBodies = Bodies(before.MainModule);
        Dictionary<string, string> newBodies = Bodies(after.MainModule);
        HashSet<string> allowed = before.MainModule.GetType(Patcher.AccessType).Methods
            .Where(method => Patcher.ReplacedMethods.Contains(method.Name)).Select(method => method.FullName).ToHashSet();
        foreach ((string name, string body) in oldBodies)
            Require(allowed.Contains(name) || newBodies[name] == body, "Unrelated method changed: " + name);
        Require(newBodies.Count == oldBodies.Count + 1, "Unexpected added or removed methods.");
        Require(before.MainModule.Resources.Count == after.MainModule.Resources.Count, "Embedded resources were added or removed.");
        foreach (EmbeddedResource resource in before.MainModule.Resources.Cast<EmbeddedResource>())
        {
            EmbeddedResource updated = (EmbeddedResource)after.MainModule.Resources.Single(item => item.Name == resource.Name);
            Require(resource.GetResourceData().SequenceEqual(updated.GetResourceData()), "Embedded data changed: " + resource.Name);
        }
        Require(after.MainModule.AssemblyReferences.All(reference => !reference.Name.Contains("Cecil") && !reference.Name.Contains("SpireAdvisorMultiplayerFix")), "Build tooling leaked into runtime dependencies.");
        Console.WriteLine($"PRESERVED unrelated_methods={oldBodies.Count - allowed.Count} resources={before.MainModule.Resources.Count}");
    }

    private static Dictionary<string, string> Bodies(ModuleDefinition module) => module.GetTypes()
        .SelectMany(type => type.Methods).Where(method => method.HasBody)
        .ToDictionary(method => method.FullName, method =>
        {
            Mono.Cecil.Cil.MethodBody body = method.Body;
            int Index(Instruction? instruction) => instruction == null ? -1 : body.Instructions.IndexOf(instruction);
            string Operand(object? operand) => operand switch
            {
                Instruction instruction => "@" + Index(instruction),
                Instruction[] targets => string.Join(",", targets.Select(Index)),
                VariableDefinition variable => "$" + variable.Index,
                ParameterDefinition parameter => "arg" + parameter.Index,
                _ => operand?.ToString() ?? ""
            };
            return string.Join(";", body.Variables.Select(variable => variable.VariableType.FullName)) + "\n"
                + string.Join("\n", body.Instructions.Select(instruction => instruction.OpCode + " " + Operand(instruction.Operand))) + "\n"
                + string.Join("\n", body.ExceptionHandlers.Select(handler => $"{handler.HandlerType}:{handler.CatchType}:{Index(handler.TryStart)}:{Index(handler.TryEnd)}:{Index(handler.HandlerStart)}:{Index(handler.HandlerEnd)}:{Index(handler.FilterStart)}"));
        });

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        _assertions++;
    }

    private sealed class ModContext(string name) : AssemblyLoadContext(name, isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name) => Default.LoadFromAssemblyName(name);
    }
}
