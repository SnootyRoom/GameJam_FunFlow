// Поиск ассетов в редакторе — для автоподстановки пустых полей в OnValidate (GameFlow, EntitySpawner, GameUI).
// Одна реализация вместо копий в каждом компоненте. В сборке игры не существует.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CatTanks
{
    public static class EditorAssetLookup
    {
        /// <summary>Первый ассет типа T в проекте (или null).</summary>
        public static T FindFirst<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        /// <summary>Все ассеты типа T в проекте.</summary>
        public static T[] FindAll<T>() where T : Object
        {
            var guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);
            var result = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++) result[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return result;
        }

        /// <summary>Подставить ассет в пустое поле. true — если что-то подставили (компонент надо пометить изменённым).</summary>
        public static bool FillIfEmpty<T>(ref T field) where T : Object
        {
            if (field != null) return false;
            field = FindFirst<T>();
            return field != null;
        }
    }
}
#endif
