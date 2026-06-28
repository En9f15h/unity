using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameSceneStartSync : MonoBehaviourPunCallbacks
{
    public static GameSceneStartSync Instance;

    [Header("節拍同步")]
    [SerializeField] private float bpm = 120f;
    [SerializeField] private int leadInBeats = 4;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;

    [Header("要同步顯示的角色根物件")]
    [SerializeField] private GameObject myCharacterRoot;
    [SerializeField] private GameObject enemyCharacterRoot;

    [Header("開場後才啟用的物件")]
    [SerializeField] private GameObject[] objectsEnableOnStart;

    private const string PROP_SCENE_READY = "GameSceneReady";
    private const string ROOM_PROP_BEAT_START_TS = "beatStartTs";
    private const string ROOM_PROP_BPM = "beatBpm";

    private bool localSceneReadySent = false;
    private bool waitRoutineStarted = false;
    private bool beatStartReady = false;
    private bool gameStartedOnBeat = false;
    private bool bgmScheduled = false;

    private int beatStartServerTimestamp = -1;
    private Coroutine waitRoutine;

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

        // 如果開場拍點還沒準備好，就退化成「現在 + N 拍」
        if (!beatStartReady)
            return now + beatMs * safeExtraBeats;

        // 注意：Photon ServerTimestamp 可能是負數，要用差值算
        int diff = now - beatStartServerTimestamp;

        // 已經走過幾拍（向上取整，確保一定是“下一拍”）
        int beatsPassed = diff <= 0 ? 0 : Mathf.CeilToInt(diff / (float)beatMs);

        return beatStartServerTimestamp + (beatsPassed + safeExtraBeats) * beatMs;
    }
    private void Start()
    {
        HideBeforeStart();
        MarkLocalSceneReady();

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
        if (myCharacterRoot != null)
            myCharacterRoot.SetActive(true);

        if (enemyCharacterRoot != null)
            enemyCharacterRoot.SetActive(true);

        if (objectsEnableOnStart != null)
        {
            for (int i = 0; i < objectsEnableOnStart.Length; i++)
            {
                if (objectsEnableOnStart[i] != null)
                    objectsEnableOnStart[i].SetActive(true);
            }
        }
    }

    private void MarkLocalSceneReady()
    {
        if (localSceneReadySent)
            return;

        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        Hashtable props = new Hashtable
        {
            { PROP_SCENE_READY, true }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        localSceneReadySent = true;

        Debug.Log("本地 GameScene ready 已送出");
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

    private bool TryCreateBeatAnchor()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return false;

        if (!BothPlayersSceneReady())
            return false;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BEAT_START_TS, out object existingTs))
        {
            beatStartServerTimestamp = System.Convert.ToInt32(existingTs);
            beatStartReady = true;
            return true;
        }

        int startTs = PhotonNetwork.ServerTimestamp + BeatDurationMs() * leadInBeats;

        Hashtable props = new Hashtable
        {
            { ROOM_PROP_BEAT_START_TS, startTs },
            { ROOM_PROP_BPM, bpm }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);

        // Master 本地先直接記住，不等 callback
        beatStartServerTimestamp = startTs;
        beatStartReady = true;

        Debug.Log($"建立開場拍點: startTs={startTs}, bpm={bpm}");
        return true;
    }

    private bool TryReadBeatAnchor()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BEAT_START_TS, out object tsObj))
            return false;

        beatStartServerTimestamp = System.Convert.ToInt32(tsObj);

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BPM, out object bpmObj))
            bpm = System.Convert.ToSingle(bpmObj);

        // 只要有讀到就算 ready，不要判斷正負
        beatStartReady = true;
        return true;
    }

    private bool HasValidBeatAnchor()
    {
        return beatStartReady;
    }

    // Photon ServerTimestamp 可能是負數，必須用差值判斷
    private bool HasReachedServerTimestamp(int targetTimestamp)
    {
        int diff = PhotonNetwork.ServerTimestamp - targetTimestamp;
        return diff >= 0;
    }

    private bool StartSyncedBGM()
    {
        if (bgmScheduled)
            return true;

        if (bgmSource == null || bgmSource.clip == null)
        {
            Debug.LogWarning("StartSyncedBGM: bgmSource 或 clip 未指定");
            return true;
        }

        if (!HasValidBeatAnchor())
            return false;

        if (!bgmSource.gameObject.activeSelf)
            bgmSource.gameObject.SetActive(true);

        if (!bgmSource.enabled)
            bgmSource.enabled = true;

        int remainMs = beatStartServerTimestamp - PhotonNetwork.ServerTimestamp;
        if (remainMs < 0)
            remainMs = 0;

        double remainSec = remainMs / 1000.0;
        double dspStart = AudioSettings.dspTime + remainSec;

        bgmSource.Stop();
        bgmSource.PlayScheduled(dspStart);
        bgmScheduled = true;

        Debug.Log($"BGM 已排程播放，remainMs={remainMs}, dspStart={dspStart}, beatStartTs={beatStartServerTimestamp}, nowTs={PhotonNetwork.ServerTimestamp}");
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
            Debug.Log("GameScene 拍點到達，正式開始");
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

        Debug.Log("GameScene 同步開場完成（拍點對齊）");
    }

    public void RegisterMyCharacter(GameObject go)
    {
        myCharacterRoot = go;

        if (myCharacterRoot != null)
            myCharacterRoot.SetActive(HasGameStarted());
    }

    public void RegisterEnemyCharacter(GameObject go)
    {
        enemyCharacterRoot = go;

        if (enemyCharacterRoot != null)
            enemyCharacterRoot.SetActive(HasGameStarted());
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
            beatStartServerTimestamp = System.Convert.ToInt32(propertiesThatChanged[ROOM_PROP_BEAT_START_TS]);
            beatStartReady = true;
            Debug.Log("收到開場拍點 server timestamp = " + beatStartServerTimestamp);
        }

        if (propertiesThatChanged.ContainsKey(ROOM_PROP_BPM))
        {
            bpm = System.Convert.ToSingle(propertiesThatChanged[ROOM_PROP_BPM]);
        }
    }

    public override void OnJoinedRoom()
    {
        if (!localSceneReadySent)
            MarkLocalSceneReady();

        TryReadBeatAnchor();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient)
            TryCreateBeatAnchor();
    }
}