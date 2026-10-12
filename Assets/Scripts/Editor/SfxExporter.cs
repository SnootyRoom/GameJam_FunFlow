// Меню CatTanks → Export SFX to WAV: сохраняет все синтезированные звуки в папку SfxPreview/ рядом с Assets.
// Удобно послушать в плеере, отдать звуковику как референс или заменить своими (Sfx.overrides).
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatTanks.EditorTools
{
    public static class SfxExporter
    {
        [MenuItem("CatTanks/Export SFX to WAV")]
        static void Export()
        {
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "SfxPreview");
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "*.wav")) File.Delete(old); // нумерация могла смениться
            int files = 0;
            foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
                for (int v = 0; v < Sfx.Variants(id); v++)
                {
                    WriteWav(Path.Combine(dir, $"{(int)id:00}_{id}_{v}.wav"), Sfx.Render(id, v));
                    files++;
                }
            WriteWav(Path.Combine(dir, "98_BulletLoop.wav"), Sfx.RenderBulletLoop());
            WriteWav(Path.Combine(dir, "99_EngineLoop.wav"), Sfx.RenderEngineLoop());
            Debug.Log($"[SfxExporter] {files + 2} файлов → {dir}");
            EditorUtility.RevealInFinder(dir);
        }

        static void WriteWav(string path, float[] data)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int rate = Sfx.Rate, bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var s in data) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767));
            }
        }
    }
}
