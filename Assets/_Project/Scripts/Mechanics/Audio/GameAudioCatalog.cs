using System;
using System.Collections.Generic;
using Project.Core.AudioSystem;
using UnityEngine;

namespace _Project.Mechanics.Audio
{
    /// <summary>
    /// One entry in <see cref="GameAudioCatalog"/>'s SFX table: a <see cref="GameSfxCue"/> paired with the real
    /// clip + per-clip volume/pitch-range that replaces whichever old AudioList/AudioSource slot it replaces (see
    /// GameSfxCue's own doc comments for the exact old call site and verified clip GUID per entry). Volume/pitch-
    /// range default to the neutral values <see cref="Project.Core.AudioSystem.SoundData"/> itself defaults to
    /// (1 and (1,1)) - the old game never varied per-clip volume or pitch at all (every old `_audioSource.Play()`
    /// call played the assigned clip at the AudioSource's own inspector-set volume/pitch, which this migration has
    /// no way to read without the Editor open - see GameAudioCatalog's class doc comment), so 1/(1,1) here exactly
    /// reproduces "play the clip as authored" rather than guessing at a scaled value.
    ///
    /// Plain <c>AudioClip</c> reference rather than a <see cref="Project.Core.AudioSystem.SoundData"/> sub-asset
    /// per cue - see GameAudioCatalog's class doc comment for why.
    /// </summary>
    [Serializable]
    public sealed class GameSfxCueEntry
    {
        [SerializeField] private GameSfxCue _cue;
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] private Vector2 _pitchRange = Vector2.one;

