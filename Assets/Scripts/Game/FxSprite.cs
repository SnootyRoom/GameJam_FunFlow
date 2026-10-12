// Жизнь одного эффекта: растёт и тает, потом удаляется. Создаёт FxPlayer.
using UnityEngine;

namespace CatTanks
{
    public class FxSprite : MonoBehaviour
    {
        float _duration, _grow, _t;
        Vector3 _scale;
        SpriteRenderer _sr;
        Color _color;

        public void Setup(float duration, float growTo)
        {
            _duration = duration;
            _grow = growTo;
            _scale = transform.localScale;
            _sr = GetComponent<SpriteRenderer>();
            _color = _sr.color;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float p = Mathf.Clamp01(_t / _duration);
            transform.localScale = _scale * Mathf.Lerp(1f, _grow, p);
            // первые 40% жизни — полная яркость, потом тает
            var c = _color;
            c.a *= p < 0.4f ? 1f : 1f - (p - 0.4f) / 0.6f;
            _sr.color = c;
            if (p >= 1f) Destroy(gameObject);
        }
    }
}
