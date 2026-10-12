// Укус при касании: раз в cooldown наносит урон всему чужой команды, во что упёрся.
using UnityEngine;

namespace CatTanks
{
    public class ContactDamage : EnemyBehaviour
    {
        public float cooldown = 1f;
        int _damage = 1;
        float _nextHit;

        protected override void OnSpawned() { _damage = Def.contactDamage; }

        void OnCollisionStay2D(Collision2D c)
        {
            if (!Active || _damage <= 0 || Time.time < _nextHit) return;
            var target = c.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.Team == Team.Enemy) return;
            Vector2 point = c.contactCount > 0 ? c.GetContact(0).point : (Vector2)transform.position;
            if (target.ApplyDamage(new DamageInfo(_damage, gameObject, Team.Enemy, DamageKind.Contact, point,
                                                  (point - (Vector2)transform.position).normalized)))
                _nextHit = Time.time + cooldown;
        }
    }
}
