using System;
using System.IO;
using System.Text;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// Only this device's player is persisted. Opponent snapshots are transient Photon data.
public static class LocalActionUsage
{
    public const string SnapshotKey = "actionUsageSnapshotV2";
    const string SaveKey = "Battle.ActionUsage.V1";
    static string preferenceKey = SaveKey;
    static string testFilePath;
    static string storageDirectory;
    static bool primaryFileValid;
    public static string StorageDirectory
    {
        get
        {
            if (storageDirectory != null) return storageDirectory;
            string root = Application.persistentDataPath;
            // This project's company name ends in a space; some Windows builds return an empty Unity path.
            // Keep PlayerPrefs identity unchanged for migration and use a stable user-local fallback.
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
                root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPredictedYouPredictedMe");
            storageDirectory = Path.Combine(root, "Statistics");
            return storageDirectory;
        }
    }
    public static string FilePath => testFilePath ?? Path.Combine(StorageDirectory, "action-usage.json");
    public static bool LastSaveSucceeded { get; private set; }
    [Serializable] sealed class History
    {
        public int version = 1;
        public long[] knight = new long[16];
        public long[] oracle = new long[16];
        public long knightDeclared, knightLies, oracleDeclared, oracleLies;
    }
    [Serializable] public sealed class Snapshot
    {
        public int version = 2, entryToken;
        public string characterId;
        public long attack, defense, declaredSlots, mismatchedSlots;
        public long Total => attack + defense;
        public bool HasHistory => Total > 0 || declaredSlots > 0;
    }
    static History history;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession()
    {
        history = null; primaryFileValid = false; LastSaveSucceeded = false;
        preferenceKey = SaveKey; testFilePath = null; storageDirectory = null;
    }
    public static int Category(string characterId, ActionType action)
    {
        if (characterId != "knight" && characterId != "oracle") return -1;
        if (characterId == "knight")
        {
            if (action == ActionType.LightAttack || action == ActionType.HeavyAttack || action == ActionType.LowAttack) return 0;
            if (action == ActionType.Parry || action == ActionType.Defense) return 1;
        }
        else
        {
            if (action == ActionType.Bolt || action == ActionType.Rift) return 0;
            if (action == ActionType.Shift || action == ActionType.Fade || action == ActionType.Ward) return 1;
        }
        return -1;
    }
    static void Load()
    {
        if (history != null) return;
        history = ReadFile(FilePath);
        primaryFileValid = history != null;
        if (history == null) history = ReadFile(FilePath + ".bak");
        // Migrate the old local record only when no usable file exists. Keep the old key as a safety copy.
        if (history == null) history = Parse(PlayerPrefs.GetString(preferenceKey, ""));
        if (history == null) history = new History();
        history.knight = Normalize(history.knight); history.oracle = Normalize(history.oracle);
        history.knightDeclared = Math.Max(0, Math.Min(1000000000000L, history.knightDeclared));
        history.oracleDeclared = Math.Max(0, Math.Min(1000000000000L, history.oracleDeclared));
        history.knightLies = Math.Max(0, Math.Min(history.knightDeclared, history.knightLies));
        history.oracleLies = Math.Max(0, Math.Min(history.oracleDeclared, history.oracleLies));
        if (!primaryFileValid) Save();
    }
    static History Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var value = JsonUtility.FromJson<History>(json);
            return value != null && value.version == 1 && value.knight != null && value.knight.Length == 16 && value.oracle != null && value.oracle.Length == 16 ? value : null;
        }
        catch (ArgumentException) { return null; }
    }
    static History ReadFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var result = new FileInfo(path).Length <= 65536 ? Parse(File.ReadAllText(path, Encoding.UTF8)) : null;
            if (result == null) Debug.LogWarning("Invalid action usage file; trying recovery: " + path);
            return result;
        }
        catch (Exception e) when (IsFileError(e))
        {
            Debug.LogWarning("Action usage file could not be read: " + path + " / " + e.Message);
            return null;
        }
    }
    static bool IsFileError(Exception e) => e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException;
    static long[] Normalize(long[] counts)
    {
        var result = new long[16];
        if (counts != null) for (int i = 0; i < Math.Min(counts.Length, result.Length); i++) result[i] = Math.Max(0, Math.Min(1000000000000L, counts[i]));
        return result;
    }
    public static bool Record(string characterId, ActionType action)
    {
        if (Category(characterId, action) < 0) return false;
        Load(); var counts = characterId == "knight" ? history.knight : history.oracle;
        counts[(int)action] = Math.Min(1000000000000L, counts[(int)action] + 1);
        Save();
        return true;
    }
    static void Save()
    {
        LastSaveSucceeded = false;
        string path = FilePath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Flush a complete temporary file before atomically replacing the last good record.
            byte[] bytes = new UTF8Encoding(false).GetBytes(JsonUtility.ToJson(history, true));
            using (var stream = new FileStream(path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path) && primaryFileValid) File.Replace(path + ".tmp", path, path + ".bak");
            else
            {
                // Do not replace a usable backup with a corrupt primary during recovery.
                if (File.Exists(path)) File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                File.Move(path + ".tmp", path);
            }
            primaryFileValid = true;
            LastSaveSucceeded = true;
        }
        catch (Exception e) when (IsFileError(e))
        {
            Debug.LogWarning("Action usage could not be saved; previous file retained: " + path + " / " + e.Message);
        }
    }
    public static void RecordClaims(string characterId, int[] actual, int[] claims)
    {
        if ((characterId != "knight" && characterId != "oracle") || actual == null || claims == null) return;
        Load(); long declared = 0, lies = 0;
        for (int i = 0; i < Math.Min(actual.Length, claims.Length); i++)
        {
            if (claims[i] < 0 || claims[i] > (int)ActionType.Sight) continue;
            declared++; if (actual[i] != claims[i]) lies++;
        }
        if (declared == 0) return;
        if (characterId == "knight") { history.knightDeclared += declared; history.knightLies += lies; }
        else { history.oracleDeclared += declared; history.oracleLies += lies; }
        Save();
    }
    public static Snapshot Read(string characterId, int token = 0)
    {
        Load(); var result = new Snapshot { characterId = characterId, entryToken = token };
        var counts = characterId == "knight" ? history.knight : history.oracle;
        for (int i = 0; i < counts.Length; i++)
        {
            int category = Category(characterId, (ActionType)i);
            if (category == 0) result.attack += counts[i];
            else if (category == 1) result.defense += counts[i];
        }
        result.declaredSlots = characterId == "knight" ? history.knightDeclared : history.oracleDeclared;
        result.mismatchedSlots = characterId == "knight" ? history.knightLies : history.oracleLies;
        return result;
    }
    public static long ReadActionCount(string characterId, ActionType action)
    {
        if (Category(characterId, action) < 0) return 0;
        Load(); return (characterId == "knight" ? history.knight : history.oracle)[(int)action];
    }
    public static string CharacterId(Player player)
    {
        if (player == null) return "";
        if (player.CustomProperties.TryGetValue(CharacterSelectPhotonKeys.SelectedCharacterId, out object id) && id is string name && (name == "knight" || name == "oracle")) return name;
        if (player.CustomProperties.TryGetValue("classIndex", out object index) && index is int value) return value == 0 ? "knight" : value == 1 ? "oracle" : "";
        return "";
    }
    public static int EntryToken(Player player) => player != null && player.CustomProperties.TryGetValue("sceneTransitionToken", out object token) && token is int value ? value : 0;
    public static string SerializeEntrySnapshot() => JsonUtility.ToJson(Read(CharacterId(PhotonNetwork.LocalPlayer), EntryToken(PhotonNetwork.LocalPlayer)));
    public static Snapshot ReadPeer(Player player, int expectedToken)
    {
        if (player == null || !player.CustomProperties.TryGetValue(SnapshotKey, out object raw) || !(raw is string json) || json.Length > 1024) return null;
        Snapshot result;
        try { result = JsonUtility.FromJson<Snapshot>(json); } catch (ArgumentException) { return null; }
        if (result == null || result.version != 2 || result.entryToken != expectedToken || result.characterId != CharacterId(player)) return null;
        if (result.attack < 0 || result.defense < 0 || result.attack > 16000000000000L || result.defense > 16000000000000L || result.declaredSlots < 0 || result.declaredSlots > 16000000000000L || result.mismatchedSlots < 0 || result.mismatchedSlots > result.declaredSlots) return null;
        return result;
    }
    // Attack and defense share a denominator. Lies use declared slots independently.
    public static int[] PercentageTenths(Snapshot snapshot)
    {
        var result = new int[3]; if (snapshot == null) return result;
        if (snapshot.Total > 0) { result[0] = (int)((snapshot.attack * 1000 + snapshot.Total / 2) / snapshot.Total); result[1] = 1000 - result[0]; }
        if (snapshot.declaredSlots > 0) result[2] = (int)((snapshot.mismatchedSlots * 1000 + snapshot.declaredSlots / 2) / snapshot.declaredSlots);
        return result;
    }
#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
    public static void UseTestStorage(string id)
    {
        id = Guid.Parse(id).ToString("N");
        preferenceKey = SaveKey + ".Test." + id;
        testFilePath = Path.Combine(StorageDirectory, "Tests", id, "action-usage.json");
        history = null; primaryFileValid = false; LastSaveSucceeded = false;
    }
    public static void ReloadTestStorage() { if (testFilePath != null) { history = null; primaryFileValid = false; } }
    public static void SeedLegacyTestStorage(string json)
    {
        if (testFilePath == null) throw new InvalidOperationException("Test storage must be selected first.");
        PlayerPrefs.SetString(preferenceKey, json); PlayerPrefs.Save(); ReloadTestStorage();
    }
    public static void ClearTestStorage()
    {
        if (testFilePath == null) return;
        PlayerPrefs.DeleteKey(preferenceKey); PlayerPrefs.Save();
        string directory = Path.GetDirectoryName(testFilePath);
        if (Directory.Exists(directory))
        {
            foreach (string path in Directory.GetFiles(directory, "action-usage.json*")) File.Delete(path);
            Directory.Delete(directory);
        }
        ResetSession();
    }
#endif
}
