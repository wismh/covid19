using UnityEngine;

namespace _Project.Mechanics.Viruses.InCell
{
    /// <summary>
    /// Replaces `ServiceLocator.Instance.Get&lt;PlayerStats&gt;().Player` (used by all three old
    /// GMTK.MicroViruses.VirusBehavior implementations - OrangeVirus/CucumberVirus/SpinerVirus, Assets/GMTK/
    /// Scripts/Behaviors/MicroVirus/Behaviors/*.cs - to find the player to home in on) with a plain
    /// constructor-injected seam. Behaviors read <see cref="PlayerTransform"/> once in Init(), exactly matching
    /// old code's snapshot-at-Init behavior (none of the three re-fetch the target after Init - if it's null
    /// they simply skip movement that frame, unlike macro virus's MoveToCellBehavior which does re-fetch a null
    /// cell target).
    /// </summary>
    public interface IPlayerTargetProvider
    {
        Transform PlayerTransform { get; }
    }
}
