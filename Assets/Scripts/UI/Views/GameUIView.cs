// Корень префаба GameCanvas: холст и ссылки на экраны (вложенные префабы Hud, Banner, Pause, MainMenu, Tutorial).
// Порядок детей = порядок отрисовки: HUD снизу, обучение сверху.
using UnityEngine;

namespace CatTanks.UI
{
    public class GameUIView : MonoBehaviour
    {
        public HudView hud;
        public BannerView banner;
        public PauseView pause;
        public MainMenuView menu;
        public TutorialView tutorial;
        public GameOverView gameOver;
    }
}
