// Отладочные пункты меню редактора для Play Mode.
using UnityEditor;
using UnityEngine;

namespace CatTanks.EditorTools
{
    public static class DebugMenu
    {
        [MenuItem("CatTanks/Debug/Spawn All Enemy Types")]
        static void SpawnShowcase()
        {
            if (!Application.isPlaying || Game.Manager == null) { Debug.LogWarning("Только в Play Mode, во время игры."); return; }
            Game.Manager.DebugSpawnShowcase();
        }

        [MenuItem("CatTanks/Debug/Give Nut Charge")]
        static void GiveNut()
        {
            if (Game.Player != null && Game.Player.Ultimate != null) Game.Player.Ultimate.DebugAddCharge();
        }

        [MenuItem("CatTanks/Debug/Start Run")]
        static void StartRun()
        {
            if (Game.Manager != null) Game.Manager.StartRun();
        }

        [MenuItem("CatTanks/Debug/Spawn All Enemy Types", true)]
        [MenuItem("CatTanks/Debug/Give Nut Charge", true)]
        [MenuItem("CatTanks/Debug/Start Run", true)]
        static bool Validate() => Application.isPlaying;
    }
}