        public GameSfxCue Cue => _cue;
        public AudioClip Clip => _clip;
        public float Volume => _volume;
        public Vector2 PitchRange => _pitchRange;
    }

    /// <summary>
    /// Named-cue SFX/music catalog for the new <c>_Project</c> code, replacing:
    /// - GMTK.AudioList's per-prefab `List&lt;AudioClip&gt;` + magic `Play(int)` index (Assets/GMTK/Scripts/
    ///   Behaviors/AudioList.cs), attached to Player.prefab for the world-scale whoosh (index 0/1) and player
    ///   hit/died (index 2/3).
    /// - The raw `GetComponents&lt;AudioSource&gt;()[0]`/`[1]` indexing duplicated across GMTK.MicroViruses.
    ///   InCellVirus, GMTK.VirusController, and GMTK.Medical (each with its own undocumented "slot 0 = X, slot 1
    ///   = Y" convention tied to Inspector component order on that specific prefab).
    /// - GMTK.AudioMixerController's `_menuTrack`/`_gameplayTrack` fields (Assets/GMTK/Scripts/Behaviors/
    ///   AudioMixerController.cs) for the two music tracks.
    ///
    /// See <see cref="GameSfxCue"/>'s own doc comments for the full per-cue mapping to old call site + verified
    /// clip. Every GUID below was read directly from the corresponding .mp3's own .meta file and cross-checked
    /// against the actual serialized AudioSource/AudioList clip references in the old prefabs (Player.prefab,
    /// Orange.prefab, Cucumber.prefab, Spiner.prefab, BigBob.prefab, PinkGlist.prefab, Medical.prefab, Cell.prefab,
    /// AudioManager.prefab) - not inferred from filenames alone, since at least two filenames turned out to be
    /// misleading once cross-checked (see GameSfxCue.PlayerDied and GameSfxCue.MedicalBoxOpened's doc comments):
    ///
    /// | Clip file            | GUID                             | Cue(s) it is wired to                       |
    /// |-----------------------|----------------------------------|----------------------------------------------|
    /// | Fire.mp3              | 00425cfd61838534d8b4214930116d07 | PlayerFired                                   |
    /// | PlayerHit.mp3          | 6f4f20da2a3f781439591f2c26d3ba94 | PlayerHit                                     |
    /// | Explosion.mp3          | 4d8c4fe52174637409737d8471662028 | PlayerDied                                    |
    /// | FromCell.mp3           | 15c9945713065444da5aa9dda7d69bc0 | ExitedMicroWorld                              |
    /// | IntoCell.mp3           | 927726475811714458d81fc02203429e | EnteredMicroWorld                             |
    /// | CellDead.mp3           | c2c1703c69ae0264e972a67077a001f0 | CellDied                                      |
    /// | LevelUp.mp3            | 6adad79e4858db2428acf158bf8322b6 | LevelUp                                       |
    /// | EnemyHit.mp3           | cbb554b1df4a9fe44abe5d28020d18d5 | MedicalBoxOpened, VirusHit                    |
    /// | Vitamins.mp3           | 36952a4a9a78f314eb920c5e6690aed1 | VitaminCollected                              |
    /// | OrangeDie.mp3          | f65a6dc1537804b429e1b7e82a41d5f3 | OrangeVirusDied                               |
    /// | CucumberDie.mp3        | 2fbac92c77cdc1141a35105885297556 | CucumberVirusDied                             |
    /// | CucumberAttack.mp3     | 7e5a947e9b5d7dc46888acb75553253a | CucumberAttack                                |
    /// | BigVirusDie.mp3        | 6f7af499f2ef05243a1543743a65104f | BigVirusDied                                  |
    /// | Click.mp3              | d0d6c835ce66be845953aded6325fc5c | MenuStartClicked                              |
    /// | intro-q24.mp3          | 18c640ef919c8514eb800858c7339c41 | MenuMusic (music, not a GameSfxCue)           |
    /// | q24.mp3                | 4b479ed03c24e23b9a9e1528ead160fa | GameplayMusic (music, not a GameSfxCue)       |
    ///
    /// That is 14 distinct SFX clips (all of Assets/GMTK/Audio/*.mp3 except the 2 music tracks) covering the 15
    /// GameSfxCue values (EnemyHit.mp3 is deliberately reused by two cues, BigVirusDie.mp3 by three call sites
    /// collapsed into one cue - see the table/GameSfxCue doc comments). TECHNICAL_AUDIT.md's "17 SFX + 2 music"
    /// count does not match what is actually present under Assets/GMTK/Audio/ (16 .mp3 files total were found by
    /// directory listing - 14 SFX + 2 music); every cue/mapping above was independently verified against the real
    /// files and prefabs rather than trusting that count, and the discrepancy is called out here rather than
    /// silently reconciled.
    ///
    /// --- Why SFX use raw AudioClip but music uses SoundData ---
    /// <see cref="Project.Core.AudioSystem.IAudioSystem"/> exposes `PlaySfx(AudioClip, float, Vector2)` directly,
    /// so one-shot cues need no wrapper asset - <see cref="GameSfxCueEntry"/> inlines clip+volume+pitchRange as a
    /// plain serialized field on this ScriptableObject, avoiding 14 tiny one-clip sub-assets. Music playback
    /// (`PlayMusic`/`CrossfadeMusic`) only accepts a <see cref="Project.Core.AudioSystem.SoundData"/> ScriptableObject
    /// reference (no raw-AudioClip overload exists on IAudioSystem for music), so the two music tracks are real
    /// SoundData assets instead: Configurations/Audio/SoundData_MenuMusic.asset and SoundData_GameplayMusic.asset.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Configurations/Audio/" + nameof(GameAudioCatalog),
        fileName = nameof(GameAudioCatalog) + "_Default")]
    public sealed class GameAudioCatalog : ScriptableObject
    {
        [SerializeField] private GameSfxCueEntry[] _sfxCues = Array.Empty<GameSfxCueEntry>();

        [Header("Music (old: GMTK.AudioMixerController._menuTrack/_gameplayTrack)")]
        [SerializeField] private SoundData _menuMusic;
        [SerializeField] private SoundData _gameplayMusic;

        private Dictionary<GameSfxCue, GameSfxCueEntry> _sfxLookup;

        public SoundData MenuMusic => _menuMusic;
        public SoundData GameplayMusic => _gameplayMusic;

        public bool TryGetSfx(GameSfxCue cue, out GameSfxCueEntry entry)
        {
            EnsureLookupBuilt();
            return _sfxLookup.TryGetValue(cue, out entry);
        }

        private void EnsureLookupBuilt()
        {
            if (_sfxLookup != null)
                return;

            _sfxLookup = new Dictionary<GameSfxCue, GameSfxCueEntry>(_sfxCues?.Length ?? 0);

            if (_sfxCues == null)
                return;

            foreach (GameSfxCueEntry entry in _sfxCues)
            {
                if (entry != null)
                    _sfxLookup[entry.Cue] = entry;
            }
        }
    }
}
