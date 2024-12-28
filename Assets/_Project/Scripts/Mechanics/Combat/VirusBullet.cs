using UnityEngine;

namespace _Project.Mechanics.Combat
{
    /// <summary>
    /// Enemy virus projectile, fired by CucumberVirus.
    /// Replaces legacy GMTK.VirusBullet (Assets/GMTK/Scripts/Behaviors/MicroVirus/Bullet/VirusBullet.cs).
    /// Inflicts damage on any GMTK.IDamageable (typically PlayerHealth).
    /// </summary>
    public class VirusBullet : MonoBehaviour
    {
        [SerializeField] private float _speed = 4f;
        [SerializeField] private float _bulletLifetime = 5f;
        [SerializeField] private int _damage = 1;

        /// <summary>Overrides the prefab-default speed/lifetime/damage with MicroVirusConfig's values - see
        /// IVirusBulletSpawner.SpawnBullet's doc comment. Left uncalled, the bullet still works using its own
        /// [SerializeField] defaults (kept as a safety net, not a redundant duplicate).</summary>
        public void Construct(float speed, float bulletLifetime, int damage)
        {
            _speed = speed;
            _bulletLifetime = bulletLifetime;
            _damage = damage;
        }

        private void Start()
        {
            Destroy(gameObject, _bulletLifetime);
        }

        private void Update()
        {
            Vector3 velocity = Time.deltaTime * _speed * transform.right;
            transform.position += velocity;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.TryGetComponent(out IDamageable damageable))
            {
                damageable.Damage(_damage, transform.right);
                Destroy(gameObject);
            }
        }
    }
}
