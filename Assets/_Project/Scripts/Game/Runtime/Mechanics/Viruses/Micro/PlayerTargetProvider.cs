using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Plain C# wrapper around the player's Transform - no MonoBehaviour, no Zenject binding.</summary>
    public sealed class PlayerTargetProvider : IPlayerTargetProvider
    {
        public Transform PlayerTransform { get; }

        public PlayerTargetProvider(Transform playerTransform)
        {
            PlayerTransform = playerTransform;
        }
    }
}
