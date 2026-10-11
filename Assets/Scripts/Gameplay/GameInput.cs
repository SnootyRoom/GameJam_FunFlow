// Обёртка над вводом: работает и со старым Input Manager, и с новым Input System.
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

        public static bool FirePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
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

        /// <summary>R — новый случайный уровень (для отладки).</summary>
        public static bool RegeneratePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }

        /// <summary>N — сразу следующий уровень (для отладки).</summary>
        public static bool NextLevelPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && k.nKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.N);
#endif
        }
    }
}
