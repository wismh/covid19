using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _Project.Mechanics.Combat
{
    /// <summary>Reusable hit-flash: swaps a SpriteRenderer to a flash Material for a fixed duration, then restores
    /// the original. Shared by Player/MacroVirus/MicroVirus damage feedback.</summary>
    public sealed class DamageFlashEffect
    {
        private readonly SpriteRenderer _spriteRenderer;
        private readonly Material _flashMaterial;
        private readonly float _durationSeconds;

        // Cached once per life (ResetForSpawn), not re-captured inside PlayAsync - two overlapping flashes would
        // otherwise race on which material to revert to and can get stuck fully white.
        private Material _originalMaterial;

        public DamageFlashEffect(SpriteRenderer spriteRenderer, Material flashMaterial, float durationSeconds)
        {
            _spriteRenderer = spriteRenderer;
            _flashMaterial = flashMaterial;
            _durationSeconds = durationSeconds;
        }

        /// <summary>Call once per life (Awake for a non-pooled entity, every OnSpawned for a pooled one) - forces
        /// back to the shared material first since a previous life may have left an instanced one behind.</summary>
        public void ResetForSpawn()
        {
            _spriteRenderer.material = _spriteRenderer.sharedMaterial;
            _originalMaterial = _spriteRenderer.material;
        }

        public async UniTaskVoid PlayAsync(CancellationToken cancellationToken)
        {
            _spriteRenderer.material = _flashMaterial;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_durationSeconds), cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _spriteRenderer.material = _originalMaterial;
        }
    }
}
