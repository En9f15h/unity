using System;
using System.IO;
using System.Text;
using UnityEngine;

// Local device history, separate from per-character action/claim statistics.
public static class LocalMatchHistory
{
    [Serializable] public sealed class Record
    {
        public int version = 1;
        public long wins, losses, draws;
        public string[] recentMatchIds = new string[0];
    }
    public static string FilePath => testPath ?? Path.Combine(LocalActionUsage.StorageDirectory, "match-history.json");
    static string testPath;
    static Record ReadFile(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > 65536) return null;
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (!json.Contains("\"version\"") || !json.Contains("\"wins\"") || !json.Contains("\"losses\"") || !json.Contains("\"draws\"") || !json.Contains("\"recentMatchIds\"")) return null;
            var value = JsonUtility.FromJson<Record>(json);
            return value != null && value.version == 1 && value.wins >= 0 && value.losses >= 0 && value.draws >= 0 &&
                value.wins <= 1000000000000L && value.losses <= 1000000000000L && value.draws <= 1000000000000L &&
                value.recentMatchIds != null && value.recentMatchIds.Length <= 128 ? value : null;
        }
        catch (ArgumentException) { return null; }
    }
    public static Record Read()
    {
        try { return ReadFile(FilePath) ?? ReadFile(FilePath + ".bak") ?? new Record(); }
        catch (Exception e) when (IsFileError(e)) { Debug.LogWarning("Cannot read match history: " + e.Message); return new Record(); }
    }
    static bool IsFileError(Exception e) => e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException;
    public static bool RecordResult(string matchId, int winnerActor, int localActor)
    {
        if (string.IsNullOrEmpty(matchId) || localActor <= 0) return false;
        string path = FilePath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Multiple local game windows share this history. Serialize read + increment + replace.
            using (var gate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var primary = ReadFile(path);
                var value = primary ?? ReadFile(path + ".bak") ?? new Record();
                if (Array.IndexOf(value.recentMatchIds, matchId) >= 0) return true;
                if (winnerActor <= 0) value.draws = Math.Min(1000000000000L, value.draws + 1);
                else if (winnerActor == localActor) value.wins = Math.Min(1000000000000L, value.wins + 1);
                else value.losses = Math.Min(1000000000000L, value.losses + 1);
                int keep = Math.Min(127, value.recentMatchIds.Length);
                var ids = new string[keep + 1];
                Array.Copy(value.recentMatchIds, Math.Max(0, value.recentMatchIds.Length - keep), ids, 0, keep);
                ids[keep] = matchId; value.recentMatchIds = ids;
                byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(value, true));
                using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (primary != null) File.Replace(path + ".tmp", path, path + ".bak");
                else
                {
                    if (File.Exists(path)) File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    File.Move(path + ".tmp", path);
                }
            }
            return true;
        }
        catch (Exception e) when (IsFileError(e)) { Debug.LogWarning("Cannot save match history; will retry: " + e.Message); return false; }
    }
#if UNITY_EDITOR || CODEX_MATCH_FEATURES
    public static void SetTestPath(string path) => testPath = path;
#endif
}
