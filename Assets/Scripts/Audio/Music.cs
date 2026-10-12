// Музыка — 8-битный чиптюн, синтезированный кодом (как Sfx): два пульса (мелодия и арпеджио), треугольник (бас), шумовые барабаны.
//   • Menu   — медленный лоу-фай, 76 BPM, джазовые септаккорды, мягкая «флейта», без барабанов: меню и стартовый экран;
//   • Battle — героический «галоп» в ми миноре, 152 BPM, две части по 8 тактов: уровни.
// Переход между темами — плавный; на паузе бой звучит тише и «из-за стены» (фильтр низких частот).
// Обе темы — бесшовные петли. Свои треки: положи Assets/Resources/Music/Menu.ogg и Battle.ogg — заменят синтез.
// Громкость следует за кнопкой звука (Sfx.MasterVolume).
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CatTanks
{
    public enum MusicTrack { None, Menu, Battle }

    public class Music : MonoBehaviour
    {
        [Range(0f, 1f)] public float volume = 0.5f;
        public float fadeTime = 0.8f;
        [Tooltip("Громкость и фильтр на паузе")]
        public float pausedVolume = 0.55f;
        public float pausedCutoff = 900f;

        static Music _instance;
        AudioSource _menu, _battle;
        AudioLowPassFilter _filter;
        float[] _menuData, _battleData;
        volatile bool _renderDone;
        bool _needMenu, _needBattle;       // решаем в главном потоке, какие темы синтезировать
        MusicTrack _track = MusicTrack.None;
        bool _muffled;
        float _duck = 1f;

        const int Rate = 44100;

        // ================================================================== Управление

        /// <summary>Сменить тему. Battle при повторном входе из меню начинается сначала.</summary>
        public static void Play(MusicTrack track, bool muffled = false, float duck = 1f)
        {
            var m = Instance;
            if (track == MusicTrack.Battle && m._track != MusicTrack.Battle && m._battle.clip != null) m._battle.time = 0f;
            m._track = track;
            m._muffled = muffled;
            m._duck = duck;
        }

        static Music Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Music");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<Music>();
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            _menu = MakeSource();
            _battle = MakeSource();
            _filter = gameObject.AddComponent<AudioLowPassFilter>();
            _filter.cutoffFrequency = 22000f;

            var menuFile = Resources.Load<AudioClip>("Music/Menu");
            var battleFile = Resources.Load<AudioClip>("Music/Battle");
            if (menuFile != null) _menu.clip = menuFile;
            if (battleFile != null) _battle.clip = battleFile;
            _needMenu = menuFile == null;
            _needBattle = battleFile == null;
            if (!_needMenu && !_needBattle) { _renderDone = true; return; }

#if UNITY_WEBGL && !UNITY_EDITOR
            RenderAll();
#else
            Task.Run(RenderAll);
#endif
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0f;
            s.volume = 0f;
            s.priority = 0; // музыку не вытесняют двадцать рикошетов
            return s;
        }

        void RenderAll()
        {
            try
            {
                // фоновый поток: только чистые вычисления — никаких обращений к объектам Unity
                if (_needMenu) _menuData = Synth.Menu();
                if (_needBattle) _battleData = Synth.Battle();
            }
            catch (Exception e) { Debug.LogException(e); }
            _renderDone = true;
        }

        void Update()
        {
            if (!_renderDone) return;
            if (_menuData != null) { _menu.clip = ToClip("Music_Menu", _menuData); _menuData = null; }
            if (_battleData != null) { _battle.clip = ToClip("Music_Battle", _battleData); _battleData = null; }

            float master = Sfx.MasterVolume * volume * _duck * (_muffled ? pausedVolume : 1f);
            float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime);
            Fade(_menu, _track == MusicTrack.Menu ? master : 0f, step);
            Fade(_battle, _track == MusicTrack.Battle ? master : 0f, step);

            float cutoff = _muffled ? pausedCutoff : 22000f;
            _filter.cutoffFrequency = Mathf.Lerp(_filter.cutoffFrequency, cutoff, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        }

        static void Fade(AudioSource s, float target, float step)
        {
            if (s.clip == null) return;
            s.volume = Mathf.MoveTowards(s.volume, target, step * Mathf.Max(target, 0.3f));
            if (s.volume > 0.001f && !s.isPlaying) s.Play();
            else if (s.volume <= 0.001f && target <= 0f && s.isPlaying) s.Pause(); // вернёмся с того же места
        }

        static AudioClip ToClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ================================================================== Синтез

        static class Synth
        {
            // Soft — гладкий треугольник с мягкой атакой («флейта»), Triangle — ступенчатый, как бас NES.
            enum Wave { Pulse12, Pulse25, Pulse50, Triangle, Soft }

            // Мелодии: один токен = восьмая нота. «-» — тянуть предыдущую, «.» — пауза.

            /// <summary>Меню: медленный лоу-фай. Джазовые септаккорды по ступеням вниз, редкая мягкая мелодия, без барабанов.</summary>
            public static float[] Menu()
            {
                const float bpm = 76f;
                string[] chords = { "Fmaj7", "Em7", "Dm7", "Cmaj7", "Fmaj7", "Em7", "Dm7", "G" };
                string lead =
                    "A5 - - - G5 - E5 - | G5 - - - - - . . | F5 - - - E5 - D5 - | E5 - - - - - . . |" +
                    "A5 - - - C6 - B5 - | G5 - - - E5 - D5 - | F5 - - - A5 - G5 - | D5 - - - - - . . ";

                var song = new Song(bpm, chords.Length);
                song.Melody(lead, Wave.Soft, 0.3f);
                song.Echo(3, 0.35f);
                int[] arp = { 0, 1, 2, 3, 2, 1, 0, 1 }; // вверх-вниз по аккорду, неторопливо
                for (int bar = 0; bar < chords.Length; bar++)
                {
                    var c = Chord(chords[bar], 4);
                    int root = c[0] - 24;
                    song.Note(bar * 8, 4, root, Wave.Triangle, 0.45f, 0.95f);      // бас половинками: корень…
                    song.Note(bar * 8 + 4, 4, root + 7, Wave.Triangle, 0.4f, 0.95f); // …и квинта
                    for (int s = 0; s < 8; s++)
                        song.Note(bar * 8 + s, 1, c[arp[s] % c.Length], Wave.Pulse12, 0.05f, 0.9f);
                    song.Hat(bar * 8 + 3, 0.02f); // еле слышное «тиканье», чтобы не было совсем пусто
                    song.Hat(bar * 8 + 7, 0.02f);
                }
                return song.Mix(0.7f);
            }

            /// <summary>Бой: героический «галоп» в ми миноре (i–VI–VII–V), скачущий бас, во второй части — тарелка и подголосок.</summary>
            public static float[] Battle()
            {
                const float bpm = 152f;
                string[] chords = { "Em", "C", "D", "B", "Em", "C", "D", "B",
                                    "Am", "Em", "C", "D", "Am", "Em", "B", "B" };
                string lead =
                    "B4 - E5 - G5 - F#5 E5 | G5 - - - E5 - C5 - | D5 - F#5 - A5 - G5 F#5 | D#5 - - - B4 - . . |" +
                    "B4 - E5 - G5 - B5 - | C6 - B5 - G5 - E5 - | F#5 - A5 - D6 - C6 A5 | B5 - - - D#5 - F#5 - |" +
                    "A5 - . A5 C6 - A5 - | G5 - . G5 B5 - G5 - | E5 - G5 - C6 - B5 - | A5 - - - F#5 - D5 - |" +
                    "C6 - B5 - A5 - E5 - | G5 - F#5 - E5 - B4 - | D#5 - F#5 - B5 - A5 - | F#5 - - - D#5 - B4 - ";

                var song = new Song(bpm, chords.Length);
                song.Melody(lead, Wave.Pulse25, 0.2f);
                song.Melody(lead, Wave.Pulse50, 0.06f, transpose: -12, from: 64); // во второй части мелодию дублирует октава ниже
                song.Echo(3, 0.22f);
                for (int bar = 0; bar < chords.Length; bar++)
                {
                    var c = Chord(chords[bar], 4);
                    bool partB = bar >= 8;
                    int root = c[0] - 24;
                    // галоп: «та-тата» на каждую долю, на 2 и 4 — октавой выше
                    for (int q = 0; q < 4; q++)
                    {
                        int n = root + (q % 2 == 1 ? 12 : 0);
                        song.Note(bar * 8 + q * 2, 1, n, Wave.Triangle, 0.5f, 0.75f);
                        song.Note(bar * 8 + q * 2 + 1, 0.5f, n, Wave.Triangle, 0.45f, 0.7f);
                        song.Note(bar * 8 + q * 2 + 1.5f, 0.5f, n, Wave.Triangle, 0.45f, 0.7f);
                    }
                    // арпеджио шестнадцатыми; во второй части ярче
                    for (int s = 0; s < 16; s++)
                        song.Note(bar * 8 + s * 0.5f, 0.5f, c[s % 3] + 12, partB ? Wave.Pulse25 : Wave.Pulse12, partB ? 0.07f : 0.05f, 0.6f);
                    // барабаны
                    song.Kick(bar * 8, 0.75f);
                    song.Kick(bar * 8 + 4, 0.75f);
                    song.Kick(bar * 8 + 5, 0.4f);
                    song.Snare(bar * 8 + 2, 0.45f);
                    song.Snare(bar * 8 + 6, 0.45f);
                    if (bar % 4 == 3) { song.Snare(bar * 8 + 7, 0.3f); song.Snare(bar * 8 + 7.5f, 0.35f); } // сбивка в конце фразы
                    for (int s = 0; s < 8; s++) song.Hat(bar * 8 + s, s % 2 == 0 ? 0.06f : 0.04f, open: partB && s % 2 == 1);
                    if (bar == 0 || bar == 8) song.Crash(bar * 8, 0.18f);
                }
                return song.Mix(0.8f);
            }

            /// <summary>MIDI-ноты аккорда: "Am", "F", "Em7", "Fmaj7", "G7"… в октаве octave.</summary>
            static int[] Chord(string name, int octave)
            {
                int rootLen = name.Length > 1 && (name[1] == '#' || name[1] == 'b') ? 2 : 1;
                int root = Midi(name.Substring(0, rootLen) + octave);
                string q = name.Substring(rootLen);
                switch (q)
                {
                    case "maj7": return new[] { root, root + 4, root + 7, root + 11 };
                    case "m7": return new[] { root, root + 3, root + 7, root + 10 };
                    case "7": return new[] { root, root + 4, root + 7, root + 10 };
                    case "m": return new[] { root, root + 3, root + 7 };
                    default: return new[] { root, root + 4, root + 7 };
                }
            }

            static int Midi(string note)
            {
                int[] semis = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G
                int i = 0, n = semis[note[i++] - 'A'];
                if (note[i] == '#') { n++; i++; }
                else if (note[i] == 'b') { n--; i++; }
                return 12 * (int.Parse(note.Substring(i)) + 1) + n;
            }

            /// <summary>Песня в восьмых: позиции и длительности нот — в восьмых долях. Всё, что вылезает за конец, заворачивается в начало — петля бесшовная.</summary>
            class Song
            {
                readonly float _step;      // секунд в восьмой
                readonly float[] _main, _lead;
                readonly System.Random _rng = new System.Random(7);

                public Song(float bpm, int bars)
                {
                    _step = 30f / bpm;
                    int len = Mathf.RoundToInt(bars * 8 * _step * Rate);
                    _main = new float[len];
                    _lead = new float[len];
                }

                /// <summary>Мелодия в отдельную дорожку (на неё потом ложится эхо). from — с какой восьмой начинать.</summary>
                public void Melody(string text, Wave wave, float amp, int transpose = 0, int from = 0)
                {
                    var tokens = new List<string>(text.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries));
                    for (int i = from; i < tokens.Count; i++)
                    {
                        if (tokens[i] == "-" || tokens[i] == ".") continue;
                        int len = 1;
                        while (i + len < tokens.Count && tokens[i + len] == "-") len++;
                        Note(i, len, Midi(tokens[i]) + transpose, wave, amp, 0.9f, lead: true);
                    }
                }

                /// <summary>Эхо на дорожку мелодии: задержка в восьмых, три повтора.</summary>
                public void Echo(int eighths, float feedback)
                {
                    int delay = Mathf.RoundToInt(eighths * _step * Rate);
                    var dry = (float[])_lead.Clone();
                    for (int k = 1; k <= 3; k++)
                    {
                        float g = Mathf.Pow(feedback, k);
                        for (int i = 0; i < dry.Length; i++) _lead[(i + delay * k) % dry.Length] += dry[i] * g;
                    }
                }

                public void Note(float at, float len, int midi, Wave wave, float amp, float gate, bool lead = false)
                {
                    var buf = lead ? _lead : _main;
                    float f = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
                    int s0 = Mathf.RoundToInt(at * _step * Rate);
                    float dur = len * _step * gate;
                    bool soft = wave == Wave.Soft;
                    float attack = soft ? 0.03f : 0.004f, release = soft ? 0.15f : 0.04f;
                    int n = Mathf.RoundToInt((dur + release) * Rate);
                    double phase = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float vib = lead && t > 0.18f ? 1f + 0.006f * Mathf.Sin(t * 2f * Mathf.PI * 5.5f) : 1f;
                        phase += f * vib / Rate;
                        float p = (float)(phase - Math.Floor(phase));
                        float v;
                        switch (wave)
                        {
                            case Wave.Pulse12: v = p < 0.125f ? 1f : -0.6f; break;
                            case Wave.Pulse25: v = p < 0.25f ? 1f : -0.6f; break;
                            case Wave.Pulse50: v = p < 0.5f ? 1f : -1f; break;
                            case Wave.Soft: v = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                            default: v = Mathf.Round((4f * Mathf.Abs(p - 0.5f) - 1f) * 8f) / 8f; break; // ступенчатый треугольник, как в NES
                        }
                        float env = Mathf.Min(1f, t / attack)
                                    * Mathf.Lerp(soft ? 0.8f : 0.65f, 1f, Mathf.Exp(-t / 0.08f))  // спад к sustain
                                    * (t > dur ? Mathf.Max(0f, 1f - (t - dur) / release) : 1f);   // релиз
                        Add(buf, s0 + i, v * env * amp);
                    }
                }

                public void Kick(float at, float amp)
                {
                    int s0 = Mathf.RoundToInt(at * _step * Rate), n = (int)(0.16f * Rate);
                    double phase = 0;
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        phase += Mathf.Lerp(45f, 160f, Mathf.Exp(-t / 0.03f)) / Rate;
                        Add(_main, s0 + i, Mathf.Sin((float)phase * 2f * Mathf.PI) * Mathf.Exp(-t / 0.05f) * amp);
                    }
                }

                public void Snare(float at, float amp)
                {
                    int s0 = Mathf.RoundToInt(at * _step * Rate), n = (int)(0.14f * Rate);
                    for (int i = 0; i < n; i++)
                    {
                        float t = i / (float)Rate;
                        float noise = (float)(_rng.NextDouble() * 2 - 1);
                        float tone = Mathf.Sin(t * 2f * Mathf.PI * 190f) * Mathf.Exp(-t / 0.03f);
                        Add(_main, s0 + i, (noise * 0.7f * Mathf.Exp(-t / 0.045f) + tone * 0.5f) * amp);
                    }
                }

                /// <summary>Хэт: «тс» (закрытый) или «тсссь» (открытый).</summary>
                public void Hat(float at, float amp, bool open = false) => Noise(at, amp, open ? 0.06f : 0.012f);

                /// <summary>Тарелка на начале части.</summary>
                public void Crash(float at, float amp) => Noise(at, amp, 0.45f);

                void Noise(float at, float amp, float tau)
                {
                    int s0 = Mathf.RoundToInt(at * _step * Rate), n = (int)(tau * 5f * Rate);
                    float prev = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float x = (float)(_rng.NextDouble() * 2 - 1);
                        float hp = x - prev; // грубый фильтр высоких частот — «тсс» вместо «шшш»
                        prev = x;
                        Add(_main, s0 + i, hp * Mathf.Exp(-i / (float)Rate / tau) * amp);
                    }
                }

                void Add(float[] buf, int i, float v) => buf[(i % buf.Length + buf.Length) % buf.Length] += v;

                /// <summary>Сведение. brightness 0…1 — насколько открыт общий фильтр (меньше — мягче и темнее).</summary>
                public float[] Mix(float brightness)
                {
                    var outBuf = new float[_main.Length];
                    float peak = 0f, lp = 0f, k = Mathf.Lerp(0.15f, 0.6f, brightness);
                    // два прохода фильтра, чтобы петля и на стыке была гладкой
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < outBuf.Length; i++)
                        {
                            float x = _main[i] + _lead[i];
                            lp += (x - lp) * k;
                            outBuf[i] = lp;
                            if (pass == 1) peak = Mathf.Max(peak, Mathf.Abs(lp));
                        }
                    float gain = peak > 0f ? 0.8f / peak : 1f;
                    for (int i = 0; i < outBuf.Length; i++) outBuf[i] *= gain;
                    return outBuf;
                }
            }
        }
    }
}
