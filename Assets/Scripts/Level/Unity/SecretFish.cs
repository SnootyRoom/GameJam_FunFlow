// Секретная рыбка: бонусная валюта для прокачки. Генератор прячет её подальше от основного маршрута.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Collider2D))]
    public class SecretFish : MonoBehaviour
    {
        public int value = 2;
        bool _taken;
        Vector3 _baseScale;

        void Reset() { GetComponent<Collider2D>().isTrigger = true; }
        void Awake() { _baseScale = transform.localScale; }

        void Update()
        {
            transform.localScale = _baseScale * (1f + 0.12f * Mathf.Sin(Time.time * 5f)); // «дышит», чтобы заметили
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (_taken || !Game.IsPlaying || other.GetComponentInParent<PlayerTank>() == null) return;
            _taken = true;
            if (Game.Manager != null) Game.Manager.CollectSecret(this);
            Fx.Pulse(transform.position, 1f, new Color(0.3f, 0.8f, 1f, 0.7f), 0.25f);
            Destroy(gameObject);
        }
    }
}
