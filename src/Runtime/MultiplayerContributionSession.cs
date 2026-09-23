namespace CombatSolver;

// Only observed root facts cross manual requests. No simulated branch, score or teammate plan does.
internal sealed class MultiplayerContributionSession
{
    private MultiplayerRootObservation? _start;
    private MultiplayerRootObservation? _last;
    private MultiplayerContributionObjective? _objective;

    public MultiplayerContributionObjective Observe(MultiplayerRootObservation root, int horizon = 14)
    {
        if (_last != null && (root.Round < _last.Round || root.LocalDamage < _last.LocalDamage
            || root.TotalDamage < _last.TotalDamage || root.UnattributedDamage < _last.UnattributedDamage))
            throw new InvalidOperationException("Multiplayer stage observation moved backwards.");
        int paid = _start == null ? 0 : MultiplayerContributionObjective.NetProgress(
            _start.EnemyHp, root.EnemyHp, root.LocalDamage - _start.LocalDamage,
            root.TotalDamage - _start.TotalDamage);
        bool changed = _start != null && !_start.Participants.SequenceEqual(root.Participants);
        if (_objective == null || root.Round > _objective.DeadlineRound || changed)
        {
            var next = MultiplayerContributionObjective.Start(root, horizon);
            if (_objective != null)
                next = next with
                {
                    Stage = _objective.Stage + 1,
                    // Roster changes during a stage do not buy extra time.
                    DeadlineRound = changed && root.Round <= _objective.DeadlineRound
                        ? _objective.DeadlineRound : next.DeadlineRound,
                    RenewalReason = changed ? "participants_changed" : "deadline_elapsed",
                    PreviousTarget = _objective.TargetDamage, PreviousProgress = paid,
                };
            _start = root;
            _objective = next;
            paid = 0;
        }
        _last = root;
        return _objective with
        {
            PaidDamage = paid,
            ObservedLocalDamage = root.LocalDamage - _start!.LocalDamage,
            ObservedTotalDamage = root.TotalDamage - _start.TotalDamage,
            ObservedUnattributedDamage = root.UnattributedDamage - _start.UnattributedDamage,
            RemainingCycles = Math.Clamp(_objective.DeadlineRound - root.Round + 1, 1, horizon),
        };
    }
}
