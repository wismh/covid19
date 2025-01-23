using UnityEngine;

namespace _Project.Mechanics.Viruses.Micro
{
    /// <summary>Seam for finding the player to home in on. Behaviors read <see cref="PlayerTransform"/> once in
    /// Init() and don't re-fetch it - if it's null they simply skip movement that frame.</summary>
    public interface IPlayerTargetProvider
    {
        Transform PlayerTransform { get; }
    }
}
