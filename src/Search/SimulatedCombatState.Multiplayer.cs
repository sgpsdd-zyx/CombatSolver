using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace CombatSolver;

internal sealed partial class SimulatedCombatState
{
    // Stable identity only. Every mutable peer value belongs to the existing branch stores.
    internal Player? AdvisorPlayer { get; set; }
    internal IReadOnlyList<Player> AdvisorExtraTurnPlayers { get; set; } = [];
    internal int AdvisorEnemyCycles { get; set; }
    // Raw observation for Search's per-cycle allowance, before next-turn setup can cost HP.
    internal int AdvisorLastEnemyCycleHpLost { get; set; }
    internal MultiplayerCycleCheckpoint? AdvisorLastEnemyCycle { get; set; }
    internal IReadOnlyDictionary<Player, int>? AdvisorEtherealCounts { get; set; }
    internal bool ExternalChoiceReached { get; private set; }
    private ForkableSet<Player>? _inactiveMultiplayerPlayers;

    internal bool IsPlayerActiveForHooks(Player player)
        => AdvisorPlayer == null || _inactiveMultiplayerPlayers?.Contains(player) != true;

    internal void SetPlayerActiveForHooks(Player player, bool active)
    {
        if (AdvisorPlayer == null) return;
        bool changed = active
            ? _inactiveMultiplayerPlayers?.Remove(player) == true
            : (_inactiveMultiplayerPlayers ??= []).Add(player);
        if (changed) InvalidateBaseHookListeners();
    }

    private bool IsMultiplayerHookOwnerActive(AbstractModel listener)
        => listener switch
        {
            RelicModel relic => IsPlayerActiveForHooks(relic.Owner),
            PowerModel power when power.Owner.Player is { } player => IsPlayerActiveForHooks(player),
            PotionModel potion => IsPlayerActiveForHooks(potion.Owner),
            CardModel card => IsPlayerActiveForHooks(card.Owner),
            EnchantmentModel enchantment when enchantment.HasCard => IsPlayerActiveForHooks(enchantment.Card.Owner),
            AfflictionModel affliction when affliction.HasCard => IsPlayerActiveForHooks(affliction.Card.Owner),
            OrbModel orb => IsPlayerActiveForHooks(orb.Owner),
            _ => true,
        };

    private AbstractModel[] CaptureMultiplayerRootListeners(
        IReadOnlyList<AbstractModel> captured, IReadOnlyList<Creature> creatures)
    {
        // Inactive owners still retain state (and may revive), even though vanilla omits their listeners.
        List<AbstractModel> listeners = [];
        foreach (Creature creature in creatures)
        {
            listeners.AddRange(creature.Powers);
            if (creature.Player is { } player)
            {
                listeners.AddRange(RelicsOf(player).Where(relic => !relic.IsMelted));
                for (int slot = 0; slot < PotionSlotCount(player); slot++)
                    if (GetPotionAtSlot(player, slot) is { } potion) listeners.Add(potion);
            }
            else if (creature.Monster is { } monster) listeners.Add(monster);
        }
        foreach (AbstractModel listener in captured)
            if (!listeners.Contains(listener)) listeners.Add(listener);
        return listeners.ToArray();
    }

    internal IReadOnlyList<PowerModel> PowersForHooks()
        => AdvisorPlayer == null || _inactiveMultiplayerPlayers is not { Count: > 0 }
            ? EffectivePowers()
            : EffectivePowers().Where(IsMultiplayerHookOwnerActive).ToArray();

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
