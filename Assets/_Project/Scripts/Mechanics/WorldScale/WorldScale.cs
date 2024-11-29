namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Replaces GMTK.GameStates.WorldState (Assets/GMTK/Scripts/GameStates/GameplayState.cs:6-10). PascalCase
    /// per this project's convention (see _Project.Mechanics.Player.UpgradeStat from Phase 2) - the old enum used
    /// SCREAMING_SNAKE_CASE members (`MICRO_WORLD`/`MACRO_WORLD`), which is not carried over.
    ///
    /// Member order intentionally differs from the old enum (old: MICRO_WORLD = 0, MACRO_WORLD = 1) - nothing in
    /// the old codebase depended on the old enum's underlying int value (no serialization, no arithmetic on it),
    /// and this is a brand-new type with no serialized data to stay binary-compatible with, so ordering follows
    /// the game's actual starting state instead (Macro first, matching GMTK.GameStates.GamePlayState's own
    /// `_worldState = WorldState.MACRO_WORLD` default).
    /// </summary>
    public enum WorldScale
    {
        Macro,
        Micro
    }
}
