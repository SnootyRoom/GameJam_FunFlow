// Описание типа врага: префаб и параметры. Новый враг = новый ассет (Create → CatTanks → Enemy Definition),
// плюс строчка в EnemyType, если генератор должен ставить его сам.
// Параметры отсюда ПЕРЕЗАПИСЫВАЮТ значения на префабе — правь баланс здесь, а не в префабе.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        public EnemyType type;
        [Tooltip("Пусто = заглушка из примитивов")]
        public GameObject prefab;
        [Tooltip("Сколько клеток занимает по стороне (краб — 2). Клетка спавна — левая нижняя")]
        [Min(1)] public int footprint = 1;

        [Header("Здоровье")]
        public int maxHp = 1;
        public float invulnerableAfterHit = 0.05f;

        [Header("Движение (EnemyCrawler)")]
        public float moveSpeed = 2.2f;
        public float aggroRange = 9f;
        public float hearRange = 4f;
        public int contactDamage = 1;

        [Header("Стрельба (EnemyShooter)")]
        public float fireInterval = 2.2f;
        public float bulletSpeed = 6f;
        public int maxBounces = 1;
        public float sightRange = 14f;

        [Header("Заглушка (если нет префаба)")]
        public Color color = Color.red;
        public float size = 0.7f;

        /// <summary>Сдвиг от центра клетки спавна до центра врага.</summary>
        public Vector2 FootprintOffset => Vector2.one * ((footprint - 1) * 0.5f);

        /// <summary>Переносит параметры на компоненты врага (те, что у него есть).</summary>
        public void ApplyTo(GameObject go)
        {
            var hp = go.GetComponentInChildren<Health>();
            if (hp != null)
            {
                hp.team = Team.Enemy;
                hp.maxHp = maxHp;
                hp.invulnerableAfterHit = invulnerableAfterHit;
                hp.ResetHp();
            }

            var crawler = go.GetComponentInChildren<EnemyCrawler>();
            if (crawler != null)
            {
                crawler.speed = moveSpeed;
                crawler.aggroRange = aggroRange;
                crawler.hearRange = hearRange;
                crawler.contactDamage = contactDamage;
            }

            var shooter = go.GetComponentInChildren<EnemyShooter>();
            if (shooter != null)
            {
                shooter.fireInterval = fireInterval;
                shooter.bulletSpeed = bulletSpeed;
                shooter.maxBounces = maxBounces;
                shooter.sightRange = sightRange;
            }
        }
    }
}
