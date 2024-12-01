namespace _Project.Save
{
    /// <summary>
    /// The full persisted save payload for COVID-19: The Beginning. Serialized to JSON by
    /// <c>Project.Core.SaveServiceModule.SaveService&lt;GameSaveState&gt;</c> (Newtonsoft.Json, via
    /// <c>ISerializer</c>) - see <see cref="_Project.Save.GameSaveModuleInstaller"/> for the exact binding.
    ///
    /// Per REFACTORING_PLAN.md Phase 6's brief, this is a genuinely NEW design, not a port: GDD.md section 11
    /// ("Відомі прогалини дизайну") states plainly the old GMTK game has "Немає умови перемоги/завершення - тільки
    /// нескінченне виживання" (no win/end condition, only endless survival) and never had any save system at all.
    /// Every field below is justified by something that concretely exists in this codebase today - nothing here
    /// is speculative scaffolding for systems (levels, unlocks, per-virus/per-cell progress) that don't exist yet:
    ///
    /// - <see cref="Version"/>: required by <c>SaveService&lt;TState&gt;</c>'s migration pipeline
    ///   (<c>SaveJsonVersion</c>/<c>SaveMigrationRunner</c> read/write this exact PascalCase "Version" key).
    /// - <see cref="MasterVolume"/>/<see cref="MusicVolume"/>/<see cref="SfxVolume"/>: mirror the three volume
    ///   concepts <c>Project.Core.AudioSystem.IAudioSystem</c> already exposes today
    ///   (<c>MasterVolume</c>/<c>MusicVolume</c>/<c>SfxVolume</c> getters, <c>SetMasterVolume</c>/
    ///   <c>SetMusicVolume</c>/<c>SetSfxVolume</c> setters - see AudioSystem.cs). Default 1f each, matching
    ///   AudioSystem's own in-memory defaults (<c>_masterVolume</c>/<c>_musicVolume</c>/<c>_sfxVolume</c> = 1f)
    ///   so a fresh save produces identical audible behaviour to never having a save at all.
    /// - <see cref="BestSurvivalTimeSeconds"/>: the single "high score" the endless mode actually has. GDD.md
    ///   section 7/11 and HudScreen.cs's own doc comment (elapsed-seconds MM:SS timer, "the sole place time was
    ///   tracked") confirm survival duration is the one thing worth persisting as a best result - there is no win
    ///   condition, no levels, nothing else to rank.
    /// - <see cref="BestPlayerLevel"/>: the one additional field the phase brief explicitly calls out as
    ///   plausibly justified - <c>PlayerStatsService.Level</c> (Phase 2) already exists as a real, concrete
    ///   in-run stat (starts at 1, increments via XP). Recording the best level reached across runs is a natural,
    ///   low-risk companion "high score" to survival time for an endless mode, using a field that already exists
    ///   rather than inventing a new progression concept. Default 1, matching PlayerStatsService's own starting
    ///   level.
    ///
    /// Deliberately NOT included: per-upgrade unlocks, per-cell/per-virus progress, settings for systems that
    /// don't exist (no graphics quality settings, no keybind remapping state, no win-condition flags) - none of
    /// these are backed by anything real in the codebase yet, and REFACTORING_PLAN.md's guiding principle
    /// throughout this migration has been "don't build for hypothetical future requirements."
    /// </summary>
    public sealed class GameSaveState
    {
        /// <summary>Schema version, read/written by SaveJsonVersion / SaveMigrationRunner. Current value: 1
        /// (GameSaveModuleInstaller.CurrentSaveVersion) - there is no prior schema to migrate from yet.</summary>
        public int Version { get; set; }

        public float MasterVolume { get; set; } = 1f;

        public float MusicVolume { get; set; } = 1f;

        public float SfxVolume { get; set; } = 1f;

        /// <summary>Longest single run's elapsed survival time, in seconds. Matches the unit HudScreen's timer
        /// already counts in (<c>_elapsedSeconds</c>, displayed MM:SS) - a future composition root reports the
        /// final elapsed time here via <see cref="IGameSaveService.RecordSurvivalTime"/> on game-over.</summary>
        public float BestSurvivalTimeSeconds { get; set; }

        /// <summary>Highest PlayerStatsService.Level reached in any single run. Default 1 to match
        /// PlayerStatsService's own starting level (a run that never levels up still "reached" level 1).</summary>
        public int BestPlayerLevel { get; set; } = 1;
    }
}
