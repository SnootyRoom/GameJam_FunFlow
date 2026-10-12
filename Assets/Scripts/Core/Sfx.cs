// Звуки, синтезированные кодом — без аудиофайлов, но по физике настоящих записей:
//   • ударная волна (N-волна) — тот самый резкий «хлопок» выстрела;
//   • широкополосное шумовое тело с падающим фильтром вместо «пиу»-тонов;
//   • помещение: ранние отражения от стен арены + диффузный хвост (свёртка через FFT с синтетическим импульсным откликом);
//   • насыщение, как у реальной записи громкого звука.
// У частых звуков несколько вариаций. Рендер идёт в фоновом потоке — старт игры не подвисает.
//
// Sfx.Play(SfxId.Shot) — в центр; Sfx.PlayAt(SfxId.Shot, pos) — со стерео и затуханием по расстоянию.
// НАСТОЯЩИЕ СЭМПЛЫ: положи файлы в Assets/Resources/Sfx/ с именем как у SfxId ("Shot.wav" или "Shot_0.wav",
// "Shot_1.wav" для вариаций) — они заменят синтез автоматически. Или назначь в overrides (индекс = SfxId).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CatTanks
{
    public enum SfxId
    {
        Shot, EnemyShot, Bounce, Hit, WallHit, Explosion, PlayerHurt, PlayerDeath, Pickup,
        UltReady, NutWhistle, NutImpact, Win, UiClick, UiHover, Alert, Empty, Purchase,
        LevelStart, ReloadStart, ReloadEnd, WhizBy, DeathKnell, BossRoar, Combo, Coin, Heal,
    }

    public class Sfx : MonoBehaviour
    {
        [Tooltip("Заменить сгенерированный звук своим: индекс = SfxId")]
        public AudioClip[] overrides = new AudioClip[0];

        public static float MasterVolume = 0.7f;
        public static AudioClip EngineLoop => _instance != null ? _instance._engine : null;
        public static AudioClip BulletLoop => _instance != null ? _instance._bullet : null;

        static Sfx _instance;
        AudioClip[][] _clips;
        AudioClip[][] _override;
        AudioClip _engine, _bullet;
        float[][][] _rendered;
        float[] _engineData, _bulletData;
        volatile bool _renderDone;
        AudioSource[] _pool;
        float[] _lastPlayed;
        int _next;

        public const int Rate = 44100;
        static System.Random _rng = new System.Random(1);
        static readonly object Lock = new object();

        // ================================================================== Воспроизведение

        public static void Play(SfxId id, float volume = 1f, float pitchJitter = 0.05f, float pitch = 1f)
        {
            var inst = Instance;
            if (inst != null) inst.PlayInternal(id, volume, pitchJitter, pitch, 0f);
        }

        /// <summary>Звук в точке мира: панорама по положению на экране, тише вдали от камеры.</summary>
        public static void PlayAt(SfxId id, Vector2 world, float volume = 1f, float pitchJitter = 0.05f, float pitch = 1f)
        {
            var inst = Instance;
            if (inst == null) return;
            inst.PlayInternal(id, volume * DistanceGain(world), pitchJitter, pitch, PanFor(world));
        }

        /// <summary>Стерео-панорама точки по положению на экране (-1..1).</summary>
        public static float PanFor(Vector2 world)
        {
            var cam = Camera.main;
            if (cam == null) return 0f;
            float halfW = cam.orthographicSize * cam.aspect;
            return Mathf.Clamp((world.x - cam.transform.position.x) / (halfW * 1.3f), -1f, 1f) * 0.75f;
        }

        /// <summary>Затухание по расстоянию от центра экрана (1 рядом, 0.25 далеко за кадром).</summary>
        public static float DistanceGain(Vector2 world)
        {
            var cam = Camera.main;
            if (cam == null) return 1f;
            float halfW = cam.orthographicSize * cam.aspect;
            float d = Vector2.Distance(world, cam.transform.position);
            return Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(halfW * 0.8f, halfW * 2.5f, d));
        }

        static Sfx Instance
        {
            get
            {
                if (MasterVolume <= 0.001f) return null;
                if (_instance == null)
                {
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Sfx>();
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            if (transform.parent == null) DontDestroyOnLoad(gameObject);

            int count = Enum.GetValues(typeof(SfxId)).Length;
            _lastPlayed = new float[count];
            _override = CollectOverrides(count);

            _pool = new AudioSource[24];
            for (int i = 0; i < _pool.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _pool[i] = src;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            RenderAll(); // в WebGL нет потоков
#else
            Task.Run(RenderAll);
#endif
        }

        AudioClip[][] CollectOverrides(int count)
        {
            var result = new AudioClip[count][];
            var res = Resources.LoadAll<AudioClip>("Sfx");
            for (int i = 0; i < count; i++)
            {
                if (i < overrides.Length && overrides[i] != null) { result[i] = new[] { overrides[i] }; continue; }
                string name = ((SfxId)i).ToString();
                var list = new List<AudioClip>();
                foreach (var c in res)
                    if (c.name == name || c.name.StartsWith(name + "_", StringComparison.Ordinal)) list.Add(c);
                if (list.Count > 0) result[i] = list.ToArray();
            }
            return result;
        }

        void RenderAll()
        {
            try
            {
                int count = _override.Length;
                var rendered = new float[count][][];
                for (int i = 0; i < count; i++)
                {
                    if (_override[i] != null) continue;
                    var id = (SfxId)i;
                    rendered[i] = new float[Variants(id)][];
                    for (int v = 0; v < rendered[i].Length; v++) rendered[i][v] = Render(id, v);
                }
                _engineData = RenderEngineLoop();
                _bulletData = RenderBulletLoop();
                _rendered = rendered;
            }
            catch (Exception e) { Debug.LogException(e); }
            _renderDone = true;
        }

        void Update()
        {
            if (_clips != null || !_renderDone || _rendered == null) return;
            // AudioClip создаём только в главном потоке
            _clips = new AudioClip[_rendered.Length][];
            for (int i = 0; i < _rendered.Length; i++)
            {
                if (_override[i] != null) { _clips[i] = _override[i]; continue; }
                _clips[i] = new AudioClip[_rendered[i].Length];
                for (int v = 0; v < _rendered[i].Length; v++) _clips[i][v] = ToClip((SfxId)i + "_" + v, _rendered[i][v]);
            }
            _engine = ToClip("EngineLoop", _engineData);
            _bullet = ToClip("BulletLoop", _bulletData);
            _rendered = null;
        }

        static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static int Variants(SfxId id)
        {
            switch (id)
            {
                case SfxId.Shot: return 4;
                case SfxId.EnemyShot: return 3;
                case SfxId.Bounce: return 5;
                case SfxId.Hit: return 3;
                case SfxId.WallHit: return 3;
                case SfxId.Explosion: return 3;
                case SfxId.WhizBy: return 3;
                default: return 1;
            }
        }

        void PlayInternal(SfxId id, float volume, float jitter, float pitch, float pan)
        {
            if (_clips == null) return; // ещё рендерится (доли секунды после старта)
            int i = (int)id;
            float now = Time.unscaledTime;
            if (now - _lastPlayed[i] < 0.03f) return; // десять рикошетов в кадре не должны оглушать
            _lastPlayed[i] = now;

            var set = _clips[i];
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Length;
            src.clip = set[UnityEngine.Random.Range(0, set.Length)];
            src.volume = volume * MasterVolume;
            src.pitch = pitch * (1f + UnityEngine.Random.Range(-jitter, jitter));
            src.panStereo = pan;
            src.Play();
        }

        // ================================================================== Рецепты

        /// <summary>Синтезировать звук в массив сэмплов (моно, Rate Гц). Публично — для экспорта в WAV.</summary>
        public static float[] Render(SfxId id, int variant)
        {
            lock (Lock) return RenderUnsafe(id, variant);
        }

        static float[] RenderUnsafe(SfxId id, int variant)
        {
            _rng = new System.Random(1000 + (int)id * 31 + variant * 7);
            float v = Rand01(); // разброс между вариациями
            float[] d;
            switch (id)
            {
                case SfxId.Shot: // танковая пушка кота: хлопок + удар в грудь + раскат, никакого звона
                    d = Buf(2.4f);
                    NWave(d, 0f, 1.6f + 0.3f * v, 1f);                                                   // ударная волна
                    NoiseF(d, 0f, 0.02f, 0.45f, 0.0002f, 0.004f, F.HP, 1500, 1500, 0.7f);                 // треск
                    SubKick(d, 0f, 0.16f, 62 - 6 * v, 30, 1.4f);                                         // мощный саб-удар
                    NoiseF(d, 0f, 0.6f, 1.2f, 0.001f, 0.09f + 0.02f * v, F.LP, 1600, 70, 0.9f);          // пороховые газы
                    NoiseF(d, 0f, 1.1f, 2.4f, 0.002f, 0.3f, F.LP, 140, 38, 1.1f);                        // раскат (без тона)
                    NoiseF(d, 0.003f, 0.25f, 0.45f, 0.001f, 0.05f, F.BP, 650 - 100 * v, 300, 0.7f);      // удар в грудь
                    Crackle(d, 0.02f, 0.2f, 8, 0.08f);
                    Saturate(d, 2f);
                    Room(d, 0.45f, 0.5f, 24);
                    Normalize(d, 0.98f);
                    break;

                case SfxId.EnemyShot: // калибр поменьше, сухой и злой
                    d = Buf(1.4f);
                    NWave(d, 0f, 0.8f + 0.2f * v, 0.9f);
                    NoiseF(d, 0f, 0.015f, 0.8f, 0.0002f, 0.003f, F.HP, 2500, 2500, 0.7f);
                    NoiseF(d, 0f, 0.3f, 0.85f, 0.001f, 0.05f, F.LP, 3800, 220, 0.8f);
                    NoiseF(d, 0f, 0.4f, 1.1f, 0.001f, 0.1f, F.LP, 200, 60, 1f);
                    SubKick(d, 0f, 0.1f, 90, 45, 0.6f);
                    Saturate(d, 1.8f);
                    Room(d, 0.4f, 0.35f, 24);
                    Normalize(d, 0.85f);
                    break;

                case SfxId.Bounce: // рикошет: удар о бетон + «вжиу» кувыркающегося снаряда
                    d = Buf(1.2f);
                    NWave(d, 0f, 0.5f, 0.5f);
                    NoiseF(d, 0f, 0.03f, 0.7f, 0.0003f, 0.006f, F.BP, 2600, 2600, 1.2f);                 // скол бетона
                    Grains(d, 0.002f, 0.05f, 5, 2000, 6000, 0.18f, false);                               // крошка
                    RicochetWhine(d, 0.008f, 0.6f + 0.15f * v, 2600 + 1600 * v, 0.5f);
                    Room(d, 0.3f, 0.3f, 7);
                    Normalize(d, 0.8f);
                    break;

                case SfxId.Hit: // снаряд пробивает корпус телефона
                    d = Buf(0.8f);
                    NWave(d, 0f, 0.5f, 0.6f);
                    NoiseF(d, 0f, 0.05f, 0.8f, 0.0005f, 0.01f, F.BP, 3000 + 600 * v, 3000, 1f);          // треск пластика
                    NoiseF(d, 0f, 0.12f, 0.6f, 0.001f, 0.03f, F.LP, 1500, 300, 0.7f);                    // корпус
                    NoiseF(d, 0f, 0.2f, 0.9f, 0.001f, 0.05f, F.LP, 260, 120, 1f);                        // глухой удар
                    Grains(d, 0.005f, 0.1f, 5, 1200, 4000, 0.12f, false);                              // обломки корпуса
                    Room(d, 0.22f, 0.25f, 6);
                    Normalize(d, 0.85f);
                    break;

                case SfxId.WallHit: // снаряд разбивается о бетон
                    d = Buf(0.8f);
                    NWave(d, 0f, 0.6f, 0.6f);
                    NoiseF(d, 0f, 0.03f, 0.7f, 0.0005f, 0.01f, F.BP, 1600 + 400 * v, 1600, 0.8f);
                    NoiseF(d, 0f, 0.15f, 0.5f, 0.001f, 0.04f, F.LP, 300, 120, 1f);
                    Grains(d, 0.005f, 0.15f, 7, 800, 3000, 0.15f, false);
                    Room(d, 0.3f, 0.3f, 7);
                    Normalize(d, 0.55f);
                    break;

                case SfxId.Explosion: // классический взрыв (враг-телефон)
                    d = Buf(2.8f);
                    ClassicExplosion(d, 0f, 1f, v);
                    Saturate(d, 1.6f);
                    Room(d, 0.45f, 0.75f, 10);
                    Normalize(d, 0.97f);
                    break;

                case SfxId.PlayerDeath: // танк кота взрывается: большой взрыв + детонация + летящее железо
                    d = Buf(4.2f);
                    ClassicExplosion(d, 0f, 1.6f, v);
                    ClassicExplosion(d, 0.38f, 0.8f, 1f - v);                                            // вторичная детонация
                    for (int i = 0; i < 7; i++) // падающие куски брони — глухие удары, не звон
                        NoiseF(d, 0.25f + 0.28f * i + Rand01() * 0.12f, 0.12f, 0.35f * (1f - i * 0.1f), 0.001f, 0.03f, F.BP, 300 + Rand01() * 500, 200, 1.2f);
                    Saturate(d, 1.7f);
                    Room(d, 0.5f, 1f, 12);
                    Normalize(d, 1f);
                    break;

                case SfxId.NutImpact: // орех весом в тонну: земля трясётся, летят камни
                    d = Buf(3.8f);
                    NWave(d, 0f, 3f, 1f);
                    NoiseF(d, 0f, 0.07f, 0.6f, 0.0005f, 0.02f, F.BP, 1200, 900, 2f);                   // треск скорлупы
                    NoiseF(d, 0f, 1.6f, 1.1f, 0.003f, 0.4f, F.LP, 1400, 50, 0.9f);
                    NoiseF(d, 0f, 2.4f, 1.2f, 0.005f, 0.9f, F.LP, 95, 28, 1.1f);                       // земля
                    Brown(d, 0f, 2.6f, 0.7f, 0.9f, 160);
                    Grains(d, 0.05f, 1.8f, 70, 300, 2500, 0.3f, false);                                // камни
                    Saturate(d, 2.2f);
                    Room(d, 0.5f, 1f, 12);
                    Normalize(d, 1f);
                    break;

                case SfxId.WhizBy: // снаряд пролетел рядом: «вжжух» с эффектом Доплера
                    d = Buf(0.7f);
                    WhizBy(d, 0.45f + 0.1f * v, 4200 + 900 * v);
                    Room(d, 0.15f, 0.2f, 5);
                    Normalize(d, 0.75f);
                    break;

                case SfxId.NutWhistle: // свист падающей бомбы (длина = время падения по умолчанию)
                    d = Buf(1.1f);
                    Whistle(d, 1.1f);
                    Normalize(d, 0.6f);
                    break;

                case SfxId.PlayerHurt: // по броне танка: глухой удар и звон металла
                    d = Buf(1.2f);
                    NWave(d, 0f, 0.8f, 0.7f);
                    NoiseF(d, 0f, 0.04f, 0.8f, 0.0005f, 0.01f, F.BP, 2500, 2500, 0.7f);
                    NoiseF(d, 0f, 0.25f, 0.8f, 0.001f, 0.06f, F.LP, 300, 90, 1f);
                    Modal(d, 0f, 290, new[] { 1f, 1.58f, 2.13f, 2.74f, 3.62f }, new[] { 1f, 0.8f, 0.6f, 0.45f, 0.3f },
                          new[] { 0.2f, 0.15f, 0.11f, 0.08f, 0.06f }, 0.45f);
                    Grains(d, 0.02f, 0.25f, 8, 1500, 5000, 0.1f, false);
                    Saturate(d, 1.5f);
                    Room(d, 0.3f, 0.35f, 8);
                    Normalize(d, 0.95f);
                    break;

                case SfxId.Pickup: // колокольчик (глокеншпиль)
                    d = Buf(1.4f);
                    Bubble(d, 0f, 520, 0.6f); Bubble(d, 0.07f, 700, 0.6f); Bubble(d, 0.14f, 950, 0.7f);
                    Room(d, 0.2f, 0.3f, 24);
                    Normalize(d, 0.7f);
                    break;

                case SfxId.UltReady: // «тук-тук» деревом + колокол: орех готов
                    d = Buf(1.4f);
                    Modal(d, 0f, 820, new[] { 1f, 2.4f, 3.7f }, new[] { 1f, 0.4f, 0.2f }, new[] { 0.05f, 0.03f, 0.02f }, 0.5f);
                    Modal(d, 0.09f, 1050, new[] { 1f, 2.4f, 3.7f }, new[] { 1f, 0.4f, 0.2f }, new[] { 0.05f, 0.03f, 0.02f }, 0.5f);
                    NoiseF(d, 0.16f, 0.5f, 0.5f, 0.15f, 0.12f, F.BP, 400, 2400, 1.2f);                 // нарастающий «вжух»
                    SubKick(d, 0.3f, 0.12f, 80, 45, 0.6f);
                    Room(d, 0.25f, 0.4f, 24);
                    Normalize(d, 0.75f);
                    break;

                case SfxId.Win:
                    d = Buf(2.6f);
                    float[] win = { 1047, 1319, 1568, 2093 };
                    for (int i = 0; i < win.Length; i++) Bell(d, i * 0.12f, win[i], 0.45f, i == win.Length - 1 ? 2f : 1f);
                    Room(d, 0.3f, 0.6f, 6);
                    Normalize(d, 0.8f);
                    break;

                case SfxId.UiClick: // механический щелчок
                    d = Buf(0.08f);
                    NoiseF(d, 0f, 0.004f, 0.6f, 0.0002f, 0.0015f, F.HP, 4000, 4000, 0.7f);
                    Modal(d, 0f, 3200, new[] { 1f, 1.7f }, new[] { 1f, 0.4f }, new[] { 0.012f, 0.008f }, 0.3f);
                    Normalize(d, 0.5f);
                    break;

                case SfxId.UiHover:
                    d = Buf(0.05f);
                    Modal(d, 0f, 5200, new[] { 1f }, new[] { 1f }, new[] { 0.006f }, 0.15f);
                    Normalize(d, 0.22f);
                    break;

                case SfxId.Alert: // телефон завибрировал: «бжж-бжж»
                    d = Buf(0.55f);
                    PhoneBuzz(d, 0f, 0.18f); PhoneBuzz(d, 0.27f, 0.18f);
                    Normalize(d, 0.6f);
                    break;

                case SfxId.Empty: // сухой щелчок бойка
                    d = Buf(0.1f);
                    Modal(d, 0f, 2600, new[] { 1f, 1.53f, 2.4f }, new[] { 1f, 0.5f, 0.3f }, new[] { 0.02f, 0.015f, 0.01f }, 0.5f);
                    NoiseF(d, 0f, 0.01f, 0.5f, 0.0003f, 0.003f, F.HP, 3000, 3000, 0.7f);
                    Normalize(d, 0.55f);
                    break;

                case SfxId.Purchase: // монетки
                    d = Buf(1f);
                    Modal(d, 0f, 3100, new[] { 1f, 2.1f, 3.3f }, new[] { 1f, 0.5f, 0.3f }, new[] { 0.25f, 0.15f, 0.1f }, 0.4f);
                    Modal(d, 0.07f, 4200, new[] { 1f, 2.1f, 3.3f }, new[] { 1f, 0.5f, 0.3f }, new[] { 0.3f, 0.18f, 0.1f }, 0.4f);
                    Grains(d, 0f, 0.1f, 4, 5000, 9000, 0.08f, true);
                    Room(d, 0.2f, 0.3f, 5);
                    Normalize(d, 0.7f);
                    break;

                case SfxId.LevelStart: // лязг люка + колокольчики
                    d = Buf(1.8f);
                    NoiseF(d, 0f, 0.15f, 0.8f, 0.001f, 0.04f, F.LP, 400, 150, 1f);                     // люк захлопнулся
                    NoiseF(d, 0.12f, 1f, 0.6f, 0.3f, 0.4f, F.LP, 120, 400, 1.5f);                      // двигатель взревел
                    Brown(d, 0.12f, 1.2f, 0.4f, 0.5f, 250);
                    Room(d, 0.25f, 0.4f, 24);
                    Normalize(d, 0.7f);
                    break;

                case SfxId.ReloadStart: // гильза вылетает и звякает об пол
                    d = Buf(0.8f);
                    Modal(d, 0f, 520, new[] { 1f, 2.2f, 3.9f, 5.3f }, new[] { 1f, 0.6f, 0.4f, 0.25f }, new[] { 0.12f, 0.08f, 0.05f, 0.04f }, 0.5f);
                    NoiseF(d, 0.04f, 0.2f, 0.35f, 0.01f, 0.06f, F.BP, 1200, 3500, 1.5f);
                    Modal(d, 0.13f, 900, new[] { 1f, 2.5f }, new[] { 1f, 0.4f }, new[] { 0.06f, 0.04f }, 0.25f);
                    Modal(d, 0.21f, 1100, new[] { 1f, 2.5f }, new[] { 1f, 0.4f }, new[] { 0.04f, 0.03f }, 0.12f);
                    Room(d, 0.2f, 0.25f, 5);
                    Normalize(d, 0.6f);
                    break;

                case SfxId.ReloadEnd: // новый снаряд в казённике, затвор закрылся
                    d = Buf(0.6f);
                    NoiseF(d, 0f, 0.12f, 0.35f, 0.005f, 0.04f, F.BP, 2500, 1200, 1.2f);
                    Modal(d, 0.08f, 1350, new[] { 1f, 2.17f, 3.4f }, new[] { 1f, 0.6f, 0.4f }, new[] { 0.04f, 0.03f, 0.02f }, 0.6f);
                    NoiseF(d, 0.08f, 0.12f, 0.9f, 0.001f, 0.035f, F.LP, 220, 110, 1f);
                    NoiseF(d, 0.08f, 0.01f, 0.2f, 0.0003f, 0.003f, F.HP, 3000, 3000, 0.7f);
                    Room(d, 0.18f, 0.25f, 5);
                    Normalize(d, 0.65f);
                    break;

                case SfxId.DeathKnell: // погребальный колокол: три низких удара с долгим гулом
                    d = Buf(7.5f);
                    for (int i = 0; i < 3; i++) ChurchBell(d, i * 2.1f, 196f, 0.6f);
                    Room(d, 0.4f, 1.6f, 24);
                    Normalize(d, 0.8f);
                    break;

                case SfxId.BossRoar: // рёв босса: низкий рык + металлический скрежет
                    d = Buf(2f);
                    Growl(d, 0f, 1.3f, 55f, 0.9f);
                    NoiseF(d, 0.05f, 1.2f, 0.35f, 0.1f, 0.4f, F.BP, 1800, 900, 6f);                    // скрежет
                    SubKick(d, 0f, 0.3f, 50, 28, 1f);
                    Saturate(d, 2.4f);
                    Room(d, 0.45f, 0.9f, 24);
                    Normalize(d, 0.95f);
                    break;

                case SfxId.Combo: // короткий сочный «поп»: высота задаётся при воспроизведении (растёт с комбо)
                    d = Buf(0.25f);
                    Bubble(d, 0f, 600, 0.8f);
                    NoiseF(d, 0f, 0.02f, 0.4f, 0.0005f, 0.006f, F.BP, 2500, 2500, 1f);
                    Normalize(d, 0.6f);
                    break;

                case SfxId.Coin: // рыбка подобрана: «плюх» пузырька
                    d = Buf(0.3f);
                    Bubble(d, 0f, 750 + 150 * v, 0.9f);
                    Normalize(d, 0.55f);
                    break;

                case SfxId.Heal: // тёплый подъём
                    d = Buf(0.8f);
                    NoiseF(d, 0f, 0.6f, 0.6f, 0.15f, 0.25f, F.BP, 300, 1200, 2f);
                    Bubble(d, 0.1f, 500, 0.6f); Bubble(d, 0.2f, 750, 0.6f);
                    Room(d, 0.25f, 0.4f, 24);
                    Normalize(d, 0.6f);
                    break;

                default:
                    d = Buf(0.05f);
                    break;
            }
            FadeTail(d, 0.03f);
            return d;
        }

        /// <summary>Дизельный двигатель танка, бесшовная петля 2 с (такты кратны длине петли).</summary>
        public static float[] RenderEngineLoop()
        {
            lock (Lock)
            {
                _rng = new System.Random(77);
                const float loop = 2f, extra = 0.25f, fire = 28f;
                var d = Buf(loop + extra);
                int pulses = Mathf.RoundToInt((loop + extra) * fire);
                for (int k = 0; k < pulses; k++)
                {
                    float t = k / fire;
                    float a = 0.8f + 0.4f * Rand01();
                    NoiseF(d, t, 0.06f, 0.6f * a, 0.001f, 0.018f, F.LP, 180, 90, 1.2f);   // такт цилиндра
                    NoiseF(d, t, 0.04f, 0.25f * a, 0.001f, 0.012f, F.LP, 650, 400, 0.7f);
                }
                Brown(d, 0f, loop + extra, 0.3f, 999f, 220);
                Sine(d, 0f, loop + extra, 420, 420, 0.03f, 0.01f, 999f);  // вой трансмиссии
                for (int k = 0; k < Mathf.RoundToInt((loop + extra) * 14); k++) // лязг траков
                    Modal(d, k / 14f, 1800 + 200 * Rand01(), new[] { 1f, 2.3f }, new[] { 1f, 0.4f }, new[] { 0.01f, 0.008f }, 0.05f);
                return MakeLoop(d, loop, extra, 0.5f);
            }
        }

        /// <summary>Шелест-свист летящего снаряда: шум воздуха + тон, «кувыркание» модулирует громкость. Петля 1 с.</summary>
        public static float[] RenderBulletLoop()
        {
            lock (Lock)
            {
                _rng = new System.Random(91);
                const float loop = 1f, extra = 0.2f;
                var d = Buf(loop + extra);
                // плотный рёв воздуха вокруг снаряда: низ + середина, лёгкое «вращение», без тона
                var a = new Biquad(); a.Set(F.BP, 650, 1.1f);
                var b = new Biquad(); b.Set(F.BP, 1700, 2f);
                var c = new Biquad(); c.Set(F.LP, 220, 0.8f);
                var top = new Biquad(); top.Set(F.LP, 2200, 0.7f); // срезаем шипение — снаряд гудит, а не шипит
                for (int i = 0; i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float spin = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 15f * t);
                    float s = a.Process(RandS()) * 1.5f + b.Process(RandS()) * 0.45f + c.Process(RandS()) * 2.2f;
                    d[i] = top.Process(s) * spin;
                }
                return MakeLoop(d, loop, extra, 0.6f);
            }
        }

        /// <summary>Склейка в петлю: хвост после loop подмешивается в начало с кроссфейдом.</summary>
        static float[] MakeLoop(float[] d, float loop, float extra, float peak)
        {
            int n = (int)(loop * Rate), x = (int)(extra * Rate);
            var outBuf = new float[n];
            Array.Copy(d, outBuf, n);
            for (int i = 0; i < x && i < n && n + i < d.Length; i++)
            {
                float w = i / (float)x;
                outBuf[i] = outBuf[i] * w + d[n + i] * (1f - w);
            }
            Normalize(outBuf, peak);
            return outBuf;
        }

        // ================================================================== Строительные блоки

        enum F { LP, HP, BP }

        struct Biquad
        {
            float b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public void Set(F type, float freq, float q)
            {
                freq = Mathf.Clamp(freq, 20f, Rate * 0.45f);
                double w = 2 * Math.PI * freq / Rate, cw = Math.Cos(w), sw = Math.Sin(w), alpha = sw / (2 * q);
                double nb0, nb1, nb2, a0 = 1 + alpha, na1 = -2 * cw, na2 = 1 - alpha;
                switch (type)
                {
                    case F.LP: nb0 = (1 - cw) / 2; nb1 = 1 - cw; nb2 = (1 - cw) / 2; break;
                    case F.HP: nb0 = (1 + cw) / 2; nb1 = -(1 + cw); nb2 = (1 + cw) / 2; break;
                    default: nb0 = alpha; nb1 = 0; nb2 = -alpha; break;
                }
                b0 = (float)(nb0 / a0); b1 = (float)(nb1 / a0); b2 = (float)(nb2 / a0);
                a1 = (float)(na1 / a0); a2 = (float)(na2 / a0);
            }

            public float Process(float x)
            {
                float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static float Rand01() => (float)_rng.NextDouble();
        static float RandS() => (float)(_rng.NextDouble() * 2 - 1);
        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        static float Env(float t, float attack, float tau) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / tau);
        static float Glide(float f0, float f1, float k) => f0 * Mathf.Pow(f1 / f0, k); // экспоненциальное скольжение

        /// <summary>Ударная волна: мгновенный скачок давления, линейный спад в разрежение и возврат. Главный «хлопок».</summary>
        static void NWave(float[] d, float start, float ms, float amp)
        {
            int s0 = (int)(start * Rate), T = Mathf.Max(2, (int)(ms * 0.001f * Rate));
            for (int i = 0; i < T * 3 && s0 + i < d.Length; i++)
            {
                float x = i < T ? 1f - 2f * i / T : -(1f - (i - T) / (2f * T));
                d[s0 + i] += x * amp;
            }
        }

        /// <summary>Шум через фильтр, частота среза скользит f0 → f1. Огибающая: атака + экспоненциальный спад.</summary>
        static void NoiseF(float[] d, float start, float dur, float amp, float attack, float tau, F type, float f0, float f1, float q)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var bq = new Biquad();
            bq.Set(type, f0, q);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                if ((i & 31) == 0 && f0 != f1) bq.Set(type, Glide(f0, f1, i / (float)n), q);
                d[s0 + i] += bq.Process(RandS()) * amp * Env(i / (float)Rate, attack, tau);
            }
        }

        static void Sine(float[] d, float start, float dur, float f0, float f1, float amp, float attack, float tau)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            double phase = Rand01();
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                phase += Glide(f0, f1, i / (float)n) / Rate;
                d[s0 + i] += (float)Math.Sin(phase * 2 * Math.PI) * amp * Env(i / (float)Rate, attack, tau);
            }
        }

        /// <summary>Модальный синтез: набор затухающих негармонических частот — металл, стекло, дерево, колокол.</summary>
        static void Modal(float[] d, float start, float f, float[] ratios, float[] amps, float[] taus, float amp)
        {
            int s0 = (int)(start * Rate);
            for (int m = 0; m < ratios.Length; m++)
            {
                float fm = f * ratios[m];
                if (fm > Rate * 0.45f) continue;
                int n = (int)(taus[m] * 7f * Rate);
                double w = 2 * Math.PI * fm / Rate, ph = Rand01() * 6.28;
                for (int i = 0; i < n && s0 + i < d.Length; i++)
                {
                    float t = i / (float)Rate;
                    float env = Mathf.Min(1f, t / 0.0005f) * Mathf.Exp(-t / taus[m]);
                    d[s0 + i] += (float)Math.Sin(ph + w * i) * amps[m] * amp * env;
                }
            }
        }

        static void Bell(float[] d, float start, float f, float amp, float length)
            => Modal(d, start, f, new[] { 1f, 2.76f, 5.4f, 8.93f }, new[] { 1f, 0.35f, 0.15f, 0.06f },
                     new[] { 0.9f * length, 0.35f * length, 0.18f * length, 0.1f * length }, amp);

        /// <summary>Коричневый шум (низкий гул), дополнительно сглаженный.</summary>
        static void Brown(float[] d, float start, float dur, float amp, float tau, float lp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var bq = new Biquad();
            bq.Set(F.LP, lp, 0.7f);
            float b = 0f;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                b = (b + RandS() * 0.02f) * 0.998f;
                d[s0 + i] += bq.Process(b) * 1.2f * amp * Env(i / (float)Rate, 0.01f, tau); // std(b) ≈ 0.32
            }
        }

        /// <summary>Россыпь мелких звуков: стекло (звенящие) или обломки (шумовые щелчки). Гуще в начале.</summary>
        static void Grains(float[] d, float start, float spread, int count, float fMin, float fMax, float amp, bool glassy)
        {
            for (int g = 0; g < count; g++)
            {
                float k = Mathf.Pow(Rand01(), 1.7f);
                float t = start + spread * k;
                float f = Mathf.Exp(Mathf.Lerp(Mathf.Log(fMin), Mathf.Log(fMax), Rand01()));
                float a = amp * (0.3f + 0.7f * Rand01()) * (1f - 0.6f * k);
                if (glassy)
                    Modal(d, t, f, new[] { 1f, 2.71f }, new[] { 1f, 0.4f }, new[] { 0.01f + 0.03f * Rand01(), 0.01f }, a);
                else
                    NoiseF(d, t, 0.03f, a, 0.0005f, 0.008f + 0.01f * Rand01(), F.BP, f, f, 3f);
            }
        }

        /// <summary>Потрескивание: редкие короткие щелчки (горящий порох, огонь).</summary>
        static void Crackle(float[] d, float start, float spread, int count, float amp)
        {
            for (int c = 0; c < count; c++)
            {
                float t = start + spread * Mathf.Pow(Rand01(), 1.4f);
                NoiseF(d, t, 0.006f, amp * (0.4f + 0.6f * Rand01()), 0.0001f, 0.0008f + 0.001f * Rand01(), F.BP, 1500 + 3500 * Rand01(), 2000, 1.5f);
            }
        }

        /// <summary>
        /// Классический взрыв: ударная волна, треск, широкополосный «бах» с падающим фильтром, саб-удар,
        /// гул пламени с неровной громкостью, потрескивание огня, обломки.
        /// </summary>
        static void ClassicExplosion(float[] d, float start, float scale, float v)
        {
            NWave(d, start, 2.5f * scale, 1f);
            NoiseF(d, start, 0.03f, 0.5f, 0.0003f, 0.008f, F.HP, 1200, 1200, 0.7f);
            NoiseF(d, start, 1.4f * scale, 1.2f, 0.002f, 0.3f * scale, F.LP, 750 - 150 * v, 50, 0.8f);
            NoiseF(d, start, 1.8f * scale, 2.4f, 0.004f, 0.6f * scale, F.LP, 110, 30, 1.1f);
            Roar(d, start + 0.02f, 1.3f * scale, 0.4f, 0.45f * scale);
            Crackle(d, start + 0.05f, 1.1f * scale, (int)(30 * scale), 0.1f);
            Grains(d, start + 0.03f, 0.9f * scale, (int)(22 * scale), 250, 2500, 0.12f, false);
        }

        /// <summary>Гул пламени: полосовой шум с хаотичной огибающей.</summary>
        static void Roar(float[] d, float start, float dur, float amp, float tau)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var bq = new Biquad();
            bq.Set(F.BP, 450, 0.6f);
            float wobble = 0f;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                wobble += 0.002f * (RandS() - wobble); // медленное «дыхание» огня
                float t = i / (float)Rate;
                float am = Mathf.Clamp01(0.6f + 6f * wobble);
                d[s0 + i] += bq.Process(RandS()) * amp * am * Env(t, 0.03f, tau) * 1.6f;
            }
        }

        /// <summary>
        /// «Вжиу» рикошета: кувыркающийся снаряд — тон падает (Доплер, потеря скорости), частота и громкость
        /// «дрожат» от вращения, вокруг тона шум воздуха.
        /// </summary>
        static void RicochetWhine(float[] d, float start, float dur, float f0, float amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            double ph = 0;
            var air = new Biquad();
            float tumble = 35f + 25f * Rand01();
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float fm = 1f + 0.05f * Mathf.Sin(2f * Mathf.PI * tumble * t + 3f * Mathf.Sin(2f * Mathf.PI * 7f * t));
                float f = f0 * (0.58f + 0.42f * Mathf.Exp(-t / 0.14f)) * fm;
                ph += f / Rate;
                if ((i & 31) == 0) air.Set(F.BP, f, 7f);
                float tone = (float)(Math.Sin(ph * 2 * Math.PI) + 0.25 * Math.Sin(ph * 4 * Math.PI));
                float am = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Mathf.PI * tumble * t));
                float env = Mathf.Min(1f, t / 0.012f) * Mathf.Exp(-t / (dur * 0.45f));
                d[s0 + i] += (tone * 0.55f + air.Process(RandS()) * 2.2f * 0.6f) * am * env * amp;
            }
        }

        /// <summary>Пролёт снаряда мимо: шум воздуха с падающей частотой и колоколообразной громкостью.</summary>
        static void WhizBy(float[] d, float dur, float f0)
        {
            int n = Mathf.Min(d.Length, (int)(dur * Rate));
            var bq = new Biquad();
            double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                float f = Glide(f0, f0 * 0.3f, Mathf.SmoothStep(0f, 1f, k));
                if ((i & 31) == 0) bq.Set(F.BP, f, 3f);
                ph += f * 0.5f / Rate;
                float env = Mathf.Exp(-Mathf.Pow((k - 0.28f) / 0.16f, 2f)); // пик — момент пролёта
                d[i] += (bq.Process(RandS()) * 2f + (float)Math.Sin(ph * 2 * Math.PI) * 0.15f) * env;
            }
        }

        /// <summary>Вибромотор телефона: низкое жужжание с биением + дребезг корпуса.</summary>
        static void PhoneBuzz(float[] d, float start, float dur)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var rattle = new Biquad();
            rattle.Set(F.BP, 320, 2f);
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 165f;
                float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(4f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(6f * Mathf.PI * f * t);
                s *= 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 35f * t);
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Min(1f, (dur - t) / 0.03f);
                d[s0 + i] += (s * 0.4f + rattle.Process(RandS()) * 0.8f) * env;
            }
        }

        /// <summary>Свист падающего снаряда: тон уходит вниз, громкость нарастает (приближается).</summary>
        static void Whistle(float[] d, float dur)
        {
            int n = Mathf.Min(d.Length, (int)(dur * Rate));
            double ph = 0;
            var air = new Biquad();
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n, t = i / (float)Rate;
                float f = Glide(2600f, 700f, k) * (1f + 0.012f * Mathf.Sin(2f * Mathf.PI * 7f * t));
                ph += f / Rate;
                if ((i & 31) == 0) air.Set(F.BP, f, 6f);
                float amp = 0.08f + 0.9f * Mathf.Pow(k, 1.6f);
                d[i] += ((float)Math.Sin(ph * 2 * Math.PI) * 0.7f + air.Process(RandS()) * 1.2f) * amp;
            }
        }

        /// <summary>Саб-удар: очень низкий короткий толчок (ощущается грудью, а не слышится как тон).</summary>
        static void SubKick(float[] d, float start, float dur, float f0, float f1, float amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float k = i / (float)n;
                ph += Glide(f0, f1, k) / Rate;
                float env = Mathf.Min(1f, i / (0.002f * Rate)) * (1f - k) * (1f - k);
                d[s0 + i] += (float)Math.Sin(ph * 2 * Math.PI) * env * amp;
            }
        }

        /// <summary>Пузырёк: короткий тон, взлетающий вверх — «плюх» (рыбная тема, никаких колокольчиков).</summary>
        static void Bubble(float[] d, float start, float f, float amp)
        {
            int s0 = (int)(start * Rate), n = (int)(0.07f * Rate);
            double ph = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float k = i / (float)n;
                ph += f * (1f + 1.4f * k * k) / Rate;
                float env = Mathf.Min(1f, i / (0.003f * Rate)) * (1f - k);
                d[s0 + i] += (float)Math.Sin(ph * 2 * Math.PI) * env * amp;
            }
        }

        /// <summary>
        /// Церковный колокол: характерный набор частот (гул ×0.5, основной, малая терция ×1.2, квинта ×1.5,
        /// октава ×2, ×2.5, ×3) с биениями и очень долгим гулом.
        /// </summary>
        static void ChurchBell(float[] d, float start, float f, float amp)
        {
            Modal(d, start, f, new[] { 0.5f, 1f, 1.2f, 1.5f, 2f, 2.51f, 3.01f, 4.07f },
                  new[] { 0.7f, 1f, 0.6f, 0.35f, 0.5f, 0.25f, 0.2f, 0.12f },
                  new[] { 3.5f, 2.6f, 1.6f, 1.2f, 1.1f, 0.7f, 0.5f, 0.3f }, amp);
            Modal(d, start, f * 1.004f, new[] { 1f, 2f }, new[] { 0.5f, 0.3f }, new[] { 2.4f, 1f }, amp * 0.6f); // биения
            NoiseF(d, start, 0.03f, amp * 0.5f, 0.0003f, 0.008f, F.BP, 900, 900, 1f);                          // удар языка
        }

        /// <summary>Рык: пилообразный низкий тон с неровной амплитудой через фильтр.</summary>
        static void Growl(float[] d, float start, float dur, float f, float amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            var lp = new Biquad(); lp.Set(F.LP, 700, 1.4f);
            double ph = 0;
            float wob = 0f;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate, k = i / (float)n;
                wob += 0.003f * (RandS() - wob);
                ph += f * (1f + 0.3f * wob) * (1f - 0.25f * k) / Rate;
                float saw = (float)(2 * (ph - Math.Floor(ph)) - 1);
                float env = Mathf.Min(1f, t / 0.08f) * (1f - k * k);
                d[s0 + i] += lp.Process(saw * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 9f * t)))) * env * amp;
            }
        }

        // ================================================================== Помещение (свёртка)

        static readonly Dictionary<int, float[]> IrCache = new Dictionary<int, float[]>();

        /// <summary>
        /// Акустика арены: свёртка с синтетическим импульсным откликом — ранние отражения от стен
        /// (early штук, 6–70 мс) и диффузный хвост с затуханием tau, который глухнет со временем (поглощение воздухом).
        /// </summary>
        static void Room(float[] d, float wet, float tau, int early)
        {
            int key = Mathf.RoundToInt(tau * 1000f) * 100 + early;
            if (!IrCache.TryGetValue(key, out var ir)) IrCache[key] = ir = BuildIR(tau, early);
            var conv = Convolve(d, ir);
            for (int i = 0; i < d.Length; i++) d[i] += conv[i] * wet;
        }

        static float[] BuildIR(float tau, int early)
        {
            var r = new System.Random(4242 + early);
            int n = Mathf.CeilToInt(Mathf.Min(tau * 5f, 2.4f) * Rate);
            var ir = new float[n];
            for (int k = 0; k < early; k++)
            {
                float t = 0.006f + 0.065f * Mathf.Pow((float)r.NextDouble(), 1.3f);
                int idx = (int)(t * Rate);
                float a = 0.7f * Mathf.Exp(-t / 0.045f) * (r.NextDouble() < 0.5 ? -1f : 1f) * (0.5f + 0.5f * (float)r.NextDouble());
                // размазываем каждое отражение на ~1.5 мс со случайным знаком — без «металлического» звона гребёнки
                int smear = (int)(0.0015f * Rate);
                for (int j = 0; j < smear && idx + j < n; j++)
                    ir[idx + j] += a * (1f - j / (float)smear) * (float)(r.NextDouble() * 2 - 1) * 0.6f;
            }
            float lp = 0f;
            int s0 = (int)(0.012f * Rate);
            for (int i = s0; i < n; i++)
            {
                float t = i / (float)Rate;
                float c = Mathf.Lerp(0.7f, 0.06f, Mathf.Clamp01(t / (tau * 2f)));
                lp += c * ((float)(r.NextDouble() * 2 - 1) - lp);
                ir[i] += lp * 0.35f * Mathf.Exp(-t / tau) * Mathf.Min(1f, (t - 0.012f) / 0.02f);
            }
            double e = 0;
            foreach (var x in ir) e += x * x;
            float g = (float)(1.0 / Math.Sqrt(Math.Max(1e-9, e)));
            for (int i = 0; i < n; i++) ir[i] *= g;
            return ir;
        }

        /// <summary>Линейная свёртка через FFT. Результат обрезан до длины x.</summary>
        static float[] Convolve(float[] x, float[] h)
        {
            int n = 1;
            while (n < x.Length + h.Length) n <<= 1;
            var ar = new double[n]; var ai = new double[n];
            var br = new double[n]; var bi = new double[n];
            for (int i = 0; i < x.Length; i++) ar[i] = x[i];
            for (int i = 0; i < h.Length; i++) br[i] = h[i];
            Fft(ar, ai, false);
            Fft(br, bi, false);
            for (int i = 0; i < n; i++)
            {
                double re = ar[i] * br[i] - ai[i] * bi[i];
                double im = ar[i] * bi[i] + ai[i] * br[i];
                ar[i] = re; ai[i] = im;
            }
            Fft(ar, ai, true);
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = (float)(ar[i] / n);
            return y;
        }

        /// <summary>Итеративное БПФ (radix-2, на месте). inverse — без нормировки.</summary>
        static void Fft(double[] re, double[] im, bool inverse)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    double t = re[i]; re[i] = re[j]; re[j] = t;
                    t = im[i]; im[i] = im[j]; im[j] = t;
                }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double ang = 2 * Math.PI / len * (inverse ? 1 : -1);
                double wr = Math.Cos(ang), wi = Math.Sin(ang);
                int half = len >> 1;
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int j = 0; j < half; j++)
                    {
                        int a = i + j, b = a + half;
                        double vr = re[b] * cr - im[b] * ci, vi = re[b] * ci + im[b] * cr;
                        re[b] = re[a] - vr; im[b] = im[a] - vi;
                        re[a] += vr; im[a] += vi;
                        double ncr = cr * wr - ci * wi;
                        ci = cr * wi + ci * wr;
                        cr = ncr;
                    }
                }
            }
        }

        // ================================================================== Обработка

        /// <summary>Мягкое насыщение — «плотность» и панч, как у записи громкого звука.</summary>
        static void Saturate(float[] d, float drive)
        {
            float norm = (float)Math.Tanh(drive);
            for (int i = 0; i < d.Length; i++) d[i] = (float)Math.Tanh(d[i] * drive) / norm;
        }

        static void Normalize(float[] d, float peak)
        {
            float m = 0f;
            for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
            if (m < 1e-6f) return;
            float k = peak / m;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
        }

        static void FadeTail(float[] d, float seconds)
        {
            int n = Mathf.Min(d.Length, (int)(seconds * Rate));
            for (int i = 0; i < n; i++) d[d.Length - 1 - i] *= i / (float)n;
        }
    }
}
