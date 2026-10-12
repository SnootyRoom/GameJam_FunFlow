// Звёзды уровня прямо сейчас (серая — можно получить, золотая с прыжком — получена, тёмная с дрожью — потеряна)
// и счёт звёзд за забег.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class StarsWidget : MonoBehaviour, IHudWidget
    {
        public Image[] stars;
        public Text total;
        public string totalFormat = "Stars: {0}";
        public Color starOn = new Color32(255, 196, 56, 255);
        public Color starPossible = new Color(1f, 1f, 1f, 0.6f);
        public Color starOff = new Color(0.35f, 0.3f, 0.4f, 0.75f);

        GameFlow _flow;
        LevelStars _level;
        StarState[] _states;
        float[] _anim;                        // >0 — прыжок (заработана), <0 — дрожь (потеряна)
        int _shownTotal = -1;

        public void Bind(HudContext hud)
        {
            _flow = hud.Flow;
            _level = hud.Level != null ? hud.Level.Stars : null;
            _states = new StarState[stars.Length];  // новый уровень — без анимации смены состояний
            _anim = new float[stars.Length];
            foreach (var s in stars) { s.rectTransform.localScale = Vector3.one; s.rectTransform.localRotation = Quaternion.identity; }
        }

        void Update()
        {
            if (_flow != null && total != null && _flow.TotalStars != _shownTotal)
            {
                _shownTotal = _flow.TotalStars;
                total.text = string.Format(totalFormat, _shownTotal);
            }
            if (_states == null) return;

            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < stars.Length; i++)
            {
                bool used = _level != null && i < _level.Rules.Length;
                if (stars[i].gameObject.activeSelf != used) stars[i].gameObject.SetActive(used);
                if (!used) continue;

                var s = _level.State(i);
                if (s != _states[i]) _anim[i] = s == StarState.Earned ? 1f : s == StarState.Lost ? -1f : 0f;
                _states[i] = s;
                stars[i].color = s == StarState.Earned ? starOn : s == StarState.Lost ? starOff : starPossible;

                float a = _anim[i];
                var rt = stars[i].rectTransform;
                rt.localScale = Vector3.one * (a > 0f ? 1f + 0.6f * a : 1f);
                rt.localRotation = Quaternion.Euler(0, 0, a < 0f ? Mathf.Sin(a * 40f) * 25f * -a : 0f);
                _anim[i] = Mathf.MoveTowards(a, 0f, dt * 2.5f);
            }
        }
    }
}
