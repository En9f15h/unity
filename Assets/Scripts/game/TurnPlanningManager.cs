using System;
using System.Collections;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[System.Serializable]
public class ActionAnimationMap
{
    public ActionType actionType;
    public string stateName;
}

public class TurnPlanningManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [Header("Beat Settings")]
    [SerializeField] private float bpm = 120f;


    [SerializeField] private BattleStepPlayer battleStepPlayer;
    public static TurnPlanningManager Instance { get; private set; }

    [Header("甇仿??閮剖?")]
    [SerializeField] private float normalStepBeats = 2f;          // 銝?砍?雿?2 ??= 1 蝘?
    [SerializeField] private float parryCounterStepBeats = 2f;    // ParryCounter 憿? 2 ??
    [SerializeField] private float effectDelayBeats = 1f;         // ?Ｗ?雿?/ HitStop 憿? 1 ??
    [Header("UI")]
    [SerializeField] private Text countdownText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Text debugText;

    [Header("Planning Slots")]
    [SerializeField] private ActionSlot[] planningSlots;

    [Header("????")]
    [SerializeField] private float planningDuration = 20f;
    [SerializeField] private float timeoutResolveDelay = 0.4f;
    private bool myHeavyPendingThisTurn = false;
    private bool enemyHeavyPendingThisTurn = false;
    [Header("?蝑?閮剖?")]
    [SerializeField] private ActionAnimationMap[] myAnimationMaps;
    [SerializeField] private ActionAnimationMap[] enemyAnimationMaps;
    

    [Header("雿宏閮剖?")]
    [SerializeField] private float moveStep = 1f;
    [SerializeField] private float minCharacterGap = 0.2f;

    [Header("??銵函")]
    [SerializeField] private float hitShakeDuration = 0.12f;
    [SerializeField] private float hitShakeStrength = 0.08f;

    [Header("Finisher Slow Motion")]
    [SerializeField] private float finisherSlowMotionBeats = 2f;
    [SerializeField] private float finisherSlowTimeScale = 0.08f;

    [Header("HP UI嚗銝?????? BattleUIManager 蝬?")]
    [SerializeField] private DirectionalHealthBarUI myHPBar;
    [SerializeField] private DirectionalHealthBarUI enemyHPBar;

    [Header("閫 / ?瑟平鞈?")]
    [SerializeField] private CharacterClassConfig[] classConfigs;
    [SerializeField] private ActionData moveForwardAction;
    [SerializeField] private ActionData moveBackwardAction;
    [SerializeField] private ActionData jumpAction;
    private bool myChargingHeavyThisStep = false;
    private bool enemyChargingHeavyThisStep = false;
    private bool myHeavyAnimationProtectedThisStep = false;
    private bool enemyHeavyAnimationProtectedThisStep = false;

    [Header("?菜銵??汗")]
    [SerializeField] private RectTransform leftEnemyPreviewRoot;
    [SerializeField] private RectTransform rightEnemyPreviewRoot;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private Sprite emptyPreviewSprite;

    [Header("?賡?")]
    [SerializeField] private int maxEnergy = 10;
    [SerializeField] private int energyPerHit = 1;
    [SerializeField] private int energyPerBlock = 1;
    [SerializeField] private int resolveLeadBeats = 1;   // ???ready 敺?蝑?銝???? Resolve

    private Coroutine resolveStartCoroutine;


    [Header("擉ㄚ Parry ???寞?")]
    [SerializeField] private GameObject parrySuccessEffectPrefab;
    [SerializeField] private Vector3 parrySuccessEffectOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float parryEffectLifeTime = 1f;

    [Header("Debug Combat State") ]
    [SerializeField] private int myHP = 30;
    [SerializeField] private int enemyHP = 30;
    [SerializeField] private int distance = 1;
    [Header("?渲???")]
    [SerializeField] private BloodHitVFXManager bloodHitVFXManager;
    [SerializeField] private bool playBloodOnUltimate = true;
    [SerializeField] private bool playBloodOnParryCounter = true;
    private CharacterUnit myUnit;
    private CharacterUnit enemyUnit;

    private Animator myAnimator;
    private Animator enemyAnimator;

    private EnergyBarUI myEnergyBar;
    private EnergyBarUI enemyEnergyBar;

    private int myEnergy = 0;
    private int enemyEnergy = 0;

    private int myActorNumber = -1;
    private int enemyActorNumber = -1;

    private bool myJumping = false;
    private bool enemyJumping = false;
    private bool playedHitFeedbackThisStep = false;
    private bool playedFinisherSlowMotionThisStep = false;

    private bool localSubmitted = false;
    private bool receivedResolution = false;
    private bool isResolving = false;

    private Coroutine resolveCoroutine;
    private int lastResolvedTurnIndex = -1;

    private RectTransform currentEnemyPreviewRoot;
    private readonly List<Image> currentEnemyPreviewImages = new List<Image>();
    private readonly Dictionary<ActionType, Sprite> enemyPreviewSpriteMap = new Dictionary<ActionType, Sprite>();

    private const string ROOM_PROP_TURN_INDEX = "turnIndex";
    private const string ROOM_PROP_TURN_START = "turnStart";
    private const string ROOM_PROP_TURN_DUR = "turnDur";

    private const string PLAYER_PROP_READY = "turnReady";
    private const string PLAYER_PROP_ACTIONS = "turnActions";
    private const string PLAYER_PROP_SUBMIT_TURN = "submitTurn";

    private const string ROOM_PROP_GAME_ENDED = "gameEnded";
    private const string ROOM_PROP_GAME_WINNER_ACTOR = "gameWinnerActor";
    private const string ROOM_PROP_GAME_END_REASON = "gameEndReason";
    [SerializeField] private int planningLeadBeats = 1;   // ??蝯?敺?蝑?銝???閬?

    private Coroutine planningStartCoroutine;
    private const byte EVENT_PLANS_READY = 11;
    private bool pendingParryCounter = false;
    private bool pendingParryCounterByMine = false;
    private int pendingParryCounterDamage = 0;
    private bool gameEnded = false;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        PhotonNetwork.AddCallbackTarget(this);
        ApplyGameResultFromRoom();

        if (gameEnded)
            return;

        SetupEnemyPreviewSide();

        BindReadyButton(readyButton);

        if (!gameEnded)
            RestartPlanningStartCoroutine();

        RefreshDebug("蝑?閬???");
    }

    public void ApplyLayout(ClassGameplayUILayout layout)
    {
        if (layout == null)
            return;

        layout.ResolveReferences();

        if (layout.CountdownText != null)
            countdownText = layout.CountdownText;

        if (layout.DebugText != null)
            debugText = layout.DebugText;

        if (layout.ReadyButton != null)
            BindReadyButton(layout.ReadyButton);

        if (layout.LeftEnemyPreviewRoot != null)
            leftEnemyPreviewRoot = layout.LeftEnemyPreviewRoot;

        if (layout.RightEnemyPreviewRoot != null)
            rightEnemyPreviewRoot = layout.RightEnemyPreviewRoot;

        if (layout.PreviewCellPrefab != null)
            previewCellPrefab = layout.PreviewCellPrefab;

        if (layout.EmptyPreviewSprite != null)
            emptyPreviewSprite = layout.EmptyPreviewSprite;

        SetupEnemyPreviewSide();
        RefreshDebug("Applied class gameplay UI layout");
    }

    private void BindReadyButton(Button button)
    {
        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnClickReady);

        readyButton = button;

        if (readyButton != null)
            readyButton.onClick.AddListener(OnClickReady);
    }

    private IEnumerator WaitForSyncedGameStart()
    {
        if (gameEnded)
            yield break;

        // ?Ⅱ靽??游?甇亙歇摰?
        while (GameSceneStartSync.Instance == null || !GameSceneStartSync.Instance.HasGameStarted())
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        while (!GameSceneStartSync.Instance.HasBeatStarted())
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        // ??Master 銝?鞎祇??啣????芰? Host ?其?銝??閬?
        if (!PhotonNetwork.IsMasterClient)
        {
            RefreshDebug("Waiting for Host to start planning.");
            planningStartCoroutine = null;
            yield break;
        }

        int planningStartTimestamp = GameSceneStartSync.Instance.GetNextBeatTimestamp(planningLeadBeats);

        RefreshDebug($"蝑?????閬?嚗tartTs={planningStartTimestamp}");

        while (!HasReachedServerTimestamp(planningStartTimestamp))
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        BeginPlanningPhase();
        RefreshDebug("Planning started.");
        planningStartCoroutine = null;
    }
    private void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);

        if (readyButton != null)
            readyButton.onClick.RemoveListener(OnClickReady);

        if (planningStartCoroutine != null)
        {
            StopCoroutine(planningStartCoroutine);
            planningStartCoroutine = null;
        }

        if (resolveCoroutine != null)
        {
            StopCoroutine(resolveCoroutine);
            resolveCoroutine = null;
        }
    }

    // =========================
    // 憭閮餃? / ????
    // =========================
    private bool IsHeavyChargingThisStep(ActionType effectiveAction, bool heavyReleaseNow)
    {
        return effectiveAction == ActionType.HeavyAttack && !heavyReleaseNow;
    }

    private bool IsHeavyAnimationProtectedThisStep(ActionType effectiveAction)
    {
        return effectiveAction == ActionType.HeavyAttack;
    }
    public void RegisterCharacter(CharacterUnit unit)
    {
        if (unit == null || unit.photonView == null || unit.photonView.Owner == null)
            return;

        int actorNumber = unit.photonView.OwnerActorNr;
        int classIndex = GetPlayerClassIndex(unit.photonView.Owner);
        CharacterClassConfig config = GetClassConfigByIndex(classIndex);

        // ?Ⅱ靽閫撌脩??迤蝣?HP / MaxHP / SlotCount
        EnsureCharacterInitialized(unit, config);

        if (unit.IsMine())
        {
            myUnit = unit;
            myAnimator = unit.GetAnimator();
            myActorNumber = actorNumber;

            if (BattleUIManager.Instance != null)
            {
                myHPBar = BattleUIManager.Instance.GetBarByOwner(unit.photonView.Owner.IsMasterClient);

                if (config != null)
                {
                    myEnergyBar = BattleUIManager.Instance.CreateMyEnergyBar(config.energyBarPrefab);
                    if (myEnergyBar != null)
                        myEnergyBar.Init(maxEnergy, myEnergy);
                }
            }

            Debug.Log($"撌脩?摰撌梁?閫: {unit.name}, ActorNumber={myActorNumber}, HP={unit.currentHP}/{unit.maxHP}");
        }
        else
        {
            enemyUnit = unit;
            enemyAnimator = unit.GetAnimator();
            enemyActorNumber = actorNumber;

            if (BattleUIManager.Instance != null)
            {
                enemyHPBar = BattleUIManager.Instance.GetBarByOwner(unit.photonView.Owner.IsMasterClient);
                enemyEnergyBar = BattleUIManager.Instance.CreateEnemyEnergyBar();
                if (enemyEnergyBar != null)
                    enemyEnergyBar.Init(maxEnergy, enemyEnergy);
            }

            if (config != null)
            {
                RebuildEnemyPreviewSlots(config.slotCount);
                BuildEnemyPreviewSpriteMap(config);
            }
            else
            {
                Debug.LogWarning("Enemy class config missing; preview cannot be built.");
            }

            Debug.Log($"撌脩?摰????? {unit.name}, ActorNumber={enemyActorNumber}, HP={unit.currentHP}/{unit.maxHP}");
        }
        if (GameSceneStartSync.Instance != null)
        {
            if (unit.IsMine())
                GameSceneStartSync.Instance.RegisterMyCharacter(unit.gameObject);
            else
                GameSceneStartSync.Instance.RegisterEnemyCharacter(unit.gameObject);
        }
        UpdateHPBars();
        UpdateEnergyBars();
    }

    public void SetPlanningSlots(ActionSlot[] slots)
    {
        planningSlots = slots;
        RefreshDebug("撌脩?摰???Slot嚗??= " + (planningSlots != null ? planningSlots.Length : 0));
    }

    // =========================
    // Update / ???批
    // =========================
    private bool HasReachedServerTimestamp(int targetTimestamp)
    {
        int diff = PhotonNetwork.ServerTimestamp - targetTimestamp;
        return diff >= 0;
    }
    private void RestartPlanningStartCoroutine()
    {
        if (gameEnded)
            return;

        if (planningStartCoroutine != null)
        {
            StopCoroutine(planningStartCoroutine);
            planningStartCoroutine = null;
        }

        planningStartCoroutine = StartCoroutine(WaitForSyncedGameStart());
    }
    private int GetResolveStartTimestamp()
    {
        if (GameSceneStartSync.Instance != null)
            return GameSceneStartSync.Instance.GetNextBeatTimestamp(resolveLeadBeats);

        // 敺??寞?嚗???GameSceneStartSync 銝嚗停?冽??bpm ?其???
        int beatMs = Mathf.RoundToInt((60f / bpm) * 1000f);
        return PhotonNetwork.ServerTimestamp + beatMs * Mathf.Max(1, resolveLeadBeats);
    }

    private IEnumerator WaitForResolveBeatThenStart(int turnIndex, int resolveStartTimestamp, int[] myActions, int[] enemyActions)
    {
        if (gameEnded)
            yield break;

        RefreshDebug($"?嗅?銵?嚗?敺?暺?憪?Turn {turnIndex}");

        while (!HasReachedServerTimestamp(resolveStartTimestamp))
        {
            if (gameEnded)
                yield break;

            yield return null;
        }

        if (gameEnded)
            yield break;

        RefreshDebug($"???圈?嚗?憪圾??Turn {turnIndex}");

        resolveStartCoroutine = null;
        resolveCoroutine = StartCoroutine(ResolveActionsInOrderCoroutine(myActions, enemyActions));
    }
    private void Update()
    {
        if (gameEnded)
            return;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out double turnStart, out double turnDur))
            return;

        if (isResolving)
        {
            if (countdownText != null)
                countdownText.text = "__";

            return;
        }

        double remain = GetRemainingSeconds(turnStart, turnDur);

        if (countdownText != null)
        {
            float remainFloat = Mathf.Max(0f, (float)remain);
            countdownText.text = Mathf.CeilToInt(remainFloat).ToString();
        }

        if (remain <= 0f && !localSubmitted)
        {
            SubmitLocalPlan();
        }

        bool timeoutReadyToResolve = remain <= -timeoutResolveDelay;

        if (PhotonNetwork.IsMasterClient && !receivedResolution)
        {
            TryFinalizePlanning(turnIndex, timeoutReadyToResolve);
        }
    }

    private void OnClickReady()
    {
        if (gameEnded || localSubmitted || isResolving)
            return;

        SubmitLocalPlan();
    }

    private void BeginPlanningPhase()
    {
        if (gameEnded)
            return;

        int nextTurnIndex = 1;

        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_INDEX, out object oldTurnObj))
        {
            nextTurnIndex = (int)oldTurnObj + 1;
        }

        Hashtable roomProps = new Hashtable
        {
            { ROOM_PROP_TURN_INDEX, nextTurnIndex },
            { ROOM_PROP_TURN_START, PhotonNetwork.Time },
            { ROOM_PROP_TURN_DUR, (double)planningDuration }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        localSubmitted = false;
        receivedResolution = false;
        isResolving = false;

        ResetLocalTurnProps(nextTurnIndex);
        ClearAllPlanningSlots();
        ClearEnemyActionsPreview();

        SetPlanningInteractable(true);

        if (readyButton != null)
            readyButton.interactable = true;

        RefreshDebug("???啣?????Turn " + nextTurnIndex);
    }

    private void ResetLocalTurnProps(int turnIndex)
    {
        int slotCount = planningSlots != null ? planningSlots.Length : 0;
        int[] empty = new int[slotCount];

        for (int i = 0; i < empty.Length; i++)
            empty[i] = (int)ActionType.None;

        Hashtable playerProps = new Hashtable
        {
            { PLAYER_PROP_READY, false },
            { PLAYER_PROP_SUBMIT_TURN, -1 },
            { PLAYER_PROP_ACTIONS, empty }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProps);
    }

    private void SubmitLocalPlan()
    {
        if (gameEnded)
            return;

        if (!TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            return;

        int[] actions = ReadLocalSlotActions();

        Hashtable props = new Hashtable
        {
            { PLAYER_PROP_READY, true },
            { PLAYER_PROP_ACTIONS, actions },
            { PLAYER_PROP_SUBMIT_TURN, turnIndex }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);

        localSubmitted = true;

        if (readyButton != null)
            readyButton.interactable = false;

        SetPlanningInteractable(false);
        RefreshDebug("撌脫?鈭方撌梁?銵?");
    }

    private int[] ReadLocalSlotActions()
    {
        if (planningSlots == null)
            return new int[0];

        int[] actions = new int[planningSlots.Length];

        for (int i = 0; i < planningSlots.Length; i++)
        {
            actions[i] = (int)ActionType.None;

            if (planningSlots[i] == null)
                continue;

            DraggableItem childItem = planningSlots[i].GetComponentInChildren<DraggableItem>(true);
            if (childItem == null)
                continue;

            ActionDragData dragData = childItem.GetComponent<ActionDragData>();
            if (dragData != null)
                actions[i] = (int)dragData.actionType;
        }

        return actions;
    }

    private void TryFinalizePlanning(int turnIndex, bool timerExpired)
    {
        if (gameEnded)
            return;

        Player[] players = PhotonNetwork.PlayerList;
        if (players == null || players.Length < 2)
            return;

        bool allSubmitted = true;

        for (int i = 0; i < players.Length; i++)
        {
            Player p = players[i];

            bool ready = false;
            int submitTurn = -999;

            if (p.CustomProperties.TryGetValue(PLAYER_PROP_READY, out object readyObj))
                ready = (bool)readyObj;

            if (p.CustomProperties.TryGetValue(PLAYER_PROP_SUBMIT_TURN, out object turnObj))
                submitTurn = (int)turnObj;

            if (!ready || submitTurn != turnIndex)
            {
                allSubmitted = false;
                break;
            }
        }

        if (!allSubmitted && !timerExpired)
            return;

        if (receivedResolution)
            return;

        Player p1 = players[0];
        Player p2 = players[1];

        int[] p1Actions = GetPlayerActionsForTurn(p1, turnIndex);
        int[] p2Actions = GetPlayerActionsForTurn(p2, turnIndex);

        // ===== ?啣?嚗esolve 閬?憪??梁?? =====
        int resolveStartTimestamp = GetResolveStartTimestamp();

        object[] content = new object[]
        {
        turnIndex,
        resolveStartTimestamp,
        p1.ActorNumber, p1Actions,
        p2.ActorNumber, p2Actions
        };

        RaiseEventOptions options = new RaiseEventOptions
        {
            Receivers = ReceiverGroup.All
        };

        PhotonNetwork.RaiseEvent(EVENT_PLANS_READY, content, options, SendOptions.SendReliable);
        receivedResolution = true;

        RefreshDebug("Host broadcast plan resolution; waiting for shared start beat.");
    }

    private int[] GetPlayerActionsForTurn(Player player, int turnIndex)
    {
        if (player.CustomProperties.TryGetValue(PLAYER_PROP_SUBMIT_TURN, out object turnObj))
        {
            if ((int)turnObj == turnIndex &&
                player.CustomProperties.TryGetValue(PLAYER_PROP_ACTIONS, out object actionsObj))
            {
                if (actionsObj is int[] intArray)
                    return intArray;

                if (actionsObj is object[] objArray)
                {
                    int[] converted = new int[objArray.Length];
                    for (int i = 0; i < objArray.Length; i++)
                        converted[i] = Convert.ToInt32(objArray[i]);
                    return converted;
                }
            }
        }

        int slotCount = planningSlots != null ? planningSlots.Length : 0;
        int[] empty = new int[slotCount];

        for (int i = 0; i < empty.Length; i++)
            empty[i] = (int)ActionType.None;

        return empty;
    }

    // =========================
    // Photon Event
    // =========================

    public void OnEvent(EventData photonEvent)
    {
        if (gameEnded)
            return;

        if (photonEvent.Code != EVENT_PLANS_READY)
            return;

        object[] data = (object[])photonEvent.CustomData;

        int turnIndex = (int)data[0];
        int resolveStartTimestamp = (int)data[1];

        if (turnIndex == lastResolvedTurnIndex)
        {
            Debug.Log("????鈭辣???嗅嚗蕭?乓urnIndex = " + turnIndex);
            return;
        }

        int actorA = (int)data[2];
        int[] actionsA = (int[])data[3];
        int actorB = (int)data[4];
        int[] actionsB = (int[])data[5];

        int myActor = PhotonNetwork.LocalPlayer.ActorNumber;

        int[] myActions = null;
        int[] enemyActions = null;

        if (actorA == myActor)
        {
            myActions = actionsA;
            enemyActions = actionsB;
        }
        else if (actorB == myActor)
        {
            myActions = actionsB;
            enemyActions = actionsA;
        }
        else
        {
            Debug.LogError("OnEvent ?⊥????砍?拙振 ActorNumber");
            return;
        }

        lastResolvedTurnIndex = turnIndex;

        ShowEnemyActionsPreview(enemyActions);

        if (resolveStartCoroutine != null)
        {
            StopCoroutine(resolveStartCoroutine);
            resolveStartCoroutine = null;
        }

        if (resolveCoroutine != null)
        {
            StopCoroutine(resolveCoroutine);
            resolveCoroutine = null;
        }

        // ===== 銝??餉圾???寞?蝑?銝??冽?暺?=====
        resolveStartCoroutine = StartCoroutine(
            WaitForResolveBeatThenStart(turnIndex, resolveStartTimestamp, myActions, enemyActions)
        );
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(ROOM_PROP_GAME_ENDED) ||
            propertiesThatChanged.ContainsKey(ROOM_PROP_GAME_WINNER_ACTOR))
        {
            ApplyGameResultFromRoom();
        }

        if (gameEnded)
            return;

        if (propertiesThatChanged.ContainsKey(ROOM_PROP_TURN_INDEX))
        {
            localSubmitted = false;
            receivedResolution = false;
            isResolving = false;

            ClearAllPlanningSlots();
            ClearEnemyActionsPreview();

            if (readyButton != null)
                readyButton.interactable = true;

            SetPlanningInteractable(true);
            RefreshDebug("?嗅?啣???憪?撌脫?蝛?Slot");
        }
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (gameEnded)
            return;

        if (changedProps.ContainsKey(PLAYER_PROP_READY) ||
            changedProps.ContainsKey(PLAYER_PROP_ACTIONS) ||
            changedProps.ContainsKey(PLAYER_PROP_SUBMIT_TURN))
        {
            RefreshDebug("?拙振?漱?湔: " + GetPlayerDisplayName(targetPlayer));
        }
    }

    private string GetPlayerDisplayName(Player player)
    {
        if (player == null)
            return "P?";

        return player.IsMasterClient ? "P1" : "P2";
    }

    // =========================
    // ??蝯?瘚?
    // =========================

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (gameEnded)
            return;

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        FinishGame(localActorNumber, "OpponentLeft", true);
    }

    private IEnumerator ResolveActionsInOrderCoroutine(int[] myActions, int[] enemyActions)
    {
        if (gameEnded || isResolving)
            yield break;

        isResolving = true;
        SetPlanningInteractable(false);

        if (readyButton != null)
            readyButton.interactable = false;

        myHeavyPendingThisTurn = false;
        enemyHeavyPendingThisTurn = false;

        int maxLen = Mathf.Max(myActions != null ? myActions.Length : 0, enemyActions != null ? enemyActions.Length : 0);
        int step = 1;

        for (int i = 0; i < maxLen; i++)
        {
            ActionType myAction = (myActions != null && i < myActions.Length) ? (ActionType)myActions[i] : ActionType.None;
            ActionType enemyAction = (enemyActions != null && i < enemyActions.Length) ? (ActionType)enemyActions[i] : ActionType.None;

            Debug.Log($"蝚?{step} ?潘??={myAction} ?菜={enemyAction}");
            yield return StartCoroutine(ResolveSingleStepCoroutine(myAction, enemyAction));

            if (CheckGameResultAfterStep())
                yield break;

            if (pendingParryCounter)
                yield return StartCoroutine(ResolveParryCounterStepCoroutine());

            if (CheckGameResultAfterStep())
                yield break;

            step++;
        }

        RefreshDebug("?券?挾蝯?摰?");

        ClearAllPlanningSlots();

        isResolving = false;
        resolveCoroutine = null;

        if (!gameEnded)
            RestartPlanningStartCoroutine();
    }
    private IEnumerator ResolveParryCounterStepCoroutine()
    {
        if (!pendingParryCounter)
            yield break;

        bool counterByMine = pendingParryCounterByMine;
        int damage = pendingParryCounterDamage;
        playedHitFeedbackThisStep = false;
        playedFinisherSlowMotionThisStep = false;

        pendingParryCounter = false;
        pendingParryCounterByMine = false;
        pendingParryCounterDamage = 0;

        CharacterUnit attacker = counterByMine ? myUnit : enemyUnit;
        CharacterUnit victim = counterByMine ? enemyUnit : myUnit;

        if (attacker == null || victim == null)
            yield break;

        Animator attackerAnimator = attacker.GetAnimator();

        // ParryCounter ?祇??箏? 2 ??
        yield return StartCoroutine(PlayParryCounterAnimationAndWait(attackerAnimator));

        // ?賭葉銵函
        PlayHitFeedback(!counterByMine);

        if (playBloodOnParryCounter && bloodHitVFXManager != null)
            PlayBloodHitEffect(counterByMine);

        if (HitStopManager.Instance != null)
            yield return StartCoroutine(HitStopManager.Instance.HitStopByBeat());

        // ?迤???
        if (counterByMine)
        {
            int hpBefore = enemyUnit != null ? enemyUnit.currentHP : enemyHP;

            if (enemyUnit != null)
                enemyUnit.TakeDamage(damage);

            enemyHP = enemyUnit != null ? enemyUnit.currentHP : Mathf.Max(0, enemyHP - damage);
            TryPlayFinisherSlowMotion(hpBefore, enemyHP);
        }
        else
        {
            int hpBefore = myUnit != null ? myUnit.currentHP : myHP;

            if (myUnit != null)
                myUnit.TakeDamage(damage);

            myHP = myUnit != null ? myUnit.currentHP : Mathf.Max(0, myHP - damage);
            TryPlayFinisherSlowMotion(hpBefore, myHP);
        }

        RefreshAllHPUI();

        // ParryCounter 憒??銝剖????箏??? 1 ??
        float postCounterDelayBeats = 0f;

        if (playedFinisherSlowMotionThisStep)
            postCounterDelayBeats = Mathf.Max(effectDelayBeats, finisherSlowMotionBeats);
        else if (playedHitFeedbackThisStep)
            postCounterDelayBeats = effectDelayBeats;

        if (postCounterDelayBeats > 0f)
            yield return new WaitForSecondsRealtime(GetBeatSeconds(postCounterDelayBeats));
    }
    private float GetClipLength(Animator animator, string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
            return 1f;

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name == clipName)
                return clips[i].length;
        }

        Debug.LogWarning("?曆??啣???Clip: " + clipName);
        return 1f;
    }
    private void RefreshAllHPUI()
    {
        UpdateHPBars();
        UpdateEnergyBars();
    }
    private IEnumerator PlayParryCounterAnimationAndWait(Animator animator)
    {
        if (animator == null)
            yield break;

        float originalSpeed = animator.speed;

        string stateName = "ParryCounter";
        string triggerName = "ParryCounter";

        float stepDuration = GetBeatSeconds(parryCounterStepBeats);
        float clipLength = GetClipLength(animator, stateName);

        if (clipLength > 0.0001f)
            animator.speed = clipLength / stepDuration;
        else
            animator.speed = 1f;

        ResetActionTriggers(animator);
        animator.SetTrigger(triggerName);

        float enterTimeout = 0.25f;
        while (enterTimeout > 0f)
        {
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);

            if (current.IsName(stateName) || next.IsName(stateName))
                break;

            enterTimeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        yield return new WaitForSecondsRealtime(stepDuration);

        animator.speed = originalSpeed;
    }
    private ActionType GetEffectiveActionForStep(ActionType selectedAction, bool isMine, out bool releaseHeavyNow)
    {
        releaseHeavyNow = false;

        if (isMine)
        {
            // 銝??澆歇蝬??????潸????
            if (myHeavyPendingThisTurn)
            {
                myHeavyPendingThisTurn = false;
                releaseHeavyNow = true;
                return ActionType.HeavyAttack;
            }

            // ???潮?圈??鳴??脣??
            if (selectedAction == ActionType.HeavyAttack)
            {
                myHeavyPendingThisTurn = true;
                return ActionType.HeavyAttack;
            }

            return selectedAction;
        }
        else
        {
            if (enemyHeavyPendingThisTurn)
            {
                enemyHeavyPendingThisTurn = false;
                releaseHeavyNow = true;
                return ActionType.HeavyAttack;
            }

            if (selectedAction == ActionType.HeavyAttack)
            {
                enemyHeavyPendingThisTurn = true;
                return ActionType.HeavyAttack;
            }

            return selectedAction;
        }
    }
    private void PlayActionAnimation(Animator animator, ActionType action, bool isMine, int currentTurn)
    {
        if (animator == null)
            return;

        ResetActionTriggers(animator);

        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        AttackActionData attackData = GetAttackData(isMine, action);

        switch (action)
        {
            case ActionType.LightAttack:
                animator.SetTrigger("LightAttack");
                break;

            case ActionType.HeavyAttack:
                if (attackData != null && attackData.requiresCharge)
                {
                    if (unit != null && unit.ShouldReleaseHeavy(currentTurn))
                        animator.SetTrigger("HeavyAttack");
                    else
                        animator.SetTrigger("HeavyCharge");
                }
                else
                {
                    animator.SetTrigger("HeavyAttack");
                }
                break;

            case ActionType.LowAttack:
                animator.SetTrigger("LowAttack");
                break;

            case ActionType.Parry:
                animator.SetTrigger("Parry");
                break;

            case ActionType.Defense:
                animator.SetTrigger("Defense");
                break;

            case ActionType.MoveForward:
                animator.SetTrigger("MoveForward");
                break;

            case ActionType.MoveBackward:
                animator.SetTrigger("MoveBackward");
                break;

            case ActionType.Jump:
                animator.SetTrigger("Jump");
                break;

            case ActionType.Dance:
                animator.SetTrigger("Dance");
                break;

            case ActionType.Ultimate:
                animator.SetTrigger("Ultimate");
                break;
        }
    }

    private void ResetActionTriggers(Animator animator)
    {
        if (animator == null)
            return;

        animator.ResetTrigger("MoveForward");
        animator.ResetTrigger("MoveBackward");
        animator.ResetTrigger("Jump");
        animator.ResetTrigger("LightAttack");
        animator.ResetTrigger("HeavyCharge");
        animator.ResetTrigger("HeavyAttack");
        animator.ResetTrigger("LowAttack");
        animator.ResetTrigger("Parry");
        animator.ResetTrigger("ParryCounter");
        animator.ResetTrigger("Defense");
        animator.ResetTrigger("Hit");
        animator.ResetTrigger("Dance");
        animator.ResetTrigger("Ultimate");
    }
    private float GetBeatDuration()
    {
        return 60f / bpm;
    }

    private float GetBeatSeconds(float beats)
    {
        return GetBeatDuration() * beats;
    }
    private bool IsMoveAction(ActionType action)
    {
        return action == ActionType.MoveForward ||
               action == ActionType.MoveBackward;
    }
    private IEnumerator ResolveSingleStepCoroutine(ActionType myAction, ActionType enemyAction)
    {
        RefreshAnimatorReferences();

        myJumping = false;
        enemyJumping = false;
        playedHitFeedbackThisStep = false;
        playedFinisherSlowMotionThisStep = false;

        if (battleStepPlayer == null)
        {
            Debug.LogError("ResolveSingleStepCoroutine: battleStepPlayer 瘝???");
            yield break;
        }

        int currentTurn = 0;
        if (TryGetCurrentTurnInfo(out int turnIndex, out _, out _))
            currentTurn = turnIndex;

        bool myHeavyReleaseNow;
        bool enemyHeavyReleaseNow;

        ActionType myEffectiveAction = GetEffectiveActionForStep(myAction, true, out myHeavyReleaseNow);
        ActionType enemyEffectiveAction = GetEffectiveActionForStep(enemyAction, false, out enemyHeavyReleaseNow);

        // ===== ??甇亙?閮?隤唳迤?刻???=====
        myChargingHeavyThisStep = IsHeavyChargingThisStep(myEffectiveAction, myHeavyReleaseNow);
        enemyChargingHeavyThisStep = IsHeavyChargingThisStep(enemyEffectiveAction, enemyHeavyReleaseNow);
        myHeavyAnimationProtectedThisStep = IsHeavyAnimationProtectedThisStep(myEffectiveAction);
        enemyHeavyAnimationProtectedThisStep = IsHeavyAnimationProtectedThisStep(enemyEffectiveAction);

        // BattleStepPlayer 頝?TurnPlanningManager ?梁??憟?BPM / ?
        battleStepPlayer.SetBeatConfig(bpm, normalStepBeats);

        // ?箸???箏? 2 ??
        yield return StartCoroutine(
            battleStepPlayer.PlayStep(
                myUnit,
                enemyUnit,
                myAnimator,
                enemyAnimator,
                myEffectiveAction,
                enemyEffectiveAction,
                currentTurn,
                myHeavyReleaseNow,
                enemyHeavyReleaseNow,
                normalStepBeats,
                () =>
                {
                    ApplyMovement(ref distance, myEffectiveAction, true);
                    ApplyMovement(ref distance, enemyEffectiveAction, false);
                }
            )
        );

        RefreshDistance();

        ResolveCombat(
            currentTurn,
            myEffectiveAction,
            enemyEffectiveAction,
            myHeavyReleaseNow,
            enemyHeavyReleaseNow
        );

        // ??甇亙????賭葉?????嚗摰? 1 ??
        bool hasSlowMotionStep =
            myEffectiveAction == ActionType.Dance ||
            enemyEffectiveAction == ActionType.Dance;

        float postStepDelayBeats = 0f;

        if (playedFinisherSlowMotionThisStep)
            postStepDelayBeats = Mathf.Max(effectDelayBeats, finisherSlowMotionBeats);
        else if (playedHitFeedbackThisStep || hasSlowMotionStep)
            postStepDelayBeats = effectDelayBeats;

        if (postStepDelayBeats > 0f)
            yield return new WaitForSecondsRealtime(GetBeatSeconds(postStepDelayBeats));

        UpdateHPBars();
        UpdateEnergyBars();

        // ===== ??甇亦????蔭 =====
        myChargingHeavyThisStep = false;
        enemyChargingHeavyThisStep = false;
        myHeavyAnimationProtectedThisStep = false;
        enemyHeavyAnimationProtectedThisStep = false;
    }    // =========================
         // ??剜 / 蝑?
         // =========================



    private KnightUltimateVFX GetKnightUltimateVFX(bool isMine)
    {
        CharacterUnit unit = isMine ? myUnit : enemyUnit;
        if (unit == null)
            return null;

        return unit.GetComponentInChildren<KnightUltimateVFX>(true);
    }

    private void PlayKnightUltimateLightningEffect(bool attackerIsMine)
    {
        KnightUltimateVFX vfx = GetKnightUltimateVFX(attackerIsMine);
        if (vfx == null)
            return;

        vfx.PlayUltimateLightning();
    }
    private void RefreshAnimatorReferences()
    {
        if (myUnit != null && myAnimator == null)
            myAnimator = myUnit.GetAnimator();

        if (enemyUnit != null && enemyAnimator == null)
            enemyAnimator = enemyUnit.GetAnimator();
    }

    private string GetAnimationStateName(ActionType action, bool isMine)
    {
        if (action == ActionType.None)
            return null;

        ActionAnimationMap[] maps = isMine ? myAnimationMaps : enemyAnimationMaps;
        if (maps == null) return null;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].actionType == action)
                return maps[i].stateName;
        }

        return null;
    }


   
    // =========================
    // 蝘餃?
    // =========================

    private void ApplyMovement(ref int currentDistance, ActionType action, bool isMine)
    {
        switch (action)
        {
            case ActionType.MoveForward:
                currentDistance -= 1;
                if (currentDistance < 0) currentDistance = 0;
                Debug.Log(isMine ? "???蝘餃?" : "?菜??蝘餃?");
                break;

            case ActionType.MoveBackward:
                currentDistance += 1;
                Debug.Log(isMine ? "???蝘餃?" : "?菜??蝘餃?");
                break;

            case ActionType.Jump:
                if (isMine)
                    myJumping = true;
                else
                    enemyJumping = true;
                Debug.Log(isMine ? "?頝唾?" : "?菜頝唾?");
                break;
        }
    }

    private IEnumerator PlayMovementStepTimed(ActionType myAction, ActionType enemyAction, float duration)
    {
        Transform myTransform = myUnit != null ? myUnit.transform : null;
        Transform enemyTransform = enemyUnit != null ? enemyUnit.transform : null;

        if (myTransform == null && enemyTransform == null)
            yield break;

        Vector3 myStart = myTransform != null ? myTransform.position : Vector3.zero;
        Vector3 enemyStart = enemyTransform != null ? enemyTransform.position : Vector3.zero;

        Vector3 myTarget = myStart + GetMoveOffset(myUnit, enemyUnit, myAction);
        Vector3 enemyTarget = enemyStart + GetMoveOffset(enemyUnit, myUnit, enemyAction);

        ResolveNoCrossTargets(ref myTarget, ref enemyTarget, myStart, enemyStart);

        float timer = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            if (myTransform != null)
                myTransform.position = Vector3.Lerp(myStart, myTarget, t);

            if (enemyTransform != null)
                enemyTransform.position = Vector3.Lerp(enemyStart, enemyTarget, t);

            yield return null;
        }

        if (myTransform != null)
            myTransform.position = myTarget;

        if (enemyTransform != null)
            enemyTransform.position = enemyTarget;
    }

    private Vector3 GetMoveOffset(CharacterUnit selfUnit, CharacterUnit otherUnit, ActionType action)
    {
        if (!IsMoveAction(action) || selfUnit == null)
            return Vector3.zero;

        bool mirrored = IsMirroredUnit(selfUnit);


        switch (action)
        {
            case ActionType.MoveForward:
                return mirrored ? Vector3.left * moveStep : Vector3.right * moveStep;

            case ActionType.MoveBackward:
                return mirrored ? Vector3.right * moveStep : Vector3.left * moveStep;

            default:
                return Vector3.zero;
        }
    }
    private bool IsMirroredUnit(CharacterUnit unit)
    {
        if (unit == null)
            return false;

        return unit.transform.lossyScale.x < 0f;
    }
    private void EnsureCharacterInitialized(CharacterUnit unit, CharacterClassConfig config)
    {
        if (unit == null || config == null)
            return;

        // 憒???????嚗停鋆??箇?鞈?
        bool needInit =
            unit.maxHP <= 0 ||
            unit.currentHP <= 0 ||
            unit.slotCount <= 0;

        if (!needInit)
            return;

        string skinName = unit.skinName;
        if (string.IsNullOrEmpty(skinName))
            skinName = "Default";

        unit.Init(config.className, skinName, config.maxHP, config.slotCount);

        Debug.Log($"鋆?憪?閫??: {unit.name}, HP={config.maxHP}, Slots={config.slotCount}");
    }


    private void ResolveNoCrossTargets(ref Vector3 myTarget, ref Vector3 enemyTarget, Vector3 myStart, Vector3 enemyStart)
    {
        bool myIsLeft = myStart.x <= enemyStart.x;

        Vector3 leftStart = myIsLeft ? myStart : enemyStart;
        Vector3 rightStart = myIsLeft ? enemyStart : myStart;

        Vector3 leftTarget = myIsLeft ? myTarget : enemyTarget;
        Vector3 rightTarget = myIsLeft ? enemyTarget : myTarget;

        Transform leftTransform = myIsLeft ? (myUnit != null ? myUnit.transform : null)
                                           : (enemyUnit != null ? enemyUnit.transform : null);

        Transform rightTransform = myIsLeft ? (enemyUnit != null ? enemyUnit.transform : null)
                                            : (myUnit != null ? myUnit.transform : null);

        float leftHalfWidth = GetCharacterHalfWidth(leftTransform);
        float rightHalfWidth = GetCharacterHalfWidth(rightTransform);

        float requiredDistance = leftHalfWidth + rightHalfWidth + minCharacterGap;

        float leftRawX = leftTarget.x;
        float rightRawX = rightTarget.x;

        if (leftRawX <= rightRawX - requiredDistance)
            goto WRITE_BACK;

        float overlap = (leftRawX + requiredDistance) - rightRawX;

        bool leftMoved = Mathf.Abs(leftRawX - leftStart.x) > 0.001f;
        bool rightMoved = Mathf.Abs(rightRawX - rightStart.x) > 0.001f;

        if (leftMoved && rightMoved)
        {
            leftRawX -= overlap * 0.5f;
            rightRawX += overlap * 0.5f;
        }
        else if (leftMoved)
        {
            leftRawX -= overlap;
        }
        else if (rightMoved)
        {
            rightRawX += overlap;
        }

    WRITE_BACK:
        leftTarget.x = leftRawX;
        rightTarget.x = rightRawX;

        if (myIsLeft)
        {
            myTarget = leftTarget;
            enemyTarget = rightTarget;
        }
        else
        {
            myTarget = rightTarget;
            enemyTarget = leftTarget;
        }
    }
    public bool IsUltimateReady()
    {
        return myEnergy >= maxEnergy;
    }




    public bool HasQueuedUltimate(ActionSlot ignoreSlot = null)
    {
        if (planningSlots == null)
            return false;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            ActionSlot slot = planningSlots[i];
            if (slot == null || slot == ignoreSlot)
                continue;

            ActionData data = slot.GetCurrentActionData();
            if (data is UltimateActionData)
                return true;
        }

        return false;
    }

    public bool CanDragOrPlaceAction(ActionData actionData, ActionSlot ignoreSlot = null, bool verbose = false)
    {
        if (actionData == null)
            return false;

        if (gameEnded || isResolving)
            return false;

        if (localSubmitted)
            return false;

        if (actionData is UltimateActionData)
        {
            if (verbose)
            {
                Debug.Log($"CanDragOrPlaceAction: 瑼Ｘ憭扳?, myEnergy={myEnergy}, maxEnergy={maxEnergy}, queued={HasQueuedUltimate(ignoreSlot)}");
            }

            if (!CanUseUltimate())
                return false;

            if (HasQueuedUltimate(ignoreSlot))
                return false;
        }

        return true;
    }
    private float GetCharacterHalfWidth(Transform target)
    {
        if (target == null)
            return 0.5f;

        Collider2D col = target.GetComponentInChildren<Collider2D>();
        if (col != null)
            return col.bounds.extents.x;

        SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            return combined.extents.x;
        }

        return 0.5f;
    }

    // =========================
    // ?圈洛 / ?餅?
    // =========================

    private void ResolveCombat(int currentTurn, ActionType myAction, ActionType enemyAction, bool myHeavyReleaseNow, bool enemyHeavyReleaseNow)
    {
        if (myAction == ActionType.Dance)
            ApplyDance(true);

        if (enemyAction == ActionType.Dance)
            ApplyDance(false);

        // ===== ??賣擉ㄚ銝???憭扳?嚗????摰?=====
        if (myAction == ActionType.Ultimate &&
            enemyAction == ActionType.Ultimate &&
            IsKnightClass(true) &&
            IsKnightClass(false))
        {
            ResolveKnightUltimateClash();
            return;
        }

        if (myAction == ActionType.Ultimate)
            TryApplyUltimate(true, enemyAction);

        if (enemyAction == ActionType.Ultimate)
            TryApplyUltimate(false, myAction);

        if (IsAttack(myAction))
            TryApplyAttack(myAction, enemyAction, true, myHeavyReleaseNow);

        if (IsAttack(enemyAction))
            TryApplyAttack(enemyAction, myAction, false, enemyHeavyReleaseNow);
    }
    private void ResolveKnightUltimateClash()
    {
        UltimateActionData myUltimate = GetUltimateData(true);
        UltimateActionData enemyUltimate = GetUltimateData(false);

        if (myUltimate == null || enemyUltimate == null)
            return;

       

        int currentDistance = GetCurrentGridDistance();

        // ??萄??餅?蝭? 1
        if (currentDistance > 1)
        {
            Debug.Log("?????憯怠之??雿??Ｚ???1嚗?賭葉");
            ConsumeAllEnergy(true);
            ConsumeAllEnergy(false);
            return;
        }

        int myHalfDamage = Mathf.Max(1, myUltimate.damage / 2);
        int enemyHalfDamage = Mathf.Max(1, enemyUltimate.damage / 2);

        if (myUnit != null)
        {
            int hpBefore = myUnit.currentHP;
            myUnit.TakeDamage(enemyHalfDamage);
            myHP = myUnit.currentHP;
            TryPlayFinisherSlowMotion(hpBefore, myHP);
        }

        if (enemyUnit != null)
        {
            int hpBefore = enemyUnit.currentHP;
            enemyUnit.TakeDamage(myHalfDamage);
            enemyHP = enemyUnit.currentHP;
            TryPlayFinisherSlowMotion(hpBefore, enemyHP);
        }

        PlayHitFeedback(true);
        PlayHitFeedback(false);

        ConsumeAllEnergy(true);
        ConsumeAllEnergy(false);

        Debug.Log($"?擉ㄚ???之嚗??孵???{enemyHalfDamage}嚗?孵???{myHalfDamage}");
    }
    private bool IsAttack(ActionType action)
    {
        return action == ActionType.LightAttack ||
               action == ActionType.HeavyAttack ||
               action == ActionType.LowAttack;
    }

    private AttackActionData GetAttackData(bool attackerIsMine, ActionType action)
    {
        CharacterClassConfig config = attackerIsMine ? GetMyClassConfig() : GetEnemyClassConfig();
        if (config == null) return null;

        switch (action)
        {
            case ActionType.LightAttack:
                return config.lightAttack;
            case ActionType.HeavyAttack:
                return config.heavyAttack;
            case ActionType.LowAttack:
                return config.lowAttack;
            default:
                return null;
        }
    }

    private void PlayBloodHitEffect(bool attackerIsMine)
    {
        if (bloodHitVFXManager == null)
            bloodHitVFXManager = FindFirstObjectByType<BloodHitVFXManager>();

        if (bloodHitVFXManager == null)
            return;

        CharacterUnit attacker = attackerIsMine ? myUnit : enemyUnit;
        CharacterUnit victim = attackerIsMine ? enemyUnit : myUnit;

        if (attacker == null || victim == null)
            return;

        bloodHitVFXManager.PlayBloodHit(attacker.transform, victim.transform);
    }
    private ActionType GetEffectiveActionForTurn(bool isMine, ActionType selectedAction, int currentTurn)
    {
        CharacterUnit unit = isMine ? myUnit : enemyUnit;

        if (unit == null)
            return selectedAction;

        if (unit.ShouldReleaseHeavy(currentTurn))
            return ActionType.HeavyAttack;

        return selectedAction;
    }
    private void PlayParrySuccessEffect(bool defenderIsMine)
    {
        if (parrySuccessEffectPrefab == null)
            return;

        CharacterUnit targetUnit = defenderIsMine ? myUnit : enemyUnit;
        if (targetUnit == null)
            return;

        Vector3 spawnPos = targetUnit.transform.position + parrySuccessEffectOffset;
        Instantiate(parrySuccessEffectPrefab, spawnPos, Quaternion.identity);
    }

    private void TriggerBeatHitStop()
    {
        if (HitStopManager.Instance == null)
            return;

        HitStopManager.Instance.StartCoroutine(HitStopManager.Instance.HitStopByBeat());
    }

    private bool DidDamageBecomeFatal(int hpBefore, int hpAfter)
    {
        return hpBefore > 0 && hpAfter <= 0;
    }

    private void TryPlayFinisherSlowMotion(int hpBefore, int hpAfter)
    {
        if (!DidDamageBecomeFatal(hpBefore, hpAfter))
            return;

        PlayFinisherSlowMotionOnce();
    }

    private void PlayFinisherSlowMotionOnce()
    {
        if (playedFinisherSlowMotionThisStep)
            return;

        playedFinisherSlowMotionThisStep = true;

        if (HitStopManager.Instance != null)
            HitStopManager.Instance.PlaySlowMotion(finisherSlowTimeScale, GetBeatSeconds(finisherSlowMotionBeats));

        Debug.Log("Finisher slow motion triggered.");
    }

    private void TryApplyAttack(ActionType attackerAction, ActionType defenderAction, bool attackerIsMine, bool heavyReleaseNow)
    {
        CharacterUnit attackerUnit = attackerIsMine ? myUnit : enemyUnit;
        CharacterUnit defenderUnit = attackerIsMine ? enemyUnit : myUnit;

        if (attackerUnit == null || defenderUnit == null)
            return;

        AttackActionData attackData = GetAttackData(attackerIsMine, attackerAction);
        if (attackData == null)
            return;

        bool defenderJumpingNow = attackerIsMine ? enemyJumping : myJumping;
        bool defenderIsMine = !attackerIsMine;

        // ?蝚砌??澆??嚗????瑕拿
        if (attackerAction == ActionType.HeavyAttack && attackData.requiresCharge && !heavyReleaseNow)
        {
            Debug.Log((attackerIsMine ? "My" : "Enemy") + " heavy attack is charging; no damage this step.");
            return;
        }

        int currentDistance = GetCurrentGridDistance();

        AttackResolutionResult result = CombatResolver.ResolveAttackDamage(
            attackData,
            defenderAction,
            defenderJumpingNow,
            currentDistance
        );

        if (result.outOfRange)
        {
            Debug.Log($"Attack out of range. Distance={currentDistance}, range={attackData.range}");
            return;
        }

        // ===== Parry ??嚗?閬銝?亙??瘀??寞??憿? Counter 甇仿? =====
        if (result.parried)
        {
            int counterDamage = attackData.damage;

            // ?芣 Parry ???寞?嚗??券ㄐ??ParryCounter ???
            PlayParrySuccessEffect(defenderIsMine);

            // Parry ??蝞摰???
            AddEnergy(defenderIsMine, energyPerBlock);

            // ??憿???ParryCounter 摮郊撽?
            QueueParryCounter(defenderIsMine, counterDamage);

            Debug.Log((attackerIsMine ? "?" : "?菜") + " ?餅?鋡?Parry ??嚗歇?憿? ParryCounter 甇仿?嚗???= " + counterDamage);
            return;
        }

        if (result.blocked)
        {
            Debug.Log((attackerIsMine ? "?" : "?菜") + " ?餅?鋡?Defense ??");
            AddEnergy(defenderIsMine, energyPerBlock);
            return;
        }

        if (result.evaded)
        {
            Debug.Log((attackerIsMine ? "?" : "?菜") + " ?餅?鋡?Jump 頨脫?");
            return;
        }

        if (result.hit && result.damage > 0)
        {
            PlayHitFeedback(defenderIsMine);
            bool fatalDamage = false;

            if (attackerIsMine)
            {
                int hpBefore = enemyUnit.currentHP;
                enemyUnit.TakeDamage(result.damage);
                enemyHP = enemyUnit.currentHP;
                fatalDamage = DidDamageBecomeFatal(hpBefore, enemyHP);
                PlayBloodHitEffect(true);
                Debug.Log("??餅??賭葉嚗摰?= " + result.damage);
            }
            else
            {
                int hpBefore = myUnit.currentHP;
                myUnit.TakeDamage(result.damage);
                myHP = myUnit.currentHP;
                fatalDamage = DidDamageBecomeFatal(hpBefore, myHP);
                PlayBloodHitEffect(false);
                Debug.Log("?菜?餅??賭葉嚗摰?= " + result.damage);
            }

            AddEnergy(attackerIsMine, energyPerHit);

            if (fatalDamage)
                PlayFinisherSlowMotionOnce();
        }
    }
    private void QueueParryCounter(bool counterByMine, int damage)
    {
        pendingParryCounter = true;
        pendingParryCounterByMine = counterByMine;
        pendingParryCounterDamage = damage;
    }
    public bool CanUseUltimate()
    {
        return myEnergy >= maxEnergy;
    }

    private void ApplyDance(bool isMine)
    {
        AddEnergy(isMine, 1);

        if (HitStopManager.Instance != null)
            HitStopManager.Instance.PlayDanceSlowMotionWithExtraTime(GetBeatSeconds(effectDelayBeats));

        Debug.Log((isMine ? "My" : "Enemy") + " dance: gain 1 energy and play slow motion.");
    }
    private void TryApplyUltimate(bool attackerIsMine, ActionType defenderAction)
    {
        UltimateActionData ultimateData = GetUltimateData(attackerIsMine);

        if (ultimateData == null)
        {
            Debug.LogWarning("TryApplyUltimate: ultimate data missing.");
            return;
        }

        int currentEnergy = attackerIsMine ? myEnergy : enemyEnergy;
        bool defenderIsMine = !attackerIsMine;

        if (currentEnergy < maxEnergy)
        {
            Debug.Log((attackerIsMine ? "My" : "Enemy") + " ultimate energy is not full.");
            return;
        }

        int currentDistance = GetCurrentGridDistance();

        // 蝭? 1嚗??Ｖ?憭?亥蝛綽?雿?瘨??
        if (currentDistance > 1)
        {
            Debug.Log((attackerIsMine ? "?" : "?菜") + " 憭扳??芸銝哨?頝頞? 1");
            ConsumeAllEnergy(attackerIsMine);
            return;
        }

        // ===== 鋡?Parry ??嚗?閬銝?亙??瘀??寞??憿? Counter 甇仿? =====
        if (defenderAction == ActionType.Parry)
        {
            int counterDamage = ultimateData.damage;

            // ?芣 Parry ???寞?
            PlayParrySuccessEffect(defenderIsMine);

            // Parry ??蝞摰???
            AddEnergy(defenderIsMine, energyPerBlock);

            // ?餅??孵之?璅?????
            ConsumeAllEnergy(attackerIsMine);

            // ??憿? Counter 甇仿?
            QueueParryCounter(defenderIsMine, counterDamage);

            Debug.Log((attackerIsMine ? "?" : "?菜") + " ??憯怠之?◤ Parry ??嚗歇?憿? ParryCounter 甇仿?嚗???= " + counterDamage);
            return;
        }

        // ?質◤ Defense ??
        if (defenderAction == ActionType.Defense)
        {
            AddEnergy(defenderIsMine, energyPerBlock);
            ConsumeAllEnergy(attackerIsMine);

            Debug.Log((attackerIsMine ? "?" : "?菜") + " ??憯怠之?◤ Defense ??");
            return;
        }

        // 銝鋡?Jump 頨脫?嚗?隞乩??斗 Jump

        int damage = ultimateData.damage;
        bool fatalDamage = false;

        if (attackerIsMine)
        {
            int hpBefore = enemyUnit != null ? enemyUnit.currentHP : enemyHP;

            if (enemyUnit != null)
                enemyUnit.TakeDamage(damage);

            enemyHP = enemyUnit != null ? enemyUnit.currentHP : Mathf.Max(0, enemyHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, enemyHP);
            PlayHitFeedback(false);

            if (playBloodOnUltimate)
                PlayBloodHitEffect(true);

            Debug.Log("?擉ㄚ憭扳??賭葉嚗? " + damage + " ?瑕拿");
        }
        else
        {
            int hpBefore = myUnit != null ? myUnit.currentHP : myHP;

            if (myUnit != null)
                myUnit.TakeDamage(damage);

            myHP = myUnit != null ? myUnit.currentHP : Mathf.Max(0, myHP - damage);
            fatalDamage = DidDamageBecomeFatal(hpBefore, myHP);
            PlayHitFeedback(true);

            if (playBloodOnUltimate)
                PlayBloodHitEffect(false);

            Debug.Log("?菜擉ㄚ憭扳??賭葉嚗? " + damage + " ?瑕拿");
        }

        ConsumeAllEnergy(attackerIsMine);

        if (fatalDamage)
            PlayFinisherSlowMotionOnce();
    }
    private UltimateActionData GetUltimateData(bool isMine)
    {
        CharacterClassConfig config = isMine ? GetMyClassConfig() : GetEnemyClassConfig();
        if (config == null || config.ultimate == null)
            return null;

        return config.ultimate;
    }

  
    private void AddEnergy(bool isMine, int value)
    {
        if (value <= 0)
            return;

        if (isMine)
        {
            myEnergy += value;
            if (myEnergy > maxEnergy) myEnergy = maxEnergy;
            Debug.Log("??賡?憓??? " + myEnergy);
        }
        else
        {
            enemyEnergy += value;
            if (enemyEnergy > maxEnergy) enemyEnergy = maxEnergy;
            Debug.Log("?菜?賡?憓??? " + enemyEnergy);
        }

        UpdateEnergyBars();

        // ???????文??賢??函???Hit Stop
        TriggerBeatHitStop();
    }

    private void ConsumeAllEnergy(bool isMine)
    {
        if (isMine)
            myEnergy = 0;
        else
            enemyEnergy = 0;

        UpdateEnergyBars();
    }

    private void UpdateEnergyBars()
    {
        if (myEnergyBar != null)
            myEnergyBar.SetEnergy(myEnergy, maxEnergy);

        if (enemyEnergyBar != null)
            enemyEnergyBar.SetEnergy(enemyEnergy, maxEnergy);
    }

    // =========================
    // HP / UI ?湔
    // =========================

    private void UpdateHPBars()
    {
        if (myUnit != null)
            myHP = myUnit.currentHP;

        if (enemyUnit != null)
            enemyHP = enemyUnit.currentHP;

        if (myHPBar != null)
        {
            int max = myUnit != null ? myUnit.maxHP : 30;
            myHPBar.SetHP(myHP, max);
        }

        if (enemyHPBar != null)
        {
            int max = enemyUnit != null ? enemyUnit.maxHP : 30;
            enemyHPBar.SetHP(enemyHP, max);
        }
    }

    // =========================
    // ?? / Parry ?寞?
    // =========================

    private bool CheckGameResultAfterStep()
    {
        UpdateHPBars();

        if (gameEnded)
            return true;

        bool myDead = myHP <= 0;
        bool enemyDead = enemyHP <= 0;

        if (!myDead && !enemyDead)
            return false;

        int winnerActorNumber = 0;

        if (myDead && !enemyDead)
            winnerActorNumber = enemyActorNumber;
        else if (!myDead && enemyDead)
            winnerActorNumber = myActorNumber;

        FinishGame(winnerActorNumber, "HPZero", true);
        return true;
    }

    private void ApplyGameResultFromRoom()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable roomProps = PhotonNetwork.CurrentRoom.CustomProperties;
        if (roomProps == null)
            return;

        if (!roomProps.TryGetValue(ROOM_PROP_GAME_ENDED, out object endedObj) || !Convert.ToBoolean(endedObj))
            return;

        int winnerActorNumber = 0;
        if (roomProps.TryGetValue(ROOM_PROP_GAME_WINNER_ACTOR, out object winnerObj))
            winnerActorNumber = Convert.ToInt32(winnerObj);

        string reason = "HPZero";
        if (roomProps.TryGetValue(ROOM_PROP_GAME_END_REASON, out object reasonObj) && reasonObj != null)
            reason = reasonObj.ToString();

        FinishGame(winnerActorNumber, reason, false);
    }

    private void FinishGame(int winnerActorNumber, string reason, bool publishToRoom)
    {
        if (gameEnded)
            return;

        gameEnded = true;
        localSubmitted = true;
        receivedResolution = true;
        isResolving = false;
        resolveCoroutine = null;
        pendingParryCounter = false;
        pendingParryCounterByMine = false;
        pendingParryCounterDamage = 0;

        if (planningStartCoroutine != null)
        {
            StopCoroutine(planningStartCoroutine);
            planningStartCoroutine = null;
        }

        if (resolveStartCoroutine != null)
        {
            StopCoroutine(resolveStartCoroutine);
            resolveStartCoroutine = null;
        }

        SetPlanningInteractable(false);
        ClearEnemyActionsPreview();

        if (readyButton != null)
            readyButton.interactable = false;

        if (countdownText != null)
            countdownText.text = "";

        int localActorNumber = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : myActorNumber;
        GameResultManager.Instance.ShowResult(winnerActorNumber, localActorNumber, reason);

        RefreshDebug("Game ended. Winner actor = " + winnerActorNumber + ", reason = " + reason);

        if (publishToRoom)
            PublishGameResultToRoom(winnerActorNumber, reason);
    }

    private void PublishGameResultToRoom(int winnerActorNumber, string reason)
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = new Hashtable
        {
            { ROOM_PROP_GAME_ENDED, true },
            { ROOM_PROP_GAME_WINNER_ACTOR, winnerActorNumber },
            { ROOM_PROP_GAME_END_REASON, reason }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
    }

    private bool IsKnightClass(bool isMine)
    {
        CharacterClassConfig config = isMine ? GetMyClassConfig() : GetEnemyClassConfig();
        if (config == null) return false;

        return config.className == "Knight";
    }

    private void PlayKnightParryCounterFeedback(bool defenderIsMine)
    {
        CharacterUnit defenderUnit = defenderIsMine ? myUnit : enemyUnit;
        Animator defenderAnim = defenderIsMine ? myAnimator : enemyAnimator;

        if (defenderUnit == null)
            return;

        if (IsKnightClass(defenderIsMine) && defenderAnim != null)
            defenderAnim.SetTrigger("ParryCounter");

        if (parrySuccessEffectPrefab != null)
        {
            Vector3 spawnPos = defenderUnit.transform.position + parrySuccessEffectOffset;
            GameObject fx = Instantiate(parrySuccessEffectPrefab, spawnPos, Quaternion.identity);
            CameraShake.Instance.Shake(0.08f, 0.08f);
            Destroy(fx, parryEffectLifeTime);
        }
    }

    private void PlayHitFeedback(bool targetIsMine)
    {
        CharacterUnit targetUnit = targetIsMine ? myUnit : enemyUnit;
        Animator targetAnim = targetIsMine ? myAnimator : enemyAnimator;

        bool targetIsHeavyAnimationProtected = targetIsMine
            ? myHeavyAnimationProtectedThisStep
            : enemyHeavyAnimationProtectedThisStep;

        // ???葉鋡急?銝哨?銝??啣?????
        if (!targetIsHeavyAnimationProtected)
        {
            if (targetAnim != null)
                targetAnim.SetTrigger("Hit");
        }
        else
        {
            if (targetAnim != null)
                targetAnim.ResetTrigger("Hit");

            Debug.Log((targetIsMine ? "My" : "Enemy") + " heavy animation protected: damage applied without Hit animation.");
        }

        // ?? / ?????臭???
        if (targetUnit != null)
            StartCoroutine(PlayHitShakeCoroutine(targetUnit.transform));

        playedHitFeedbackThisStep = true;
    }

    private IEnumerator PlayHitShakeCoroutine(Transform target)
    {
        if (target == null)
            yield break;

        Vector3 origin = target.position;
        float timer = 0f;

        while (timer < hitShakeDuration)
        {
            timer += Time.deltaTime;
            float offsetX = UnityEngine.Random.Range(-hitShakeStrength, hitShakeStrength);
            target.position = origin + new Vector3(offsetX, 0f, 0f);
            yield return null;
        }

        target.position = origin;
    }

    // =========================
    // ?菜銵??汗
    // =========================

    private void SetupEnemyPreviewSide()
    {
        bool isHost = PhotonNetwork.IsMasterClient;

        if (isHost)
        {
            currentEnemyPreviewRoot = rightEnemyPreviewRoot;

            if (leftEnemyPreviewRoot != null)
                leftEnemyPreviewRoot.gameObject.SetActive(false);

            if (rightEnemyPreviewRoot != null)
                rightEnemyPreviewRoot.gameObject.SetActive(true);
        }
        else
        {
            currentEnemyPreviewRoot = leftEnemyPreviewRoot;

            if (leftEnemyPreviewRoot != null)
                leftEnemyPreviewRoot.gameObject.SetActive(true);

            if (rightEnemyPreviewRoot != null)
                rightEnemyPreviewRoot.gameObject.SetActive(false);
        }
    }

    private void RebuildEnemyPreviewSlots(int slotCount)
    {
        if (currentEnemyPreviewRoot == null)
        {
            Debug.LogWarning("currentEnemyPreviewRoot ?箇征嚗瘜遣蝡?寥?閬賣");
            return;
        }

        if (previewCellPrefab == null)
        {
            Debug.LogWarning("previewCellPrefab 瘝???");
            return;
        }

        for (int i = currentEnemyPreviewRoot.childCount - 1; i >= 0; i--)
            Destroy(currentEnemyPreviewRoot.GetChild(i).gameObject);

        currentEnemyPreviewImages.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            GameObject cell = Instantiate(previewCellPrefab, currentEnemyPreviewRoot);
            cell.name = "EnemyPreview_" + i;

            RectTransform rect = cell.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            Image img = cell.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = emptyPreviewSprite;
                img.color = Color.white;
                currentEnemyPreviewImages.Add(img);
            }
        }
    }

    private void BuildEnemyPreviewSpriteMap(CharacterClassConfig config)
    {
        enemyPreviewSpriteMap.Clear();

        if (config == null)
        {
            Debug.LogWarning("BuildEnemyPreviewSpriteMap: config ?箇征");
            return;
        }

        RegisterActionPreviewSprite(config.lightAttack);
        RegisterActionPreviewSprite(config.heavyAttack);
        RegisterActionPreviewSprite(config.lowAttack);
        RegisterActionPreviewSprite(config.parry);
        RegisterActionPreviewSprite(config.defense);
        RegisterActionPreviewSprite(config.dance);
        RegisterActionPreviewSprite(config.ultimate);

        RegisterActionPreviewSprite(moveForwardAction);
        RegisterActionPreviewSprite(moveBackwardAction);
        RegisterActionPreviewSprite(jumpAction);
    }

    private void RegisterActionPreviewSprite(ActionData actionData)
    {
        if (actionData == null || actionData.sourcePrefab == null)
            return;

        Image img = actionData.sourcePrefab.GetComponent<Image>();

        if (img == null || img.sprite == null)
            img = actionData.sourcePrefab.GetComponentInChildren<Image>(true);

        if (img == null || img.sprite == null)
        {
            Debug.LogWarning("???圈?閬賢?: " + actionData.actionType);
            return;
        }

        enemyPreviewSpriteMap[actionData.actionType] = img.sprite;
    }
    private int GetCurrentGridDistance()
    {
        if (myUnit == null || enemyUnit == null)
            return distance;

        float worldDistance = Mathf.Abs(enemyUnit.transform.position.x - myUnit.transform.position.x);

        // 靘雿?銝甇?moveStep 蝞?撟暹
        int gridDistance = Mathf.RoundToInt(worldDistance / moveStep);

        // ?撠??1 ?潘??踹?鞎澆云餈?霈?0
        if (gridDistance < 1)
            gridDistance = 1;

        return gridDistance;
    }
    private void RefreshDistance()
    {
        distance = GetCurrentGridDistance();
        Debug.Log("Current grid distance = " + distance);
    }
    private void ShowEnemyActionsPreview(int[] enemyActions)
    {
        if (currentEnemyPreviewImages == null || currentEnemyPreviewImages.Count == 0)
            return;

        ClearEnemyActionsPreview();

        int previewIndex = 0;
        int actionIndex = 0;

        while (actionIndex < enemyActions.Length && previewIndex < currentEnemyPreviewImages.Count)
        {
            ActionType action = (ActionType)enemyActions[actionIndex];

            SetPreviewImage(currentEnemyPreviewImages[previewIndex], action);

            int cost = GetSlotCost(action);

            if (cost == 2 && previewIndex + 1 < currentEnemyPreviewImages.Count)
                SetPreviewImage(currentEnemyPreviewImages[previewIndex + 1], action);

            previewIndex += cost;
            actionIndex += cost;
        }
    }

    private void ClearEnemyActionsPreview()
    {
        if (currentEnemyPreviewImages == null) return;

        for (int i = 0; i < currentEnemyPreviewImages.Count; i++)
        {
            if (currentEnemyPreviewImages[i] == null) continue;

            currentEnemyPreviewImages[i].sprite = emptyPreviewSprite;
            currentEnemyPreviewImages[i].color = Color.white;
        }
    }

    private void SetPreviewImage(Image target, ActionType action)
    {
        if (target == null) return;

        Sprite sprite = GetPreviewSprite(action);
        target.sprite = sprite != null ? sprite : emptyPreviewSprite;
        target.color = Color.white;
    }

    private Sprite GetPreviewSprite(ActionType action)
    {
        if (enemyPreviewSpriteMap.TryGetValue(action, out Sprite sprite))
            return sprite;

        return null;
    }

    private int GetSlotCost(ActionType action)
    {
        switch (action)
        {
            case ActionType.HeavyAttack:
                return 2;
            default:
                return 1;
        }
    }

    // =========================
    // ?祉撌亙
    // =========================

    private int GetPlayerClassIndex(Player player)
    {
        if (player != null &&
            player.CustomProperties != null &&
            player.CustomProperties.TryGetValue("classIndex", out object classObj))
        {
            return (int)classObj;
        }

        return 0;
    }

    private CharacterClassConfig GetClassConfigByIndex(int index)
    {
        if (classConfigs == null || classConfigs.Length == 0)
            return null;

        if (index < 0 || index >= classConfigs.Length)
            index = 0;

        return classConfigs[index];
    }

    private CharacterClassConfig GetMyClassConfig()
    {
        return GetClassConfigByIndex(GetPlayerClassIndex(PhotonNetwork.LocalPlayer));
    }

    private CharacterClassConfig GetEnemyClassConfig()
    {
        if (enemyUnit == null || enemyUnit.photonView == null || enemyUnit.photonView.Owner == null)
            return null;

        return GetClassConfigByIndex(GetPlayerClassIndex(enemyUnit.photonView.Owner));
    }

    private void SetPlanningInteractable(bool value)
    {
        if (readyButton != null)
            readyButton.interactable = value;

        if (planningSlots != null)
        {
            for (int i = 0; i < planningSlots.Length; i++)
            {
                if (planningSlots[i] != null)
                {
                    CanvasGroup cg = planningSlots[i].GetComponent<CanvasGroup>();
                    if (cg == null)
                        cg = planningSlots[i].gameObject.AddComponent<CanvasGroup>();

                    cg.interactable = value;
                    cg.blocksRaycasts = value;
                }
            }
        }

        ActionDragSource.GlobalDragEnabled = value;
    }

    private void ClearAllPlanningSlots()
    {
        if (planningSlots == null) return;

        for (int i = 0; i < planningSlots.Length; i++)
        {
            if (planningSlots[i] != null)
                planningSlots[i].ClearPlacedItemOnly();
        }
    }

    private bool TryGetCurrentTurnInfo(out int turnIndex, out double turnStart, out double turnDur)
    {
        turnIndex = -1;
        turnStart = 0;
        turnDur = 0;

        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_INDEX, out object turnObj))
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_START, out object startObj))
            return false;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_TURN_DUR, out object durObj))
            return false;

        turnIndex = (int)turnObj;
        turnStart = (double)startObj;
        turnDur = (double)durObj;
        return true;
    }
    private void PlayActionAnimation(Animator animator, ActionType action, bool isMine, int currentTurn, bool heavyReleaseNow)
    {
        if (animator == null)
            return;

        ResetActionTriggers(animator);

        switch (action)
        {
            case ActionType.LightAttack:
                animator.SetTrigger("LightAttack");
                break;

            case ActionType.HeavyAttack:
                if (heavyReleaseNow)
                    animator.SetTrigger("HeavyAttack");
                else
                    animator.SetTrigger("HeavyCharge");
                break;

            case ActionType.LowAttack:
                animator.SetTrigger("LowAttack");
                break;

            case ActionType.Parry:
                animator.SetTrigger("Parry");
                break;

            case ActionType.Defense:
                animator.SetTrigger("Defense");
                break;

            case ActionType.MoveForward:
                animator.SetTrigger("MoveForward");
                break;

            case ActionType.MoveBackward:
                animator.SetTrigger("MoveBackward");
                break;

            case ActionType.Jump:
                animator.SetTrigger("Jump");
                break;

            case ActionType.Dance:
                animator.SetTrigger("Dance");
                break;

            case ActionType.Ultimate:
                animator.SetTrigger("Ultimate");
                break;
        }
    }


    private double GetRemainingSeconds(double turnStart, double turnDur)
    {
        double elapsed = PhotonNetwork.Time - turnStart;
        return turnDur - elapsed;
    }

    private void RefreshDebug(string msg)
    {
        Debug.Log(msg);

        if (debugText != null)
            debugText.text = msg;
    }
}
