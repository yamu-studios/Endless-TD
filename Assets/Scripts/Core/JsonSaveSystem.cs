using System;
using System.IO;
using UnityEngine;

namespace ETD.Core
{
    /// <summary>
    /// Small JSON save/load wrapper.
    /// Steam Auto-Cloud does not need special code. It syncs the local files this system writes.
    /// </summary>
    public static class JsonSaveSystem
    {
        public static void Save<T>(string path, T data)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(data, true);
                string tempPath = path + ".tmp";
                string backupPath = path + ".bak";

                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                    File.Copy(path, backupPath, true);

                if (File.Exists(path))
                    File.Delete(path);

                File.Move(tempPath, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveSystem] Save failed at path: {path}\n{e}");
            }
        }

        public static bool TryLoad<T>(string path, out T data)
        {
            data = default;

            try
            {
                if (!File.Exists(path))
                    return false;

                string json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                    return false;

                data = JsonUtility.FromJson<T>(json);
                return data != null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveSystem] Load failed at path: {path}\n{e}");
                return false;
            }
        }

        public static bool Exists(string path)
        {
            return File.Exists(path);
        }

        public static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[JsonSaveSystem] Delete failed at path: {path}\n{e}");
            }
        }
    }
}
