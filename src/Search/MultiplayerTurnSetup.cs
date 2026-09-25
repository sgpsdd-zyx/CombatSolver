namespace CombatSolver;

// Immutable progress of the root's local native setup; never reused on a later turn.
internal sealed record MultiplayerTurnSetup(int RelicIndex);
