using System;
using Project.Core.AudioSystem;
using _Project.Mechanics.WorldScale;

namespace _Project.Mechanics.Audio
{
    /// <summary>
    /// Plain C# service (POCO), not a MonoBehaviour and not bound through Zenject - matches the Mechanics/*
    /// hand-composition philosophy from REFACTORING_PLAN.md section 4 (`new GameAudioService(...)`), the same
    /// idiom Phase 2's PlayerStatsService and Phase 4's WorldScaleController already established. Wraps the
    /// Zenject-bound <see cref="IAudioSystem"/> (installed in Phase 1's ProjectContextInstaller via
    /// AudioSystemInstaller, resolvable from the container a future composition root already holds) plus the
    /// named <see cref="GameAudioCatalog"/> from this same folder, replacing GMTK.AudioManagerFactory/
    /// GMTK.AudioMixerController (Assets/GMTK/Scripts/AudioManagerFactory.cs, Scripts/Behaviors/
    /// AudioMixerController.cs) per REFACTORING_PLAN.md section 5's mapping row for that system.
    ///
    /// --- World-scale audio seam: subscribing to WorldScaleController rather than editing Phase 4's files ---
    /// Old GMTK.GameStates.TransitionIntoMacroWorld/TransitionIntoMicroWorld directly played the player's
    /// whoosh SFX (`_player.GetComponent&lt;AudioList&gt;().Play(0/1)`) and swapped the gameplay-music
    /// AudioSource's `outputAudioMixerGroup` between AudioMixerController's `_master`/`_incell` groups
    /// (`SetMaster()`/`SetIncell()`). Phase 4's own WorldScaleTransition/WorldScaleTransitionContext (Assets/
    /// _Project/Scripts/Mechanics/WorldScale/) already thread the RAW PLUMBING for both through a
    /// WorldScaleTransitionContext instance a future composition root builds per-call (PlayerAudioSource,
    /// PlayerEnterMacroClip/PlayerEnterMicroClip, GameplayMusicAudioSource, MacroMixerGroup/MicroMixerGroup) -
    /// but nothing in the codebase yet POPULATES those context fields with real values, since the composition
    /// root that would do so does not exist yet either. Per this phase's brief, WorldScaleTransition/
    /// WorldScaleTransitionContext are Phase 4's files and off-limits to edit here.
    ///
    /// This class instead subscribes directly to <see cref="WorldScaleController.OnEnteredMicroWorld"/>/
    /// <see cref="WorldScaleController.OnExitedMicroWorld"/> - events Phase 4 already exposes publicly - and
    /// plays the equivalent whoosh cue from there. This is a SECOND, independent path to roughly the same
    /// audible result (not a call into WorldScaleTransitionContext's fields), chosen because it works today
    /// without requiring the not-yet-built composition root to first wire PlayerAudioSource/PlayerEnterMacroClip/
    /// etc. into every WorldScaleTransitionContext it constructs. If a future phase DOES populate those context
    /// fields too, both paths would fire (this subscription AND WorldScaleTransition's own context-driven
    /// playback) - flagged here explicitly so whoever wires the real composition root notices and picks exactly
    /// one path (most likely: stop populating the context's audio fields once this subscription exists, since
    /// GameAudioService/GameAudioCatalog is the intended long-term home for named audio cues per REFACTORING_PLAN
    /// .md's Phase 5 mapping, not raw AudioClip fields threaded through a per-call context object).
    ///
    /// --- What this seam does NOT (yet) do: the mixer-group swap ---
    /// Old `SetMaster()`/`SetIncell()` gave the in-cell mixed audio a distinct EQ (InCellMixer.mixer's Master
    /// group has an extra ParamEQ effect DefaultMixer.mixer's Master group lacks - verified by reading both
    /// .mixer YAML files directly) by routing the gameplay-music AudioSource through one of TWO ENTIRELY SEPARATE
    /// AudioMixer assets (InCellMixer is its own top-level mixer, not a child group inside DefaultMixer).
    /// Per Phase 0's documented compromise, the ProjectContext.prefab AudioSystem instance's `_mixer`/`_sfxGroup`/
    /// `_musicGroup` are ALL bound to DefaultMixer's single "Master" group - there is no second exposed group or
    /// mixer wired into AudioSystem to swap to, IAudioSystem exposes no API to change which AudioMixerGroup its
    /// internal music AudioSources output to after Awake (`_musicGroup` is a private serialized field, no
    /// setter), and this phase is explicitly barred from hand-editing .mixer YAML to add new exposed parameters/
    /// groups (too risky to validate without the Editor). So the mixer-group swap is a genuine, documented gap,
    /// not silently faked here - see this file's <see cref="HandleEnteredMicroWorld"/>/
    /// <see cref="HandleExitedMicroWorld"/> for exactly what IS reproduced (the whoosh SFX only). A real fix
    /// needs either: (a) a second AudioMixerGroup authored under DefaultMixer in the Editor with its own exposed
    /// EQ/volume parameters and AudioSystem's serialized fields extended to reference it, or (b) wiring the
    /// existing separate InCellMixer.mixer asset's own Master group into a dedicated raw AudioSource the
    /// composition root manages outside of AudioSystem (closer to how the old code worked, but reintroduces the
    /// exact "manual AudioSource swap" pattern REFACTORING_PLAN.md section 5 says AudioSystem should replace).
    /// Either way, it is Editor-authoring work, deferred past this phase.
    /// </summary>
    public sealed class GameAudioService : IDisposable
    {
        private readonly IAudioSystem _audioSystem;
        private readonly GameAudioCatalog _catalog;
        private readonly WorldScaleController _worldScaleController;

