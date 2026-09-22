namespace CombatSolver;

internal sealed partial class CombatBeamSolver
{
    private MultiplayerFinalBatch PrepareMultiplayerPublicationCandidates(IEnumerable<SearchNode> nodes,
        IReadOnlyList<SearchNode>? retainedScope, long elapsedMilliseconds)
        => PrepareMultiplayerFinalCandidates(nodes.Concat(_contributionWitnesses));
}
