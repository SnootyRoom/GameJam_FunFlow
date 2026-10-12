// Звёзды за уровень. Главная цель — доехать до рыбы; остальные звёзды — за то, КАК проехал.
// Условия задаются в LevelConfig (по умолчанию: дойти до выхода / раздавить всех жуков / без попаданий).
// Считает всё сам по событиям уровня; HUD спрашивает текущее состояние, баннер — итог.
using System;
using UnityEngine;

namespace CatTanks
{
    public enum StarRule
    {
        ReachExit,   // доехать до рыбы (выдаётся всегда при прохождении)
        KillAll,     // раздавить всех жуков на уровне
        NoDamage,    // ни одного попадания по коту
        UnderPar,    // успеть за контрольное время (зависит от длины пути)
    }

    public enum StarState { Possible, Earned, Lost }

    public class LevelStars
    {
        public readonly StarRule[] Rules;
        public readonly float ParTime;     // контрольное время (для UnderPar), секунды игрового времени
        public bool[] Result { get; private set; }   // итог — после Complete()
        public int EarnedCount { get; private set; }

        readonly LevelContext _ctx;
        readonly float _startTime;
        int _lastHp;
        bool _damaged, _completed;

        public LevelStars(LevelContext ctx, StarRule[] rules, float parTime)
        {
            _ctx = ctx;
            Rules = rules ?? Array.Empty<StarRule>();
            ParTime = parTime;
            _startTime = Time.time; // игровое время: пауза не считается
            if (ctx.PlayerHealth != null)
            {
                _lastHp = ctx.PlayerHealth.Hp;
                ctx.PlayerHealth.OnHpChanged += HandleHp; // ctx живёт ровно один уровень — отписка не нужна
            }
        }

        void HandleHp(int hp, int max)
        {
            if (hp < _lastHp) _damaged = true;
            _lastHp = hp;
        }

        public float Elapsed => Time.time - _startTime;

        /// <summary>Состояние звезды прямо сейчас — для HUD.</summary>
        public StarState State(int i)
        {
            if (Result != null) return Result[i] ? StarState.Earned : StarState.Lost;
            switch (Rules[i])
            {
                case StarRule.ReachExit: return StarState.Possible;
                case StarRule.KillAll: return _ctx.AliveEnemyCount() == 0 ? StarState.Earned : StarState.Possible;
                case StarRule.NoDamage: return _damaged ? StarState.Lost : StarState.Possible;
                case StarRule.UnderPar: return Elapsed > ParTime ? StarState.Lost : StarState.Possible;
                default: return StarState.Possible;
            }
        }

        /// <summary>Уровень пройден: фиксируем итог.</summary>
        public bool[] Complete()
        {
            if (_completed) return Result;
            _completed = true;
            var r = new bool[Rules.Length];
            for (int i = 0; i < r.Length; i++)
            {
                var s = State(i);
                // «пока не потеряна» (без попаданий, в срок) — значит заработана; зачистка — только если реально все мертвы
                r[i] = Rules[i] == StarRule.ReachExit || (Rules[i] == StarRule.KillAll ? s == StarState.Earned : s != StarState.Lost);
                if (r[i]) EarnedCount++;
            }
            Result = r;
            return r;
        }

        public static string Label(StarRule rule)
        {
            switch (rule)
            {
                case StarRule.ReachExit: return "FISH";
                case StarRule.KillAll: return "ALL BUGS";
                case StarRule.NoDamage: return "NO HITS";
                case StarRule.UnderPar: return "FAST";
                default: return "";
            }
        }
    }
}
