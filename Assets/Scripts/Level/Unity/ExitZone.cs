// Точка Б. Только сообщает, что игрок въехал; что делать дальше, решает GameFlow.
using System;
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Collider2D))]
    public class ExitZone : MonoBehaviour
    {
        public event Action PlayerEntered;

        void Reset() { GetComponent<Collider2D>().isTrigger = true; }

        void OnTriggerEnter2D(Collider2D other)
        {
            var hp = other.GetComponentInParent<Health>();
            if (hp == null || !hp.IsPlayer) return; // игрок = объект с Health команды Player, какой бы скрипт им ни управлял
            PlayerEntered?.Invoke();
        }
    }
}
