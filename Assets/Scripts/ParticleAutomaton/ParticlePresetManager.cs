using System;
using System.IO;
using UnityEngine;

namespace ParticleAutomaton
{
    public static class ParticlePresetManager
    {
        static string Dir      => Path.Combine(Application.persistentDataPath, "ParticleAutomaton", "Presets");
        static string FilePath(string name) => Path.Combine(Dir, name + ".json");

        public static string[] ListAll()
        {
            Directory.CreateDirectory(Dir);
            SeedBuiltins();

            var files = Directory.GetFiles(Dir, "*.json");
            var names = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
                names[i] = Path.GetFileNameWithoutExtension(files[i]);
            Array.Sort(names, StringComparer.OrdinalIgnoreCase);
            return names;
        }

        public static bool Load(string name, out ParticleAutomatonConfig cfg)
        {
            var path = FilePath(name);
            if (!File.Exists(path)) { cfg = null; return false; }

            // Construct with field initializers first so missing JSON keys keep their defaults.
            cfg = new ParticleAutomatonConfig();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), cfg);
            return true;
        }

        public static void Save(string name, ParticleAutomatonConfig cfg)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath(name), JsonUtility.ToJson(cfg, prettyPrint: true));
        }

        public static void Delete(string name)
        {
            var path = FilePath(name);
            if (File.Exists(path)) File.Delete(path);
        }

        public static bool IsBuiltin(string name) =>
            Array.IndexOf(BuiltinPresets.Names, name) >= 0;

        public static bool ResetBuiltin(string name)
        {
            var cfg = BuiltinPresets.Get(name);
            if (cfg == null) return false;
            Save(name, cfg);
            return true;
        }

        static void SeedBuiltins()
        {
            foreach (var name in BuiltinPresets.Names)
                if (!File.Exists(FilePath(name)))
                    Save(name, BuiltinPresets.Get(name));
        }
    }
}