        /// <param name="audioSystem">Resolved from the Zenject container (bound by AudioSystemInstaller in
        /// Phase 1's ProjectContextInstaller) - passed in explicitly rather than [Inject]-ed onto this class
        /// itself, since GameAudioService is a hand-composed Mechanics/* POCO per REFACTORING_PLAN.md section 4,
        /// not a DI-managed type.</param>
        /// <param name="catalog">Configurations/Audio/GameAudioCatalog_Default.asset (or any variant) - the
        /// cue -&gt; clip/SoundData mapping.</param>
        /// <param name="worldScaleController">Optional: only present in the gameplay scene composition (the
        /// Menu scene has no world-scale mechanic at all - GMTK.GameStates.GamePlayState/WorldState never existed
        /// there). When supplied, this service subscribes to its OnEnteredMicroWorld/OnExitedMicroWorld events for
        /// the world-scale whoosh cues (see this class's own doc comment) and unsubscribes in
        /// <see cref="Dispose"/>. When null, the world-scale audio seam is simply not wired - the rest of this
        /// service (PlaySfx, music) still works normally.</param>
        public GameAudioService(IAudioSystem audioSystem, GameAudioCatalog catalog, WorldScaleController worldScaleController = null)
        {
            _audioSystem = audioSystem ?? throw new ArgumentNullException(nameof(audioSystem));
            _catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            _worldScaleController = worldScaleController;

            if (_worldScaleController != null)
            {
                // Phase 8 testing fix (2026-09-11): subscribed to OnEntering*/OnExiting*-timed events instead of
                // OnEntered*/OnExited* - see WorldScaleController.OnEnteringMicroWorld's doc comment for why the
                // whoosh needs to play at the START of the transition, not after it finishes.
                _worldScaleController.OnEnteringMicroWorld += HandleEnteredMicroWorld;
                _worldScaleController.OnEnteringMacroWorld += HandleExitedMicroWorld;
            }
        }

        /// <summary>Unsubscribes from WorldScaleController's events. A future composition root should call this
        /// when tearing down the gameplay scene's hand-composed graph (mirrors the "unsubscribe in OnClose, not
        /// OnDestroy-via-GC" discipline REFACTORING_PLAN.md section 4 asks of UI screens - here applied to a
        /// plain POCO's external event subscription instead of a MonoBehaviour lifecycle method).</summary>
        public void Dispose()
        {
            if (_worldScaleController == null)
                return;

            _worldScaleController.OnEnteringMicroWorld -= HandleEnteredMicroWorld;
            _worldScaleController.OnEnteringMacroWorld -= HandleExitedMicroWorld;
        }

        /// <summary>Plays a one-shot SFX by named cue. Old: whichever `AudioList.Play(int)` or
        /// `_audioSources[n].Play()` call site the cue replaces - see GameSfxCue's per-value doc comments. Silently
        /// no-ops if the cue has no catalog entry or the entry's clip is unassigned, matching
        /// IAudioSystem.PlaySfx's own null-clip no-op behaviour rather than throwing for a content/config gap.
        /// </summary>
        public void PlaySfx(GameSfxCue cue, float volumeScale = 1f)
        {
            if (!_catalog.TryGetSfx(cue, out GameSfxCueEntry entry) || entry.Clip == null)
                return;

            _audioSystem.PlaySfx(entry.Clip, entry.Volume * volumeScale, entry.PitchRange);
        }

        /// <summary>
        /// Old: the MENU-scene half of GMTK.AudioMixerController.TryInitialize()/Update() - playing
        /// `_menuTrack` (`_audioSources[0]`) on scene load, and the pitch=0 pre-buffering dance that let
        /// `_gameplayTrack` start silently underneath it so the eventual switch had no audible gap
        /// (AudioMixerController.cs:47-63). That hand-rolled pre-buffer trick is NOT reproduced here - IAudioSystem
        /// .PlayMusic already crossfades cleanly between two internal music AudioSources (see AudioSystem.cs's
        /// CrossfadeMusic) whenever music is already playing, which is a strictly better replacement for the same
        /// "no audible gap" goal, not a like-for-like port of the workaround itself.
        /// </summary>
        public void PlayMenuMusic() => _audioSystem.PlayMusic(_catalog.MenuMusic);

        /// <summary>Old: the SampleScene half of the same AudioMixerController.TryInitialize() path
        /// (`_gameplayTrack`, `_audioSources[1]`, `loop = false` initially per the old code's own comment-free
        /// quirk - reproduced instead as a normal looping track here, since SoundData_GameplayMusic.asset's
        /// `loop` behaviour is controlled by the `loop` parameter on IAudioSystem.PlayMusic, defaulting to true
        /// like every other music track in this system; the old one-shot-then-implicitly-stop behaviour had no
        /// discoverable gameplay purpose distinct from just looping the gameplay track normally).</summary>
        public void PlayGameplayMusic() => _audioSystem.PlayMusic(_catalog.GameplayMusic);

        /// <summary>Old: TransitionIntoMacroWorld's `_player.GetComponent&lt;AudioList&gt;().Play(0)` - see this
        /// class's own doc comment for what this does and does NOT reproduce (whoosh SFX only, no mixer swap).
        /// </summary>
        private void HandleExitedMicroWorld() => PlaySfx(GameSfxCue.ExitedMicroWorld);

        /// <summary>Old: TransitionIntoMicroWorld's `_player.GetComponent&lt;AudioList&gt;().Play(1)` - see this
        /// class's own doc comment for what this does and does NOT reproduce (whoosh SFX only, no mixer swap).
        /// </summary>
        private void HandleEnteredMicroWorld() => PlaySfx(GameSfxCue.EnteredMicroWorld);
    }
}
