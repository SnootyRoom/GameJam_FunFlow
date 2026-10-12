// Лут с врагов: рыбки (валюта прокачки) и сердечки (+1 HP). Выпрыгивают из врага, ложатся на пол,
// а когда кот рядом — сами притягиваются к нему (магнит). Через lifetime исчезают (последние 3 с мигают).
// LootSystem решает, что выпадет; здесь — только поведение подбираемого предмета.
using UnityEngine;

namespace CatTanks
{
    public enum PickupType { Fish, Heart }

    public class Pickup : MonoBehaviour
    {
        public PickupType type;
        public int value = 1;
        public float lifetime = 14f;
        public float magnetRadius = 2.8f;

        Vector2 _vel;
        float _age;
        bool _magnet;
        SpriteRenderer[] _srs;
        Vector3 _baseScale;

        public static Pickup Spawn(PickupType type, Vector2 pos, int value, Vector2 kick)
        {
            var go = new GameObject(type == PickupType.Fish ? "FishPickup" : "HeartPickup");
            go.transform.SetParent(Game.LevelRoot, false);
            go.transform.position = pos;
            var color = type == PickupType.Fish ? new Color(1f, 0.8f, 0.25f) : new Color(1f, 0.3f, 0.35f);
            Prims.Child("Glow", go.transform, Prims.Glow, Prims.WithAlpha(color, 0.45f), Vector2.zero, Vector2.one * 1.1f, 14);
            Prims.Child("Icon", go.transform, type == PickupType.Fish ? Prims.Fish : Prims.Heart, color, Vector2.zero,
                        Vector2.one * (type == PickupType.Fish ? 0.42f + 0.06f * (value - 1) : 0.46f), 15);
            var p = go.AddComponent<Pickup>();
            p.type = type;
            p.value = value;
            p._vel = kick;
            return p;
        }

        void Awake()
        {
            _srs = GetComponentsInChildren<SpriteRenderer>();
            _baseScale = transform.localScale;
        }

        void Update()
        {
            if (!Game.IsPlaying) return;
            float dt = Time.deltaTime;
            _age += dt;
            if (_age > lifetime) { Destroy(gameObject); return; }

            Vector2 pos = transform.position;
            var player = Game.Player;
            if (player != null && player.isActiveAndEnabled && _age > 0.35f)
            {
                Vector2 to = (Vector2)player.transform.position - pos;
                float d = to.magnitude;
                if (d < 0.55f) { Collect(player); return; }
                if (d < magnetRadius) _magnet = true;
                if (_magnet) _vel = Vector2.MoveTowards(_vel, to.normalized * 14f, 40f * dt);
            }
            if (!_magnet) _vel *= Mathf.Exp(-5f * dt);
            transform.position = pos + _vel * dt;

            // покачивание и мигание перед исчезновением
            transform.localScale = _baseScale * (1f + 0.1f * Mathf.Sin(_age * 7f));
            bool visible = lifetime - _age > 3f || Mathf.Repeat(_age * 8f, 1f) < 0.6f;
            foreach (var sr in _srs) if (sr != null) sr.enabled = visible;
        }

        void Collect(PlayerTank player)
        {
            var gm = Game.Manager;
            if (type == PickupType.Fish)
            {
                if (gm != null) gm.AddFish(value);
            }
            else if (player.Health.Hp < player.Health.maxHp) player.Health.Heal(value);
            else if (gm != null) gm.AddScore(150); // здоровье и так полное — очки
            GameEvents.RaisePickupCollected(this);
            Destroy(gameObject);
        }
    }
}
