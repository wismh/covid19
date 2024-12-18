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
