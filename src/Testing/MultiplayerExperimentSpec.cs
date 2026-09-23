using System.Text.Json;

namespace CombatSolver;

internal sealed record MultiplayerExperimentOptions
{
    public int SchemaVersion { get; init; } = 1;
    public string RootManifestPath { get; init; } = "";
    public string CandidateId { get; init; } = "";
    public string PartnerPolicy { get; init; } = "immediate_attack";
    public string Schedule { get; init; } = "alternate_one_action";
    public string RequestCadence { get; init; } = "after_peer_batch";
    public string Profile { get; init; } = "smoke_350";
    public string Mode { get; init; } = "Pilot";
    public bool CreditSharedDamage { get; init; } = true;
    public int EnemyCycleLimit { get; init; } = 12;
    public int DecisionLimit { get; init; } = 256;
    public int QueryLimit { get; init; } = 24;
    public string? ExpectedOpeningPath { get; init; }
    public string? ExpectedFirstCardId { get; init; }
}

internal sealed record MultiplayerExperimentCard(string CardId, int Count);
internal sealed class MultiplayerExperimentBoundaryException(string outcome, string message)
    : InvalidOperationException(message)
{
    internal string Outcome { get; } = outcome;
}
internal sealed record MultiplayerExperimentPlayer(string CharacterId, MultiplayerExperimentCard[] Deck);
internal sealed record MultiplayerExperimentRoot(string Id, string Seed, string EncounterId,
    int MinimumActiveEnemies, string PlannedMechanismStratum,
    MultiplayerExperimentPlayer Local, MultiplayerExperimentPlayer Peer);

internal sealed record MultiplayerExperimentSpec(MultiplayerExperimentOptions Options,
    MultiplayerExperimentRoot Root, int CurrentHp, int Ascension, int ActIndex,
    string SourceCluster, string[] PeerAllowedCardIds)
{
    internal const string ScenarioId = "MULTIPLAYER-MANUAL-LOOP";

    internal SolverSearchProfile SearchProfile => Options.Profile switch
    {
        "smoke_350" => new(12, 350, 32, 18, 24, 3000),
        "medium_nodes_time_capped" => new(60, 240000, 32, 18, 24, 10000),
        _ => throw new InvalidDataException("Unknown multiplayer experiment profile."),
    };

    internal static MultiplayerExperimentSpec Load(string path)
    {
        var options = JsonSerializer.Deserialize<MultiplayerExperimentOptions>(
            File.ReadAllText(path), UnattendedTestFiles.JsonOptions)
            ?? throw new InvalidDataException("Empty multiplayer experiment configuration.");
        if (options.SchemaVersion != 1
            || options.PartnerPolicy is not ("immediate_attack" or "survival_first")
            || options.Schedule is not ("local_first" or "peer_batch_first" or "alternate_one_action")
            || options.RequestCadence is not ("turn_start" or "after_peer_batch")
            || options.Mode is not ("Contract" or "Pilot" or "FirstRequest" or "RootIsolation")
            || options.EnemyCycleLimit is < 1 or > 12
            || options.DecisionLimit is < 1 or > 256 || options.QueryLimit is < 1 or > 24)
            throw new InvalidDataException("Unsupported multiplayer experiment configuration.");
        string manifestPath = Path.GetFullPath(options.RootManifestPath, Path.GetDirectoryName(Path.GetFullPath(path))!);
        options = options with
        {
            RootManifestPath = manifestPath,
            ExpectedOpeningPath = options.ExpectedOpeningPath is { } expected
                ? Path.GetFullPath(expected, Path.GetDirectoryName(Path.GetFullPath(path))!) : null,
        };
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement common = manifest.RootElement.GetProperty("common");
        if (common.GetProperty("partySize").GetInt32() != 2
            || common.GetProperty("localNetId").GetUInt64() != 1
            || common.GetProperty("peerNetId").GetUInt64() != 2
            || common.GetProperty("upgradesPerCard").GetInt32() != 0
            || common.GetProperty("potionsPerPlayer").GetArrayLength() != 0
            || common.GetProperty("enchantmentsPerCard").GetArrayLength() != 0)
            throw new InvalidDataException("This pilot only supports the registered unupgraded two-player recipes.");
        MultiplayerExperimentRoot[] roots = manifest.RootElement.GetProperty("candidates")
            .Deserialize<MultiplayerExperimentRoot[]>(UnattendedTestFiles.JsonOptions)!;
        MultiplayerExperimentRoot root = roots.Single(candidate => candidate.Id == options.CandidateId);
        string[] allowed = manifest.RootElement.GetProperty("peerAllowedCardIds").Deserialize<string[]>()!;
        foreach (var player in new[] { root.Local, root.Peer })
            if (player.Deck.Sum(card => card.Count) < 10 || player.Deck.Any(card => card.Count <= 0)
                || player.Deck.Select(card => card.CardId).Distinct().Count() != player.Deck.Length)
                throw new InvalidDataException("Invalid complete experiment deck.");
        if (root.Peer.Deck.Any(card => !allowed.Contains(card.CardId, StringComparer.Ordinal)))
            throw new InvalidDataException("Peer deck contains an unregistered choice source.");
        var spec = new MultiplayerExperimentSpec(options, root,
            common.GetProperty("currentHpPerPlayer").GetInt32(), common.GetProperty("ascension").GetInt32(),
            common.GetProperty("actIndexForTest").GetInt32(),
            manifest.RootElement.GetProperty("sourceCluster").GetString()!, allowed);
        _ = spec.SearchProfile;
        return spec;
    }
}
