using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilMethodAttributes = Mono.Cecil.MethodAttributes;

namespace SpireAdvisorMultiplayerFix;

internal static class Patcher
{
    internal const string Version = "0.1.3";
    internal const string AccessType = "RealtimeAdvice.GameAccess";
    internal const string ResolverName = "ResolveLocalPlayer";
    internal static readonly string[] ReplacedMethods = ["ReadDeckCards", "ReadPlayerHp", "ReadOwnedRelics"];

    internal static byte[] Create(string inputPath, string gameDirectory)
    {
        using DefaultAssemblyResolver assemblyResolver = new();
        assemblyResolver.AddSearchDirectory(gameDirectory);
        assemblyResolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        using AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(inputPath,
            new ReaderParameters { AssemblyResolver = assemblyResolver });
        using AssemblyDefinition game = AssemblyDefinition.ReadAssembly(Path.Combine(gameDirectory, "sts2.dll"),
            new ReaderParameters { AssemblyResolver = assemblyResolver });
        ModuleDefinition module = assembly.MainModule;
        TypeDefinition access = module.GetType(AccessType)
            ?? throw new InvalidDataException("The input is not the supported RealtimeAdvice mod.");

        if (assembly.Name.HasPublicKey || module.Types.Single(type => type.Name == "<Module>").Methods.Count != 0)
            throw new InvalidDataException("Signed assemblies and module initializers are not supported.");
        if (access.Methods.Any(method => method.Name == ResolverName))
            throw new InvalidDataException("The local-player fix is already present.");
        MethodDefinition deck = Method(access, "ReadDeckCards");
        if (!deck.Body.Instructions.Any(instruction => instruction.Operand is MethodReference method
                && method.Name == "ExtractDeckRecursive" && method.DeclaringType.FullName == AccessType))
            throw new InvalidDataException("The input deck reader differs from the reviewed version.");

        TypeDefinition player = GameType("MegaCrit.Sts2.Core.Entities.Players.Player");
        TypeDefinition runManager = GameType("MegaCrit.Sts2.Core.Runs.RunManager");
        TypeDefinition localContext = GameType("MegaCrit.Sts2.Core.Context.LocalContext");
        TypeDefinition creature = GameType("MegaCrit.Sts2.Core.Entities.Creatures.Creature");
        TypeDefinition pile = GameType("MegaCrit.Sts2.Core.Entities.Cards.CardPile");
        MethodDefinition getMe = localContext.Methods.Single(method => method.Name == "GetMe"
            && method.Parameters.Count == 1
            && method.Parameters[0].ParameterType.FullName == "MegaCrit.Sts2.Core.Runs.IPlayerCollection");

        MethodDefinition resolve = new(ResolverName,
            CecilMethodAttributes.Private | CecilMethodAttributes.Static | CecilMethodAttributes.HideBySig,
            module.ImportReference(player));
        access.Methods.Add(resolve);
        ILProcessor il = resolve.Body.GetILProcessor();
        // LocalContext owns the network identity, including the single-player identity.
        il.Emit(OpCodes.Call, Import(Method(runManager, "get_Instance")));
        il.Emit(OpCodes.Callvirt, Import(Method(runManager, "DebugOnlyGetState")));
        il.Emit(OpCodes.Call, Import(getMe));
        il.Emit(OpCodes.Ret);

        MethodReference emptyDeck = module.ImportReference(typeof(List<object>).GetConstructor(Type.EmptyTypes)!);
        MethodReference addCards = module.ImportReference(typeof(List<object>).GetMethod(nameof(List<object>.AddRange))!);
        il = Reset(deck);
        VariableDefinition localPlayer = Local(deck, resolve.ReturnType);
        VariableDefinition cards = Local(deck, deck.ReturnType);
        il.Emit(OpCodes.Call, resolve);
        il.Emit(OpCodes.Stloc, localPlayer);
        il.Emit(OpCodes.Newobj, emptyDeck);
        il.Emit(OpCodes.Stloc, cards);
        Instruction returnCards = Instruction.Create(OpCodes.Ldloc, cards);
        il.Emit(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Brfalse, returnCards);
        il.Emit(OpCodes.Ldloc, cards);
        il.Emit(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Callvirt, Import(Method(player, "get_Deck")));
        il.Emit(OpCodes.Callvirt, Import(Method(pile, "get_Cards")));
        il.Emit(OpCodes.Callvirt, addCards);
        il.Append(returnCards);
        il.Emit(OpCodes.Ret);

        MethodDefinition hp = Method(access, "ReadPlayerHp");
        MethodReference hpPair = module.ImportReference(typeof(ValueTuple<int, int>).GetConstructor([typeof(int), typeof(int)])!);
        il = Reset(hp);
        localPlayer = Local(hp, resolve.ReturnType);
        il.Emit(OpCodes.Call, resolve);
        il.Emit(OpCodes.Stloc, localPlayer);
        Instruction readHp = Instruction.Create(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Brtrue, readHp);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Newobj, hpPair);
        il.Emit(OpCodes.Ret);
        il.Append(readHp);
        il.Emit(OpCodes.Callvirt, Import(Method(player, "get_Creature")));
        il.Emit(OpCodes.Callvirt, Import(Method(creature, "get_CurrentHp")));
        il.Emit(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Callvirt, Import(Method(player, "get_Creature")));
        il.Emit(OpCodes.Callvirt, Import(Method(creature, "get_MaxHp")));
        il.Emit(OpCodes.Newobj, hpPair);
        il.Emit(OpCodes.Ret);

