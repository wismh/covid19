namespace _Project.Mechanics.Audio
{
    /// <summary>
    /// Replaces the magic-index <c>GMTK.AudioList.Play(int)</c> pattern (Assets/GMTK/Scripts/Behaviors/
    /// AudioList.cs) and the raw <c>GetComponents&lt;AudioSource&gt;()[0]</c>/<c>[1]</c> indexing used directly by
    /// several other old behaviours - both are exactly the "magic index, tribal knowledge tied to Inspector list
    /// order" smell TECHNICAL_AUDIT.md section 2.2 calls out (per-prefab, undocumented, and index 0/1/2/3 means a
    /// DIFFERENT clip depending on which GameObject's component it is). One named enum value per distinct SFX use
    /// found while reading every old call site, cross-checked against the actual AudioClip GUID each old
    /// prefab's AudioSource/AudioList slot points to (not guessed from naming alone) - see GameAudioCatalog's
    /// class doc comment for the full GUID table.
    ///
    /// --- Why one flat enum, not split per entity type ---
    /// The old game has ~15 distinct SFX use sites across Player/Cell/Virus(macro+micro)/Medical/Menu, using only
    /// 14 distinct .mp3 files (several sites intentionally share a clip - see below). That's a small, flat,
    /// finite set with no combinatorial growth (it does not scale with wave count, upgrade count, or virus
    /// variant count the way e.g. UpgradeStat's per-stat split does) - a single enum keeps GameAudioService's
    /// public surface ("play cue X") trivially simple for callers (PlayerHealth, CellHealth, InCellVirusController,
    /// MacroVirusController, etc. - none of which this phase is allowed to edit, but whose future call sites will
    /// read this enum) without needing a per-entity-type sub-catalog. Splitting (e.g. a separate
    /// InCellVirusSfxCue/MacroVirusSfxCue) would only pay off if the cue set were expected to grow per-variant,
    /// which nothing in the old code or GDD.md suggests.
    /// </summary>
    public enum GameSfxCue
    {
        /// <summary>Old: GMTK.PlayerTurretController.Update() - `_audioSource.Play()` on the turret's own
        /// AudioSource each time a bullet is fired (PlayerTurretController.cs:48). Clip: Fire.mp3.</summary>
        PlayerFired,

        /// <summary>Old: GMTK.PlayerDamageable.Damage() - `_audioList.Play(2)` (PlayerDamageable.cs:77), every
        /// time the player takes damage (armor-ignore proc or not). Clip: PlayerHit.mp3.</summary>
        PlayerHit,

        /// <summary>Old: GMTK.PlayerDamageable.AnimationOnDied() - `_audioList.Play(3)` (PlayerDamageable.cs:84).
        /// Clip: Explosion.mp3 - verified directly from Player.prefab's AudioList._clips[3] GUID; this is the
        /// SAME .mp3 file whose name might suggest "Medical box opened", but Medical.prefab's own AudioSource
        /// actually plays EnemyHit.mp3 for that (see MedicalBoxOpened below) - Explosion.mp3 is exclusively the
        /// player-death clip in the actual asset wiring, not a shared "explosion" sound.</summary>
        PlayerDied,

        /// <summary>Old: GMTK.GameStates.TransitionIntoMacroWorld.Start() - `_player.GetComponent&lt;AudioList&gt;
        /// ().Play(0)` (TransitionIntoMacroWorld.cs:32), the player's macro-exit whoosh. Clip: FromCell.mp3 -
        /// verified from Player.prefab's AudioList._clips[0].</summary>
        ExitedMicroWorld,

        /// <summary>Old: GMTK.GameStates.TransitionIntoMicroWorld.Start() - `_player.GetComponent&lt;AudioList&gt;
        /// ().Play(1)` (TransitionIntoMicroWorld.cs:30), the player's micro-entry whoosh. Clip: IntoCell.mp3 -
        /// verified from Player.prefab's AudioList._clips[1].</summary>
        EnteredMicroWorld,

        /// <summary>Old: GMTK.CellController.Die() - `_audioSource.Play()` (CellController.cs:128). Clip:
        /// CellDead.mp3 - verified from Cell.prefab's AudioSource.</summary>
        CellDied,

        /// <summary>Old: GMTK.UI.BarsHUD.OnExperienceChange() - `_audioSource.Play()`, only on the tick a new
        /// level is reached (BarsHUD.cs:62). Clip: LevelUp.mp3.</summary>
        LevelUp,

        /// <summary>Old: GMTK.Medical.Damage() - `_audioSources[0].Play()` (Medical.cs:32), when the vitamin box
        /// is shot open. Clip: EnemyHit.mp3 - verified from Medical.prefab's first AudioSource; despite the box
        /// visually "exploding", the wired clip is the same generic hit sound used by every virus's damage-taken
        /// SFX (see VirusHit below), NOT Explosion.mp3 (that one is reserved for player death - see PlayerDied).
        /// Kept as its own named cue rather than reusing VirusHit because the TWO enum values may legitimately
        /// point to different clips later (e.g. if a real explosion clip is ever authored for the box) without
        /// having to re-thread call sites.</summary>
        MedicalBoxOpened,

        /// <summary>Old: GMTK.Medical.VitaminCallback() - `_audioSources[1].Play()`, guarded by `if (!isPlaying)`
        /// so overlapping vitamin pickups don't stack the sound (Medical.cs:49-50). Clip: Vitamins.mp3.</summary>
        VitaminCollected,

        /// <summary>
        /// Old: shared "took damage" SFX for every virus entity - GMTK.MicroViruses.InCellVirus.AnimationOnDamage()
        /// `_audioSources[0].Play()` (InCellVirus.cs:130, used by ALL THREE in-cell variants: Orange/Cucumber/
        /// Spiner) AND GMTK.VirusController.AnimationOnDamage() `_audioSources[0].Play()` (VirusController.cs:105,
        /// used by BOTH macro variants: BigBob/PinkGlist). Clip: EnemyHit.mp3 - verified identical across all 5
        /// prefabs (Orange/Cucumber/Spiner/BigBob/PinkGlist all wire AudioSource slot 0 to the same GUID). One
        /// cue for all 5 entity types, matching the old code's own behaviour (it was already one shared clip, not
        /// 5 distinct ones) - see this enum's class doc comment for why a flat enum rather than a per-type split.
        /// </summary>
        VirusHit,

        /// <summary>Old: GMTK.MicroViruses.InCellVirus.AnimationOnDied(), on Orange.prefab specifically -
        /// `_audioSources[1].Play()` (InCellVirus.cs:117). Clip: OrangeDie.mp3 - verified from Orange.prefab's
        /// second AudioSource.</summary>
        OrangeVirusDied,

        /// <summary>Old: same AnimationOnDied() as above, on Cucumber.prefab. Clip: CucumberDie.mp3 - verified
        /// from Cucumber.prefab's second AudioSource.</summary>
        CucumberVirusDied,

        /// <summary>Old: GMTK.VirusBehaviors.CucumberVirus's ranged-attack behaviour - `_attackAudio.Play()`
        /// (CucumberVirus.cs:70), a THIRD, separate AudioSource on Cucumber.prefab from the two InCellVirus
        /// damage/died slots above (kept its own field, `_bulletSpawnPoint.GetComponent&lt;AudioSource&gt;()`).
        /// Clip: CucumberAttack.mp3 - verified from Cucumber.prefab.</summary>
        CucumberAttack,

        /// <summary>
        /// Old: same AnimationOnDied() pattern as Orange/Cucumber, but shared across THREE different prefabs -
        /// GMTK.MicroViruses.InCellVirus on Spiner.prefab (`_audioSources[1].Play()`, InCellVirus.cs:117) AND
        /// GMTK.VirusController.AnimationOnDied() on BOTH BigBob.prefab and PinkGlist.prefab
        /// (`_audioSources[1].Play()`, VirusController.cs:93). Clip: BigVirusDie.mp3 - verified identical across
        /// all three prefabs despite the "Big" in the filename suggesting it was authored only for the two macro
        /// variants; Spiner.prefab (an in-cell/micro virus) reuses the exact same GUID for its own death sound
        /// rather than having a dedicated "SpinerDie" clip (no such file exists in Assets/GMTK/Audio/). One cue
        /// for all three, matching the actual asset wiring.
        /// </summary>
        BigVirusDied,

        /// <summary>Old: MenuManager.Play() - `_audioSource.Play()` (MenuManager.cs:36), the menu's
        /// Play-button click, right before the intro cutscene starts. Clip: Click.mp3.</summary>
        MenuStartClicked,
    }
}
