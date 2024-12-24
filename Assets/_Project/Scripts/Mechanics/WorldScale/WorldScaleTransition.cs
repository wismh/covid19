using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Ports GMTK.GameStates.TransitionIntoMacroWorld and TransitionIntoMicroWorld (Assets/GMTK/Scripts/
    /// GameStates/TransitionIntoMacroWorld.cs, TransitionIntoMicroWorld.cs) into one awaitable class implementing
    /// <see cref="IWorldScaleTransition"/>. Both old classes are near-identical in shape (resolve refs from
    /// ServiceLocator, flip a batch of instant physics/audio/sprite state, kick off four parallel DOTween.To
    /// tweens, fix up layer/collider/volume in the last tween's OnComplete) - kept as one class with two methods
    /// rather than split into MacroWorldTransition/MicroWorldTransition, since a single WorldScaleConfig and a
    /// single set of DOTween-to-UniTask plumbing (see below) is shared by both and splitting would mean either
    /// duplicating that plumbing or introducing a third shared base class for two ~40-line methods.
    ///
    /// --- DOTween-to-UniTask bridging: which approach, and why ---
    /// The task brief names two established patterns: `sequence.AsyncWaitForCompletion()` (if DOTween ships an
    /// async/UniTask extension module) or a `UniTaskCompletionSource` set from `OnComplete`. Checked Assets/
    /// Plugins/Demigiant/DOTween/Modules/ - it contains Audio/EPOOutline/Physics/Physics2D/Sprite/UnityVersion/
    /// Utils modules, no Async/UniTask module. Also grepped Assets/Plugins/Demigiant/DOTween/DOTween.XML (the
    /// precompiled DOTween.dll's API docs) for "AsyncWaitForCompletion"/"AwaitForCompletion" - zero matches, so
    /// this DOTween build's core DLL doesn't expose it either. Conclusion: no async extension is available in
    /// this project, so this class uses the <see cref="UniTaskCompletionSource"/> + `OnComplete` fallback.
    ///
    /// Old code created FOUR independent `DOTween.To(...)` calls per transition (scale, camera orthographic size,
    /// camera rotation, position) with no relationship to each other beyond sharing the same duration and no
    /// stagger - i.e. they already ran fully in parallel. Rather than juggle four separate completion sources,
    /// this class inserts all four into one `DOTween.Sequence()` at time 0 (`.Insert(0f, tween)`), which is
    /// behaviorally identical (same parallel timing, same total duration) but gives a single handle: one
    /// `OnComplete` (replacing the old position-tween-only OnComplete - functionally equivalent since every
    /// inserted tween shares the same duration and therefore completes on the same frame) and one thing to
    /// `Kill()` on cancellation.
    ///
    /// Neither old class ever calls `.SetEase(...)` on any tween, so every tween here inherits DOTween's
    /// project-wide default ease exactly like the old code did - see WorldScaleConfig's class doc comment for
    /// where that default (Ease.OutQuad) actually comes from (Assets/Resources/DOTweenSettings.asset).
    /// </summary>
    public sealed class WorldScaleTransition : IWorldScaleTransition
    {
        // ProjectSettings/TagManager.asset layer list (also documented in TECHNICAL_AUDIT.md). The original jam
        // project misspelled this layer "Barier" - fixed to "Barrier" as part of the project-wide naming pass
        // (TagManager.asset layer 15 renamed alongside this constant and the scene GameObjects/textures that
        // shared the typo).
        private const string DefaultLayerName = "Default";
        private const string InsideCellPlayerLayerName = "InsideCellPlayer";
        private const string BarrierLayerName = "Barrier";

        private readonly WorldScaleConfig _config;

        public WorldScaleTransition(WorldScaleConfig config)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Ports GMTK.GameStates.TransitionIntoMacroWorld.Start().</summary>
        public async UniTask EnterMacroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Old: `if (_player == null) return;` (TransitionIntoMacroWorld.cs:20-21) - TransitionIntoMicroWorld
            // has NO equivalent null check (see EnterMicroWorldAsync below). This asymmetry is in the original
            // code, not introduced here.
            if (context.Player == null)
                return;

            // Old: `cellController.SetMacroSprite();` (CellController.SetMacroSprite, ported onto CellVisuals in
            // Phase 3 with an identical signature).
            context.CellVisuals?.SetMacroSprite();

            // Old: `cellController.SetVelocity(WorldState.MACRO_WORLD);` - CellController.cs:148-151. Inlined
            // here since Phase 3 never ported CellController.SetVelocity onto any Cells/* class (see
            // WorldScaleTransitionContext.CellRigidbody2D's doc comment). Old code ALSO does a second, redundant
            // `nearestCell.GetComponent<Rigidbody2D>().isKinematic = false;` immediately after this call
            // (TransitionIntoMacroWorld.cs:29) - already false from the line below, so that second assignment is
            // a genuine no-op in the original and is deliberately collapsed into this single assignment rather
            // than duplicated.
            if (context.CellRigidbody2D != null)
            {
                context.CellRigidbody2D.isKinematic = false;
                Vector2 maxVelocity = _config.CellMacroMaxVelocity;
                context.CellRigidbody2D.linearVelocity = new Vector2(
                    UnityEngine.Random.Range(-maxVelocity.x, maxVelocity.x),
                    UnityEngine.Random.Range(-maxVelocity.y, maxVelocity.y));
            }

            // Old: `_player.GetComponent<DriftMovableObject>().Stop();` -> PlayerMovement.Stop() (Phase 2).
            context.PlayerMovement?.Stop();

            // Old: `nearestCell.GetComponent<Animator>().speed = 1;`
            if (context.CellAnimator != null)
                context.CellAnimator.speed = 1f;

            // Old: `_player.GetChild(0).GetChild(0).localScale = new(1f, 1f, 1);` - instant, NOT DOTween'd
            // (distinct from the root `_player.transform.localScale` DOTween below).
            if (context.PlayerVisualRoot != null)
                context.PlayerVisualRoot.localScale = _config.MacroPlayerScale;

            // Old: `_player.GetComponent<AudioList>().Play(0);` -> AudioList.Play: `_source.clip = _clips[i];
            // _source.Play();` (AudioList.cs:16-20).
            if (context.PlayerAudioSource != null && context.PlayerEnterMacroClip != null)
            {
                context.PlayerAudioSource.clip = context.PlayerEnterMacroClip;
                context.PlayerAudioSource.Play();
            }

            // Old: `ServiceLocator.Instance.Get<AudioMixerController>().SetMaster();` -> `_audioSources[1].
            // outputAudioMixerGroup = _master;` (AudioMixerController.cs:71-74) - the gameplay MUSIC source, not
            // the player's whoosh-SFX source above.
            if (context.GameplayMusicAudioSource != null)
                context.GameplayMusicAudioSource.outputAudioMixerGroup = context.MacroMixerGroup;

            // Old: `ServiceLocator.Instance.Get<PlayerStats>().SpeedScale = 1f;` - see WorldScaleController's
            // class doc comment, seam item 4, and WorldScaleTransitionContext.SetPlayerSpeedScale's doc comment.
            context.SetPlayerSpeedScale?.Invoke(1f);

            // Old: `_camera.Target = _player;` - see WorldScaleTransitionContext.SetCameraTarget's doc comment.
            context.SetCameraTarget?.Invoke(context.Player);

            // Old: `var outsidePosition = _player.position + _player.right * 5; if (Physics2D.Linecast(_player.
            // position, outsidePosition, LayerMask.GetMask("Barier"))) outsidePosition = _player.position -
            // _player.right * 5;` (TransitionIntoMacroWorld.cs:39-42).
            Vector3 outsidePosition = context.Player.position + context.Player.right * _config.MacroExitOffsetDistance;
            if (Physics2D.Linecast(context.Player.position, outsidePosition, LayerMask.GetMask(BarrierLayerName)))
                outsidePosition = context.Player.position - context.Player.right * _config.MacroExitOffsetDistance;

            Transform cameraTransform = context.Camera != null ? context.Camera.transform : null;

            await RunTweensAsync(
                playerTransform: context.Player,
                targetPlayerScale: _config.MacroPlayerScale,
                camera: context.Camera,
                targetOrthographicSize: _config.MacroCameraOrthographicSize,
                cameraTransform: cameraTransform,
                targetCameraRotationZ: 0f,
                targetPlayerPosition: outsidePosition,
                onComplete: () =>
                {
                    // Old: `_player.gameObject.layer = LayerMask.NameToLayer("Default"); _player.
                    // GetComponent<BoxCollider2D>().isTrigger = false; // _player.GetComponent<Rigidbody2D>().
                    // isKinematic = false; ServiceLocator.Instance.Get<HUD>().AdviceGetOutSet(false);`
                    // (TransitionIntoMacroWorld.cs:72-77).
                    context.Player.gameObject.layer = LayerMask.NameToLayer(DefaultLayerName);

                    if (context.PlayerCollider != null)
                        context.PlayerCollider.isTrigger = false;

                    // Old commented-out line `// _player.GetComponent<Rigidbody2D>().isKinematic = false;` is
                    // deliberately NOT re-enabled here - see this class's own trailing doc comment ("Open
                    // question: the commented-out isKinematic lines") for why.

                    // Old: `ServiceLocator.Instance.Get<HUD>().AdviceGetOutSet(false);` - GMTK.UI.HUD has no
                    // _Project port yet (Phase 5 concern, see AdviceDialog.cs's Update()/OnPlayerPenetration()
                    // for the source behaviour). Not reproduced - a future HudScreen listens to
                    // WorldScaleController.OnExitedMicroWorld instead.
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>Ports GMTK.GameStates.TransitionIntoMicroWorld.Start().</summary>
        public async UniTask EnterMicroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            // Old: `cellController.SetMicroSprite();`
            context.CellVisuals?.SetMicroSprite();

            // Old: `cellController.SetMicroSprite()` also did `_inCellVirusManager.SetVirusesCount(_virusCount);`
            // right here - see WorldScaleTransitionContext.CellMicroVirusSpawner's doc comment (Phase 7 testing
            // fix, 2026-09-11) for why this has to be lazy/on-entry rather than reactive.
            if (context.CellMicroVirusSpawner != null && context.CellInfectionState != null)
                context.CellMicroVirusSpawner.SetVirusesCount(context.CellInfectionState.VirusCount);

            // Old: `cellController.SetVelocity(WorldState.MICRO_WORLD);` - CellController.cs:152-155, inlined for
            // the same reason as the macro branch above. Old code ALSO does a redundant second
            // `nearestCell.GetComponent<Rigidbody2D>().isKinematic = true;` right after (TransitionIntoMicroWorld
            // .cs:32) - collapsed into this one assignment, same as the macro branch.
            if (context.CellRigidbody2D != null)
            {
                context.CellRigidbody2D.linearVelocity = Vector2.zero;
                context.CellRigidbody2D.angularVelocity = 0f;
                context.CellRigidbody2D.isKinematic = true;
            }

            // Old: `_player.GetComponent<DriftMovableObject>().Stop();`
            context.PlayerMovement?.Stop();

            // Old: `_player.GetComponent<BoxCollider2D>().isTrigger = true;`
            if (context.PlayerCollider != null)
                context.PlayerCollider.isTrigger = true;

            // Old: `_player.GetChild(0).GetChild(0).localScale = new(0.3f, 0.3f, 1);` - instant, not DOTween'd.
            if (context.PlayerVisualRoot != null)
                context.PlayerVisualRoot.localScale = _config.MicroPlayerScale;

            // Old: `_player.GetComponent<AudioSource>().volume = 0.4f;` then `_player.GetComponent<AudioList>().
            // Play(1);` (TransitionIntoMicroWorld.cs:29-30).
            if (context.PlayerAudioSource != null)
            {
                context.PlayerAudioSource.volume = _config.MicroPlayerAudioVolume;

                if (context.PlayerEnterMicroClip != null)
                {
                    context.PlayerAudioSource.clip = context.PlayerEnterMicroClip;
                    context.PlayerAudioSource.Play();
                }
            }

            // Old: `nearestCell.GetComponent<Animator>().speed = 0;`
            if (context.CellAnimator != null)
                context.CellAnimator.speed = 0f;

            // Old: `ServiceLocator.Instance.Get<AudioMixerController>().SetIncell();`
            if (context.GameplayMusicAudioSource != null)
                context.GameplayMusicAudioSource.outputAudioMixerGroup = context.MicroMixerGroup;

            // Old: `ServiceLocator.Instance.Get<PlayerStats>().SpeedScale = 0.01f;`
            context.SetPlayerSpeedScale?.Invoke(0.01f);

            // Old: `_camera.Target = nearestCell;`
            context.SetCameraTarget?.Invoke(context.Cell);

            // Old: `var insidePosition = _player.position + (nearestCell.position - _player.position) / 2;`
            // (TransitionIntoMicroWorld.cs:40) - the midpoint between player and cell.
            Vector3 insidePosition = context.Player.position + (context.Cell.position - context.Player.position) / 2f;

            // Old: camera rotation.z DOTween's target is `nearestCell.rotation.z` - a runtime value read at call
            // time, not a WorldScaleConfig number (see WorldScaleConfig's class doc comment).
            float targetCameraRotationZ = context.Cell.rotation.z;

            Transform cameraTransform = context.Camera != null ? context.Camera.transform : null;

            await RunTweensAsync(
                playerTransform: context.Player,
                targetPlayerScale: _config.MicroPlayerScale,
                camera: context.Camera,
                targetOrthographicSize: _config.MicroCameraOrthographicSize,
                cameraTransform: cameraTransform,
                targetCameraRotationZ: targetCameraRotationZ,
                targetPlayerPosition: insidePosition,
                onComplete: () =>
                {
                    // Old: `_player.gameObject.layer = LayerMask.NameToLayer("InsideCellPlayer"); _player.
                    // GetComponent<BoxCollider2D>().isTrigger = false; _player.GetComponent<AudioSource>().volume
                    // = 1; // _player.GetComponent<Rigidbody2D>().isKinematic = false; ServiceLocator.Instance.
                    // Get<HUD>().AdviceGetOutSet(true);` (TransitionIntoMicroWorld.cs:70-76).
                    context.Player.gameObject.layer = LayerMask.NameToLayer(InsideCellPlayerLayerName);

                    if (context.PlayerCollider != null)
                        context.PlayerCollider.isTrigger = false;

                    if (context.PlayerAudioSource != null)
                        context.PlayerAudioSource.volume = _config.RestoredPlayerAudioVolume;

                    // Commented-out `isKinematic = false` intentionally not reinstated - see this class's
                    // trailing doc comment.

                    // Old: `ServiceLocator.Instance.Get<HUD>().AdviceGetOutSet(true);` - see the macro branch's
                    // equivalent comment; HUD is a Phase 5 concern.
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Bundles the four parallel DOTween.To(...) calls both old transitions make (player scale, camera
        /// orthographic size, camera rotation.z, player position) into one Sequence so the whole group can be
        /// awaited/cancelled as a unit - see this class's own header doc comment for why this is behaviorally
        /// identical to the old code's four independent tweens.
        /// </summary>
        private async UniTask RunTweensAsync(
            Transform playerTransform,
            Vector3 targetPlayerScale,
            UnityEngine.Camera camera,
            float targetOrthographicSize,
            Transform cameraTransform,
            float targetCameraRotationZ,
            Vector3 targetPlayerPosition,
            Action onComplete,
            CancellationToken cancellationToken)
        {
            float duration = _config.TransitionDurationSeconds;
            var sequence = DOTween.Sequence();

            // Old: `DOTween.To(() => _player.transform.localScale, scale => _player.localScale = scale,
            // target, duration);`
            sequence.Insert(0f, DOTween.To(
                () => playerTransform.localScale,
                scale => playerTransform.localScale = scale,
                targetPlayerScale,
                duration));

            // Old: `DOTween.To(() => Camera.main.orthographicSize, size => Camera.main.orthographicSize = size,
            // target, duration);`
            if (camera != null)
            {
                sequence.Insert(0f, DOTween.To(
                    () => camera.orthographicSize,
                    size => camera.orthographicSize = size,
                    targetOrthographicSize,
                    duration));
            }

            // Old: `DOTween.To(() => _camera.transform.rotation.z, angle => _camera.transform.rotation = new
            // Quaternion(_camera.transform.rotation.x, _camera.transform.rotation.y, angle, _camera.transform.
            // rotation.w), target, duration);` - reproduced exactly, including constructing the Quaternion
            // components this way rather than via Euler angles (matches the old code's own, slightly unusual,
            // direct-component approach verbatim).
            if (cameraTransform != null)
            {
                sequence.Insert(0f, DOTween.To(
                    () => cameraTransform.rotation.z,
                    angle => cameraTransform.rotation = new Quaternion(
                        cameraTransform.rotation.x, cameraTransform.rotation.y, angle, cameraTransform.rotation.w),
                    targetCameraRotationZ,
                    duration));
            }

            // Old: `DOTween.To(() => _player.position, position => _player.position = position, target,
            // duration).OnComplete(() => { ... });` - the only tween with an OnComplete in the old code; folded
            // into the shared sequence's OnComplete below instead (see header doc comment).
            sequence.Insert(0f, DOTween.To(
                () => playerTransform.position,
                position => playerTransform.position = position,
                targetPlayerPosition,
                duration));

            sequence.OnComplete(() => onComplete?.Invoke());

            // Explicit rather than relying on Assets/Resources/DOTweenSettings.asset's `defaultAutoKill: 1` - the
            // completion signal below depends on OnKill firing once the sequence finishes naturally (DOTween
            // auto-kills a completed tween/sequence by default, which is what fires OnKill with no manual Kill()
            // call), so this is pinned explicitly rather than trusting a project-wide setting that could change.
            sequence.SetAutoKill(true);

            var completionSource = new UniTaskCompletionSource();
            sequence.OnKill(() => completionSource.TrySetResult());

            using (cancellationToken.Register(() => sequence.Kill(false)))
            {
                sequence.Play();
                await completionSource.Task;
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        // --- Open question: the commented-out isKinematic lines (REFACTORING_PLAN.md Phase 4 / task brief) ---
        //
        // Both old files end their position tween's OnComplete with a commented-out
        // `// _player.GetComponent<Rigidbody2D>().isKinematic = false;` (TransitionIntoMacroWorld.cs:74,
        // TransitionIntoMicroWorld.cs:73) immediately after setting `.isTrigger = false`. Investigated rather than
        // guessed: Assets/GMTK/Prefabs/Player.prefab's Rigidbody2D component (around line 5500) serializes
        // `m_BodyType: 0`, which is RigidbodyType2D.Dynamic (Kinematic is 1) - so the player's Rigidbody2D is
        // ALREADY non-kinematic (isKinematic == false) by default from the prefab itself. Neither transition ever
        // sets it to `true` anywhere else (only the CELL's Rigidbody2D toggles isKinematic, via SetVelocity above)
        // - so both commented-out lines would assign a value the field already holds; as written, they are dead,
        // functionally-inert no-ops even if uncommented.
        //
        // This doesn't fully explain WHY they exist, though: GMTK.PlayerBodyController.FixedUpdate
        // unconditionally zeroes the Rigidbody2D's velocity every fixed frame regardless of isKinematic
        // (`_rd.linearVelocity = Vector3.zero; _rd.angularVelocity = 0;`), and movement itself is fully
        // transform-driven (DriftMovableObject.Update: `transform.position += ...`) - so even if some earlier,
        // now-gone version of the code DID toggle the player kinematic at some point, the line as it stands today
        // has no discoverable functional purpose given everything else these two files and PlayerBodyController
        // currently do. Most likely explanation: a leftover from an earlier iteration (possibly one where
        // entering/exiting a cell toggled player physics mode too, mirroring the cell's own isKinematic toggle)
        // that was abandoned - within the jam's 96-hour deadline (GDD.md section 1) commenting out rather than
        // deleting is a plausible "don't touch it, not sure why it's here" move. Could not determine a definitive
        // reason from the surrounding code/prefabs alone, so per the task brief this is flagged as a known open
        // question rather than re-enabled or silently dropped: WorldScaleTransition intentionally does NOT touch
        // the player's Rigidbody2D.isKinematic at all in either direction, matching the OLD code's ACTUAL runtime
        // behavior (both lines were dead/commented in the source being ported), not the commented-out intent.
    }
}
