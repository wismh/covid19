using System;
using UnityEngine;
using UnityEngine.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.InCell;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Everything GMTK.GameStates.TransitionIntoMacroWorld/TransitionIntoMicroWorld used to resolve from
    /// ServiceLocator at the top of Start() (Assets/GMTK/Scripts/GameStates/TransitionIntoMacroWorld.cs:17-18,
    /// TransitionIntoMicroWorld.cs:17-18) plus the specific GetComponent/GetChild calls both files make on the
    /// player and cell GameObjects - collected into one plain parameter object, per REFACTORING_PLAN.md's
    /// no-ServiceLocator rule. WorldScaleTransition takes this as a per-call parameter (not a constructor
    /// dependency) because the "which cell" and "which player" involved genuinely differ per call, unlike
    /// WorldScaleConfig which is the same asset for every transition.
    ///
    /// All fields are plain public fields, not [SerializeField] - this is never itself a MonoBehaviour/asset, just
    /// a call-site bundle a future composition root constructs from whatever live scene references it holds (per
    /// the phase brief: "a future composition root wires up who calls this with what").
    /// </summary>
    public sealed class WorldScaleTransitionContext
    {
        // --- Player ---

        /// <summary>Old: `ServiceLocator.Instance.Get&lt;PlayerStats&gt;().Player`.</summary>
        public Transform Player;

        /// <summary>
        /// Old: `_player.GetChild(0).GetChild(0)` - the nested visual transform both files scale INSTANTLY
        /// (not DOTween'd) to WorldScaleConfig.MacroPlayerScale/MicroPlayerScale, distinct from `Player.localScale`
        /// itself which IS one of the DOTween'd values.
        /// </summary>
        public Transform PlayerVisualRoot;

        /// <summary>Old: `_player.GetComponent&lt;BoxCollider2D&gt;()` - toggled `.isTrigger` during the transition.</summary>
        public BoxCollider2D PlayerCollider;

        /// <summary>
        /// Old: `_player.GetComponent&lt;AudioSource&gt;()`, also the AudioSource GMTK.AudioList.Play(int) plays
        /// clips through (`_source.clip = _clips[i]; _source.Play();` - Assets/GMTK/Scripts/Behaviors/
        /// AudioList.cs:16-20). Used for both the .volume dip/restore (micro only) and the transition-whoosh clip.
        /// </summary>
        public AudioSource PlayerAudioSource;

        /// <summary>Old: `_player.GetComponent&lt;AudioList&gt;().Play(0)` inside TransitionIntoMacroWorld.</summary>
        public AudioClip PlayerEnterMacroClip;

        /// <summary>Old: `_player.GetComponent&lt;AudioList&gt;().Play(1)` inside TransitionIntoMicroWorld.</summary>
        public AudioClip PlayerEnterMicroClip;

        /// <summary>
        /// Old: `_player.GetComponent&lt;DriftMovableObject&gt;().Stop()`. Phase 2 already ported this exact
        /// method onto PlayerMovement (Assets/_Project/Scripts/Mechanics/Player/PlayerMovement.cs) - referencing
        /// its public API here is composition, not a Phase-2 edit.
        /// </summary>
        public PlayerMovement PlayerMovement;

        // --- Camera ---

        /// <summary>Old: `Camera.main` - orthographic size is DOTween'd on this.</summary>
        public Camera Camera;

        /// <summary>
        /// Old: `ServiceLocator.Instance.Get&lt;CameraFollow&gt;()` - used for two things: (1) `_camera.transform`,
        /// whose `.rotation.z` is DOTween'd (see WorldScaleTransition), and (2) `_camera.Target = ...` reassigned
        /// AFTER the ortho-size/rotation/position tweens are kicked off (not gated on their completion). GMTK.
        /// CameraFollow (Assets/GMTK/Scripts/Behaviors/Player/CameraFollow.cs) has no _Project equivalent yet, so
        /// rather than reference the old GMTK type directly, (1) is covered by <see cref="Camera"/>'s own
        /// `.transform`, and (2) is exposed as this optional delegate - a future camera-follow port's composition
        /// root supplies it (e.g. `context.SetCameraTarget = t =&gt; cameraFollow.Target = t;`); if left null the
        /// step is simply skipped rather than throwing, since target-following is a presentation concern the
        /// transition itself has no opinion on.
        /// </summary>
        public Action<Transform> SetCameraTarget;

        /// <summary>
        /// Old: `ServiceLocator.Instance.Get&lt;PlayerStats&gt;().SpeedScale = 1f` (macro) / `= 0.01f` (micro) -
        /// GMTK.Services.PlayerStats.cs:14-30, consumed by GMTK.PlayerBodyController.FixedUpdate (line 41) to
        /// throttle forward/backward thrust input to 1% while in micro world (GDD.md section 2). Phase 2's
        /// PlayerMovement (Assets/_Project/Scripts/Mechanics/Player/PlayerMovement.cs) has no equivalent hook yet
        /// and this phase cannot edit Phase-2 files, so WorldScaleTransition still invokes this delegate with the
        /// correct value (preserving the STEP end-to-end) but there is nothing to wire it to until PlayerMovement
        /// grows a speed-scale multiplier - see WorldScaleController's class doc comment, seam item 4, for the
        /// full explanation. Left null-safe (invoked only if non-null) exactly like SetCameraTarget above.
        /// </summary>
        public Action<float> SetPlayerSpeedScale;

        // --- Cell being entered/exited ---

        /// <summary>Old: `nearestCell` (`CellManager.FindNearToPoint(...)`) - the cell transform itself, used for
        /// GameObject.layer-independent position math and as the micro-transition's rotation-tween target.</summary>
        public Transform Cell;

        /// <summary>
        /// Old: `nearestCell.GetComponent&lt;CellController&gt;().SetMacroSprite()`/`SetMicroSprite()`. Phase 3
        /// already ported both methods onto CellVisuals with identical signatures/behaviour (Assets/_Project/
        /// Scripts/Mechanics/Cells/CellVisuals.cs) - referencing its public API here is composition, not a
        /// Phase-3 edit.
        /// </summary>
        public CellVisuals CellVisuals;

        /// <summary>
        /// Old: `nearestCell.GetComponent&lt;Rigidbody2D&gt;()`, mutated two ways per transition - once inside
        /// `cellController.SetVelocity(state)` (isKinematic + linear/angular velocity) and once more, redundantly,
        /// via a second direct `.isKinematic = ...` assignment right after (see WorldScaleTransition's doc
        /// comment for why the redundant second assignment is collapsed rather than duplicated). Phase 3 never
        /// ported CellController.SetVelocity onto any of CellHealth/CellInfectionState/CellVisuals, so this stays
        /// a raw Rigidbody2D reference rather than a Phase-3 method call.
        /// </summary>
        public Rigidbody2D CellRigidbody2D;

        /// <summary>Old: `nearestCell.GetComponent&lt;Animator&gt;().speed = 1` (macro) / `= 0` (micro) - freezes
        /// the cell's idle animation while the player is inside it.</summary>
        public Animator CellAnimator;

        /// <summary>
        /// Phase 7 testing fix (2026-09-11): old CellController.SetMicroSprite() -> `_inCellVirusManager.
        /// SetVirusesCount(_virusCount)`, called lazily, once, at the exact moment the player enters this cell -
        /// see InCellVirusSpawner's own "SetVirusesCount: LAZY, on micro-world entry only" doc comment for why a
        /// reactive alternative was tried and rejected. WorldScaleTransition.EnterMicroWorldAsync reads
        /// <see cref="CellInfectionState"/>.VirusCount and calls this alongside CellVisuals.SetMicroSprite().
        /// </summary>
        public InCellVirusSpawner CellInCellVirusSpawner;

        /// <summary>See <see cref="CellInCellVirusSpawner"/>'s doc comment - supplies the current virus count at
        /// the moment of entry.</summary>
        public CellInfectionState CellInfectionState;

        // --- Global audio mix (distinct from the player's own AudioSource above) ---

        /// <summary>
        /// Old: `ServiceLocator.Instance.Get&lt;AudioMixerController&gt;()`, whose `SetMaster()`/`SetIncell()`
        /// (Assets/GMTK/Scripts/Behaviors/AudioMixerController.cs:66-74) reassign `.outputAudioMixerGroup` on a
        /// SEPARATE AudioSource (the DontDestroyOnLoad gameplay-music track, `_audioSources[1]`) - NOT the
        /// player's own AudioSource used for the whoosh SFX above. GMTK.AudioMixerController has no _Project
        /// equivalent yet (the Phase-1-installed `_Lumenwake.AudioSystem` is the eventual replacement, per
        /// REFACTORING_PLAN.md section 5's "AudioManagerFactory/AudioMixerController -&gt; _Lumenwake.AudioSystem"
        /// mapping - a Phase 5 concern), so this context exposes the same raw pieces AudioMixerController.
        /// SetMaster()/SetIncell() manipulated directly: the music AudioSource plus the two AudioMixerGroup
        /// targets it swaps between.
        /// </summary>
        public AudioSource GameplayMusicAudioSource;

        /// <summary>Old: AudioMixerController's private `_master` AudioMixerGroup field.</summary>
        public AudioMixerGroup MacroMixerGroup;

        /// <summary>Old: AudioMixerController's private `_incell` AudioMixerGroup field.</summary>
        public AudioMixerGroup MicroMixerGroup;
    }
}
