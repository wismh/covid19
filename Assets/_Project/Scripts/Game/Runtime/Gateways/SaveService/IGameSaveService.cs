namespace _Project.Gateways.SaveService
{
    /// <summary>Domain-specific facade over the generic save service: named, intention-revealing methods instead
    /// of a raw mutable state POCO. Every mutating method persists via a debounced save internally.</summary>
    public interface IGameSaveService
    {
        float GetMasterVolume();
        float GetMusicVolume();
        float GetSfxVolume();

        void SetMasterVolume(float volume);
        void SetMusicVolume(float volume);
        void SetSfxVolume(float volume);

        float GetBestSurvivalTimeSeconds();

        /// <returns>True if this was a new best (record) survival time.</returns>
        bool RecordSurvivalTime(float seconds);

        int GetBestPlayerLevel();

        /// <returns>True if this was a new best (record) level.</returns>
        bool RecordPlayerLevel(int level);

        /// <summary>Forces an immediate, non-debounced write to disk.</summary>
        void SaveImmediate();
    }
}
