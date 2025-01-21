using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>
    /// Awaitable Macro/Micro world-scale transition: flips instant physics/audio/sprite state, then runs the
    /// player-scale/camera-size/camera-rotation/player-position tweens as one <see cref="DG.Tweening.Sequence"/>
    /// (they all share one duration and no stagger, so a single sequence is behaviorally identical to four
    /// independent tweens but gives one handle to await/cancel). Bridged to UniTask via a
    /// <see cref="UniTaskCompletionSource"/> set from the sequence's OnKill, since this DOTween build has no
    /// async/UniTask extension module.
    /// </summary>
    public sealed class WorldScaleTransition : IWorldScaleTransition
    {
        private const string DefaultLayerName = "Default";
        private const string InsideCellPlayerLayerName = "InsideCellPlayer";
        private const string BarrierLayerName = "Barrier";

        private readonly WorldScaleConfig _config;

        public WorldScaleTransition(WorldScaleConfig config)
        {
            _config = config != null ? config : throw new ArgumentNullException(nameof(config));
        }

        public async UniTask EnterMacroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Player == null)
                return;

            context.CellVisuals?.SetMacroSprite();

            if (context.CellRigidbody2D != null)
            {
                context.CellRigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                Vector2 maxVelocity = _config.CellMacroMaxVelocity;
                context.CellRigidbody2D.linearVelocity = new Vector2(
                    UnityEngine.Random.Range(-maxVelocity.x, maxVelocity.x),
                    UnityEngine.Random.Range(-maxVelocity.y, maxVelocity.y));
            }

            context.PlayerMovement?.Stop();

            if (context.CellAnimator != null)
                context.CellAnimator.speed = 1f;

            if (context.PlayerVisualRoot != null)
                context.PlayerVisualRoot.localScale = _config.MacroPlayerScale;

            context.SetPlayerSpeedScale?.Invoke(_config.MacroSpeedScale);
            context.SetCameraTarget?.Invoke(context.Player);

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
                    context.Player.gameObject.layer = LayerMask.NameToLayer(DefaultLayerName);

                    if (context.PlayerCollider != null)
                        context.PlayerCollider.isTrigger = false;
                },
                cancellationToken: cancellationToken);
        }

        public async UniTask EnterMicroWorldAsync(WorldScaleTransitionContext context, CancellationToken cancellationToken = default)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.CellVisuals?.SetMicroSprite();

            if (context.CellMicroVirusSpawner != null && context.CellInfectionState != null)
                context.CellMicroVirusSpawner.SetVirusesCount(context.CellInfectionState.VirusCount);

            if (context.CellRigidbody2D != null)
            {
                context.CellRigidbody2D.linearVelocity = Vector2.zero;
                context.CellRigidbody2D.angularVelocity = 0f;
                context.CellRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            }

            context.PlayerMovement?.Stop();

            if (context.PlayerCollider != null)
                context.PlayerCollider.isTrigger = true;

            if (context.PlayerVisualRoot != null)
                context.PlayerVisualRoot.localScale = _config.MicroPlayerScale;

            if (context.CellAnimator != null)
                context.CellAnimator.speed = 0f;

            context.SetPlayerSpeedScale?.Invoke(_config.MicroSpeedScale);
            context.SetCameraTarget?.Invoke(context.Cell);

            Vector3 insidePosition = context.Player.position + (context.Cell.position - context.Player.position) / 2f;
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
                    context.Player.gameObject.layer = LayerMask.NameToLayer(InsideCellPlayerLayerName);

                    if (context.PlayerCollider != null)
                        context.PlayerCollider.isTrigger = false;
                },
                cancellationToken: cancellationToken);
        }

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

            sequence.Insert(0f, DOTween.To(
                () => playerTransform.localScale,
                scale => playerTransform.localScale = scale,
                targetPlayerScale,
                duration));

            if (camera != null)
            {
                sequence.Insert(0f, DOTween.To(
                    () => camera.orthographicSize,
                    size => camera.orthographicSize = size,
                    targetOrthographicSize,
                    duration));
            }

            if (cameraTransform != null)
            {
                sequence.Insert(0f, DOTween.To(
                    () => cameraTransform.rotation.z,
                    angle => cameraTransform.rotation = new Quaternion(
                        cameraTransform.rotation.x, cameraTransform.rotation.y, angle, cameraTransform.rotation.w),
                    targetCameraRotationZ,
                    duration));
            }

            sequence.Insert(0f, DOTween.To(
                () => playerTransform.position,
                position => playerTransform.position = position,
                targetPlayerPosition,
                duration));

            sequence.OnComplete(() => onComplete?.Invoke());

            // Pinned explicitly rather than relying on DOTweenSettings.asset's project-wide default: the
            // completion signal below depends on OnKill firing when the sequence finishes naturally.
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

        // Deliberately never touches the player's Rigidbody2D.isKinematic - it stays Dynamic (the prefab default)
        // throughout both transitions; only the cell's own Rigidbody2D toggles kinematic state.
    }
}
