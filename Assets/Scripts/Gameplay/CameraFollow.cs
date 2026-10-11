// Камера плавно следует за игроком и не выходит за края арены.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Camera))]
    public class CameraFollow : MonoBehaviour
    {
        public float orthographicSize = 8f; // половина высоты видимой области в клетках
        public float smooth = 8f;
        public bool clampToLevel = true;

        Camera _cam;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = orthographicSize;
        }

        /// <summary>Мгновенно перескочить к игроку (после генерации уровня).</summary>
        public void Snap()
        {
            var lb = LevelBuilder.Instance;
            if (lb == null || lb.Player == null) return;
            transform.position = Clamp(lb.Player.transform.position);
        }

        void LateUpdate()
        {
            var lb = LevelBuilder.Instance;
            if (lb == null || lb.Player == null) return;
            Vector3 target = Clamp(lb.Player.transform.position);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }

        Vector3 Clamp(Vector3 p)
        {
            p.z = -10f;
            var lb = LevelBuilder.Instance;
            if (!clampToLevel || lb == null || lb.Data == null) return p;
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            float W = lb.Data.width, H = lb.Data.height;
            p.x = W <= 2 * w ? W / 2f : Mathf.Clamp(p.x, w, W - w);
            p.y = H <= 2 * h ? H / 2f : Mathf.Clamp(p.y, h, H - h);
            return p;
        }
    }
}
