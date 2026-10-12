// Корень префаба GameCanvas: холст и ссылки на экраны (вложенные префабы Hud, Banner, Pause, MainMenu).
// Порядок детей = порядок отрисовки: HUD снизу, главное меню сверху.
using UnityEngine;

namespace CatTanks.UI
{
    public class GameUIView : MonoBehaviour
    {
        public HudView hud;
        public BannerView banner;
        public PauseView pause;
        public MainMenuView menu;
    }
}
