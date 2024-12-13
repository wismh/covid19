using DG.Tweening;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace _Project.Mechanics.Cells
{
    /// <summary>
    /// Ports the sprite/shader/entrails half of GMTK.CellController (Assets/GMTK/Scripts/Behaviors/Cell/
    /// CellController.cs - SetMacroSprite/SetMicroSprite/ShuffleEntrails + the "_Blend" shader tween from
    /// PoisonCoroutine). See CellInfectionState's doc comment for the full split rationale.
    ///
    /// Fixes the fragile lookup TECHNICAL_AUDIT.md section 2.2 calls out by name: old code did
    /// `transform.GetChild(0).Find("Entrails")` inside ShuffleEntrails() (and `transform.GetChild(0)` again in
    /// SetMacroSprite/SetMicroSprite/Start) - any reordering/renaming of children in the Unity hierarchy would
    /// silently break this at runtime with no compile error. Replaced with direct [SerializeField] references
    /// (<see cref="_entrailsRoot"/> for the child GameObject that gets toggled, <see cref="_entrailsContainer"/>
    /// for the "Entrails" transform whose children get shuffled).
    ///
    /// Composition: holds a direct [SerializeField] reference to <see cref="CellHealth"/> and subscribes to its
    /// OnHealthChanged event to drive the "_Blend" tween - same sibling-reference idiom as CellHealth's own
    /// reference to CellInfectionState (mirrors PlayerHealth/PlayerMovement from Phase 2). CellHealth itself has
    /// no reference back to CellVisuals - health/poison logic doesn't need to know how (or whether) it's
    /// presented, matching the "mechanics shouldn't own presentation" principle PlayerTurret's ReloadProgress01
    /// seam already established.
    ///
    /// Out of scope: old CellController.SetMicroSprite() also called
    /// `_inCellVirusManager.SetVirusesCount(_virusCount)` to spawn/reposition in-cell virus instances to match
    /// the infection count. That's a spawning/pooling concern for a future in-cell-virus manager class (not part
    /// of this phase's file list - Phase 3 only asks for the InCellVirusController/behaviors/config, not a
    /// spawner), so it's left for a future composition root to wire via CellInfectionState.OnVirusCountChanged.
    ///
    /// Death animation/audio (Phase 8 testing fix, 2026-09-11): now also subscribes to CellHealth.OnDied - see
    /// <see cref="_animator"/>'s doc comment.
    /// </summary>
    public sealed class CellVisuals : MonoBehaviour
    {
        [SerializeField] private CellHealth _health;

        [SerializeField] private SpriteRenderer _spriteRenderer;

        // Old: `transform.GetChild(0)` - the entrails child GameObject, toggled on/off between macro/micro sprite
        // modes (macro: hidden: micro: shown + shuffled).
        [SerializeField] private GameObject _entrailsRoot;

        // Old: `transform.GetChild(0).Find("Entrails")` - the container whose direct children are the individual
        // entrail pieces repositioned by ShuffleEntrails().
        [SerializeField] private Transform _entrailsContainer;

        [SerializeField] private Texture _macroWorldSprite;
        [SerializeField] private Sprite _macroWorldBadSprite;
        [SerializeField] private Texture _microWorldSprite;
        [SerializeField] private Sprite _microWorldBadSprite;

        /// <summary>
        /// Phase 8 testing fix (2026-09-11): old CellController.Die() played a death animation/sound
        /// (`_animator.enabled = true; _animator.Play("Base Layer.Dead"); _audioSource.Play();`) that this class
        /// never ported - CellHealth.OnDied's own doc comment explicitly scoped that out ("played a death
        /// animation/audio... None of that is reproduced here"). User-reported symptom: no "explosion" animation
        /// when a cell dies. Resolved via GetComponent in Awake rather than new [SerializeField]s - Cell.prefab
        /// already carries a root-level Animator ("Cell" controller, with a "Dead" state under "Base Layer",
        /// confirmed live) and AudioSource ("CellDead" clip) - the exact same layout/reasoning
        /// MacroVirusController/InCellVirusController already used for their own death audio.
        /// </summary>
        private Animator _animator;
        private AudioSource _deathAudioSource;

        private CellConfig _config;

        [Inject]
        public void Construct(CellConfig config)
        {
            _config = config;
        }

        private void Awake()
        {
            // See _animator's doc comment.
            _animator = GetComponent<Animator>();
            _deathAudioSource = GetComponent<AudioSource>();
        }

        private void Start()
        {
            // Old: CellController.Start() -> `_spriteRenderer.material.SetFloat("_Blend", 1f);
            // transform.GetChild(0).gameObject.SetActive(false); ... _animator.enabled = false;` (the last part,
            // on the line right after the entrails-hide, was missed by the initial port - see _animator's own
            // doc comment for why that matters beyond just the death animation: Cell.controller's only other
            // state, "Idle (Empty)", was left free to keep animating every frame the whole time the cell was
            // alive, fighting the shader "_Blend" tween and SetMacroSprite/SetMicroSprite for control of the same
            // SpriteRenderer - user-reported symptom this also explains: the wrong (non-"empty") sprite showing
            // once a cell got sick while the player was inside it. Old code kept the Animator OFF except for the
            // brief "Dead" playback; restored that exactly.
            if (_spriteRenderer != null)
                _spriteRenderer.material.SetFloat("_Blend", 1f);

            if (_entrailsRoot != null)
                _entrailsRoot.SetActive(false);

            if (_animator != null)
                _animator.enabled = false;

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _health.OnDied += HandleDied;
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.OnDied -= HandleDied;
            }
        }

        /// <summary>Old: CellController.Die() -> `_animator.enabled = true; _animator.Play("Base Layer.Dead");
        /// _audioSource.Play();` - see _animator's own doc comment for why it starts disabled.</summary>
        private void HandleDied(int virusCountAtDeath)
        {
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Play("Base Layer.Dead");
            }

            if (_deathAudioSource != null)
                _deathAudioSource.Play();
        }

        /// <summary>Old: CellController.PoisonCoroutine -> `DOTween.To(() => _spriteRenderer.material.GetFloat
        /// ("_Blend"), blend => _spriteRenderer.material.SetFloat("_Blend", blend), _health / 100,
        /// k_poisonCoroutineTimeStep);` - the literal `100` was the (also hardcoded) max health; both now read
        /// from CellConfig instead.</summary>
        private void HandleHealthChanged(float health)
        {
            if (_spriteRenderer == null)
                return;

            float targetBlend = health / _config.BaseHealth;

            DOTween.To(
                () => _spriteRenderer.material.GetFloat("_Blend"),
                blend => _spriteRenderer.material.SetFloat("_Blend", blend),
                targetBlend,
                _config.PoisonTickIntervalSeconds
            );
        }

        /// <summary>Old: CellController.SetMacroSprite().</summary>
        public void SetMacroSprite()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = _macroWorldBadSprite;
                _spriteRenderer.material.SetTexture("_SecondaryTex", _macroWorldSprite);
            }

            if (_entrailsRoot != null)
                _entrailsRoot.SetActive(false);
        }

        /// <summary>Old: CellController.SetMicroSprite() (minus the `_inCellVirusManager.SetVirusesCount(...)`
        /// call - see class doc comment for why that's deferred).</summary>
        public void SetMicroSprite()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sprite = _microWorldBadSprite;
                _spriteRenderer.material.SetTexture("_SecondaryTex", _microWorldSprite);
            }

            if (_entrailsRoot != null)
                _entrailsRoot.SetActive(true);

            ShuffleEntrails();
        }

        /// <summary>Old: CellController.ShuffleEntrails().</summary>
        public void ShuffleEntrails()
        {
            if (_entrailsContainer == null)
                return;

            Bounds area = _config.EntrailsArea;

            foreach (Transform entrail in _entrailsContainer)
            {
                var position = new Vector3(
                    Random.Range(area.min.x, area.max.x),
                    Random.Range(area.min.y, area.max.y),
                    0
                );

                entrail.localPosition = position;

                if (entrail.TryGetComponent(out Rigidbody2D rd))
                {
                    rd.linearVelocity = new Vector3(
                        Random.Range(-0.2f, 0.2f),
                        Random.Range(-0.2f, 0.2f),
                        0
                    );
                }
            }
        }
    }
}
