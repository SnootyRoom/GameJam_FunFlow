// Отдача башни: при выстреле владельца башня откатывается назад вдоль ствола и пружинит обратно.
// Только визуально — точка вылета снаряда считается от места покоя (PlayerTank.MuzzlePosition).
// Слушает Bullet.Fired: основной выстрел (с рикошетами) — сильный откат, пулемёт (без рикошетов) — слабый.
using UnityEngine;

namespace CatTanks
{
    public class TurretRecoil : MonoBehaviour
    {
        public Transform turret;
        [Tooltip("Откат при основном выстреле, юниты")]
        public float distance = 0.12f;
        [Tooltip("Откат при выстреле пулемёта, юниты")]
        public float minigunDistance = 0.04f;
        [Tooltip("Как быстро башня возвращается на место")]
        public float returnSpeed = 14f;

        Vector3 _rest;
        Transform _restOf;                   // для какой башни запомнено место покоя
        float _offset;

        /// <summary>Есть ли запомненное место покоя (башня найдена).</summary>
        public bool HasRest => EnsureTurret();
        /// <summary>Положение башни в покое (в координатах родителя) — без отката.</summary>
        public Vector3 RestLocalPosition => _rest;

        // башню могут назначить позже, чем добавлен компонент (заглушка игрока) — ищем при первой надобности
        bool EnsureTurret()
        {
            if (turret == null) { var tank = GetComponent<PlayerTank>(); if (tank != null) turret = tank.turret; }
            if (turret == null) return false;
            if (_restOf != turret) { _restOf = turret; _rest = turret.localPosition; _offset = 0f; }
            return true;
        }

        void OnEnable() { Bullet.Fired += OnFired; }
        void OnDisable() { Bullet.Fired -= OnFired; }

        void OnFired(Bullet b)
        {
            if (b.Owner != gameObject) return;
            _offset = Mathf.Max(_offset, b.maxBounces > 0 ? distance : minigunDistance);
        }

        void LateUpdate()
        {
            if (!EnsureTurret()) return;
            _offset = Mathf.MoveTowards(_offset, 0f, (_offset * returnSpeed + 0.01f) * Time.deltaTime);
            turret.localPosition = _rest;
            if (_offset > 0.0001f) turret.position -= turret.right * _offset;
        }
    }
}
