// Что нужно виджетам HUD: игра, текущий уровень, танк игрока и его ульта.
// HudView раздаёт это всем виджетам (IHudWidget) на старте каждого уровня; дальше каждый виджет сам следит за своим.
// Новый элемент HUD = новый компонент с IHudWidget в префабе Hud — ни HudView, ни GameUI править не нужно.

namespace CatTanks.UI
{
    public class HudContext
    {
        public GameFlow Flow;
        public LevelContext Level;          // null — уровня нет (меню): виджеты прячутся / отписываются
        public PlayerTank Tank;             // может быть null, если игрока написали по-своему
        public PlayerUltimate Ultimate;     // может быть null

        public static HudContext For(GameFlow flow, LevelContext level)
        {
            var player = level != null ? level.Player : null;
            return new HudContext
            {
                Flow = flow,
                Level = level,
                Tank = player != null ? player.GetComponent<PlayerTank>() : null,
                Ultimate = player != null ? player.GetComponent<PlayerUltimate>() : null,
            };
        }
    }

    public interface IHudWidget
    {
        /// <summary>Новый уровень (или его нет). Вызывается до первого кадра уровня.</summary>
        void Bind(HudContext hud);
    }
}
