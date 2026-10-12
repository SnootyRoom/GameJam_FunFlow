// Тема интерфейса: шрифт, картинки, цвета и тексты, из которых GameUIBuilder собирает префабы UI.
// Нужна только при (пере)сборке префабов: меню CatTanks → UI → Собрать префабы UI.
// После сборки вёрстку правят прямо в префабах (Assets/Prefabs/UI) — тема их не трогает, пока не пересоберёшь.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks.UI
{
    [CreateAssetMenu(menuName = "CatTanks/UI Theme", fileName = "UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Тексты")]
        public string gameTitle = "Cat in a Tank";
        public string controlsHint = "WASD - move   Mouse - aim   LMB - fire   RMB - machine gun   Esc - pause";

        [Header("Шрифт и картинки художника (пусто = примитивы)")]
        public Font font;
        public Sprite logo;                   // вместо текстового заголовка, ~1024×512
        public Sprite menuBackground;         // фон главного меню; заполняет экран, лишнее обрезается
        public Sprite heartFull;              // 48×48
        public Sprite heartEmpty;
        public Sprite exitArrow;              // 48×48, смотрит вправо
        public Sprite crosshair;              // 32×32
        public Sprite panelSprite;            // полоса баннера «Level Complete»

        [Header("Текстовая кнопка (пауза, настройки)")]
        [Tooltip("Плашка кнопки (start_button_1). Уголки из той же картинки подставятся сами")]
        public Sprite buttonSprite;
        public Sprite buttonCornerTopLeft;
        public Sprite buttonCornerBottomRight;

        [Header("Кнопки главного меню (картинки с готовым текстом; пусто = текстовая кнопка)")]
        public Sprite startButton;
        public Sprite settingsButton;
        public Sprite exitButton;

        [Header("Пиксельные заготовки (создаются сборщиком в Assets/Art/UI/Generated)")]
        public Sprite pixelPanel;             // подложка паузы, 9-slice
        public Sprite pixelCursor;            // стрелка у наведённой текстовой кнопки
        public Sprite circle, ring, triangle; // заглушки сердец, прицела и стрелки на выход

        [Header("Цвета")]
        public Color accent = new Color(1f, 0.65f, 0.25f);
        public Color buttonColor = new Color(0.22f, 0.24f, 0.28f, 0.95f);
        public Color textColor = Color.white;
        public Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        [Tooltip("Подложка паузы")]
        public Color menuPanelColor = new Color(1f, 1f, 1f, 0.55f);
        public Color heartColor = new Color(0.95f, 0.3f, 0.35f);

#if UNITY_EDITOR
        // Кнопка нарезана на 3 спрайта. Какой бы из них ни назначили в Button Sprite, раскладываем по размеру:
        // самый большой — плашка, из остальных крайний левый — левый верхний уголок, крайний правый — правый нижний.
        void OnValidate()
        {
            if (buttonSprite == null) return;
            var path = UnityEditor.AssetDatabase.GetAssetPath(buttonSprite);
            var parts = new List<Sprite>();
            foreach (var a in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is Sprite s) parts.Add(s);
            if (parts.Count < 3) return;

            Sprite plate = parts[0];
            foreach (var s in parts) if (s.rect.width * s.rect.height > plate.rect.width * plate.rect.height) plate = s;
            Sprite left = null, right = null;
            foreach (var s in parts)
            {
                if (s == plate) continue;
                if (left == null || s.rect.x < left.rect.x) left = s;
                if (right == null || s.rect.x > right.rect.x) right = s;
            }
            if (buttonSprite == plate && buttonCornerTopLeft == left && buttonCornerBottomRight == right) return;
            buttonSprite = plate;
            buttonCornerTopLeft = left;
            buttonCornerBottomRight = right;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
