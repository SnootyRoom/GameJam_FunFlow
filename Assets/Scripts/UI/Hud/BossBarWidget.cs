// Полоска здоровья босса внизу экрана: табличка с именем (из EnemyDefinition.bossNameplate) и сегменты.
// Выезжает снизу, когда босс впервые попал на экран; уезжает, когда он погиб. Босс — враг с EnemyDefinition.boss.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class BossBarWidget : MonoBehaviour, IHudWidget
    {
        public RectTransform root;
        public SegmentBar bar;
        public Image nameplate;
        [Tooltip("Насколько ниже места полоска прячется, px")]
        public float hiddenOffset = 260f;
        public float slideTime = 0.35f;

        LevelContext _ctx;
        Health _boss;
        Transform _bossTransform;
        bool _revealed, _dead;
        float _slide;          // 0 — спрятана, 1 — на месте
        Vector2 _rest;
        bool _cached;

        public void Bind(HudContext hud)
        {
            if (!_cached) { _cached = true; _rest = root.anchoredPosition; }
            if (_ctx != null) _ctx.EnemyAdded -= OnEnemyAdded;
            Track(null);
            _ctx = hud.Level;
            _slide = 0f;
            Place();
            if (_ctx == null) return;
            _ctx.EnemyAdded += OnEnemyAdded;
            foreach (var e in _ctx.Enemies) OnEnemyAdded(e);
        }

        void OnEnemyAdded(GameObject enemy)
        {
            if (_boss != null || enemy == null) return;
            var tag = enemy.GetComponent<EnemyTag>();
            if (tag == null || !tag.IsBoss) return;
            Track(enemy.GetComponentInChildren<Health>());
            if (_boss == null) return;
            _bossTransform = enemy.transform;
            if (nameplate != null)
            {
                nameplate.sprite = tag.definition.bossNameplate;
                nameplate.gameObject.SetActive(nameplate.sprite != null);
                if (nameplate.sprite != null) nameplate.SetNativeSize();
                nameplate.rectTransform.sizeDelta *= 1.5f; // как и рамка: пиксель картинки = 1.5 px UI
            }
        }

        void Track(Health boss)
        {
            if (_boss != null) { _boss.OnHpChanged -= OnHp; _boss.OnDeath -= OnDeath; }
            _boss = boss;
            _bossTransform = null;
            _revealed = _dead = false;
            if (_boss == null) return;
            _boss.OnHpChanged += OnHp;
            _boss.OnDeath += OnDeath;
            bar.Set(_boss.Hp, _boss.maxHp, instant: true);
        }

        void OnHp(int hp, int max) => bar.Set(hp, max);
        void OnDeath() => _dead = true;

        void OnDestroy() { Track(null); if (_ctx != null) _ctx.EnemyAdded -= OnEnemyAdded; }

        void Update()
        {
            // босс впервые на экране — полоска выезжает и дальше не прячется, пока он жив
            if (!_revealed && _boss != null && _bossTransform != null && _ctx != null && _ctx.IsOnScreen(_bossTransform.position))
                _revealed = true;
            bool show = _revealed && !_dead;
            _slide = Mathf.MoveTowards(_slide, show ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, slideTime));
            Place();
        }

        void Place()
        {
            bool visible = _slide > 0.001f;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            float k = 1f - Mathf.Pow(1f - _slide, 3f); // плавное торможение в конце
            root.anchoredPosition = _rest - new Vector2(0f, Mathf.Round(hiddenOffset * (1f - k)));
        }
    }
}
