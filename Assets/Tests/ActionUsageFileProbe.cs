#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.IO;
using UnityEngine;

public static class ActionUsageFileProbe
{
    [Serializable] sealed class Legacy
    {
        public int version = 1;
        public long[] knight = new long[16], oracle = new long[16];
        public long knightDeclared = 5, knightLies = 2, oracleDeclared = 4, oracleLies = 1;
    }
    public static void CheckRecovery(Action<bool, string> check)
    {
        LocalActionUsage.UseTestStorage(Guid.NewGuid().ToString("N"));
        try
        {
            check(Path.IsPathRooted(LocalActionUsage.FilePath), "Statistics path is absolute even when Unity persistent path is unavailable");
            var legacy = new Legacy();
            legacy.knight[(int)ActionType.LightAttack] = 7;
            legacy.knight[(int)ActionType.HeavyAttack] = 2;
            legacy.knight[(int)ActionType.Defense] = 3;
            legacy.oracle[(int)ActionType.Bolt] = 5;
            LocalActionUsage.SeedLegacyTestStorage(JsonUtility.ToJson(legacy));
            var migrated = LocalActionUsage.Read("knight");
            check(migrated.attack == 9 && migrated.defense == 3 && migrated.declaredSlots == 5 && migrated.mismatchedSlots == 2, "Legacy PlayerPrefs counts and lies migrate without reset");
            check(File.Exists(LocalActionUsage.FilePath) && LocalActionUsage.LastSaveSucceeded, "Migration writes a durable JSON file");
            check(LocalActionUsage.Read("oracle").attack == 5, "Migration preserves the other class history");
            legacy.knight[(int)ActionType.LightAttack] = 999;
            LocalActionUsage.SeedLegacyTestStorage(JsonUtility.ToJson(legacy));
            check(LocalActionUsage.Read("knight").attack == 9, "Existing JSON takes precedence over stale legacy preferences");
            LocalActionUsage.Record("knight", ActionType.LightAttack);
            check(File.Exists(LocalActionUsage.FilePath + ".bak") && !File.Exists(LocalActionUsage.FilePath + ".tmp"), "Atomic save retains a backup and consumes temporary file");
            LocalActionUsage.ReloadTestStorage();
            check(LocalActionUsage.Read("knight").attack == 10, "File reload retains incremented counts");
            File.WriteAllText(LocalActionUsage.FilePath, "{broken");
            LocalActionUsage.ReloadTestStorage();
            check(LocalActionUsage.Read("knight").attack == 9 && LocalActionUsage.LastSaveSucceeded, "Corrupt primary recovers previous good file rather than legacy data");
            check(Directory.GetFiles(Path.GetDirectoryName(LocalActionUsage.FilePath), "*.corrupt-*").Length == 1, "Corrupt file is preserved for inspection");
            LocalActionUsage.ReloadTestStorage();
            check(LocalActionUsage.Read("knight").attack == 9, "Recovered primary remains valid after reload");
            string before = File.ReadAllText(LocalActionUsage.FilePath);
            Directory.CreateDirectory(LocalActionUsage.FilePath + ".tmp");
            try
            {
                LocalActionUsage.Record("knight", ActionType.LightAttack);
                check(!LocalActionUsage.LastSaveSucceeded && File.ReadAllText(LocalActionUsage.FilePath) == before, "Write failure leaves existing statistics intact");
            }
            finally { Directory.Delete(LocalActionUsage.FilePath + ".tmp"); }
            LocalActionUsage.Record("knight", ActionType.LightAttack);
            check(LocalActionUsage.LastSaveSucceeded, "Later successful save includes pending in-memory increments");
            LocalActionUsage.ReloadTestStorage();
            check(LocalActionUsage.Read("knight").attack == 11, "Pending and subsequent increments survive reload");
        }
        finally { LocalActionUsage.ClearTestStorage(); }
    }

    // Three separate player launches prove persistence across process exit, not just a cache reset.
    public static bool TryRun(string[] args, string output)
    {
        int index = Array.IndexOf(args, "-usage-file-probe");
        if (index < 0) return false;
        Directory.CreateDirectory(output);
        string mode = index + 1 < args.Length ? args[index + 1] : "";
        int exitCode = 0;
        try
        {
            int idIndex = Array.IndexOf(args, "-usage-storage-id");
            LocalActionUsage.UseTestStorage(args[idIndex + 1]);
            if (mode == "seed")
            {
                LocalActionUsage.ClearTestStorage();
                LocalActionUsage.UseTestStorage(args[idIndex + 1]);
                LocalActionUsage.Record("knight", ActionType.LightAttack);
                LocalActionUsage.Record("knight", ActionType.LightAttack);
                LocalActionUsage.Record("knight", ActionType.Defense);
                LocalActionUsage.Record("oracle", ActionType.Bolt);
                LocalActionUsage.Record("oracle", ActionType.Ward);
                LocalActionUsage.Record("oracle", ActionType.Ward);
                LocalActionUsage.RecordClaims("knight", new[] { 0, 8, 4 }, new[] { 0, 4, -1 });
                LocalActionUsage.RecordClaims("oracle", new[] { 10, 14 }, new[] { 11, 14 });
                if (!LocalActionUsage.LastSaveSucceeded) throw new Exception("Seed was not saved.");
                File.Copy(LocalActionUsage.FilePath, Path.Combine(output, "sample-statistics.json"), true);
            }
            else if (mode == "reload" || mode == "verify")
            {
                var knight = LocalActionUsage.Read("knight"); var oracle = LocalActionUsage.Read("oracle");
                if (knight.attack != (mode == "reload" ? 2 : 3) || knight.defense != 1 || knight.declaredSlots != 2 || knight.mismatchedSlots != 1 || oracle.attack != 1 || oracle.defense != 2 || oracle.declaredSlots != 2 || oracle.mismatchedSlots != 1)
                    throw new Exception("Statistics did not survive process restart.");
                if (mode == "reload")
                {
                    LocalActionUsage.Record("knight", ActionType.HeavyAttack);
                    if (!LocalActionUsage.LastSaveSucceeded) throw new Exception("Increment was not saved.");
                }
                else LocalActionUsage.ClearTestStorage();
            }
            else throw new Exception("Unknown restart probe mode.");
            File.WriteAllText(Path.Combine(output, mode + ".txt"), "PASS: " + mode + " across real player process launches");
            File.WriteAllText(Path.Combine(output, "production-file-path.txt"), Path.Combine(LocalActionUsage.StorageDirectory, "action-usage.json"));
        }
        catch (Exception e)
        {
            exitCode = 1;
            File.WriteAllText(Path.Combine(output, mode + ".txt"), "FAILED: " + e);
        }
        Application.Quit(exitCode);
        return true;
    }
}
#endif
