// Линия прицела: показывает, куда полетит основной снаряд, с учётом рикошетов.
// Траекторию считает Ricochet.Simulate — тот же код, что у снаряда и ИИ, поэтому линия всегда честная.
// Вешается рядом с PlayerTank. LineRenderer создаётся сам на дочернем объекте.
using System.Collections.Generic;
using UnityEngine;

namespace CatTanks
{
    public class AimPreview : MonoBehaviour
    {
        public PlayerTank tank;
        [Tooltip("Длина линии в юнитах (по всем отрезкам)")]
        public float maxDistance = 15f;
        public float bulletRadius = 0.12f;
        public float startWidth = 0.08f, endWidth = 0.03f;
        public Color startColor = new Color(1f, 1f, 0.5f, 0.9f);
        public Color endColor = new Color(1f, 0.3f, 0.3f, 0.3f);
        public int sortingOrder = 9;   // под танком, над полом и стенами

        LineRenderer _line;
        readonly List<Vector2> _points = new List<Vector2>();

        void Awake()
        {
            if (tank == null) tank = GetComponent<PlayerTank>();

            var go = new GameObject("AimLine");
            go.transform.SetParent(transform, false);
            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.startWidth = startWidth;
            _line.endWidth = endWidth;
            _line.startColor = startColor;
            _line.endColor = endColor;
            _line.numCapVertices = 0;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.sortingOrder = sortingOrder;
            // материал берём у спрайтов танка — он точно совместим с текущим рендер-пайплайном (URP 2D)
            var sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) _line.sharedMaterial = sr.sharedMaterial;
        }

        void LateUpdate()
        {
            bool show = tank != null && tank.isActiveAndEnabled && Time.timeScale > 0f;
            if (show)
            {
                Ricochet.Simulate(tank.MuzzlePosition, tank.AimDirection, bulletRadius, tank.maxBounces, maxDistance,
                                  tank.transform, null, _points);
                show = _points.Count > 1;
            }

            _line.enabled = show;
            if (!show) return;
            _line.positionCount = _points.Count;
            for (int i = 0; i < _points.Count; i++) _line.SetPosition(i, _points[i]);
        }
    }
}
