using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public enum SceneTransitionState
{
    Idle,
    PlayingIn,
    Loading,
    WaitingForPlayers,
    PlayingOut
}

public class SceneTransitionManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const string ResourcePath = "Prefab/SceneTransitionSystem";
    private const string TransitionObjectName = "TransSceneAnimation";
    private const string CharacterSelectSceneName = "CharacterSelectScene";
    private const string GameSceneName = "GameScene";
    private const string InClipResourcePath = "UI/SceneTransition/Animation/TransSceneIn";
    private const string LoadingClipResourcePath = "UI/SceneTransition/Animation/Loading";
    private const string OutClipResourcePath = "UI/SceneTransition/Animation/TransSceneOut";

    private const string PropTransitionScene = "sceneTransitionScene";
    private const string PropTransitionToken = "sceneTransitionToken";
    private const string PropTransitionInReady = "sceneTransitionInReady";
    private const string PropTransitionSceneReady = "sceneTransitionReady";

    private static SceneTransitionManager instance;

    [Header("Existing Transition UI")]
    [SerializeField] private Canvas transitionCanvas;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private int sortingOrder = 6000;

    [Header("Existing Animation Clips")]
    [SerializeField] private AnimationClip inClip;
    [SerializeField] private AnimationClip loadingClip;
    [SerializeField] private AnimationClip outClip;

    public static SceneTransitionManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindExistingInstance();

            if (instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>(ResourcePath);
                GameObject obj = prefab != null
                    ? Instantiate(prefab)
                    : new GameObject(TransitionObjectName);

                obj.name = TransitionObjectName;
                instance = obj.GetComponent<SceneTransitionManager>();
                if (instance == null)
                    instance = obj.AddComponent<SceneTransitionManager>();
            }

            return instance;
        }
    }

    public SceneTransitionState State { get; private set; } = SceneTransitionState.Idle;

    private Coroutine transitionCoroutine;
    private Coroutine loadingLoopCoroutine;
    private string targetSceneName;
    private int transitionToken;
    private bool photonTransitionActive;
    private bool photonLoadStarted;
    private bool localSceneLoadedForToken;
    private bool releaseReceived;
    private string locallyInitializedSceneName;

    public static void RequestSceneTransition(string sceneName)
    {
        Instance.Request(sceneName);
    }

    public static void EnsureForCharacterSelectScene()
    {
        if (SceneManager.GetActiveScene().name != CharacterSelectSceneName)
            return;

        _ = Instance;
    }

    public static void NotifyLocalSceneInitializationReady(string sceneName)
    {
        if (instance == null || string.IsNullOrWhiteSpace(sceneName))
            return;

        instance.MarkLocalSceneInitializationReady(sceneName);
    }

    public void PlayTransitionCollision()
    {
        AudioManager.Instance.PlayTransitionCollision();
    }

    public static float GetActiveOutDurationSeconds(string sceneName)
    {
        if (instance == null ||
            instance.State == SceneTransitionState.Idle ||
            instance.targetSceneName != sceneName ||
            instance.outClip == null)
        {
            return 0f;
        }

        return instance.outClip.length;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneLoadEnsure()
    {
        SceneManager.sceneLoaded -= EnsureTransitionForLoadedScene;
        SceneManager.sceneLoaded += EnsureTransitionForLoadedScene;
        EnsureForCharacterSelectScene();
    }

    private static void EnsureTransitionForLoadedScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == CharacterSelectSceneName && instance == null)
            _ = Instance;
    }

    private static SceneTransitionManager FindExistingInstance()
    {
        SceneTransitionManager found = FindFirstObjectByType<SceneTransitionManager>(FindObjectsInactive.Include);
        if (found == null)
            return null;

        if (!found.gameObject.activeSelf)
            found.gameObject.SetActive(true);

        return found;
    }

    public void Request(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return;

        if (State != SceneTransitionState.Idle)
        {
            Debug.Log("[SceneTransition] Transition request ignored because another transition is active.");
            return;
        }

        bool useAnimatedTransition = ShouldUseAnimatedTransition(sceneName);
        bool destroyAfterRequest = ShouldDestroyWhenLeavingGameScene(sceneName);
        if (destroyAfterRequest)
            PrepareToLeaveGameScene();

        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.AutomaticallySyncScene = true;

            if (PhotonNetwork.IsMasterClient)
            {
                if (useAnimatedTransition)
                    BroadcastBegin(sceneName, GenerateTransitionToken());
                else
                    LoadPhotonSceneWithoutAnimation(sceneName);
            }
            else
            {
                RaiseTransitionEvent(
                    GamePhotonEventCodes.SceneTransitionRequest,
                    new object[] { sceneName, useAnimatedTransition },
                    ReceiverGroup.MasterClient);
            }

            if (destroyAfterRequest)
                DestroyTransitionInstance(sceneName);

            return;
        }

        if (!useAnimatedTransition)
        {
            LoadLocalSceneWithoutAnimation(sceneName);
            if (destroyAfterRequest)
                DestroyTransitionInstance(sceneName);

            return;
        }

        int localToken = GenerateTransitionToken();
        transitionCoroutine = StartCoroutine(LocalTransitionRoutine(sceneName, localToken));
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameObject.name = TransitionObjectName;
        DontDestroyOnLoad(gameObject);

        ResolveReferences();
        SetTransitionVisible(false);
    }

    public override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public override void OnDisable()
    {
        base.OnDisable();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void ResolveReferences()
    {
        EnsureRootVisibleScale();

        if (transitionCanvas == null)
            transitionCanvas = GetComponent<Canvas>();

        if (graphicRaycaster == null)
            graphicRaycaster = GetComponent<GraphicRaycaster>();

        if (transitionCanvas != null)
        {
            transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            transitionCanvas.sortingOrder = sortingOrder;
        }

        if (inClip == null)
            inClip = FindClipByName("TransSceneIn");

        if (inClip == null)
            inClip = Resources.Load<AnimationClip>(InClipResourcePath);

        if (loadingClip == null)
            loadingClip = FindClipByName("Loading");

        if (loadingClip == null)
            loadingClip = Resources.Load<AnimationClip>(LoadingClipResourcePath);

        if (outClip == null)
            outClip = FindClipByName("TransSceneOut");

        if (outClip == null)
            outClip = Resources.Load<AnimationClip>(OutClipResourcePath);

        RegisterClipWithAnimation(inClip);
        RegisterClipWithAnimation(loadingClip);
        RegisterClipWithAnimation(outClip);
    }

    private AnimationClip FindClipByName(string clipName)
    {
        Animation animation = GetComponent<Animation>();
        if (animation == null)
            return null;

        foreach (AnimationState state in animation)
        {
            if (state != null && state.clip != null && state.clip.name == clipName)
                return state.clip;
        }

        return null;
    }

    private void RegisterClipWithAnimation(AnimationClip clip)
    {
        if (clip == null)
            return;

        if (!clip.legacy)
            return;

        Animation animation = GetComponent<Animation>();
        if (animation == null)
            return;

        if (animation.GetClip(clip.name) == null)
            animation.AddClip(clip, clip.name);
    }

    private void BroadcastBegin(string sceneName, int token)
    {
        Debug.Log($"[SceneTransition] Begin token={token} scene={sceneName}.");
        RaiseTransitionEvent(
            GamePhotonEventCodes.SceneTransitionBegin,
            new object[] { sceneName, token },
            ReceiverGroup.All);
    }

    private void RaiseTransitionEvent(byte eventCode, object content, ReceiverGroup receivers)
    {
        RaiseEventOptions options = new RaiseEventOptions { Receivers = receivers };
        PhotonNetwork.RaiseEvent(eventCode, content, options, SendOptions.SendReliable);
    }

    private int GenerateTransitionToken()
    {
        int timestamp = PhotonNetwork.ServerTimestamp;
        if (timestamp == 0)
            timestamp = Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);

        return Mathf.Abs(timestamp);
    }

    private bool ShouldUseAnimatedTransition(string sceneName)
    {
        return SceneManager.GetActiveScene().name == CharacterSelectSceneName &&
               sceneName == GameSceneName;
    }

    private bool ShouldDestroyWhenLeavingGameScene(string sceneName)
    {
        return SceneManager.GetActiveScene().name == GameSceneName &&
               sceneName != GameSceneName;
    }

    private void PrepareToLeaveGameScene()
    {
        GameSceneStartSync.ClearPhotonSyncStateForLeavingGameScene();
        TurnPlanningManager.ClearLocalTransmittedActionPayloadForLeavingGameScene();
    }

    private void LoadLocalSceneWithoutAnimation(string sceneName)
    {
        Debug.Log("[SceneTransition] Loading " + sceneName + " without transition animation.");
        SceneManager.LoadScene(sceneName);
    }

    private void LoadPhotonSceneWithoutAnimation(string sceneName)
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        Debug.Log("[SceneTransition] Master loading " + sceneName + " without transition animation.");
        PhotonNetwork.LoadLevel(sceneName);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == GamePhotonEventCodes.SceneTransitionRequest)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            object[] data = photonEvent.CustomData as object[];
            if (data == null || data.Length < 1)
                return;

            if (State != SceneTransitionState.Idle)
            {
                Debug.Log("[SceneTransition] Master ignored scene transition request while busy.");
                return;
            }

            string requestedSceneName = (string)data[0];
            bool useAnimatedTransition = data.Length < 2
                ? ShouldUseAnimatedTransition(requestedSceneName)
                : System.Convert.ToBoolean(data[1]);

            if (useAnimatedTransition)
                BroadcastBegin(requestedSceneName, GenerateTransitionToken());
            else
            {
                if (ShouldDestroyWhenLeavingGameScene(requestedSceneName))
                    PrepareToLeaveGameScene();

                LoadPhotonSceneWithoutAnimation(requestedSceneName);
                if (ShouldDestroyWhenLeavingGameScene(requestedSceneName))
                    DestroyTransitionInstance(requestedSceneName);
            }

            return;
        }

        if (photonEvent.Code == GamePhotonEventCodes.SceneTransitionBegin)
        {
            object[] data = photonEvent.CustomData as object[];
            if (data == null || data.Length < 2)
                return;

            BeginPhotonTransition((string)data[0], System.Convert.ToInt32(data[1]));
            return;
        }

        if (photonEvent.Code == GamePhotonEventCodes.SceneTransitionRelease)
        {
            object[] data = photonEvent.CustomData as object[];
            if (data == null || data.Length < 2)
                return;

            HandleRelease((string)data[0], System.Convert.ToInt32(data[1]));
        }
    }

    private void BeginPhotonTransition(string sceneName, int token)
    {
        if (State != SceneTransitionState.Idle)
        {
            if (transitionToken != token)
                Debug.Log("[SceneTransition] Ignored stale begin event while another transition is active.");

            return;
        }

        photonTransitionActive = true;
        photonLoadStarted = false;
        localSceneLoadedForToken = false;
        releaseReceived = false;
        locallyInitializedSceneName = null;
        targetSceneName = sceneName;
        transitionToken = token;

        Debug.Log($"[SceneTransition] Received begin token={token} scene={sceneName}.");
        transitionCoroutine = StartCoroutine(PhotonTransitionRoutine());
    }

    private IEnumerator PhotonTransitionRoutine()
    {
        State = SceneTransitionState.PlayingIn;
        SetTransitionVisible(true);
        AudioManager.Instance.PlayTransitionSlideIn();

        yield return PlayClipOnce(inClip);
        Debug.Log("[SceneTransition] In completed.");

        State = SceneTransitionState.Loading;
        StartLoadingLoop();
        ReportTransitionProperty(true, false);
        Debug.Log("[SceneTransition] Entering Loading state.");

        if (PhotonNetwork.IsMasterClient)
            TryStartPhotonLoad();
    }

    private IEnumerator LocalTransitionRoutine(string sceneName, int token)
    {
        targetSceneName = sceneName;
        transitionToken = token;
        photonTransitionActive = false;
        locallyInitializedSceneName = null;

        Debug.Log($"[SceneTransition] Begin local token={token} scene={sceneName}.");

        State = SceneTransitionState.PlayingIn;
        SetTransitionVisible(true);
        AudioManager.Instance.PlayTransitionSlideIn();

        yield return PlayClipOnce(inClip);
        Debug.Log("[SceneTransition] In completed.");

        State = SceneTransitionState.Loading;
        StartLoadingLoop();
        Debug.Log("[SceneTransition] Entering Loading state.");

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        if (loadOperation != null)
        {
            while (!loadOperation.isDone)
                yield return null;
        }

        Debug.Log("[SceneTransition] Local scene loaded: " + sceneName + ".");

        State = SceneTransitionState.WaitingForPlayers;
        yield return WaitForLocalSceneInitialization(sceneName);
        Debug.Log("[SceneTransition] Local game initialization ready.");

        yield return PlayOutRoutine();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!photonTransitionActive || scene.name != targetSceneName)
            return;

        if (localSceneLoadedForToken)
            return;

        localSceneLoadedForToken = true;
        Debug.Log("[SceneTransition] Local scene loaded: " + scene.name + ".");
        StartCoroutine(ReportPhotonSceneReadyWhenInitialized(scene.name, transitionToken));
    }

    private IEnumerator ReportPhotonSceneReadyWhenInitialized(string sceneName, int token)
    {
        State = SceneTransitionState.WaitingForPlayers;

        yield return WaitForLocalSceneInitialization(sceneName);

        if (!photonTransitionActive || token != transitionToken)
            yield break;

        Debug.Log("[SceneTransition] Local game initialization ready.");
        ReportTransitionProperty(true, true);

        if (PhotonNetwork.IsMasterClient)
            TryReleasePhotonTransition();
    }

    private IEnumerator WaitForLocalSceneInitialization(string sceneName)
    {
        yield return null;

        if (sceneName == "GameScene")
        {
            if (!PhotonNetwork.InRoom)
                yield break;

            while (GameSceneStartSync.Instance == null)
            {
                if (!PhotonNetwork.InRoom)
                    yield break;

                yield return null;
            }

            while (!GameSceneStartSync.Instance.HasLocalSceneReadyBeenSent() &&
                   locallyInitializedSceneName != sceneName)
            {
                if (!PhotonNetwork.InRoom)
                    yield break;

                yield return null;
            }

            while (!GameSceneStartSync.Instance.HasPreparedBeatStart())
            {
                if (!PhotonNetwork.InRoom)
                    yield break;

                yield return null;
            }

            yield return null;
        }
    }

    private void MarkLocalSceneInitializationReady(string sceneName)
    {
        locallyInitializedSceneName = sceneName;
        Debug.Log("[SceneTransition] Local initialization notification: " + sceneName + ".");
    }

    private void ReportTransitionProperty(bool inReady, bool sceneReady)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PropTransitionScene, targetSceneName },
            { PropTransitionToken, transitionToken },
            { PropTransitionInReady, inReady },
            { PropTransitionSceneReady, sceneReady }
        });
    }

    private void TryStartPhotonLoad()
    {
        if (!PhotonNetwork.IsMasterClient || photonLoadStarted)
            return;

        if (!AreAllPlayersReady(PropTransitionInReady))
            return;

        photonLoadStarted = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        Debug.Log("[SceneTransition] Master loading " + targetSceneName + ".");
        PhotonNetwork.LoadLevel(targetSceneName);
    }

    private void TryReleasePhotonTransition()
    {
        if (!PhotonNetwork.IsMasterClient || releaseReceived)
            return;

        if (!AreAllPlayersReady(PropTransitionSceneReady))
            return;

        releaseReceived = true;
        Debug.Log($"[SceneTransition] All players ready for token={transitionToken}.");
        RaiseTransitionEvent(
            GamePhotonEventCodes.SceneTransitionRelease,
            new object[] { targetSceneName, transitionToken },
            ReceiverGroup.All);
    }

    private bool AreAllPlayersReady(string readyKey)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        Player[] players = PhotonNetwork.PlayerList;
        if (players == null || players.Length == 0)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            Player player = players[i];
            if (player == null)
                return false;

            if (!player.CustomProperties.TryGetValue(PropTransitionScene, out object sceneObj) ||
                sceneObj == null ||
                sceneObj.ToString() != targetSceneName)
                return false;

            if (!player.CustomProperties.TryGetValue(PropTransitionToken, out object tokenObj) ||
                System.Convert.ToInt32(tokenObj) != transitionToken)
                return false;

            if (!player.CustomProperties.TryGetValue(readyKey, out object readyObj) ||
                !(readyObj is bool ready) ||
                !ready)
                return false;
        }

        return true;
    }

    private void HandleRelease(string sceneName, int token)
    {
        if (!photonTransitionActive || token != transitionToken || sceneName != targetSceneName)
        {
            Debug.Log("[SceneTransition] Ignored stale release event.");
            return;
        }

        if (State == SceneTransitionState.PlayingOut || State == SceneTransitionState.Idle)
            return;

        releaseReceived = true;
        Debug.Log("[SceneTransition] Playing Out.");

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(PlayOutRoutine());
    }

    private IEnumerator PlayOutRoutine()
    {
        State = SceneTransitionState.PlayingOut;
        StopLoadingLoop();
        AudioManager.Instance.PlayTransitionEnd();

        yield return PlayClipOnce(outClip);

        SetTransitionVisible(false);
        photonTransitionActive = false;
        photonLoadStarted = false;
        localSceneLoadedForToken = false;
        releaseReceived = false;
        targetSceneName = null;
        transitionToken = 0;
        locallyInitializedSceneName = null;
        transitionCoroutine = null;
        State = SceneTransitionState.Idle;

        Debug.Log("[SceneTransition] Transition completed.");
    }

    private void DestroyTransitionInstance(string requestedSceneName)
    {
        StopLoadingLoop();

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        SetTransitionVisible(false);
        photonTransitionActive = false;
        photonLoadStarted = false;
        localSceneLoadedForToken = false;
        releaseReceived = false;
        targetSceneName = null;
        transitionToken = 0;
        locallyInitializedSceneName = null;
        State = SceneTransitionState.Idle;

        Debug.Log("[SceneTransition] Destroying TransSceneAnimation after leaving GameScene.");
        if (instance == this)
            instance = null;

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void StartLoadingLoop()
    {
        StopLoadingLoop();
        if (loadingClip != null)
            loadingLoopCoroutine = StartCoroutine(PlayClipLoop(loadingClip));
    }

    private void StopLoadingLoop()
    {
        if (loadingLoopCoroutine != null)
        {
            StopCoroutine(loadingLoopCoroutine);
            loadingLoopCoroutine = null;
        }
    }

    private IEnumerator PlayClipOnce(AnimationClip clip)
    {
        if (clip == null)
            yield break;

        float length = Mathf.Max(0.01f, clip.length);
        float elapsed = 0f;
        int nextEventIndex = 0;
        AnimationEvent[] animationEvents = GetSortedAnimationEvents(clip);

        while (elapsed < length)
        {
            clip.SampleAnimation(gameObject, elapsed);
            float previous = elapsed;
            elapsed = Mathf.Min(length, elapsed + Time.unscaledDeltaTime);
            InvokeAnimationEvents(animationEvents, previous, elapsed, ref nextEventIndex);
            yield return null;
        }

        clip.SampleAnimation(gameObject, length);
    }

    private AnimationEvent[] GetSortedAnimationEvents(AnimationClip clip)
    {
        if (clip == null)
            return Array.Empty<AnimationEvent>();

        AnimationEvent[] animationEvents = clip.events;
        if (animationEvents == null || animationEvents.Length <= 1)
            return animationEvents ?? Array.Empty<AnimationEvent>();

        Array.Sort(animationEvents, (a, b) => a.time.CompareTo(b.time));
        return animationEvents;
    }

    private void InvokeAnimationEvents(AnimationEvent[] animationEvents, float previousTime, float currentTime, ref int nextEventIndex)
    {
        if (animationEvents == null)
            return;

        const float tolerance = 0.0001f;
        while (nextEventIndex < animationEvents.Length)
        {
            AnimationEvent animationEvent = animationEvents[nextEventIndex];
            if (animationEvent == null)
            {
                nextEventIndex++;
                continue;
            }

            if (animationEvent.time > currentTime + tolerance)
                break;

            if (animationEvent.time >= previousTime - tolerance &&
                !string.IsNullOrEmpty(animationEvent.functionName))
            {
                SendMessage(animationEvent.functionName, SendMessageOptions.DontRequireReceiver);
            }

            nextEventIndex++;
        }
    }

    private IEnumerator PlayClipLoop(AnimationClip clip)
    {
        float length = Mathf.Max(0.01f, clip.length);
        float elapsed = 0f;

        while (true)
        {
            clip.SampleAnimation(gameObject, Mathf.Repeat(elapsed, length));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetTransitionVisible(bool visible)
    {
        if (visible)
            EnsureRootVisibleScale();

        if (transitionCanvas != null)
            transitionCanvas.enabled = visible;

        if (graphicRaycaster != null)
            graphicRaycaster.enabled = visible;
    }

    private void EnsureRootVisibleScale()
    {
        if (transform.localScale == Vector3.zero)
            transform.localScale = Vector3.one;
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (!PhotonNetwork.IsMasterClient || !photonTransitionActive)
            return;

        if (changedProps.ContainsKey(PropTransitionInReady))
        {
            Debug.Log($"[SceneTransition] Actor {targetPlayer.ActorNumber} in-ready for token={transitionToken}.");
            TryStartPhotonLoad();
        }

        if (changedProps.ContainsKey(PropTransitionSceneReady))
        {
            Debug.Log($"[SceneTransition] Actor {targetPlayer.ActorNumber} scene-ready for token={transitionToken}.");
            TryReleasePhotonTransition();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient || !photonTransitionActive)
            return;

        TryStartPhotonLoad();
        TryReleasePhotonTransition();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (!PhotonNetwork.IsMasterClient || !photonTransitionActive)
            return;

        TryStartPhotonLoad();
        TryReleasePhotonTransition();
    }
}
