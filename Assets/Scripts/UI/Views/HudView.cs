// HUD в бою. Лежит на корне префаба Hud. Сам почти ничего не рисует: элементы — отдельные виджеты (IHudWidget)
// внутри префаба (сердца, уровень, ульта, боезапас, пулемёт, звёзды, стрелка, прицел), каждый следит за своим.
// HudView раздаёт им контекст уровня и показывает всплывающие подсказки.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class HudView : MonoBehaviour
    {
        public Text toast;

        IHudWidget[] _widgets;
        float _toastUntil;

        /// <summary>Новый уровень (level = null — уровня нет): раздать всем виджетам.</summary>
        public void Bind(GameFlow flow, LevelContext level)
        {
            if (_widgets == null) _widgets = GetComponentsInChildren<IHudWidget>(true);
            var ctx = HudContext.For(flow, level);
            foreach (var w in _widgets) w.Bind(ctx);
        }

        public void ShowToast(string text, float seconds)
        {
            if (toast == null) return;
            toast.text = text;
            _toastUntil = Time.unscaledTime + seconds;
            toast.gameObject.SetActive(true);
        }

        void Update()
        {
            if (toast != null && toast.gameObject.activeSelf && Time.unscaledTime >= _toastUntil) toast.gameObject.SetActive(false);
        }
    }
}
