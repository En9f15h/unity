using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameSceneStartSync : MonoBehaviourPunCallbacks
{
    public static GameSceneStartSync Instance;

    [Header("Beat Sync")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private int leadInBeats = 4;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;

    [Header("Character Roots")]
    [SerializeField] private GameObject myCharacterRoot;
    [SerializeField] private GameObject enemyCharacterRoot;

    [Header("Enable On Start")]
    [SerializeField] private GameObject[] objectsEnableOnStart;

    private const string PROP_SCENE_READY = "GameSceneReady";
    private const string ROOM_PROP_BEAT_START_TS = "beatStartTs";
    private const string ROOM_PROP_BPM = "beatBpm";
    private const string ROOM_PROP_BATTLE_BGM_INDEX = "battleBgmIndex";

    private bool localSceneReadySent = false;
    private bool waitRoutineStarted = false;
    private bool beatStartReady = false;
    private bool gameStartedOnBeat = false;
    private bool bgmScheduled = false;

    private int beatStartServerTimestamp = -1;
    private int battleBgmIndex = -1;
    private Coroutine waitRoutine;

    public static void ClearPhotonSyncStateForLeavingGameScene()
    {
        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { PROP_SCENE_READY, false }
            });
        }

        if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { ROOM_PROP_BEAT_START_TS, null },
                { ROOM_PROP_BPM, null },
                { ROOM_PROP_BATTLE_BGM_INDEX, null }
            });
        }

        Debug.Log("[GameSceneStartSync] Cleared GameScene sync state.");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
    public int GetBeatDurationMs()
    {
        return Mathf.RoundToInt((60f / bpm) * 1000f);
    }

    public int GetNextBeatTimestamp(int extraBeats = 1)
    {
        int safeExtraBeats = Mathf.Max(1, extraBeats);
        int beatMs = GetBeatDurationMs();
        int now = PhotonNetwork.ServerTimestamp;

        // Fallback to now + N beats until the shared beat anchor is ready.
        if (!beatStartReady)
            return now + beatMs * safeExtraBeats;

        // Photon ServerTimestamp can be negative, so compare by difference.
        int diff = now - beatStartServerTimestamp;

        // Round up so the returned timestamp is always on a future beat.
        int beatsPassed = diff <= 0 ? 0 : Mathf.CeilToInt(diff / (float)beatMs);

        return beatStartServerTimestamp + (beatsPassed + safeExtraBeats) * beatMs;
    }
    private void Start()
    {
        HideBeforeStart();

        if (!waitRoutineStarted)
        {
            waitRoutineStarted = true;
            waitRoutine = StartCoroutine(WaitAndStartGame());
        }
    }

    private int BeatDurationMs()
    {
        return Mathf.RoundToInt((60f / bpm) * 1000f);
    }

    private void HideBeforeStart()
    {
        if (myCharacterRoot != null)
            myCharacterRoot.SetActive(false);

        if (enemyCharacterRoot != null)
            enemyCharacterRoot.SetActive(false);

        if (objectsEnableOnStart != null)
        {
            for (int i = 0; i < objectsEnableOnStart.Length; i++)
            {
                if (objectsEnableOnStart[i] != null)
                    objectsEnableOnStart[i].SetActive(false);
            }
        }

        if (bgmSource != null)
            bgmSource.Stop();
    }

    private void ShowAfterStart()
    {
        ShowCharacterRoots();

        if (objectsEnableOnStart != null)
        {
            for (int i = 0; i < objectsEnableOnStart.Length; i++)
            {
                if (objectsEnableOnStart[i] != null)
                    objectsEnableOnStart[i].SetActive(true);
            }
        }
    }

    private void ShowCharacterRoots()
    {
        ShowCharacterWithEntrance(myCharacterRoot);
        ShowCharacterWithEntrance(enemyCharacterRoot);
    }

    private void ShowCharacterWithEntrance(GameObject character)
    {
        if (character == null) return;
        character.SetActive(true);
        // Use the shared beat anchor so a late spawn catches up or skips an expired entrance.
        int elapsedMs = unchecked(PhotonNetwork.ServerTimestamp - beatStartServerTimestamp);
        character.GetComponent<CharacterShaderFeedback>()?.BeginEntrance(
            GetBeatDurationMs() / 1000f, Mathf.Max(0, elapsedMs) / 1000f);
    }

    public void MarkLocalSceneReady()
    {
        if (localSceneReadySent)
            return;

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        Hashtable props = new Hashtable
        {
            { PROP_SCENE_READY, true },
            { LocalActionUsage.SnapshotKey, LocalActionUsage.SerializeEntrySnapshot() }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        localSceneReadySent = true;

        Debug.Log("Local GameScene ready sent");
    }

    private bool BothPlayersSceneReady()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (PhotonNetwork.CurrentRoom.PlayerCount != 2)
            return false;

        Player[] players = PhotonNetwork.PlayerList;
        if (players == null || players.Length != 2)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];
            if (p == null)
                return false;

            if (!p.CustomProperties.TryGetValue(PROP_SCENE_READY, out object readyObj))
                return false;

            if (!(readyObj is bool ready) || !ready)
                return false;
        }

        return true;
    }

    public bool AreBothPlayersSceneReady()
    {
        return BothPlayersSceneReady();
    }

    public bool HasLocalSceneReadyBeenSent()
    {
        return localSceneReadySent;
    }

    public bool HasPreparedBeatStart()
    {
        if (beatStartReady)
            return true;

        return TryReadBeatAnchor();
    }

    private bool TryCreateBeatAnchor()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return false;

        if (!BothPlayersSceneReady())
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BEAT_START_TS, out object existingTs) &&
            existingTs != null)
        {
            beatStartServerTimestamp = System.Convert.ToInt32(existingTs);
            TryReadBattleBGMIndex();
            beatStartReady = true;
            return true;
        }

        int startLeadBeats = GetStartLeadInBeats();
        int startTs = PhotonNetwork.ServerTimestamp + BeatDurationMs() * startLeadBeats;
        battleBgmIndex = SelectBattleBGMIndex();

        Hashtable props = new Hashtable
        {
            { ROOM_PROP_BEAT_START_TS, startTs },
            { ROOM_PROP_BPM, bpm },
            { ROOM_PROP_BATTLE_BGM_INDEX, battleBgmIndex }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        // Master keeps the value locally without waiting for the callback.
        beatStartServerTimestamp = startTs;
        beatStartReady = true;

        Debug.Log($"Created beat anchor: startTs={startTs}, bpm={bpm}, leadInBeats={startLeadBeats}");
        return true;
    }

    private int GetStartLeadInBeats()
    {
        int safeLeadInBeats = Mathf.Max(1, leadInBeats);
        float transitionOutSeconds = SceneTransitionManager.GetActiveOutDurationSeconds(gameObject.scene.name);
        if (transitionOutSeconds <= 0f)
            return safeLeadInBeats;

        float beatSeconds = 60f / Mathf.Max(1f, bpm);
        int transitionOutBeats = Mathf.CeilToInt(transitionOutSeconds / beatSeconds);
        return Mathf.Max(safeLeadInBeats, transitionOutBeats + 1);
    }

    private bool TryReadBeatAnchor()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BEAT_START_TS, out object tsObj) ||
            tsObj == null)
            return false;

        beatStartServerTimestamp = System.Convert.ToInt32(tsObj);

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BPM, out object bpmObj))
            bpm = System.Convert.ToSingle(bpmObj);

        TryReadBattleBGMIndex();

        // Reading the anchor is enough; timestamp sign is not meaningful.
        beatStartReady = true;
        return true;
    }

    private int SelectBattleBGMIndex()
    {
        if (AudioManager.TryGetInstance(out AudioManager audioManager))
            return audioManager.SelectBattleBGMIndex();

        return -1;
    }

    private void TryReadBattleBGMIndex()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BATTLE_BGM_INDEX, out object bgmObj) ||
            bgmObj == null)
        {
            return;
        }

        battleBgmIndex = System.Convert.ToInt32(bgmObj);
    }

    private bool HasValidBeatAnchor()
    {
        return beatStartReady;
    }

    // Photon ServerTimestamp can be negative, so compare by difference.
    private bool HasReachedServerTimestamp(int targetTimestamp)
    {
        int diff = PhotonNetwork.ServerTimestamp - targetTimestamp;
        return diff >= 0;
    }

    private bool StartSyncedBGM()
    {
        if (bgmScheduled)
            return true;

        if (!HasValidBeatAnchor())
            return false;

        int remainMs = beatStartServerTimestamp - PhotonNetwork.ServerTimestamp;
        if (remainMs < 0)
            remainMs = 0;

        double remainSec = remainMs / 1000.0;
        double dspStart = AudioSettings.dspTime + remainSec;

        if (AudioManager.TryGetInstance(out AudioManager audioManager) &&
            audioManager.ScheduleBattleBGMIndex(battleBgmIndex, dspStart))
        {
            bgmScheduled = true;
            Debug.Log($"Battle BGM scheduled through AudioManager. index={battleBgmIndex}, remainMs={remainMs}, dspStart={dspStart}, beatStartTs={beatStartServerTimestamp}, nowTs={PhotonNetwork.ServerTimestamp}");
            return true;
        }

        if (bgmSource == null || bgmSource.clip == null)
        {
            Debug.LogWarning("StartSyncedBGM: AudioManager battle BGM and fallback bgmSource are not assigned");
            return true;
        }

        if (!bgmSource.gameObject.activeSelf)
            bgmSource.gameObject.SetActive(true);

        if (!bgmSource.enabled)
            bgmSource.enabled = true;

        bgmSource.Stop();
        bgmSource.PlayScheduled(dspStart);
        bgmScheduled = true;

        Debug.Log($"BGM scheduled, remainMs={remainMs}, dspStart={dspStart}, beatStartTs={beatStartServerTimestamp}, nowTs={PhotonNetwork.ServerTimestamp}");
        return true;
    }

    private IEnumerator WaitForBeatAndStartGame()
    {
        while (!HasValidBeatAnchor())
        {
            TryReadBeatAnchor();
            yield return null;
        }

        while (!HasReachedServerTimestamp(beatStartServerTimestamp))
            yield return null;

        if (!gameStartedOnBeat)
        {
            gameStartedOnBeat = true;
            ShowAfterStart();
            Debug.Log("GameScene beat reached, start enabled");
        }
    }

    private IEnumerator WaitAndStartGame()
    {
        while (!BothPlayersSceneReady())
        {
            if (PhotonNetwork.IsMasterClient)
                TryCreateBeatAnchor();

            yield return null;
        }

        while (!beatStartReady)
        {
            if (PhotonNetwork.IsMasterClient)
                TryCreateBeatAnchor();

            if (!beatStartReady)
                TryReadBeatAnchor();

            yield return null;
        }

        while (!StartSyncedBGM())
            yield return null;

        yield return StartCoroutine(WaitForBeatAndStartGame());

        Debug.Log("GameScene synced start completed");
    }

    public void RegisterMyCharacter(GameObject go)
    {
        myCharacterRoot = go;

        if (myCharacterRoot != null)
        {
            if (gameStartedOnBeat) ShowCharacterWithEntrance(myCharacterRoot);
            else myCharacterRoot.SetActive(false);
        }
    }

    public void RegisterEnemyCharacter(GameObject go)
    {
        enemyCharacterRoot = go;

        if (enemyCharacterRoot != null)
        {
            if (gameStartedOnBeat) ShowCharacterWithEntrance(enemyCharacterRoot);
            else enemyCharacterRoot.SetActive(false);
        }
    }

    public bool HasBeatStarted()
    {
        return gameStartedOnBeat;
    }

    public bool HasGameStarted()
    {
        return gameStartedOnBeat;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey(PROP_SCENE_READY))
        {
            if (PhotonNetwork.IsMasterClient)
                TryCreateBeatAnchor();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(ROOM_PROP_BEAT_START_TS))
        {
            object timestampValue = propertiesThatChanged[ROOM_PROP_BEAT_START_TS];
            if (timestampValue == null)
            {
                beatStartServerTimestamp = -1;
                beatStartReady = false;
                battleBgmIndex = -1;
            }
            else
            {
                beatStartServerTimestamp = System.Convert.ToInt32(timestampValue);
                beatStartReady = true;
                Debug.Log("Received beat anchor server timestamp = " + beatStartServerTimestamp);
            }
        }

        if (propertiesThatChanged.ContainsKey(ROOM_PROP_BPM) &&
            propertiesThatChanged[ROOM_PROP_BPM] != null)
        {
            bpm = System.Convert.ToSingle(propertiesThatChanged[ROOM_PROP_BPM]);
        }

        if (propertiesThatChanged.ContainsKey(ROOM_PROP_BATTLE_BGM_INDEX))
        {
            object bgmValue = propertiesThatChanged[ROOM_PROP_BATTLE_BGM_INDEX];
            battleBgmIndex = bgmValue == null ? -1 : System.Convert.ToInt32(bgmValue);
        }
    }

    public override void OnJoinedRoom()
    {
        TryReadBeatAnchor();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient)
            TryCreateBeatAnchor();
    }

    private void OnDestroy()
    {
        if (waitRoutine != null)
        {
            StopCoroutine(waitRoutine);
            waitRoutine = null;
        }

        if (Instance == this)
            Instance = null;
    }
}
