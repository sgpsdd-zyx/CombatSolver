using MegaCrit.Sts2.Core.Entities.Players;

namespace CombatSolver;

internal sealed partial class SimulatedCombatState
{
    // Stable identity only. Every mutable peer value belongs to the existing branch stores.
    internal Player? AdvisorPlayer { get; set; }
    internal IReadOnlyList<Player> AdvisorExtraTurnPlayers { get; set; } = [];
    internal int AdvisorEnemyCycles { get; set; }
    // Raw observation for Search's per-cycle allowance, before next-turn setup can cost HP.
    internal int AdvisorLastEnemyCycleHpLost { get; set; }
    internal IReadOnlyDictionary<Player, int>? AdvisorEtherealCounts { get; set; }
    internal bool ExternalChoiceReached { get; private set; }

    internal void ResetMultiplayerHistoryWindow()
    {
        // Native HappenedThisTurn compares every player's turn number, including peers
        // sitting out an extra turn. Power lifecycle counters remain participant-owned.
        foreach (var creature in Creatures)
        {
            ResetTurnCounter(ref _cardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _manualCardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _attacksPlayedThisTurn, creature);
            ResetTurnCounter(ref _shivsPlayedThisTurn, creature);
            ResetTurnCounter(ref _blockCardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _skillCardsPlayedThisTurn, creature);
            ResetTurnCounter(ref _cardsExhaustedThisTurn, creature);
            ResetTurnCounter(ref _cardsDiscardedThisTurn, creature);
            ResetTurnCounter(ref _creatureAttacksThisTurn, creature);
            ResetTurnCounter(ref _cardPlaySeriesStartedThisTurn, creature);
            ResetTurnCounter(ref _zeroCostAttackStartsThisTurn, creature);
            ResetTurnCounter(ref _attackPlayStartsThisTurn, creature);
            ResetTurnCounter(ref _cardPlayStartsThisTurn, creature);
            ResetTurnCounter(ref _attackSkillStartsThisTurn, creature);
        }
        foreach (Player player in Players)
        {
            ResetTurnCounter(ref _energySpentThisTurn, player);
            ResetTurnCounter(ref _starsGainedThisTurn, player);
            ResetTurnCounter(ref _nonHandDrawsThisTurn, player);
            ResetTurnCounter(ref _statusCardsDrawnThisTurn, player);
        }
        _fetchCardsPlayedThisTurn?.Clear();
        _unblockedDamageThisTurn = null;
        _poweredAttackHitsThisTurn?.Clear();
        _doomAppliersThisTurn?.Clear();
    }

    internal void RequireLocalChoice(Player player)
    {
        if (AdvisorPlayer != null && !ReferenceEquals(AdvisorPlayer, player))
        {
            ExternalChoiceReached = true;
            throw new ExternalPlayerChoiceException(player.NetId);
        }
    }
}

internal sealed class ExternalPlayerChoiceException(ulong playerId)
    : InvalidOperationException($"Prediction reached a choice owned by teammate {playerId}.");
