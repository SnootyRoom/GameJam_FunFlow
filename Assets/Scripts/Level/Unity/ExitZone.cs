// Точка Б: когда игрок въезжает в триггер, уровень пройден.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Collider2D))]
    public class ExitZone : MonoBehaviour
    {
        public bool requireAllEnemiesDead = false; // включи, если выход открывается только после зачистки

        void Reset() { GetComponent<Collider2D>().isTrigger = true; }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerTank>() == null && !other.CompareTag("Player")) return;
            var lb = LevelBuilder.Instance;
            if (lb == null) return;
            if (requireAllEnemiesDead && lb.AliveEnemyCount() > 0) return;
            lb.CompleteLevel();
        }
    }
}
