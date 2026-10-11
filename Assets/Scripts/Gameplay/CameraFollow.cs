// Камера плавно следует за игроком и не выходит за края арены. Уровень передаёт GameFlow через Init.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour, ILevelActor
    {
        public float orthographicSize = 8f; // половина высоты видимой области в клетках
        public float smooth = 8f;
        public bool clampToLevel = true;
        public Color background = new Color(0.16f, 0.17f, 0.19f);

        Camera _cam;
        LevelContext _ctx;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = orthographicSize;
            _cam.backgroundColor = background;
        }

        public void Init(LevelContext ctx)
        {
            _ctx = ctx;
            if (_ctx.Player != null) transform.position = Clamp(_ctx.Player.transform.position); // сразу к игроку
        }

        void LateUpdate()
        {
            if (_ctx == null || _ctx.Player == null) return;
            Vector3 target = Clamp(_ctx.Player.transform.position);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.unscaledDeltaTime));
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
