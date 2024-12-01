namespace _Project.Save
{
    /// <summary>
    /// Domain-specific facade over the generic <c>Project.Core.SaveServiceModule.ISaveService&lt;GameSaveState&gt;</c>,
    /// per REFACTORING_PLAN.md section 4's explicit design precedent (citing the `space-bac` reference project):
    /// callers get named, intention-revealing methods instead of a raw <c>.State</c> POCO reference they could
    /// mutate from anywhere. Every mutating method here persists via the underlying save service's debounced
    /// <c>RequestSave()</c> internally - callers never need to remember to save after a change.
    /// </summary>
    public interface IGameSaveService
    {
        float GetMasterVolume();

        float GetMusicVolume();

        float GetSfxVolume();

        /// <summary>Persists a new master volume (0-1, clamped) and requests a debounced save.</summary>
        void SetMasterVolume(float volume);

        /// <summary>Persists a new music volume (0-1, clamped) and requests a debounced save.</summary>
        void SetMusicVolume(float volume);

        /// <summary>Persists a new SFX volume (0-1, clamped) and requests a debounced save.</summary>
        void SetSfxVolume(float volume);

        float GetBestSurvivalTimeSeconds();

        /// <summary>
        /// Reports a completed run's elapsed survival time. Updates <c>BestSurvivalTimeSeconds</c> only if
        /// <paramref name="seconds"/> beats the current best, and requests a debounced save either way (so a
        /// non-record run's play session is still not silently dropped if the app quits mid-debounce and
        /// nothing else triggers a save this session).
        /// </summary>
        /// <returns>True if this was a new best (record) survival time.</returns>
        bool RecordSurvivalTime(float seconds);

        int GetBestPlayerLevel();

        /// <summary>Reports the highest PlayerStatsService.Level reached in a run. Same "always request a save,
        /// only update on improvement" semantics as <see cref="RecordSurvivalTime"/>.</summary>
        /// <returns>True if this was a new best (record) level.</returns>
        bool RecordPlayerLevel(int level);

        /// <summary>Forces an immediate, non-debounced write to disk (e.g. on app quit - see
        /// <see cref="SaveTrigger"/> - or right after game-over, where losing the last few hundred milliseconds
        /// of debounce window to a crash/force-quit would be most noticeable to the player).</summary>
        void SaveImmediate();
    }
}