        MethodDefinition relics = Method(access, "ReadOwnedRelics");
        il = Reset(relics);
        localPlayer = Local(relics, resolve.ReturnType);
        VariableDefinition ids = Local(relics, relics.ReturnType);
        il.Emit(OpCodes.Call, resolve);
        il.Emit(OpCodes.Stloc, localPlayer);
        il.Emit(OpCodes.Newobj, module.ImportReference(typeof(List<string>).GetConstructor(Type.EmptyTypes)!));
        il.Emit(OpCodes.Stloc, ids);
        Instruction returnIds = Instruction.Create(OpCodes.Ldloc, ids);
        il.Emit(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Brfalse, returnIds);
        il.Emit(OpCodes.Ldloc, localPlayer);
        il.Emit(OpCodes.Ldloc, ids);
        // Retain the original ID normalization, but never reuse an unowned five-second cache.
        il.Emit(OpCodes.Call, Method(access, "CollectRelicIds"));
        il.Emit(OpCodes.Pop);
        il.Append(returnIds);
        il.Emit(OpCodes.Ret);

        assembly.Name.Version = new Version(0, 1, 3, 0);
        SetVersionAttribute(typeof(AssemblyFileVersionAttribute), Version + ".0");
        SetVersionAttribute(typeof(AssemblyInformationalVersionAttribute), Version + "+local-player-fix");
        using MemoryStream output = new();
        assembly.Write(output);
        return output.ToArray();

        TypeDefinition GameType(string name) => game.MainModule.GetType(name)
            ?? throw new InvalidDataException("Missing game API: " + name);

        MethodReference Import(MethodDefinition method)
        {
            if (!method.IsPublic)
                throw new InvalidDataException("The fix only calls public game APIs: " + method.FullName);
            return module.ImportReference(method);
        }

        void SetVersionAttribute(Type type, string value)
        {
            foreach (CustomAttribute old in assembly.CustomAttributes.Where(attribute => attribute.AttributeType.FullName == type.FullName).ToArray())
                assembly.CustomAttributes.Remove(old);
            CustomAttribute attribute = new(module.ImportReference(type.GetConstructor([typeof(string)])!));
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
            assembly.CustomAttributes.Add(attribute);
        }
    }

    private static MethodDefinition Method(TypeDefinition type, string name) => type.Methods.Single(method => method.Name == name);

    private static ILProcessor Reset(MethodDefinition method)
    {
        method.Body = new Mono.Cecil.Cil.MethodBody(method) { InitLocals = true };
        return method.Body.GetILProcessor();
    }

    private static VariableDefinition Local(MethodDefinition method, TypeReference type)
    {
        VariableDefinition local = new(type);
        method.Body.Variables.Add(local);
        return local;
    }
}
