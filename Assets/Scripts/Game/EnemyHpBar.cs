// Маленькая полоска HP над врагом (enemy_hp_frame + enemy_hp_fill). Висит над ним и не крутится вместе с ним.
// Показывается после первого попадания (настраивается в FxLibrary), заливка сжимается к левому краю.
// Вешает FxPlayer на каждого обычного врага; у боссов вместо неё — большая полоска в HUD.
using UnityEngine;

namespace CatTanks
{
    public class EnemyHpBar : MonoBehaviour
    {
        Health _hp;
        Material _material;
        Transform _bar, _fillT;
        SpriteRenderer _frame, _fill;
        float _height, _fillWidth;
        bool _onlyWhenHurt;

        public void Setup(FxLibrary lib, Health hp)
        {
            _hp = hp;
            _onlyWhenHurt = lib.enemyHpOnlyWhenHurt;

            // высота: над верхним краем спрайтов врага
            float top = 0.5f;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>())
                top = Mathf.Max(top, r.bounds.extents.y);
            _height = top + lib.enemyHpOffset;

            var own = GetComponentInChildren<SpriteRenderer>();
            if (own != null) _material = own.sharedMaterial; // тот же материал, что у спрайтов уровня (URP 2D)

            _bar = new GameObject("HpBar").transform;
            _bar.SetParent(transform, false);
            Vector2 frameSize = lib.enemyHpFrame.bounds.size;
            float k = lib.enemyHpWidth / Mathf.Max(0.001f, frameSize.x); // один масштаб на обе картинки — пиксели совпадают
            _frame = Make("Frame", lib.enemyHpFrame, 30, k);
            _fill = Make("Fill", lib.enemyHpFill, 31, k);
            _fillT = _fill.transform;
            _fillWidth = lib.enemyHpFill.bounds.size.x * k;

            _hp.OnHpChanged += Refresh;
            Refresh(_hp.Hp, _hp.maxHp);
        }

        SpriteRenderer Make(string name, Sprite sprite, int order, float scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_bar, false);
            go.transform.localScale = Vector3.one * scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order; // поверх врагов и снарядов
            if (_material != null) sr.sharedMaterial = _material;
            return sr;
        }

        /// <summary>Корень полоски — чтобы другие эффекты (мигание перед выстрелом) её не трогали.</summary>
        public Transform BarRoot => _bar;

        void Refresh(int hp, int max)
        {
            float frac = max > 0 ? Mathf.Clamp01((float)hp / max) : 0f;
            _fillT.localScale = new Vector3(_frame.transform.localScale.x * frac, _fillT.localScale.y, 1f);
            _fillT.localPosition = new Vector3(-_fillWidth * (1f - frac) / 2f, 0f, 0f); // прижата к левому краю рамки
            _bar.gameObject.SetActive(hp > 0 && (!_onlyWhenHurt || hp < max));
        }

        void LateUpdate()
        {
            if (_bar == null) return;
            _bar.SetPositionAndRotation(transform.position + Vector3.up * _height, Quaternion.identity);
            // родитель может быть отмасштабирован — держим полоску одного размера
            var s = transform.lossyScale;
            _bar.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 1f);
        }

        void OnDestroy()
        {
            if (_hp != null) _hp.OnHpChanged -= Refresh;
        }
    }
}
