// Полоска HP над врагом (для тех, у кого больше 1 HP). Появляется после первого попадания.
// Отдельный объект, а не ребёнок врага — чтобы не крутилась вместе с ним.
using UnityEngine;

namespace CatTanks
{
    public class WorldHealthBar : MonoBehaviour
    {
        Health _target;
        Transform _fill;
        SpriteRenderer _bg, _fillSr;
        float _width, _height = 0.1f, _offsetY, _shown, _display = 1f;

        public static WorldHealthBar Attach(Health target, float targetSize)
        {
            var go = new GameObject("HpBar");
            go.transform.SetParent(Game.LevelRoot, false);
            var bar = go.AddComponent<WorldHealthBar>();
            bar._target = target;
            bar._width = Mathf.Clamp(targetSize, 0.6f, 1.6f);
            bar._offsetY = targetSize * 0.65f + 0.2f;
            bar._bg = Prims.Child("Bg", go.transform, Prims.Square, new Color(0f, 0f, 0f, 0.6f), Vector2.zero,
                                  new Vector2(bar._width + 0.06f, bar._height + 0.06f), 40).GetComponent<SpriteRenderer>();
            var fill = Prims.Child("Fill", go.transform, Prims.Square, new Color(0.4f, 1f, 0.45f), Vector2.zero,
                                   new Vector2(bar._width, bar._height), 41);
            bar._fill = fill.transform;
            bar._fillSr = fill.GetComponent<SpriteRenderer>();
            bar.SetAlpha(0f);
            return bar;
        }

        void LateUpdate()
        {
            if (_target == null || _target.IsDead) { Destroy(gameObject); return; }
            transform.position = (Vector2)_target.transform.position + Vector2.up * _offsetY;

            float k = _target.Hp / (float)Mathf.Max(1, _target.maxHp);
            if (k < 1f) _shown = Mathf.MoveTowards(_shown, 1f, Time.deltaTime * 6f);
            _display = Mathf.Lerp(_display, k, 1f - Mathf.Exp(-12f * Time.deltaTime));

            _fill.localScale = new Vector3(_width * _display, _height, 1f);
            _fill.localPosition = new Vector3(-_width * (1f - _display) / 2f, 0f, 0f);
            _fillSr.color = Color.Lerp(new Color(1f, 0.3f, 0.25f), new Color(0.4f, 1f, 0.45f), k);
            SetAlpha(_shown);
        }

        void SetAlpha(float a)
        {
            _bg.color = Prims.WithAlpha(_bg.color, 0.6f * a);
            _fillSr.color = Prims.WithAlpha(_fillSr.color, a);
        }
    }
}
