// Скин кота: цвета (для примитивов) и/или спрайты. Открывается, когда игрок дошёл до unlockAtLevel.
// Create → CatTanks → Skin, добавить в GameConfig. Пусто = CreateDefaults().
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    [CreateAssetMenu(menuName = "CatTanks/Skin", fileName = "Skin_")]
    public class SkinDefinition : ScriptableObject
    {
        public string id = "classic";
        public string displayName = "Рыжик";
        public int unlockAtLevel = 1;
        public Color bodyColor = new Color(0.30f, 0.55f, 0.30f);
        public Color trackColor = new Color(0.15f, 0.2f, 0.15f);
        public Color catColor = new Color(1f, 0.65f, 0.25f);
        public Sprite bodySprite;   // пусто = оставить как есть
        public Sprite catSprite;

        static SkinDefinition Make(string id, string name, int unlock, Color body, Color track, Color cat)
        {
            var s = CreateInstance<SkinDefinition>();
            s.name = id; s.id = id; s.displayName = name; s.unlockAtLevel = unlock;
            s.bodyColor = body; s.trackColor = track; s.catColor = cat;
            return s;
        }

        public static List<SkinDefinition> CreateDefaults() => new List<SkinDefinition>
        {
            Make("classic", "Рыжик", 1, new Color(0.30f, 0.55f, 0.30f), new Color(0.15f, 0.2f, 0.15f), new Color(1f, 0.65f, 0.25f)),
            Make("night", "Чёрный кот", 3, new Color(0.20f, 0.22f, 0.30f), new Color(0.08f, 0.08f, 0.1f), new Color(0.12f, 0.12f, 0.14f)),
            Make("snow", "Снежок", 5, new Color(0.75f, 0.85f, 0.95f), new Color(0.4f, 0.45f, 0.55f), new Color(0.97f, 0.97f, 0.97f)),
            Make("gold", "Золотой", 8, new Color(0.85f, 0.65f, 0.15f), new Color(0.45f, 0.3f, 0.05f), new Color(1f, 0.85f, 0.4f)),
        };
    }
}
