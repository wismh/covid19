using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.AudioSystem;
using UnityEngine;

namespace _Project.Mechanics.Combat
{
    /// <summary>Reusable pooled-enemy death timing: disable collider, wait, play the death animation/audio, wait,
    /// invoke completion. Shared by MacroVirusController/MicroVirusController - their sequences are identical.
    /// PlayerHealth's differs enough (extra movement/turret/arrow disabling, different final semantics) that it
    /// keeps its own.</summary>
    public sealed class PooledEntityDeathSequence
    {
        private readonly Collider2D _collider;
        private readonly Animator _animator;
        private readonly IAudioSystem _audioSystem;
        private readonly SoundData _deathSound;
        private readonly float _animationDelaySeconds;
        private readonly float _killDelaySeconds;

        public PooledEntityDeathSequence(
            Collider2D collider,
            Animator animator,
            IAudioSystem audioSystem,
            SoundData deathSound,
            float animationDelaySeconds,
            float killDelaySeconds)
        {
            _collider = collider;
            _animator = animator;
            _audioSystem = audioSystem;
            _deathSound = deathSound;
            _animationDelaySeconds = animationDelaySeconds;
            _killDelaySeconds = killDelaySeconds;
        }

        public async UniTaskVoid RunAsync(CancellationToken cancellationToken, Action onComplete)
        {
            _collider.enabled = false;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_animationDelaySeconds), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _animator.Play("Base Layer.Died");
            _audioSystem.PlaySfx(_deathSound);

            // Waits before invoking completion (which despawns back into the pool) so the death animation/audio
            // actually has time to be seen/heard first.
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_killDelaySeconds), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            onComplete?.Invoke();
        }
    }
}
