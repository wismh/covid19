namespace _Project.Entities.Permanent
{
    /// <summary>The full persisted save payload. Endless survival mode has no win condition or levels, so the
    /// only "high scores" worth tracking are best survival time and best player level reached.</summary>
    public sealed class GameSaveState
    {
        /// <summary>Schema version, read/written by SaveJsonVersion / SaveMigrationRunner.</summary>
        public int Version { get; set; }

        public float MasterVolume { get; set; } = 1f;

        public float MusicVolume { get; set; } = 1f;

        public float SfxVolume { get; set; } = 1f;

        public float BestSurvivalTimeSeconds { get; set; }

        public int BestPlayerLevel { get; set; } = 1;
    }
}
