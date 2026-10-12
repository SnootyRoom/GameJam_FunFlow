// Камера: следует за игроком, не выходит за края арены и подстраивает зум под разрешение экрана.
// Зум пересчитывается каждый кадр, поэтому смена разрешения или правка в инспекторе применяются сразу.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour, ILevelActor
    {
        public enum ZoomMode
        {
            CellsHigh,     // по высоте всегда видно cellsHigh клеток, на любом экране одинаково по геймплею
            PixelPerfect,  // клетка = ровно pixelsPerCell пикселей экрана; чем больше монитор, тем больше видно
            FitLevel,      // вся арена целиком помещается на экран при любом соотношении сторон
        }

        [Header("Зум")]
        public ZoomMode zoom = ZoomMode.CellsHigh;
        [Tooltip("CellsHigh: сколько клеток видно по высоте")]
        public float cellsHigh = 16f;
        [Tooltip("PixelPerfect: пикселей экрана на клетку (= PPU спрайтов)")]
        public int pixelsPerCell = 64;
        [Tooltip("FitLevel: запас вокруг арены в клетках")]
        public float fitPadding = 0.5f;

        [Header("Слежение")]
        public float smooth = 8f;
        public bool clampToLevel = true;
        public Color background = new Color(0.16f, 0.17f, 0.19f);

        Camera _cam;
        Vector3 _base;                       // положение без тряски

        static float _shakeStrength, _shakeUntil, _shakeDuration;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _shakeStrength = _shakeUntil = _shakeDuration = 0f; }

        /// <summary>Тряхнуть камеру (взрыв ореха и т.п.). strength — амплитуда в клетках. Сильнее перебивает слабее.</summary>
        public static void Shake(float strength, float duration)
        {
            if (Time.unscaledTime < _shakeUntil && strength < _shakeStrength) return;
            _shakeStrength = strength;
            _shakeDuration = Mathf.Max(0.01f, duration);
            _shakeUntil = Time.unscaledTime + duration;
        }

        static Vector3 ShakeOffset()
        {
            float left = _shakeUntil - Time.unscaledTime;
            if (left <= 0f) return Vector3.zero;
            float k = _shakeStrength * left / _shakeDuration; // затухает к концу
            return new Vector3(Random.Range(-k, k), Random.Range(-k, k), 0f);
        }

        LevelContext _ctx;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.backgroundColor = background;
            ApplyZoom();
        }

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            ApplyZoom();
            if (_ctx.Player != null) _base = Clamp(_ctx.Player.transform.position); // сразу к игроку
            else _base = transform.position;
            transform.position = _base;
            PublishView();
        }

        void LateUpdate()
        {
            ApplyZoom(); // экран могли развернуть, сменить разрешение или поменять настройки
            if (_ctx == null) return;
            Vector3 target = _ctx.Player != null && zoom != ZoomMode.FitLevel
                ? Clamp(_ctx.Player.transform.position)
                : LevelCenter();
            _base = Vector3.Lerp(_base, target, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
            transform.position = _base + ShakeOffset();
            PublishView();
        }

        /// <summary>Сообщаем уровню, что видно на экране — враги не стреляют из-за его края.</summary>
        void PublishView()
        {
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            Vector2 c = transform.position;
            _ctx.View = new Rect(c.x - w, c.y - h, 2f * w, 2f * h);
            _ctx.HasView = true;
        }

        void ApplyZoom()
        {
            float size;
            switch (zoom)
            {
                case ZoomMode.PixelPerfect:
                    size = Screen.height / (2f * Mathf.Max(1, pixelsPerCell));
                    break;
                case ZoomMode.FitLevel:
                    if (_ctx == null || _ctx.Data == null) { size = cellsHigh / 2f; break; }
                    float halfW = _ctx.Data.width / 2f + fitPadding, halfH = _ctx.Data.height / 2f + fitPadding;
                    size = Mathf.Max(halfH, halfW / Mathf.Max(0.01f, _cam.aspect));
                    break;
                default:
                    size = cellsHigh / 2f;
                    break;
            }
            _cam.orthographicSize = Mathf.Max(1f, size);
        }

        Vector3 LevelCenter()
        {
            if (_ctx == null || _ctx.Data == null) return transform.position;
            return new Vector3(_ctx.Data.width / 2f, _ctx.Data.height / 2f, -10f);
        }

        Vector3 Clamp(Vector3 p)
        {
            p.z = -10f;
            if (!clampToLevel || _ctx == null || _ctx.Data == null) return p;
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            float W = _ctx.Data.width, H = _ctx.Data.height;
            p.x = W <= 2 * w ? W / 2f : Mathf.Clamp(p.x, w, W - w);
            p.y = H <= 2 * h ? H / 2f : Mathf.Clamp(p.y, h, H - h);
            return p;
        }
    }
}
