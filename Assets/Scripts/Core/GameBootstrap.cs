// Если в сцене есть LevelBuilder, но нет GameManager — создаём объект Game с GameManager, эффектами и UI.
// Так игровая сцена остаётся почти пустой и меньше конфликтов в git, когда её трогают несколько человек.
// В сцене без LevelBuilder (меню, песочница для врагов) ничего не создаётся.
using UnityEngine;

namespace CatTanks
{
    static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;
            if (Object.FindAnyObjectByType<LevelBuilder>() == null) return;

            var go = new GameObject("Game");
            go.AddComponent<GameManager>();
            go.AddComponent<Feedback>();
            go.AddComponent<ComboSystem>();
            go.AddComponent<LootSystem>();
            go.AddComponent<HudView>();
            go.AddComponent<ScreensView>();
            go.AddComponent<Sfx>();
        }
    }
}
