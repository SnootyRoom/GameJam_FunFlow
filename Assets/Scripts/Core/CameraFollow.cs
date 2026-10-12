// Камера: плавно следует за игроком, заглядывает в сторону прицела, не выходит за края арены,
// умеет трястись (CameraFollow.Shake), толкаться (Kick) и «пульсировать» зумом (Punch).
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public float orthographicSize = 8f; // половина высоты видимой области в клетках
        public float smooth = 7f;
        public bool clampToLevel = true;

        [Header("Заглядывание к прицелу")]
        public float lookAhead = 0.22f;      // доля расстояния до курсора
        public float maxLookAhead = 3f;

        [Header("Тряска")]
        public float maxShakeOffset = 0.45f;
        public float maxShakeAngle = 2.5f;
        public float traumaDecay = 1.6f;

        public static bool ShakeEnabled = true;

        static CameraFollow _main;
        Camera _cam;
        Vector3 _base;          // позиция без тряски
        float _trauma, _zoomPunch;
        Vector2 _kick;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = orthographicSize;
            _main = this;
            _base = transform.position;
        }

        void OnDestroy() { if (_main == this) _main = null; }

        /// <summary>Повесить CameraFollow на Main Camera (если ещё нет) и мгновенно перескочить к игроку.</summary>
        public static void SnapMain()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            if (Game.Level != null) cam.backgroundColor = Prims.Dark(Game.Level.wallColor, 0.45f);
            follow.Snap();
        }

        /// <summary>Тряска: 0.2 — выстрел/попадание, 0.5 — урон, 1 — ульта. Складывается и сама затухает.</summary>
        public static void Shake(float trauma)
        {
            if (_main != null && ShakeEnabled) _main._trauma = Mathf.Clamp01(_main._trauma + trauma);
        }

        /// <summary>Короткий толчок камеры в направлении (отдача).</summary>
        public static void Kick(Vector2 dir, float strength)
        {
            if (_main != null && ShakeEnabled) _main._kick += dir.normalized * strength;
        }

        /// <summary>Зум-пульс: камера на миг приближается (amount — доля размера).</summary>
        public static void Punch(float amount)
        {
            if (_main != null && ShakeEnabled) _main._zoomPunch = Mathf.Max(_main._zoomPunch, amount);
        }

        public void Snap()
        {
            if (Game.Player == null) return;
            _base = Clamp(Game.Player.transform.position);
            transform.position = _base;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (Game.Player != null && Game.Player.isActiveAndEnabled)
            {
                Vector2 p = Game.Player.transform.position;
                if (Game.IsPlaying)
                {
                    Vector2 toMouse = GameInput.MouseWorld() - p;
                    p += Vector2.ClampMagnitude(toMouse * lookAhead, maxLookAhead);
                }
                _base = Vector3.Lerp(_base, Clamp(p), 1f - Mathf.Exp(-smooth * dt));
            }

            // тряска по шуму Перлина — плавнее, чем случайные рывки
            float shake = _trauma * _trauma;
            float tt = Time.unscaledTime * 30f;
            Vector3 offset = new Vector3(Mathf.PerlinNoise(tt, 0.3f) * 2f - 1f, Mathf.PerlinNoise(0.7f, tt) * 2f - 1f) * (shake * maxShakeOffset);
            float angle = (Mathf.PerlinNoise(tt, tt) * 2f - 1f) * shake * maxShakeAngle;
            _trauma = Mathf.Max(0f, _trauma - traumaDecay * dt);

            _kick = Vector2.Lerp(_kick, Vector2.zero, 1f - Mathf.Exp(-14f * dt));
            offset += (Vector3)_kick;

            transform.position = _base + offset;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            _zoomPunch = Mathf.Lerp(_zoomPunch, 0f, 1f - Mathf.Exp(-8f * dt));
            _cam.orthographicSize = orthographicSize * (1f - _zoomPunch);
        }

        Vector3 Clamp(Vector3 p)
        {
            p.z = -10f;
            var data = Game.Level != null ? Game.Level.Data : null;
            if (!clampToLevel || data == null || _cam == null) return p;
            float h = orthographicSize, w = h * _cam.aspect;
            float W = data.width, H = data.height;
            p.x = W <= 2 * w ? W / 2f : Mathf.Clamp(p.x, w, W - w);
            p.y = H <= 2 * h ? H / 2f : Mathf.Clamp(p.y, h, H - h);
            return p;
        }
    }
}
