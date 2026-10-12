// Номер уровня и счётчик живых врагов.
using UnityEngine;
using UnityEngine.UI;

namespace CatTanks.UI
{
    public class LevelInfoWidget : MonoBehaviour, IHudWidget
    {
        public Text level;
        public Text enemies;
        [Tooltip("{0} — число")]
        public string levelFormat = "Level {0}";
        public string enemiesFormat = "Enemies: {0}";

        LevelContext _ctx;

        public void Bind(HudContext hud)
        {
            if (_ctx != null) _ctx.EnemyCountChanged -= SetEnemies;
            _ctx = hud.Level;
            if (_ctx == null) return;
            _ctx.EnemyCountChanged += SetEnemies;
            if (level != null) level.text = string.Format(levelFormat, _ctx.LevelNumber);
            SetEnemies(_ctx.AliveEnemyCount());
        }

        void SetEnemies(int n)
        {
            if (enemies != null) enemies.text = string.Format(enemiesFormat, n);
        }
    }
}
