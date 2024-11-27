using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.DamagePopupModule
{
    public sealed class DamagePopupSpawner : IDamagePopupSpawner
    {
        private readonly struct ActivePopupState
        {
            public readonly DamagePopupView View;
            public readonly float LastHitTime;
            public readonly float AccumulatedAmount;
            public readonly bool IsCrit;
            public readonly Color? CustomColor;

            public ActivePopupState(DamagePopupView view, float lastHitTime, float accumulatedAmount, bool isCrit, Color? customColor)
            {
                View = view;
                LastHitTime = lastHitTime;
                AccumulatedAmount = accumulatedAmount;
                IsCrit = isCrit;
                CustomColor = customColor;
            }
        }

        private const float MergeCooldownSeconds = 0.2f;

        private readonly DamagePopupView.Factory _factory;
        private readonly Dictionary<Transform, ActivePopupState> _activePopupsByTarget = new();
        private readonly Dictionary<DamagePopupView, Transform> _targetByPopup = new();

        public DamagePopupSpawner(DamagePopupView.Factory factory)
        {
            _factory = factory;
        }

        public void Spawn(Vector3 worldPosition, DamagePopupInfo info)
        {
            if (info.Amount <= 0f)
            {
                return;
            }

            _factory.Create(worldPosition, info);
        }

        public void Spawn(Transform target, Vector3 worldPosition, DamagePopupInfo info)
        {
            if (info.Amount <= 0f)
            {
                return;
            }

            if (!target)
            {
                Spawn(worldPosition, info);
                return;
            }

            float now = Time.unscaledTime;
            bool hasExisting = _activePopupsByTarget.TryGetValue(target, out ActivePopupState existing);
            bool shouldMerge = hasExisting && now - existing.LastHitTime <= MergeCooldownSeconds;
            if (shouldMerge)
            {
                float mergedAmount = existing.AccumulatedAmount + info.Amount;
                bool mergedCrit = existing.IsCrit || info.IsCrit;
                Color? mergedColor = info.CustomColor ?? existing.CustomColor;
                DamagePopupInfo mergedInfo = new(mergedAmount, mergedCrit, mergedColor);
                existing.View.Restart(worldPosition, mergedInfo);
                _activePopupsByTarget[target] = new ActivePopupState(existing.View, now, mergedAmount, mergedCrit, mergedColor);
                return;
            }

            DamagePopupView popup = _factory.Create(worldPosition, info);
            popup.OnPopUpDespawned -= HandlePopupOnPopUpDespawned;
            popup.OnPopUpDespawned += HandlePopupOnPopUpDespawned;
            _activePopupsByTarget[target] = new ActivePopupState(popup, now, info.Amount, info.IsCrit, info.CustomColor);
            _targetByPopup[popup] = target;
        }

        private void HandlePopupOnPopUpDespawned(DamagePopupView popup)
        {
            popup.OnPopUpDespawned -= HandlePopupOnPopUpDespawned;

            bool hasTarget = _targetByPopup.TryGetValue(popup, out Transform target);
            if (!hasTarget)
            {
                return;
            }

            _targetByPopup.Remove(popup);
            bool hasState = _activePopupsByTarget.TryGetValue(target, out ActivePopupState state);
            if (!hasState || state.View != popup)
            {
                return;
            }

            _activePopupsByTarget.Remove(target);
        }
    }
}
