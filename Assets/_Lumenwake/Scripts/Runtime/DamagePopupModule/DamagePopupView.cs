using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace Project.Core.DamagePopupModule
{
    /// <summary>
    /// Pooled world-space damage number that floats upward and fades out.
    /// Spawned via <see cref="Factory"/> with the world position and the hit payload; despawns itself when the tween completes.
    /// </summary>
    public sealed class DamagePopupView : MonoBehaviour, IPoolable<Vector3, DamagePopupInfo, IMemoryPool>
    {
        public event Action<DamagePopupView> OnPopUpDespawned;

        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Animation")]
        [SerializeField] private float floatDistance = 0.8f;
        [SerializeField] private float floatDistanceVariance = 0.1f;
        [SerializeField, Min(1.01f)] private float critFloatDistanceMultiplier = 1.45f;
        [SerializeField] private float duration = 0.9f;
        [SerializeField] private float durationVariance = 0.12f;
        [SerializeField, Min(0.05f)] private float minDuration = 0.5f;
        [SerializeField] private float spawnHorizontalJitter = 0.1f;
        [SerializeField] private Ease moveEase = Ease.InCubic;
        [SerializeField] private Ease fadeEase = Ease.OutCubic;

        [Header("Style")]
        [SerializeField] private Color normalColor = new Color(1f, 0.28f, 0.24f, 1f);
        [SerializeField] private Color critColor = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField, Min(0.01f)] private float normalScale = 1f;
        [SerializeField, Min(0.01f)] private float critScale = 1.35f;

        private IMemoryPool _pool;
        private Sequence _activeSequence;
        private int _animationGeneration;
        private Transform _billboardFacing;
        private Vector3 _baseScale = Vector3.zero;

        private void Awake()
        {
            EnsureBaseScale();
            TryCacheBillboardFacing();
        }

        private void EnsureBaseScale()
        {
            if (_baseScale == Vector3.zero)
            {
                _baseScale = transform.localScale;
                if (_baseScale == Vector3.zero)
                {
                    _baseScale = Vector3.one;
                }
            }
        }

        private void TryCacheBillboardFacing()
        {
            if (_billboardFacing)
            {
                return;
            }

            Camera mainCamera = Camera.main;
            _billboardFacing = mainCamera ? mainCamera.transform : null;
        }

        public void OnSpawned(Vector3 worldPosition, DamagePopupInfo info, IMemoryPool pool)
        {
            TryCacheBillboardFacing();

            _pool = pool;

            if (!_billboardFacing)
            {
                DespawnSelf();
                return;
            }

            Restart(worldPosition, info);
        }

        public void Restart(Vector3 worldPosition, DamagePopupInfo info)
        {
            float jitter = spawnHorizontalJitter;
            Vector3 planar = worldPosition + new Vector3(
                Random.Range(-jitter, jitter),
                0f,
                Random.Range(-jitter, jitter));

            float rise = Mathf.Max(0.05f, floatDistance + Random.Range(-floatDistanceVariance, floatDistanceVariance));
            if (info.IsCrit)
            {
                rise *= critFloatDistanceMultiplier;
            }

            float flightDuration = Mathf.Max(minDuration, duration + Random.Range(-durationVariance, durationVariance));

            EnsureBaseScale();
            float scaleMultiplier = info.IsCrit ? critScale : normalScale;
            transform.position = planar;
            transform.localScale = new Vector3(
                _baseScale.x * scaleMultiplier,
                _baseScale.y * scaleMultiplier,
                _baseScale.z * scaleMultiplier);
            transform.rotation = _billboardFacing.rotation;

            canvasGroup.alpha = 1f;
            label.color = info.CustomColor ?? (info.IsCrit ? critColor : normalColor);
            label.SetText("{0}", Mathf.Max(1, Mathf.RoundToInt(info.Amount)));

            _animationGeneration++;
            int generation = _animationGeneration;

            _activeSequence?.Kill();
            float targetY = planar.y + rise;
            _activeSequence = DOTween.Sequence()
                .SetTarget(this)
                .SetUpdate(true)
                .Append(transform.DOMoveY(targetY, flightDuration).SetEase(moveEase))
                .Join(canvasGroup.DOFade(0f, flightDuration).SetEase(fadeEase))
                .OnKill(() => OnAnimationKilled(generation));
        }

        public void OnDespawned()
        {
            _animationGeneration++;
            _activeSequence?.Kill();
            _activeSequence = null;
            canvasGroup.alpha = 0f;
            _pool = null;
        }

        private void OnAnimationKilled(int generation)
        {
            if (generation != _animationGeneration || _pool == null)
            {
                return;
            }

            DespawnSelf();
        }

        private void DespawnSelf()
        {
            OnPopUpDespawned?.Invoke(this);
            _pool?.Despawn(this);
        }

        public class Factory : PlaceholderFactory<Vector3, DamagePopupInfo, DamagePopupView>
        {
        }
    }
}
