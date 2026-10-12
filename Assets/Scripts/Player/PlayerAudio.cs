// Двигатель танка: петля дизеля, обороты и громкость растут со скоростью. Глохнет на паузе и после смерти.
using UnityEngine;

namespace CatTanks
{
    public class PlayerAudio : MonoBehaviour
    {
        public float idleVolume = 0.12f, maxVolume = 0.32f;
        public float idlePitch = 0.75f, maxPitch = 1.25f;

        AudioSource _engine;
        PlayerTank _tank;
        float _speed;

        void Start()
        {
            _tank = GetComponent<PlayerTank>();
            var clip = Sfx.EngineLoop;
            if (clip == null) return;
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
            if (_engine == null) return;
            bool running = Game.IsPlaying && _tank != null && !_tank.Health.IsDead;
            if (!running) { if (_engine.isPlaying) _engine.Pause(); return; }
            if (!_engine.isPlaying) _engine.UnPause();

            float target = _tank.Movement != null ? _tank.Movement.Speed01 : 0f;
            _speed = Mathf.Lerp(_speed, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            // в замедлении времени (прицел ореха) двигатель тоже «тянется»
            float slow = Mathf.Lerp(0.6f, 1f, Time.timeScale);
            _engine.pitch = Mathf.Lerp(idlePitch, maxPitch, _speed) * slow;
            _engine.volume = Mathf.Lerp(idleVolume, maxVolume, _speed) * Sfx.MasterVolume;
        }
    }
}
