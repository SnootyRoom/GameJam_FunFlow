// Библиотека эффектов (Create → CatTanks → Fx Library): какая картинка на какое событие, размер и длительность.
// Художник меняет вид эффектов здесь, код не трогает. Проигрывает их FxPlayer.
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Fx Library", fileName = "FxLibrary")]
    public class FxLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Effect
        {
            [Tooltip("Картинка эффекта. Пусто = кружок цвета color")]
            public Sprite sprite;
            public Color color = Color.white;
            [Tooltip("Размер в клетках (по большей стороне)")]
            public float size = 0.6f;
            [Tooltip("Сколько живёт, секунд")]
            public float duration = 0.15f;
            [Tooltip("Во сколько раз вырастает к концу (1 — не растёт)")]
            public float growTo = 1.4f;
            [Tooltip("Повернуть случайно на 0/90/180/270° — чтобы одинаковые эффекты не выглядели копиями")]
            public bool randomRotation = true;
        }

        [Tooltip("Вспышка у ствола при выстреле")]
        public Effect muzzleFlash = new Effect { size = 0.5f, duration = 0.08f, growTo = 1.2f };
        [Tooltip("Искры при рикошете")]
        public Effect ricochet = new Effect { size = 0.5f, duration = 0.15f };
        [Tooltip("Снаряд разбился о стену")]
        public Effect wallHit = new Effect { size = 0.6f, duration = 0.18f };
        [Tooltip("Попадание в танк или врага")]
        public Effect targetHit = new Effect { size = 0.9f, duration = 0.2f, growTo = 1.3f };
        [Tooltip("Враг уничтожен")]
        public Effect enemyDeath = new Effect { size = 1.4f, duration = 0.35f, growTo = 1.6f };
        [Tooltip("Удар ореха (ульта). Размер умножается на радиус удара")]
        public Effect nutImpact = new Effect { size = 1.2f, duration = 0.5f, growTo = 1.8f };

        [Header("Полоска HP над врагом (кроме боссов)")]
        [Tooltip("Рамка enemy_hp_frame. Пусто = полосок нет. Заполняется сама командой CatTanks → UI → Собрать префабы UI")]
        public Sprite enemyHpFrame;
        [Tooltip("Заливка enemy_hp_fill — сжимается по оставшемуся здоровью")]
        public Sprite enemyHpFill;
        [Tooltip("Ширина полоски в клетках")]
        public float enemyHpWidth = 0.75f;
        [Tooltip("Насколько выше центра врага (в долях его размера + запас)")]
        public float enemyHpOffset = 0.2f;
        [Tooltip("Показывать только после первого попадания")]
        public bool enemyHpOnlyWhenHurt = true;
    }
}
