// Звук летящего снаряда: шелест-свист с эффектом Доплера (приближается — выше, удаляется — ниже),
// стерео по положению на экране. Чужой снаряд, пролетевший рядом с котом, даёт резкий «вжух» (WhizBy).
// Вешается на снаряд из Feedback — сам Bullet про звук ничего не знает.
using UnityEngine;

namespace CatTanks
{
    [RequireComponent(typeof(Bullet))]
    public class BulletAudio : MonoBehaviour
    {
        public float volume = 0.3f;
        const float SoundSpeed = 30f; // «скорость звука» в юнитах/с — подобрана, чтобы Доплер был слышен, но не мультяшен

        Bullet _bullet;
        AudioSource _src;
        float _prevPlayerDist = float.MaxValue;
        bool _whizzed;

        static int _active;
        const int MaxActive = 10; // карусель босса — сотня снарядов; звучат только первые 10

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _active = 0;

        void OnEnable() { _active++; }
        void OnDisable() { _active--; }

        public static void Attach(Bullet b, float volume)
        {
            var clip = Sfx.BulletLoop;
            if (clip == null || Sfx.MasterVolume <= 0.001f || (_active >= MaxActive && b.Team != Team.Player)) return; // свои снаряды звучат всегда
            var a = b.gameObject.AddComponent<BulletAudio>();
            a.volume = volume;
            a._src = b.gameObject.AddComponent<AudioSource>();
            a._src.clip = clip;
            a._src.loop = true;
            a._src.spatialBlend = 0f;
            a._src.playOnAwake = false;
            a._src.volume = 0f;
            a._src.time = Random.Range(0f, clip.length * 0.9f); // разные снаряды — разная фаза шелеста
            a._src.Play();
        }

        void Awake() { _bullet = GetComponent<Bullet>(); }

        void Update()
        {
            if (_src == null) return;
            if (!Game.IsPlaying) { _src.volume = 0f; return; }

            var cam = Camera.main;
            Vector2 pos = _bullet.Position;
            Vector2 listener = cam != null ? (Vector2)cam.transform.position : pos;
            Vector2 toListener = listener - pos;
            float dist = toListener.magnitude;
            float approach = dist > 0.01f ? Vector2.Dot(_bullet.Velocity, toListener / dist) : 0f;

            float basePitch = 0.75f + _bullet.speed / 28f; // быстрые снаряды звенят выше
            _src.pitch = Mathf.Clamp(basePitch * (1f + approach / SoundSpeed), 0.5f, 2f) * Mathf.Lerp(0.7f, 1f, Time.timeScale);
            _src.panStereo = Sfx.PanFor(pos);
            float fadeIn = Mathf.Clamp01(_src.time * 20f);
            _src.volume = volume * Sfx.MasterVolume * Sfx.DistanceGain(pos) * fadeIn;

            // чужой снаряд проходит рядом с котом — момент наибольшего сближения
            var player = Game.Player;
            if (!_whizzed && _bullet.Team != Team.Player && player != null && player.isActiveAndEnabled)
            {
                float pd = Vector2.Distance(pos, player.transform.position);
                if (pd > _prevPlayerDist && _prevPlayerDist < 1.6f && _prevPlayerDist > 0.45f)
                {
                    _whizzed = true;
                    Sfx.PlayAt(SfxId.WhizBy, pos, 0.9f, 0.08f, Mathf.Clamp(_bullet.speed / 8f, 0.8f, 1.4f));
                }
                _prevPlayerDist = pd;
            }
        }
    }
}
