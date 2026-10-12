// Обёртка над вводом: весь код игры спрашивает клавиши только здесь.
// Хочешь геймпад или ребинд — меняешь этот файл, остальное не трогаешь.
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CatTanks
{
    public static class GameInput
    {
        /// <summary>WASD / стрелки: x — влево-вправо, y — вперёд-назад.</summary>
        public static Vector2 Move()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
            return new Vector2(x, y);
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }

        public static Vector2 MouseScreen()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        /// <summary>Курсор в мировых координатах.</summary>
        public static Vector2 MouseWorld()
        {
            var cam = Camera.main;
            return cam != null ? (Vector2)cam.ScreenToWorldPoint(MouseScreen()) : Vector2.zero;
        }

        /// <summary>ЛКМ или пробел — выстрел.</summary>
        public static bool FirePressed() => MouseDown(0) || Key(KeyCode.Space);

        /// <summary>Только пробел (на экранах меню мышь занята кнопками).</summary>
        public static bool FireKeyPressed() => Key(KeyCode.Space);

        /// <summary>ПКМ или Q нажаты в этом кадре — начать прицеливание ульты.</summary>
        public static bool UltimatePressed() => MouseDown(1) || Key(KeyCode.Q);

        /// <summary>ПКМ или Q удерживаются.</summary>
        public static bool UltimateHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current; var k = Keyboard.current;
            return (m != null && m.rightButton.isPressed) || (k != null && k.qKey.isPressed);
#else
            return Input.GetMouseButton(1) || Input.GetKey(KeyCode.Q);
#endif
        }

        /// <summary>R — перезарядка (во время игры).</summary>
        public static bool ReloadPressed() => Key(KeyCode.R);

        public static bool PausePressed() => Key(KeyCode.Escape) || Key(KeyCode.P);
        public static bool ConfirmPressed() => Key(KeyCode.Return) || Key(KeyCode.KeypadEnter);
        /// <summary>R на экране поражения — заново (во время игры R — перезарядка).</summary>
        public static bool RestartPressed() => Key(KeyCode.R);

        public static bool DebugNextLevelPressed() => Key(KeyCode.N);
        public static bool DebugRegeneratePressed() => Key(KeyCode.F5);
        public static bool DebugUltPressed() => Key(KeyCode.U);

        // ------------------------------------------------------------------

        static bool MouseDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            if (m == null) return false;
            return button == 0 ? m.leftButton.wasPressedThisFrame : m.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        static bool Key(KeyCode code)
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return false;
            switch (code)
            {
                case KeyCode.Space: return k.spaceKey.wasPressedThisFrame;
                case KeyCode.Q: return k.qKey.wasPressedThisFrame;
                case KeyCode.Escape: return k.escapeKey.wasPressedThisFrame;
                case KeyCode.P: return k.pKey.wasPressedThisFrame;
                case KeyCode.Return: return k.enterKey.wasPressedThisFrame;
                case KeyCode.KeypadEnter: return k.numpadEnterKey.wasPressedThisFrame;
                case KeyCode.R: return k.rKey.wasPressedThisFrame;
                case KeyCode.N: return k.nKey.wasPressedThisFrame;
                case KeyCode.F5: return k.f5Key.wasPressedThisFrame;
                case KeyCode.U: return k.uKey.wasPressedThisFrame;
                default: return false; // добавь сюда клавишу, если понадобится новая
            }
#else
            return Input.GetKeyDown(code);
#endif
        }
    }
}
