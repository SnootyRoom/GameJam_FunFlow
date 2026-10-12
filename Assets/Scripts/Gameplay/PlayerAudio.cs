// Двигатель танка (из ветки shishki): петля дизеля, обороты и громкость растут со скоростью.
// Глохнет на паузе и после смерти; в замедлении (прицел ореха) «тянется» вместе со временем.
using UnityEngine;

namespace CatTanks
{
    public class PlayerAudio : MonoBehaviour
    {
        public float idleVolume = 0.12f, maxVolume = 0.32f;
        public float idlePitch = 0.75f, maxPitch = 1.25f;

        AudioSource _engine;
        PlayerTank _tank;
        Health _health;
        Vector2 _lastPos;
        float _speed;

        void Start()
        {
            _tank = GetComponent<PlayerTank>();
            _health = GetComponentInChildren<Health>();
            _lastPos = transform.position;
            var clip = Sfx.EngineLoop; // звуки синтезируются в фоне — петля может быть ещё не готова
            if (clip != null) StartEngine(clip);
        }

        void StartEngine(AudioClip clip)
        {
            _engine = gameObject.AddComponent<AudioSource>();
            _engine.clip = clip;
            _engine.loop = true;
            _engine.spatialBlend = 0f;
            _engine.playOnAwake = false;
            _engine.volume = 0f;
            _engine.Play();
        }

        void Update()
        {
            if (_engine == null)
            {
                var clip = Sfx.EngineLoop;
                if (clip == null) return;
                StartEngine(clip);
            }

            bool running = Time.timeScale > 0f && (_health == null || !_health.IsDead);
            if (!running) { if (_engine.isPlaying) _engine.Pause(); return; }
            if (!_engine.isPlaying) _engine.UnPause();

            // скорость считаем по перемещению — танк двигается MovePosition, у тела скорости может не быть
            Vector2 pos = transform.position;
            float moved = Time.deltaTime > 0f ? Vector2.Distance(pos, _lastPos) / Time.deltaTime : 0f;
            _lastPos = pos;
            float max = _tank != null ? Mathf.Max(0.1f, _tank.moveSpeed) : 3.5f;
            _speed = Mathf.Lerp(_speed, Mathf.Clamp01(moved / max), 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));

            float slow = Mathf.Lerp(0.6f, 1f, Time.timeScale);
            _engine.pitch = Mathf.Lerp(idlePitch, maxPitch, _speed) * slow;
            _engine.volume = Mathf.Lerp(idleVolume, maxVolume, _speed) * Sfx.MasterVolume;
        }
    }
}
