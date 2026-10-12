// Единая точка доступа к главным системам. Системы сами регистрируются здесь в Awake/OnEnable.
// Пример: Game.Player.transform.position, Game.Level.Data, Game.Config.friendlyFire.
using UnityEngine;

namespace CatTanks
{
    public static class Game
    {
        public static GameManager Manager { get; internal set; }
        public static LevelBuilder Level { get; internal set; }
        public static PlayerTank Player { get; internal set; }

        public static GameConfig Config => Manager != null ? Manager.Config : GameConfig.Default;
        public static GameState State => Manager != null ? Manager.State : GameState.Playing;

        /// <summary>Идёт активная игра (не пауза, не экран победы/поражения). Без GameManager — всегда true, удобно для тестовых сцен.</summary>
        public static bool IsPlaying => State == GameState.Playing;

        /// <summary>
        /// Можно читать игровой ввод: идёт игра и состояние сменилось не в этом кадре
        /// (клик по кнопке «Продолжить» не должен тут же стрелять).
        /// </summary>
        public static bool InputReady => IsPlaying && (Manager == null || Time.frameCount > Manager.StateChangedFrame);

        /// <summary>Открыто модальное окно UI (настройки, скины) — Esc/Enter обрабатывает оно, а не игра.</summary>
        public static bool ModalOpen { get; set; }

        /// <summary>Корень текущего уровня — сюда кладём всё, что должно исчезнуть при перезагрузке уровня.</summary>
        public static Transform LevelRoot => Level != null ? Level.Root : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Manager = null; Level = null; Player = null; ModalOpen = false; }
    }
}
