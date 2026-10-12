// Прицел под курсором (сам курсор в бою скрыт). Пока выстрел не готов — меньше и прозрачнее,
// в момент готовности «щёлкает». Виден только во время игры.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class CrosshairWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform crosshair;
        [Range(0f, 1f)] public float notReadyAlpha = 0.35f;
        [Range(0.3f, 1f)] public float notReadyScale = 0.75f;

        GameFlow _flow;
        PlayerTank _tank;
        Image _image;
        Color _color;
        bool _ready = true;
        float _pop;

        public void Bind(HudContext hud)
        {
            _flow = hud.Flow;
            _tank = hud.Tank;
            _ready = true;
        }

        void Awake()
        {
            _image = crosshair.GetComponent<Image>();
            if (_image != null) _color = _image.color;
        }

        void Update()
        {
            bool show = _flow != null && _flow.State == GameState.Playing;
            if (crosshair.gameObject.activeSelf != show) crosshair.gameObject.SetActive(show);
            if (!show) return;

            var area = (RectTransform)crosshair.parent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, GameInput.MouseScreen(), null, out var local))
                crosshair.anchoredPosition = local;

            bool ready = _tank == null || _tank.CanFire;
            if (ready && !_ready) _pop = 1f; // выстрел снова готов — прицел «щёлкает»
            _ready = ready;
            _pop = Mathf.MoveTowards(_pop, 0f, Time.unscaledDeltaTime * 5f);
            crosshair.localScale = Vector3.one * ((ready ? 1f : notReadyScale) + 0.3f * _pop);
            if (_image != null)
            {
                var c = _color;
                c.a *= ready ? 1f : notReadyAlpha;
                _image.color = c;
            }
        }
    }
}
