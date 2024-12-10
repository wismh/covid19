using UnityEngine;

namespace GMTK
{
    public class VirusBullet : MonoBehaviour
    {
        [SerializeField] private float _speed;
        [SerializeField] private float _bulletLifetime;
        [SerializeField] private int _damage;

        private void Start()
        {
            Destroy(gameObject, _bulletLifetime);
        }

        private void Update()
        {
            var velocity = Time.deltaTime * _speed * transform.right;
            transform.position += velocity;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Phase 7 testing fix (2026-09-11): was PlayerDamageable specifically - that component was removed
            // from Player.prefab (it was shadowing the new PlayerHealth via the same IDamageable lookup GMTK.
            // Bullet.cs uses; see that removal's commit message), which silently broke Cucumber's ranged attack
            // entirely (this bullet could never find its target anymore). IDamageable is the generic interface
            // both PlayerHealth and this old code already agree on elsewhere (GMTK.Bullet, InCellVirusController.
            // OnCollisionStay2D) - matches that convention instead of the now-gone concrete type.
            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                damageable.Damage(_damage, transform.right);
                Destroy(gameObject);
            }
        }
    }
}