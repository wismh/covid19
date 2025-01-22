using DG.Tweening;
using Project.Core.AudioSystem;
using UnityEngine;
using Zenject;
using Random = UnityEngine.Random;

namespace _Project.Mechanics.Cells
{
    /// <summary>Ports the sprite/shader/entrails half of the old cell controller plus its death animation/audio.</summary>
    public sealed class CellVisuals : MonoBehaviour
    {
        [SerializeField] private CellHealth _health;

        [SerializeField] private SpriteRenderer _spriteRenderer;

        [SerializeField] private GameObject _entrailsRoot;

        [SerializeField] private Transform _entrailsContainer;

        [SerializeField] private Texture _macroWorldSprite;
        [SerializeField] private Sprite _macroWorldBadSprite;
        [SerializeField] private Texture _microWorldSprite;
        [SerializeField] private Sprite _microWorldBadSprite;

        [SerializeField] private SoundData _deathSound;

        private Animator _animator;
        private CellConfig _config;
        private IAudioSystem _audioSystem;

        [Inject]
        public void Construct(CellConfig config, IAudioSystem audioSystem)
        {
            _config = config;
            _audioSystem = audioSystem;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            _spriteRenderer.material.SetFloat("_Blend", 1f);
            _entrailsRoot.SetActive(false);

            // Kept off except during the death animation - otherwise the "Idle (Empty)" state keeps
            // animating and fights SetMacroSprite/SetMicroSprite for control of the same SpriteRenderer.
            if (_animator != null)
                _animator.enabled = false;

            _health.OnHealthChanged += HandleHealthChanged;
            _health.OnDied += HandleDied;
        }

        private void OnDestroy()
        {
            _health.OnHealthChanged -= HandleHealthChanged;
            _health.OnDied -= HandleDied;
        }

        private void HandleDied(int virusCountAtDeath)
        {
            if (_animator != null)
            {
                _animator.enabled = true;
                _animator.Play("Base Layer.Dead");
            }

            _audioSystem.PlaySfx(_deathSound);
        }

        private void HandleHealthChanged(float health)
        {
            float targetBlend = health / _config.BaseHealth;

            DOTween.To(
                () => _spriteRenderer.material.GetFloat("_Blend"),
                blend => _spriteRenderer.material.SetFloat("_Blend", blend),
                targetBlend,
                _config.PoisonTickIntervalSeconds
            );
        }

        public void SetMacroSprite()
        {
            _spriteRenderer.sprite = _macroWorldBadSprite;
            _spriteRenderer.material.SetTexture("_SecondaryTex", _macroWorldSprite);

            _entrailsRoot.SetActive(false);
        }

        public void SetMicroSprite()
        {
            _spriteRenderer.sprite = _microWorldBadSprite;
            _spriteRenderer.material.SetTexture("_SecondaryTex", _microWorldSprite);

            _entrailsRoot.SetActive(true);

            ShuffleEntrails();
        }

        public void ShuffleEntrails()
        {
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
