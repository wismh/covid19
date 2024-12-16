using GMTK.GameStates;
using GMTK.Services;
using System.Collections;
using UnityEngine;

namespace GMTK
{
    public class Bullet : MonoBehaviour
    {
        [SerializeField] private float _speed;
        [SerializeField] private float _bulletLifetime;
        [SerializeField] private int _damage;

        private BulletManager _bulletManager;
        private GamePlayState _gamePlayState;
        private PlayerStats _playerStats;

        private void Update()
        {
            // Phase 9 testing fix (2026-09-12): scale used to be set only once, in OnEnable() below, from
            // whatever GamePlayState.State read at that instant. User-reported symptom: the first shot fired in
            // the same frame the player finishes entering a cell still flies at full macro size despite already
            // being inside - a one-frame race between this bullet's OnEnable() (pooled bullets re-enable via
            // BulletManager.SpawnBullet -> ObjectPool.Get(), which can run from PlayerTurret.Update()) and
            // WorldScaleController's transition completion callback (which drives GamePlayState.SyncWorldState()
            // via MainSceneComposition - see that class's "Legacy state bridge" wiring) - Unity doesn't guarantee
            // which of two different MonoBehaviours' per-frame callbacks runs first, and a DOTween OnComplete
            // firing after this Update() in the same frame left the just-spawned bullet's scale stuck at
            // whatever OnEnable() guessed. Re-checking (and self-correcting) scale every frame here means a
            // bullet can be wrong for at most the one frame it was actually racing on, instead of for its whole
            // flight.
            Vector3 targetScale = _gamePlayState.State == WorldState.MICRO_WORLD ? new Vector3(0.3f, 0.3f, 1f) : Vector3.one;
            if (transform.localScale != targetScale)
                transform.localScale = targetScale;

            var velocity = Time.deltaTime * _speed * transform.right;
            if (_gamePlayState.State == WorldState.MICRO_WORLD)
                velocity *= 0.3f;
            transform.position += velocity;
        }

        private void OnEnable()
        {
            _bulletManager ??= ServiceLocator.Instance.Get<BulletManager>();
            _gamePlayState ??= ServiceLocator.Instance.Get<GamePlayState>();
            _playerStats ??= ServiceLocator.Instance.Get<PlayerStats>();

            StartCoroutine(BulletDestroyByDelay());

            if (_gamePlayState.State == WorldState.MICRO_WORLD) transform.localScale = new(0.3f, 0.3f, 1);
            if (_gamePlayState.State == WorldState.MACRO_WORLD) transform.localScale = new(1f, 1f, 1);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                var damage = _damage * (_playerStats.GetLevelUpgrade(UI.UpgradeType.PLAYER_DAMAGE) / 2);
                damage = damage > 1 ? damage : 1;

                damageable.Damage(damage, transform.right);
                _bulletManager.DestroyBullet(transform);
            }
        }

        private IEnumerator BulletDestroyByDelay()
        {
            yield return new WaitForSeconds(_bulletLifetime);
            _bulletManager.DestroyBullet(transform);
        }
    }
}