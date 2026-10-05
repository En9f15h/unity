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

public partial class TurnPlanningManager : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [Header("Beat Settings")]
    [SerializeField] private float bpm = 120f;


    [SerializeField] private BattleStepPlayer battleStepPlayer;
    public static TurnPlanningManager Instance { get; private set; }

    [Header("Step Timing")]
    [SerializeField] private float normalStepBeats = 2f;          // Standard action step length in beats.
    [SerializeField] private float parryCounterStepBeats = 2f;    // Inserted ParryCounter step length in beats.
    [SerializeField] private float effectDelayBeats = 1f;         // Extra post-effect delay in beats.
    [Header("UI")]
    [SerializeField] private Text countdownText;
    [SerializeField] private Button readyButton;
    [SerializeField] private Text debugText;

    [Header("Planning Slots")]
    [SerializeField] private ActionSlot[] planningSlots;

    [Header("Planning Timing")]
    [SerializeField] private float planningDuration = 20f;
    [SerializeField] private float timeoutResolveDelay = 0.4f;
    private bool myHeavyPendingThisTurn = false;
    private bool enemyHeavyPendingThisTurn = false;
    private bool myRiftPendingThisTurn = false;
    private bool enemyRiftPendingThisTurn = false;
    [Header("Animation Maps")]
    [SerializeField] private ActionAnimationMap[] myAnimationMaps;
    [SerializeField] private ActionAnimationMap[] enemyAnimationMaps;


    [Header("Movement Settings")]
    [SerializeField] private float moveStep = 1f;
    [SerializeField] private float minCharacterGap = 0.2f;
    [SerializeField] private bool verboseMovementCollisionLogs = false;
    public float WorldUnitsPerTile => Mathf.Max(0.01f, moveStep);

    public float TilesToWorld(float tiles)
    {
        return tiles * WorldUnitsPerTile;
    }

    [Header("Oracle Board")]
    [SerializeField] private float boardOriginX = 0f;
    [SerializeField] private int boardMinCell = -4;
    [SerializeField] private int boardMaxCell = 4;
    [SerializeField] private int[] blockedBoardCells;
    [SerializeField] private bool constrainOracleSpecialMovementToWall = true;
    [SerializeField] private float oracleMovementWallMinX = -8.5f;
    [SerializeField] private float oracleMovementWallMaxX = 8.5f;
    [SerializeField] private int oracleShiftMaxFinalDistance = 3;

    [Header("Hit Shake")]
    [SerializeField] private float hitShakeDuration = 0.12f;
    [SerializeField] private float hitShakeStrength = 0.08f;

    [Header("Finisher Slow Motion")]
    [SerializeField] private float finisherSlowMotionBeats = 2f;
    [SerializeField] private float finisherSlowTimeScale = 0.08f;

    [Header("Health UI")]
    [SerializeField] private DirectionalHealthBarUI myHPBar;
    [SerializeField] private DirectionalHealthBarUI enemyHPBar;

    [Header("Class and Action Data")]
    [SerializeField] private CharacterClassConfig[] classConfigs;
    [SerializeField] private ActionData moveForwardAction;
    [SerializeField] private ActionData moveBackwardAction;
    [SerializeField] private ActionData jumpAction;
    private readonly HashSet<int> warnedMissingMovementColliderIds = new HashSet<int>();
    private bool myChargingHeavyThisStep = false;
    private bool enemyChargingHeavyThisStep = false;
    private bool myChargingRiftThisStep = false;
    private bool enemyChargingRiftThisStep = false;
    private bool myShiftInterruptedThisStep = false;
    private bool enemyShiftInterruptedThisStep = false;
    private bool myFadeInterruptedThisStep = false;
    private bool enemyFadeInterruptedThisStep = false;
    private int myLastShiftTurn = -999;
    private int enemyLastShiftTurn = -999;
    private int myLastFadeTurn = -999;
    private int enemyLastFadeTurn = -999;
    private bool myHeavyAnimationProtectedThisStep = false;
    private bool enemyHeavyAnimationProtectedThisStep = false;

    [Header("Enemy Preview UI")]
    [SerializeField] private RectTransform leftEnemyPreviewRoot;
    [SerializeField] private RectTransform rightEnemyPreviewRoot;
    [SerializeField] private GameObject previewCellPrefab;
    [SerializeField] private Sprite emptyPreviewSprite;
    [SerializeField] private RectTransform hostSlotRoot;
    [SerializeField] private RectTransform clientSlotRoot;
    [SerializeField, Min(0f)] private float enemyPreviewBelowSlotRootOffset = 72f;

    [Header("Enemy Progressive Reveal")]
    [SerializeField] private EnemyActionRevealController enemyActionRevealController;

    [Header("Claim UI")]
    [SerializeField] private ClaimController claimController;

    [Header("Sight UI")]
    [SerializeField] private bool mirrorEnemyPreviewSlotOrder;
    [SerializeField] private bool mirrorLocalPlanningSlotOrder;
    [SerializeField] private bool verboseSightLogs;

    [Header("Energy")]
    [SerializeField] private int maxEnergy = 10;
    [SerializeField] private int energyPerHit = 1;
    [SerializeField] private int energyPerBlock = 1;
    [SerializeField] private int resolveLeadBeats = 1;   // Beats after both plans are ready before resolve starts.

    private Coroutine resolveStartCoroutine;


    [Header("Parry Success Effect")]
    [SerializeField] private GameObject parrySuccessEffectPrefab;
    [SerializeField] private Vector3 parrySuccessEffectOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private float parryEffectLifeTime = 1f;

    [Header("Debug Combat State") ]
    [SerializeField] private int myHP = 30;
    [SerializeField] private int enemyHP = 30;
    [SerializeField] private int distance = 1;
    [Header("Blood Hit Effects")]
    [SerializeField] private BloodHitVFXManager bloodHitVFXManager;
    [SerializeField] private bool playBloodOnUltimate = true;
    [SerializeField] private bool playBloodOnParryCounter = true;

    [Header("Shift Ready Indicator")]
    [SerializeField] private Sprite shiftReadyArrowSprite;
    [SerializeField] private Color shiftReadyArrowColor = new Color(0.62f, 0.92f, 1f, 1f);
    [SerializeField] private Vector2 shiftReadyArrowScale = new Vector2(0.42f, 0.5f);
    [SerializeField] private float shiftReadyArrowCharacterSizeRatio = 0.5f;
    [SerializeField] private float shiftReadyArrowHeadPadding = 0.28f;
    [SerializeField] private int shiftReadyArrowSortingOffset = 20;

    private CharacterUnit myUnit;
    private CharacterUnit enemyUnit;

    private Animator myAnimator;
    private Animator enemyAnimator;

    private EnergyBarUI myEnergyBar;
    private EnergyBarUI enemyEnergyBar;

    private int myEnergy = 0;
    private int enemyEnergy = 0;
    private bool mySightEnergyLockedFull = false;
    private bool enemySightEnergyLockedFull = false;
    public event Action<bool, int, int> EnergyChanged;
    public event Action<int, bool> SightStateChanged;

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
    private RectTransform capturedHostSlotRoot;
    private RectTransform capturedClientSlotRoot;
    private RectTransform capturedLeftEnemyPreviewRoot;
    private RectTransform capturedRightEnemyPreviewRoot;
    private Vector2 hostSlotRootInitialAnchoredPosition;
    private Vector2 clientSlotRootInitialAnchoredPosition;
    private Vector2 leftEnemyPreviewRootInitialAnchoredPosition;
    private Vector2 rightEnemyPreviewRootInitialAnchoredPosition;
    private bool capturedInitialActionDisplayLayoutPositions;
    private bool actionDisplayLayoutActive;
    private readonly List<Image> currentEnemyPreviewImages = new List<Image>();
    private readonly List<SightSlotView> currentEnemyPreviewSightViews = new List<SightSlotView>();
    private readonly Dictionary<ActionType, Sprite> enemyPreviewSpriteMap = new Dictionary<ActionType, Sprite>();
    private readonly Dictionary<int, int> sightAcceptedSnapshotRevisions = new Dictionary<int, int>();
    private readonly Dictionary<int, int> sightLocalSnapshotRevisions = new Dictionary<int, int>();
    private bool suppressSightSlotPublishing;

    private const string ROOM_PROP_TURN_INDEX = "turnIndex";
    private const string ROOM_PROP_TURN_START = "turnStart";
    private const string ROOM_PROP_TURN_DUR = "turnDur";

    private const string PLAYER_PROP_READY = "turnReady";
    private const string PLAYER_PROP_ACTIONS = "turnActions";
    private const string PLAYER_PROP_SUBMIT_TURN = "submitTurn";
    private const string PLAYER_PROP_ACTIONS_TURN = "actionsTurn";
    private const int DEFAULT_TRANSMITTED_ACTION_SLOT_COUNT = 5;

    private const string ROOM_PROP_SIGHT_PREFIX = "Sight_";

    private const string ROOM_PROP_GAME_ENDED = "gameEnded";
    private const string ROOM_PROP_GAME_WINNER_ACTOR = "gameWinnerActor";
    private const string ROOM_PROP_GAME_END_REASON = "gameEndReason";
    [SerializeField] private int planningLeadBeats = 1;   // Beats before the planning phase starts on the shared timeline.

    private Coroutine planningStartCoroutine;
    private const byte EVENT_PLANS_READY = GamePhotonEventCodes.PlansReady;
    private const byte EVENT_ORACLE_SIGHT_ACTIVATION_REQUEST = GamePhotonEventCodes.OracleSightActivationRequest;
    private const byte EVENT_ORACLE_SIGHT_ACTIVATED = GamePhotonEventCodes.OracleSightActivated;
    private const byte EVENT_ORACLE_SIGHT_SLOT_SNAPSHOT = GamePhotonEventCodes.OracleSightSlotSnapshot;
    private const byte EVENT_CLAIM_SNAPSHOT = GamePhotonEventCodes.ClaimSnapshot;
    private bool pendingParryCounter = false;
    private bool pendingParryCounterByMine = false;
    private int pendingParryCounterDamage = 0;
    private bool gameEnded = false;
    private GameObject localShiftReadyArrow;
    private int transmittedActionSlotCount = DEFAULT_TRANSMITTED_ACTION_SLOT_COUNT;
    private bool initializedTransmittedActionsForGameScene;
    private bool clearedTransmittedActionPayloadForLeavingGameScene;
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
        PhotonNetwork.AutomaticallySyncScene = false;
        if (PhotonNetwork.IsMasterClient)
            ResetSightMatchState();

        ApplyGameResultFromRoom();

        if (gameEnded)
            return;

        SetupEnemyPreviewSide();
        CaptureInitialActionDisplayLayoutPositions();

        BindReadyButton(readyButton);

        if (!gameEnded)
            RestartPlanningStartCoroutine();

        RefreshDebug("Waiting for planning phase.");
    }

    public void ApplyLayout(ClassGameplayUILayout layout)
    {
        if (layout == null)
            return;

        bool keepActionDisplayLayout = actionDisplayLayoutActive;

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

        if (layout.MasterSlotRoot is RectTransform masterSlotRootRect)
            hostSlotRoot = masterSlotRootRect;

        if (layout.ClientSlotRoot is RectTransform clientSlotRootRect)
            clientSlotRoot = clientSlotRootRect;

        if (layout.PreviewCellPrefab != null)
            previewCellPrefab = layout.PreviewCellPrefab;

        if (layout.EmptyPreviewSprite != null)
            emptyPreviewSprite = layout.EmptyPreviewSprite;

        SetupEnemyPreviewSide();
        CaptureInitialActionDisplayLayoutPositions();

        if (keepActionDisplayLayout)
            ApplyActionDisplayLayout();

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

    private void OnDestroy()
    {
        RestoreActionDisplayLayout();

        if (initializedTransmittedActionsForGameScene)
            ClearLocalTransmittedActionPayloadForLeavingGameScene();

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

        DestroyLocalShiftReadyIndicator();
    }

    // Character registration and class setup.


    public void RegisterCharacter(CharacterUnit unit)
    {
        if (unit == null || unit.photonView == null || unit.photonView.Owner == null)
            return;

        int actorNumber = unit.photonView.OwnerActorNr;
        int classIndex = GetPlayerClassIndex(unit.photonView.Owner);
        CharacterClassConfig config = GetClassConfigByIndex(classIndex);

        // Ensure dynamically spawned units have class HP, max HP, and slot count.
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
                    {
                        myEnergyBar.Init(maxEnergy, myEnergy);
                        BindEnergyUI(myEnergyBar, true, myActorNumber);
                    }
                }
            }

            Debug.Log($"Registered local character: {unit.name}, ActorNumber={myActorNumber}, HP={unit.currentHP}/{unit.maxHP}");
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
                {
                    enemyEnergyBar.Init(maxEnergy, enemyEnergy);
                    BindEnergyUI(enemyEnergyBar, false, enemyActorNumber);
                }
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

            Debug.Log($"Registered enemy character: {unit.name}, ActorNumber={enemyActorNumber}, HP={unit.currentHP}/{unit.maxHP}");
        }
        if (GameSceneStartSync.Instance != null)
        {
            if (unit.IsMine())
                GameSceneStartSync.Instance.RegisterMyCharacter(unit.gameObject);
            else
                GameSceneStartSync.Instance.RegisterEnemyCharacter(unit.gameObject);
        }
        SyncSightEnergyLocksFromSightState();
        UpdateHPBars();
        UpdateEnergyBars();
        RefreshLocalShiftReadyIndicator();
    }

    public void SetPlanningSlots(ActionSlot[] slots)
    {
        planningSlots = slots;
        if (planningSlots != null)
            Array.Sort(planningSlots, (a, b) => GetSafeSlotIndex(a).CompareTo(GetSafeSlotIndex(b)));

        if (planningSlots != null && planningSlots.Length > 0)
            transmittedActionSlotCount = planningSlots.Length;

        RegisterLocalSightSlotViews();
        EnsureClaimControllerForCurrentUi();
        RefreshLocalShiftReadyIndicator();
        RefreshDebug("Planning slots set: " + (planningSlots != null ? planningSlots.Length : 0));
    }

    public void InitializeLocalTransmittedActionsForGameScene(int slotCount)
    {
        transmittedActionSlotCount = Mathf.Max(1, slotCount);
        initializedTransmittedActionsForGameScene = true;
        clearedTransmittedActionPayloadForLeavingGameScene = false;
        ResetLocalTurnProps(-1);
        RefreshDebug("Initialized transmitted actions: " + transmittedActionSlotCount);
    }

    public static void ClearLocalTransmittedActionPayloadForLeavingGameScene()
    {
        if (Instance != null)
        {
            Instance.ClearLocalTransmittedActionPayload();
            return;
        }

        ClearLocalTransmittedActionPayloadProperties();
        ClearTurnRoomPropertiesForLeavingGameScene();
    }

    private void ClearLocalTransmittedActionPayload()
    {
        if (clearedTransmittedActionPayloadForLeavingGameScene)
            return;

        clearedTransmittedActionPayloadForLeavingGameScene = true;
        localSubmitted = false;
        receivedResolution = false;
        isResolving = false;
        ClearPendingLocalPlan();
        HideLocalShiftReadyIndicator();
        HideAllPlanningReadyIndicators();

        ClearLocalTransmittedActionPayloadProperties();
        ClearTurnRoomPropertiesForLeavingGameScene();
    }

    private static void ClearLocalTransmittedActionPayloadProperties()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null)
            return;

        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { PLAYER_PROP_READY, false },
            { PLAYER_PROP_SUBMIT_TURN, -1 },
            { PLAYER_PROP_ACTIONS, null },
            { PLAYER_PROP_ACTIONS_TURN, -1 }
        });

        Debug.Log("[TurnPlanningManager] Cleared local transmitted action payload for leaving GameScene.");
    }

    private static void ClearTurnRoomPropertiesForLeavingGameScene()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { ROOM_PROP_TURN_INDEX, null },
            { ROOM_PROP_TURN_START, null },
            { ROOM_PROP_TURN_DUR, null }
        });

        Debug.Log("[TurnPlanningManager] Cleared turn room properties for leaving GameScene.");
    }

    private void RefreshPlanningReadyIndicatorsFromPhoton()
    {
        if (BattleUIManager.Instance == null)
            return;

        if (gameEnded || isResolving || receivedResolution)
        {
            HideAllPlanningReadyIndicators();
            return;
        }

        if (!TryGetCurrentTurnInfo(out int currentTurn, out _, out _))
        {
            HideAllPlanningReadyIndicators();
            return;
        }

        bool masterReady = false;
        bool clientReady = false;
        Player[] players = PhotonNetwork.PlayerList;

        if (players != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                Player player = players[i];
                if (player == null)
                    continue;

                bool readyForCurrentTurn = IsPlayerReadyForTurn(player, currentTurn);
                if (player.IsMasterClient)
                    masterReady = readyForCurrentTurn;
                else
                    clientReady = readyForCurrentTurn;
            }
        }

        BattleUIManager.Instance.SetReadyIndicators(masterReady, clientReady);
    }

    private bool IsPlayerReadyForTurn(Player player, int turnIndex)
    {
        if (player == null || player.CustomProperties == null)
            return false;

        bool ready = false;
        int submitTurn = -999;

        if (player.CustomProperties.TryGetValue(PLAYER_PROP_READY, out object readyObj) && readyObj != null)
            ready = Convert.ToBoolean(readyObj);

        if (player.CustomProperties.TryGetValue(PLAYER_PROP_SUBMIT_TURN, out object turnObj) && turnObj != null)
            submitTurn = Convert.ToInt32(turnObj);

        return ready && submitTurn == turnIndex;
    }

    private void SetPlayerReadyIndicator(Player player, bool active)
    {
        if (BattleUIManager.Instance == null || player == null)
            return;

        BattleUIManager.Instance.SetReadyIndicatorByOwner(player.IsMasterClient, active);
    }

    private void HideAllPlanningReadyIndicators()
    {
        if (BattleUIManager.Instance != null)
            BattleUIManager.Instance.HideAllReadyIndicators();
    }

    private void RefreshLocalShiftReadyIndicator()
    {
        if (gameEnded || localSubmitted)
        {
            HideLocalShiftReadyIndicator();
            return;
        }

        if (localShiftReadyArrow != null && localShiftReadyArrow.activeSelf)
            ShowLocalShiftReadyIndicator();
    }

    private void ShowLocalShiftReadyIndicator()
    {
        if (myUnit == null)
            return;

        if (localShiftReadyArrow == null)
            localShiftReadyArrow = CreateLocalShiftReadyArrow();

        if (localShiftReadyArrow == null)
            return;

        Transform arrowTransform = localShiftReadyArrow.transform;
        if (arrowTransform.parent != myUnit.transform)
            arrowTransform.SetParent(myUnit.transform, false);

        arrowTransform.localPosition = GetShiftReadyArrowLocalPosition(myUnit);
        arrowTransform.localRotation = Quaternion.identity;
        arrowTransform.localScale = GetShiftReadyArrowScale(myUnit);

        ApplyShiftReadyArrowSorting(myUnit);
        localShiftReadyArrow.SetActive(true);
    }

    private GameObject CreateLocalShiftReadyArrow()
    {
        GameObject arrow = new GameObject("LocalShiftReadyDownArrow");
        arrow.transform.SetParent(myUnit != null ? myUnit.transform : transform, false);

        SpriteRenderer renderer = arrow.AddComponent<SpriteRenderer>();
        renderer.sprite = shiftReadyArrowSprite != null ? shiftReadyArrowSprite : RuntimeMagicSpriteLibrary.DownArrow;
        renderer.color = shiftReadyArrowColor;

        return arrow;
    }

    private Vector3 GetShiftReadyArrowScale(CharacterUnit unit)
    {
        Vector3 fallbackScale = new Vector3(
            Mathf.Max(0.01f, shiftReadyArrowScale.x),
            Mathf.Max(0.01f, shiftReadyArrowScale.y),
            1f
        );

        if (localShiftReadyArrow == null)
            return fallbackScale;

        SpriteRenderer arrowRenderer = localShiftReadyArrow.GetComponent<SpriteRenderer>();
        if (arrowRenderer == null || arrowRenderer.sprite == null)
            return fallbackScale;

        Vector2 spriteSize = arrowRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return fallbackScale;

        if (!TryGetUnitSpriteBounds(unit, out Bounds unitBounds))
            return fallbackScale;

        float sizeRatio = Mathf.Max(0.01f, shiftReadyArrowCharacterSizeRatio);
        float targetWorldWidth = unitBounds.size.x * sizeRatio;
        float targetWorldHeight = unitBounds.size.y * sizeRatio;
        if (targetWorldWidth <= 0f || targetWorldHeight <= 0f)
            return fallbackScale;

        Transform parent = unit != null ? unit.transform : localShiftReadyArrow.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        float parentScaleX = Mathf.Max(0.0001f, Mathf.Abs(parentScale.x));
        float parentScaleY = Mathf.Max(0.0001f, Mathf.Abs(parentScale.y));
        float localScaleX = Mathf.Max(0.01f, targetWorldWidth / (spriteSize.x * parentScaleX));
        float localScaleY = Mathf.Max(0.01f, targetWorldHeight / (spriteSize.y * parentScaleY));
        if (float.IsNaN(localScaleX) || float.IsInfinity(localScaleX) ||
            float.IsNaN(localScaleY) || float.IsInfinity(localScaleY))
            return fallbackScale;

        return new Vector3(localScaleX, localScaleY, 1f);
    }

    private Vector3 GetShiftReadyArrowLocalPosition(CharacterUnit unit)
    {
        Vector3 worldPosition = unit.transform.position + Vector3.up * (1.8f + Mathf.Max(0f, shiftReadyArrowHeadPadding));

        if (TryGetUnitSpriteBounds(unit, out Bounds bounds))
            worldPosition = new Vector3(unit.transform.position.x, bounds.max.y + Mathf.Max(0f, shiftReadyArrowHeadPadding), unit.transform.position.z);

        Vector3 localPosition = unit.transform.InverseTransformPoint(worldPosition);
        localPosition.z = 0f;
        return localPosition;
    }

    private bool TryGetUnitSpriteBounds(CharacterUnit unit, out Bounds bounds)
    {
        bounds = default;
        if (unit == null)
            return false;

        SpriteRenderer[] renderers = unit.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;

            if (localShiftReadyArrow != null && renderer.transform.IsChildOf(localShiftReadyArrow.transform))
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void ApplyShiftReadyArrowSorting(CharacterUnit unit)
    {
        if (localShiftReadyArrow == null || unit == null)
            return;

        SpriteRenderer arrowRenderer = localShiftReadyArrow.GetComponent<SpriteRenderer>();
        if (arrowRenderer == null)
            return;

        SpriteRenderer referenceRenderer = GetPrimaryUnitRenderer(unit);
        if (referenceRenderer != null)
        {
            arrowRenderer.sortingLayerID = referenceRenderer.sortingLayerID;
            arrowRenderer.sortingOrder = referenceRenderer.sortingOrder + shiftReadyArrowSortingOffset;
        }
        else
        {
            arrowRenderer.sortingOrder = shiftReadyArrowSortingOffset;
        }
    }

    private SpriteRenderer GetPrimaryUnitRenderer(CharacterUnit unit)
    {
        if (unit == null)
            return null;

        SpriteRenderer[] renderers = unit.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer bestRenderer = null;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null)
                continue;

            if (localShiftReadyArrow != null && renderer.transform.IsChildOf(localShiftReadyArrow.transform))
                continue;

            if (bestRenderer == null || renderer.sortingOrder > bestRenderer.sortingOrder)
                bestRenderer = renderer;
        }

        return bestRenderer;
    }

    private void HideLocalShiftReadyIndicator()
    {
        if (localShiftReadyArrow != null)
            localShiftReadyArrow.SetActive(false);
    }

    private void DestroyLocalShiftReadyIndicator()
    {
        if (localShiftReadyArrow != null)
        {
            Destroy(localShiftReadyArrow);
            localShiftReadyArrow = null;
        }
    }


    private void EnsureCharacterInitialized(CharacterUnit unit, CharacterClassConfig config)
    {
        if (unit == null || config == null)
            return;

        // Initialize stale unit data when the prefab lifecycle has not run yet.
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

        Debug.Log($"Initialized character from class config: {unit.name}, HP={config.maxHP}, Slots={config.slotCount}");
    }


    // Class lookup helpers.

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


    private void RefreshDebug(string msg)
    {
        Debug.Log(msg);

        if (debugText != null)
            debugText.text = msg;
    }
}
