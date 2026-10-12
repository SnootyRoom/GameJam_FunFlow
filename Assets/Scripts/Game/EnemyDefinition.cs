// Описание типа врага — ЕДИНСТВЕННОЕ место, где настраивается враг: вид, здоровье, движение, стрельба, ИИ.
// Ассеты лежат в Assets/Data/Enemies (Create → CatTanks → Enemy Definition), спавнер берёт их из своего списка.
// Сколько врагов и где ставить — это правила уровня, они в LevelConfig (Assets/Data).
// Параметры отсюда ПЕРЕЗАПИСЫВАЮТ значения на префабе и компонентах — правь баланс здесь.
using CatTanks.Level;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        public EnemyType type;
        [Tooltip("Пусто = враг собирается из спрайта ниже (или из примитивов, если и спрайта нет)")]
        public GameObject prefab;
        [Tooltip("Сколько клеток занимает по стороне (краб — 2). Клетка спавна — левая нижняя")]
        [Min(1)] public int footprint = 1;

        [Header("Вид без префаба")]
        [Tooltip("Спрайт врага. Пусто = заглушка из примитивов цвета color")]
        public Sprite sprite;
        [Tooltip("Куда смотрит голова на картинке, в градусах: 0 — вправо, 90 — вверх")]
        public float spriteFacing = 90f;
        [Tooltip("Размер врага в клетках (по большей стороне спрайта)")]
        public float size = 0.7f;
        public Color color = Color.red;

        [Header("Здоровье")]
        public int maxHp = 1;
        public float invulnerableAfterHit = 0.05f;

        [Header("Движение и таран (EnemyCrawler)")]
        public float moveSpeed = 2.2f;
        [Tooltip("Замечает игрока на этой дистанции, если видит его (и сам на экране)")]
        public float aggroRange = 9f;
        [Tooltip("Слышит игрока на этой дистанции даже из-за стены")]
        public float hearRange = 4f;
        [Tooltip("Как часто пересчитывать путь к игроку, сек")]
        public float repathInterval = 0.4f;
        public int contactDamage = 1;
        [Tooltip("Пауза между укусами при контакте, сек")]
        public float contactCooldown = 1f;

        [Header("Стрельба (EnemyShooter)")]
        public float fireInterval = 2.2f;
        public float bulletSpeed = 6f;
        [Tooltip("Сколько рикошетов учитывает при прицеливании и сколько делает снаряд")]
        public int maxBounces = 1;
        public float sightRange = 14f;
        [Tooltip("Разброс выстрела, градусы — чтобы ИИ не был снайпером")]
        public float aimErrorDegrees = 2f;
        [Tooltip("Скорость поворота башни/спрайта, градусы в секунду")]
        public float turnSpeed = 270f;
        public Color bulletColor = new Color(1f, 0.35f, 0.9f);
        [Tooltip("Спрайт снаряда врага. Пусто = кружок цвета bulletColor")]
        public Sprite bulletSprite;

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
                crawler.repathInterval = repathInterval;
                crawler.contactDamage = contactDamage;
                crawler.contactCooldown = contactCooldown;
            }

            var shooter = go.GetComponentInChildren<EnemyShooter>();
            if (shooter != null)
            {
                shooter.fireInterval = fireInterval;
                shooter.bulletSpeed = bulletSpeed;
                shooter.maxBounces = maxBounces;
                shooter.sightRange = sightRange;
                shooter.aimErrorDegrees = aimErrorDegrees;
                shooter.turretTurnSpeed = turnSpeed;
                shooter.bulletColor = bulletColor;
                shooter.bulletSprite = bulletSprite;
            }
        }
    }
}
