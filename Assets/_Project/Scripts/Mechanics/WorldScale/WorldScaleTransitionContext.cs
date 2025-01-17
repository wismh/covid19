using System;
using UnityEngine;
using UnityEngine.Audio;
using _Project.Mechanics.Cells;
using _Project.Mechanics.Player;
using _Project.Mechanics.Viruses.Micro;

namespace _Project.Mechanics.WorldScale
{
    /// <summary>Per-call parameters for a Macro/Micro transition - which player/cell/camera/audio pieces are
    /// involved differs every call, unlike WorldScaleConfig which is the same asset for every transition.</summary>
    public sealed class WorldScaleTransitionContext
    {
        // --- Player ---

        public Transform Player;

        /// <summary>The nested visual transform scaled instantly (not DOTween'd), distinct from
        /// <see cref="Player"/>'s own transform, which is one of the DOTween'd values.</summary>
        public Transform PlayerVisualRoot;

        public BoxCollider2D PlayerCollider;

        /// <summary>Used for both the volume dip/restore (micro only) and the transition-whoosh clip.</summary>
        public AudioSource PlayerAudioSource;

        public AudioClip PlayerEnterMacroClip;

        public AudioClip PlayerEnterMicroClip;

        public PlayerMovement PlayerMovement;

        // --- Camera ---

        public UnityEngine.Camera Camera;

        /// <summary>Optional: invoked with the new follow target after the transition's tweens are kicked off. No-op
        /// if left null - target-following is a presentation concern this transition has no opinion on.</summary>
        public Action<Transform> SetCameraTarget;

        /// <summary>Optional: invoked with the world-scale speed multiplier. No-op if left null.</summary>
        public Action<float> SetPlayerSpeedScale;

        // --- Cell being entered/exited ---

        public Transform Cell;

        public CellVisuals CellVisuals;

        public Rigidbody2D CellRigidbody2D;

        /// <summary>Freezes the cell's idle animation while the player is inside it.</summary>
        public Animator CellAnimator;

        /// <summary>Set lazily, once, at the exact moment the player enters this cell - see MicroVirusSpawner's
        /// "SetVirusesCount: LAZY, on micro-world entry only" doc comment for why.</summary>
        public MicroVirusSpawner CellMicroVirusSpawner;

        /// <summary>Supplies the current virus count at the moment of entry for <see cref="CellMicroVirusSpawner"/>.</summary>
        public CellInfectionState CellInfectionState;

        // --- Global audio mix (distinct from the player's own AudioSource above) ---

        public AudioSource GameplayMusicAudioSource;

        public AudioMixerGroup MacroMixerGroup;

        public AudioMixerGroup MicroMixerGroup;
    }
}
