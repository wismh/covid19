using UnityEngine;

namespace Project.Core.DamagePopupModule
{
    public readonly struct DamagePopupInfo
    {
        public DamagePopupInfo(float amount, bool isCrit = false, Color? customColor = null)
        {
            Amount = amount;
            IsCrit = isCrit;
            CustomColor = customColor;
        }

        public float Amount { get; }
        public bool IsCrit { get; }
        public Color? CustomColor { get; }
    }
}
