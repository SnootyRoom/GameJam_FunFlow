// Меню паузы. Лежит на корне префаба Pause. Что делают кнопки — назначает GameUI.
using UnityEngine;

namespace CatTanks.UI
{
    public class PauseView : MonoBehaviour
    {
        public PixelButton resume;
        public PixelButton restart;
        public PixelButton mainMenu;
    }
}
