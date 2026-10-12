// Движение корпуса на WASD с плавным разгоном и торможением.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerMovement : MonoBehaviour
    {
        public bool tankControls = false;  // true: W/S вперёд-назад, A/D поворот; false: WASD по сторонам света
        public float moveSpeed = 3.5f;
        public float turnSpeed = 180f;
        public float acceleration = 22f;   // юнит/с² — насколько быстро набирает и сбрасывает скорость

        public Vector2 Velocity { get; private set; }
        public float Speed01 => moveSpeed > 0 ? Velocity.magnitude / moveSpeed : 0f;

        Rigidbody2D _rb;
        PlayerTank _tank;
        Vector2 _move;
        float _forward;    // скорость вдоль корпуса в танковом режиме

        void Awake()
        {
            _rb = Prims.AddTopDownBody(gameObject);
            _rb.freezeRotation = true; // столкновения не крутят танк, поворачиваем сами
            _tank = GetComponent<PlayerTank>();
        }

        public void Apply(PlayerStats s, bool tank)
        {
            moveSpeed = s.moveSpeed;
            turnSpeed = s.turnSpeed;
            tankControls = tank;
        }

        void Update()
        {
            _move = _tank == null || _tank.InputEnabled ? GameInput.Move() : Vector2.zero;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (tankControls)
            {
                _rb.rotation = _rb.rotation - _move.x * turnSpeed * dt;
                _forward = Mathf.MoveTowards(_forward, _move.y * moveSpeed, acceleration * dt);
                Velocity = (Vector2)transform.right * _forward;
            }
            else
            {
                Vector2 m = Vector2.ClampMagnitude(_move, 1f);
                Velocity = Vector2.MoveTowards(Velocity, m * moveSpeed, acceleration * dt);
                if (m.sqrMagnitude > 0.01f)
                {
                    // корпус разворачивается кратчайшим путём; ехать задом тоже можно — танк не крутится на 180°
                    float target = Mathf.Atan2(m.y, m.x) * Mathf.Rad2Deg;
                    float back = target + 180f;
                    if (Mathf.Abs(Mathf.DeltaAngle(_rb.rotation, back)) < Mathf.Abs(Mathf.DeltaAngle(_rb.rotation, target))) target = back;
                    _rb.rotation = Mathf.MoveTowardsAngle(_rb.rotation, target, turnSpeed * 2.5f * dt);
                }
            }
            _rb.MovePosition(_rb.position + Velocity * dt);
        }
    }
}
