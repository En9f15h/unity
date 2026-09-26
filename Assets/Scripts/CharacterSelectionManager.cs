using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class CharacterSelectionManager : MonoBehaviourPunCallbacks
{
    [Header("Data")]
    [SerializeField] private CharacterSelectionDefinition[] characters;
    [SerializeField] private MapSelectionDefinition[] maps;
    [SerializeField] private string battleSceneName = "GameScene";

    [Header("Local Selectors")]
    [SerializeField] private CharacterSelectorController characterSelector;
    [SerializeField] private MapSelectorController mapSelectorController;

    [Header("Legacy Card Fallback")]
    [SerializeField] private Transform characterCardRoot;
    [SerializeField] private GameObject characterCardPrefab;
    [SerializeField] private MapSelectionController mapSelectionController;

    [Header("Panels")]
    [SerializeField] private CharacterSelectionPlayerPanel youPanel;
    [SerializeField] private CharacterSelectionPlayerPanel enemyPanel;

    [Header("Room Settings")]
    [SerializeField] private CharacterSelectionPrivacyController privacyController;
    [SerializeField] private bool defaultPublicSelection = false;

    [Header("Skill Popups")]
    [SerializeField] private CharacterSkillPopup youSkillPopup;
    [SerializeField] private CharacterSkillPopup enemySkillPopup;

    [Header("Top Bar")]
    [SerializeField] private Button backButton;
    [SerializeField] private TMP_Text roomNameText;
    [SerializeField] private TMP_Text playerCountText;
    [SerializeField] private TMP_Text statusText;

    [Header("Phase UI")]
    [SerializeField] private TMP_Text selectedMapText;
    [SerializeField] private float revealDuration = 1.5f;
    [SerializeField] private float countdownDuration = 3f;

    [Header("Optional Animation")]
    [SerializeField] private CharacterSelectionAnimationController vsAnimation;

    private readonly Dictionary<string, CharacterSelectionDefinition> characterById = new Dictionary<string, CharacterSelectionDefinition>();
    private readonly Dictionary<string, MapSelectionDefinition> mapById = new Dictionary<string, MapSelectionDefinition>();
    private readonly List<CharacterSelectionCard> characterCards = new List<CharacterSelectionCard>();

    private CharacterSelectionDefinition committedLocalCharacter;
    private int localSkinIndex;
    private string selectedMapId;
    private bool publicSelection;
    private bool localReady;
    private CharacterSelectionPhase currentPhase = CharacterSelectionPhase.Selecting;
    private double phaseStartTime = -1d;
    private bool isLoadingBattleScene;
    private bool selectorWired;
    private bool roomReadyInitialized;
    private bool pendingLocalSelectionProperties;
    private bool pendingLocalReadyProperties;

    private void Awake()
    {
        SceneTransitionManager.EnsureForCharacterSelectScene();

        if (!PhotonNetwork.IsConnected || PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("CharacterSelectionManager: not inside a Photon room, returning to StartScene.");
            SceneTransitionManager.RequestSceneTransition("StartScene");
            return;
        }
        PhotonNetwork.AutomaticallySyncScene = true;
        BuildLookupTables();
        WireStaticUi();
        RebuildCharacterCardsIfNeeded();
    }

    private void Start()
    {
        AudioManager.Instance.PlaySelectCharacterBGM();

        InitializeControllers();
        LoadRoomStateFromProperties();
        LoadLocalStateFromProperties();
        if (CanSendRoomOperations())
        {
            roomReadyInitialized = true;
            EnsureMasterRoomDefaults();
        }
        EnsureLocalSelectionDefaults();
        TryFlushPendingLocalProperties();
        RefreshAllViews();
        TryStartRevealIfReady();
    }

    private void Update()
    {
        TryInitializeRoomWhenJoined();
        TryFlushPendingLocalProperties();
        AdvanceTimedPhaseIfNeeded();
    }

    public void PreviewCharacter(CharacterSelectionDefinition definition)
    {
        if (definition == null || localReady || currentPhase != CharacterSelectionPhase.Selecting)
            return;

        int skinIndex = definition.GetValidSkinIndex(localSkinIndex);
        youPanel?.SetCharacter(definition, skinIndex);
        youSkillPopup?.SetAccessible(true);
        youSkillPopup?.BindSkills(definition.skills);
    }

    public void RestoreCommittedPreview()
    {
        RefreshLocalPanel();
    }

    public void CommitCharacterSelection(CharacterSelectionDefinition definition)
    {
        if (definition == null || localReady || currentPhase != CharacterSelectionPhase.Selecting)
            return;

        if (characterSelector != null)
        {
            characterSelector.SetCharacterById(definition.characterId);
            return;
        }

        CommitLocalSelection(definition, definition.GetValidSkinIndex(localSkinIndex), false);
    }

    public void SelectPreviousSkin()
    {
        if (characterSelector != null)
        {
            characterSelector.SelectPreviousSkin();
            return;
        }

        ChangeLegacyCharacter(-1);
    }

    public void SelectNextSkin()
    {
        if (characterSelector != null)
        {
            characterSelector.SelectNextSkin();
            return;
        }

        ChangeLegacyCharacter(1);
    }

    public void ConfirmReady()
    {
        if (localReady || currentPhase != CharacterSelectionPhase.Selecting)
            return;

        CharacterSelectionDefinition definition = characterSelector != null ? characterSelector.GetCurrentDefinition() : committedLocalCharacter;
        int skinIndex = characterSelector != null ? characterSelector.GetCurrentSkinIndex() : localSkinIndex;
        if (definition == null)
            definition = GetFirstValidCharacter();

        if (definition == null)
        {
            Debug.LogWarning("CharacterSelectionManager: cannot ready because no valid character definition exists.");
            return;
        }

        CommitLocalSelection(definition, skinIndex, true);
        RefreshAllViews();
        TryStartRevealIfReady();
    }

    public void CancelReady()
    {
        SetLocalReady(false);

        if (PhotonNetwork.IsMasterClient && currentPhase != CharacterSelectionPhase.Selecting && currentPhase != CharacterSelectionPhase.Loading)
            ResetRoomPhaseToSelecting("Local player cancelled ready.");

        RefreshAllViews();
    }

    public void RequestPublicSelectionChange(bool value)
    {
        if (!CanModifyRoomSettings() || PhotonNetwork.CurrentRoom == null)
        {
            RefreshRoomControls();
            return;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { CharacterSelectPhotonKeys.PublicSelection, value }
        });
    }

    public void RequestMapSelectionChange(MapSelectionDefinition definition)
    {
        if (!CanModifyRoomSettings() || definition == null || !definition.isUnlocked || string.IsNullOrEmpty(definition.mapId))
        {
            RefreshRoomControls();
            return;
        }

        SetRoomMap(definition);
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        else
            SceneTransitionManager.RequestSceneTransition("LobbyScene");
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps == null)
            return;

        bool selectionChanged = changedProps.ContainsKey(CharacterSelectPhotonKeys.SelectedCharacterId) ||
                                changedProps.ContainsKey(CharacterSelectPhotonKeys.SelectedSkinIndex) ||
                                changedProps.ContainsKey(CharacterSelectPhotonKeys.IsReady);

        if (!selectionChanged)
            return;

        if (targetPlayer == PhotonNetwork.LocalPlayer)
        {
            LoadLocalStateFromProperties();
            SyncSelectorToLocalState();
        }

        RefreshAllViews();

        if (PhotonNetwork.IsMasterClient)
        {
            if (currentPhase == CharacterSelectionPhase.Selecting)
                TryStartRevealIfReady();
            else if (ShouldCancelActivePhase())
                ResetRoomPhaseToSelecting("Player property changed during reveal/countdown.");
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged == null)
            return;

        CharacterSelectionPhase oldPhase = currentPhase;

        if (propertiesThatChanged.ContainsKey(CharacterSelectPhotonKeys.SelectedMapId) ||
            propertiesThatChanged.ContainsKey(CharacterSelectPhotonKeys.PublicSelection) ||
            propertiesThatChanged.ContainsKey(CharacterSelectPhotonKeys.SelectionPhase) ||
            propertiesThatChanged.ContainsKey(CharacterSelectPhotonKeys.PhaseStartTime))
        {
            LoadRoomStateFromProperties();
            RefreshAllViews();

            if (oldPhase != currentPhase)
                PlayPhaseEnteredAnimation(currentPhase);
        }

        if (oldPhase != CharacterSelectionPhase.Selecting && currentPhase == CharacterSelectionPhase.Selecting)
        {
            SetLocalReady(false);
            RefreshAllViews();
        }

        if (PhotonNetwork.IsMasterClient && currentPhase == CharacterSelectionPhase.Selecting)
            TryStartRevealIfReady();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        RefreshAllViews();
        TryStartRevealIfReady();
    }

    public override void OnJoinedRoom()
    {
        TryInitializeRoomWhenJoined();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        SetLocalReady(false);

        if (PhotonNetwork.IsMasterClient && currentPhase != CharacterSelectionPhase.Selecting)
            ResetRoomPhaseToSelecting("A player left the room.");

        RefreshAllViews();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        EnsureMasterRoomDefaults();
        RefreshAllViews();

        if (PhotonNetwork.LocalPlayer == newMasterClient && currentPhase != CharacterSelectionPhase.Selecting)
            ResetRoomPhaseToSelecting("MasterClient switched during a synchronized phase.");
    }

    public override void OnLeftRoom()
    {
        SceneTransitionManager.RequestSceneTransition("LobbyScene");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("CharacterSelectionManager: Photon disconnected: " + cause);
        SceneTransitionManager.RequestSceneTransition("StartScene");
    }

    private void BuildLookupTables()
    {
        characterById.Clear();
        if (characters != null)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                CharacterSelectionDefinition definition = characters[i];
                if (definition == null || string.IsNullOrEmpty(definition.characterId))
                    continue;

                if (!characterById.ContainsKey(definition.characterId))
                    characterById.Add(definition.characterId, definition);

                if (definition.legacyClassIndex < 0)
                    definition.legacyClassIndex = i;
            }
        }

        mapById.Clear();
        if (maps != null)
        {
            for (int i = 0; i < maps.Length; i++)
            {
                MapSelectionDefinition definition = maps[i];
                if (definition == null || string.IsNullOrEmpty(definition.mapId))
                    continue;

                if (!mapById.ContainsKey(definition.mapId))
                    mapById.Add(definition.mapId, definition);
            }
        }
    }

    private void WireStaticUi()
    {
        if (backButton != null)
            backButton.onClick.AddListener(LeaveRoom);

        if (youPanel != null)
        {
            if (characterSelector == null)
            {
                if (youPanel.PreviousSkinButton != null)
                    youPanel.PreviousSkinButton.onClick.AddListener(SelectPreviousSkin);
                if (youPanel.NextSkinButton != null)
                    youPanel.NextSkinButton.onClick.AddListener(SelectNextSkin);
            }

            if (youPanel.ConfirmButton != null)
                youPanel.ConfirmButton.onClick.AddListener(ConfirmReady);
            if (youPanel.CancelReadyButton != null)
                youPanel.CancelReadyButton.onClick.AddListener(CancelReady);
        }
    }

    private void InitializeControllers()
    {
        if (privacyController != null)
            privacyController.Initialize(this);

        if (characterSelector != null)
        {
            characterSelector.SetCharacters(characters);
            if (!selectorWired)
            {
                characterSelector.SelectionChanged += OnLocalSelectorChanged;
                selectorWired = true;
            }
        }

        if (mapSelectorController != null)
        {
            mapSelectorController.SetMaps(maps);
            mapSelectorController.MapSelected += RequestMapSelectionChange;
        }

        if (mapSelectionController != null)
        {
            mapSelectionController.SetMaps(maps);
            mapSelectionController.MapSelected += RequestMapSelectionChange;
        }
    }

    private void EnsureLocalSelectionDefaults()
    {
        CharacterSelectionDefinition definition = committedLocalCharacter;
        int skinIndex = localSkinIndex;

        if (definition == null)
            definition = characterSelector != null ? characterSelector.GetCurrentDefinition() : GetFirstValidCharacter();

        if (definition == null)
            return;

        skinIndex = definition.GetValidSkinIndex(skinIndex);
        CommitLocalSelection(definition, skinIndex, false);
        SyncSelectorToLocalState();
    }

    private void OnLocalSelectorChanged(CharacterSelectionDefinition definition, int skinIndex)
    {
        if (definition == null)
            return;

        if (localReady || currentPhase != CharacterSelectionPhase.Selecting)
        {
            SyncSelectorToLocalState();
            return;
        }

        CommitLocalSelection(definition, skinIndex, false);
        RefreshAllViews();
        TryStartRevealIfReady();
    }

    private void CommitLocalSelection(CharacterSelectionDefinition definition, int skinIndex, bool ready)
    {
        if (definition == null)
            return;

        committedLocalCharacter = definition;
        localSkinIndex = definition.GetValidSkinIndex(skinIndex);
        WriteLocalSelectionProperties(ready);
    }

    private void SyncSelectorToLocalState()
    {
        if (characterSelector == null || committedLocalCharacter == null)
            return;

        characterSelector.SetSelectionWithoutNotify(committedLocalCharacter.characterId, localSkinIndex);
    }

    private void LoadRoomStateFromProperties()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        Hashtable props = PhotonNetwork.CurrentRoom.CustomProperties;
        if (TryGetString(props, CharacterSelectPhotonKeys.SelectedMapId, out string mapId))
            selectedMapId = mapId;

        publicSelection = TryGetBool(props, CharacterSelectPhotonKeys.PublicSelection, out bool publicValue) ? publicValue : defaultPublicSelection;
        currentPhase = TryGetPhase(props, CharacterSelectPhotonKeys.SelectionPhase, out CharacterSelectionPhase phase) ? phase : CharacterSelectionPhase.Selecting;
        phaseStartTime = TryGetDouble(props, CharacterSelectPhotonKeys.PhaseStartTime, out double time) ? time : -1d;
    }

    private void LoadLocalStateFromProperties()
    {
        Player localPlayer = PhotonNetwork.LocalPlayer;
        if (localPlayer == null || localPlayer.CustomProperties == null)
            return;

        if (TryGetString(localPlayer.CustomProperties, CharacterSelectPhotonKeys.SelectedCharacterId, out string characterId))
            committedLocalCharacter = ResolveCharacter(characterId);

        if (TryGetInt(localPlayer.CustomProperties, CharacterSelectPhotonKeys.SelectedSkinIndex, out int skinIndex))
            localSkinIndex = committedLocalCharacter != null ? committedLocalCharacter.GetValidSkinIndex(skinIndex) : Mathf.Max(0, skinIndex);

        localReady = TryGetBool(localPlayer.CustomProperties, CharacterSelectPhotonKeys.IsReady, out bool readyValue) && readyValue;
    }

    private void EnsureMasterRoomDefaults()
    {
        if (!PhotonNetwork.IsMasterClient || !CanSendRoomOperations())
            return;

        Hashtable roomProps = PhotonNetwork.CurrentRoom.CustomProperties;
        Hashtable updates = new Hashtable();

        if (!roomProps.ContainsKey(CharacterSelectPhotonKeys.SelectionPhase))
            updates[CharacterSelectPhotonKeys.SelectionPhase] = (int)CharacterSelectionPhase.Selecting;

        if (!roomProps.ContainsKey(CharacterSelectPhotonKeys.PhaseStartTime))
            updates[CharacterSelectPhotonKeys.PhaseStartTime] = -1d;

        if (!roomProps.ContainsKey(CharacterSelectPhotonKeys.PublicSelection))
            updates[CharacterSelectPhotonKeys.PublicSelection] = defaultPublicSelection;

        if (!HasValidMapId(selectedMapId))
        {
            MapSelectionDefinition firstMap = GetFirstUnlockedMap();
            if (firstMap != null)
            {
                selectedMapId = firstMap.mapId;
                updates[CharacterSelectPhotonKeys.SelectedMapId] = firstMap.mapId;
            }
        }

        // Repair rooms that retained a map ID without its matching sprite-array index.
        MapSelectionDefinition selectedMap = ResolveMap(selectedMapId);
        if (selectedMap != null && selectedMap.legacyStageIndex >= 0 &&
            (!TryGetInt(roomProps, CharacterSelectPhotonKeys.LegacyStageIndex, out int stageIndex) ||
             stageIndex != selectedMap.legacyStageIndex))
        {
            updates[CharacterSelectPhotonKeys.LegacyStageIndex] = selectedMap.legacyStageIndex;
        }

        if (updates.Count > 0)
            PhotonNetwork.CurrentRoom.SetCustomProperties(updates);
    }

    private void RefreshAllViews()
    {
        Player localPlayer = PhotonNetwork.LocalPlayer;
        Player remotePlayer = GetRemotePlayer();

        if (youPanel != null)
        {
            youPanel.BindAsYou(localPlayer);
            youPanel.SetCrownVisible(localPlayer != null && localPlayer.IsMasterClient);
        }

        if (enemyPanel != null)
        {
            if (remotePlayer != null)
                enemyPanel.BindAsEnemy(remotePlayer);
            else
                enemyPanel.ClearCharacter();

            enemyPanel.SetCrownVisible(remotePlayer != null && remotePlayer.IsMasterClient);
        }

        RefreshLocalPanel();
        RefreshEnemyPanel(remotePlayer);
        RefreshTopBar();
        RefreshLegacyCards(remotePlayer);
        RefreshRoomControls();
    }

    private void RefreshLocalPanel()
    {
        if (youPanel == null)
            return;

        CharacterSelectionDefinition definition = committedLocalCharacter;
        int skinIndex = localSkinIndex;

        if (definition == null && characterSelector != null)
        {
            definition = characterSelector.GetCurrentDefinition();
            skinIndex = characterSelector.GetCurrentSkinIndex();
        }

        if (definition != null)
        {
            skinIndex = definition.GetValidSkinIndex(skinIndex);
            youPanel.SetCharacter(definition, skinIndex);
            youSkillPopup?.SetAccessible(true);
            youSkillPopup?.BindSkills(definition.skills);
        }
        else
        {
            youPanel.ShowWaitingForEnemy();
            youSkillPopup?.BindSkills(null);
        }

        bool canInteract = CanSendRoomOperations() && currentPhase == CharacterSelectionPhase.Selecting && !localReady;
        characterSelector?.SetInteractable(canInteract);
        youPanel.SetInteractable(canInteract);
        youPanel.SetReadyState(localReady);
        youPanel.SetReadyControls(localReady);
    }

    private void RefreshEnemyPanel(Player remotePlayer)
    {
        if (enemyPanel == null)
            return;

        if (remotePlayer == null)
        {
            enemyPanel.ShowWaitingForEnemy();
            enemyPanel.SetReadyState(false);
            enemyPanel.SetWaitingOverlayVisible(PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount <= 1);
            enemySkillPopup?.SetAccessible(false);
            return;
        }

        enemyPanel.SetWaitingOverlayVisible(false);

        bool revealEnemy = ShouldShowRemoteSelection(remotePlayer);
        if (!revealEnemy)
        {
            enemyPanel.ShowHiddenSelection();
            enemyPanel.SetReadyState(IsPlayerReady(remotePlayer));
            enemySkillPopup?.SetAccessible(false);
            return;
        }

        CharacterSelectionDefinition remoteDefinition = GetPlayerCharacter(remotePlayer);
        int skinIndex = GetPlayerSkinIndex(remotePlayer, remoteDefinition);
        enemyPanel.SetCharacter(remoteDefinition, skinIndex);
        enemyPanel.SetReadyState(IsPlayerReady(remotePlayer));
        enemySkillPopup?.SetAccessible(remoteDefinition != null);
        enemySkillPopup?.BindSkills(remoteDefinition != null ? remoteDefinition.skills : null);
    }

    private void RefreshTopBar()
    {
        if (roomNameText != null)
            roomNameText.text = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : string.Empty;

        if (playerCountText != null)
            playerCountText.text = string.Empty;

        if (statusText != null)
            statusText.text = GetPhaseLabel(currentPhase);

        MapSelectionDefinition map = ResolveMap(selectedMapId);
        if (selectedMapText != null)
            selectedMapText.text = map != null ? map.displayName : string.Empty;
    }

    private void RefreshRoomControls()
    {
        bool canModifyRoom = CanModifyRoomSettings();

        if (privacyController != null)
            privacyController.SetValue(publicSelection, canModifyRoom);

        if (mapSelectorController != null)
        {
            mapSelectorController.SetSelectedMapId(selectedMapId);
            mapSelectorController.SetInteractable(canModifyRoom);
        }

        if (mapSelectionController != null)
        {
            mapSelectionController.SetSelectedMapId(selectedMapId);
            mapSelectionController.SetCardsInteractable(canModifyRoom, PhotonNetwork.IsMasterClient);
        }
    }

    private void RebuildCharacterCardsIfNeeded()
    {
        if (characterSelector != null || characterCardRoot == null || characterCardPrefab == null || characters == null)
            return;

        bool canUsePanelArrows = youPanel != null && youPanel.PreviousSkinButton != null && youPanel.NextSkinButton != null;
        if (canUsePanelArrows)
        {
            characterCardRoot.gameObject.SetActive(false);
            return;
        }

        for (int i = characterCards.Count - 1; i >= 0; i--)
        {
            if (characterCards[i] != null)
                Destroy(characterCards[i].gameObject);
        }

        characterCards.Clear();

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] == null)
                continue;

            GameObject cardObject = Instantiate(characterCardPrefab, characterCardRoot);
            CharacterSelectionCard card = cardObject.GetComponent<CharacterSelectionCard>();
            if (card == null)
            {
                Debug.LogError("CharacterSelectionManager: character card prefab is missing CharacterSelectionCard.", cardObject);
                Destroy(cardObject);
                continue;
            }

            card.Initialize(characters[i], this);
            characterCards.Add(card);
        }
    }

    private void ChangeLegacyCharacter(int delta)
    {
        if (localReady || currentPhase != CharacterSelectionPhase.Selecting || characters == null || characters.Length == 0)
            return;

        int currentIndex = 0;
        if (committedLocalCharacter != null)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] == committedLocalCharacter)
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        for (int i = 0; i < characters.Length; i++)
        {
            currentIndex += delta < 0 ? -1 : 1;
            if (currentIndex < 0)
                currentIndex = characters.Length - 1;
            if (currentIndex >= characters.Length)
                currentIndex = 0;

            CharacterSelectionDefinition definition = characters[currentIndex];
            if (definition == null || string.IsNullOrEmpty(definition.characterId))
                continue;

            CommitLocalSelection(definition, 0, false);
            RefreshAllViews();
            TryStartRevealIfReady();
            return;
        }
    }

    private void RefreshLegacyCards(Player remotePlayer)
    {
        if (characterCards.Count == 0)
            return;

        string localId = committedLocalCharacter != null ? committedLocalCharacter.characterId : string.Empty;
        string remoteId = GetPlayerCharacterId(remotePlayer);
        bool hideEnemyMarker = remotePlayer == null || (!publicSelection && currentPhase == CharacterSelectionPhase.Selecting);
        bool canInteract = currentPhase == CharacterSelectionPhase.Selecting && !localReady;

        for (int i = 0; i < characterCards.Count; i++)
        {
            CharacterSelectionCard card = characterCards[i];
            if (card == null || card.Definition == null)
                continue;

            bool youSelected = card.Definition.characterId == localId;
            bool enemySelected = !string.IsNullOrEmpty(remoteId) && card.Definition.characterId == remoteId;
            card.SetInteractable(canInteract);
            card.SetMarkers(youSelected, enemySelected, hideEnemyMarker, localReady);
        }
    }

    private void AdvanceTimedPhaseIfNeeded()
    {
        if (!PhotonNetwork.IsMasterClient || !CanSendRoomOperations() || isLoadingBattleScene)
            return;

        if (currentPhase == CharacterSelectionPhase.Revealing && GetPhaseElapsed() >= revealDuration)
        {
            if (ShouldCancelActivePhase())
                ResetRoomPhaseToSelecting("Reveal phase became invalid.");
            else
                SetRoomPhase(CharacterSelectionPhase.Countdown);
        }
        else if (currentPhase == CharacterSelectionPhase.Countdown && GetPhaseElapsed() >= countdownDuration)
        {
            if (ShouldCancelActivePhase())
            {
                ResetRoomPhaseToSelecting("Countdown phase became invalid.");
                return;
            }

            SetRoomPhase(CharacterSelectionPhase.Loading);
            LoadBattleSceneAsMaster();
        }
    }

    private void TryStartRevealIfReady()
    {
        if (!PhotonNetwork.IsMasterClient || !CanSendRoomOperations() || currentPhase != CharacterSelectionPhase.Selecting)
            return;

        if (CanStartReveal())
            SetRoomPhase(CharacterSelectionPhase.Revealing);
    }

    private bool CanStartReveal()
    {
        if (PhotonNetwork.CurrentRoom == null || PhotonNetwork.CurrentRoom.PlayerCount != 2)
            return false;

        if (!HasValidMapId(selectedMapId))
            return false;

        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (!IsPlayerReady(players[i]) || !IsValidSelection(players[i]))
                return false;
        }

        return true;
    }

    private bool ShouldCancelActivePhase()
    {
        if (currentPhase == CharacterSelectionPhase.Selecting || currentPhase == CharacterSelectionPhase.Loading)
            return false;

        return !CanStartReveal();
    }

    private void SetRoomPhase(CharacterSelectionPhase phase)
    {
        if (!PhotonNetwork.IsMasterClient || !CanSendRoomOperations())
            return;

        double startTime = phase == CharacterSelectionPhase.Selecting ? -1d : PhotonNetwork.Time;
        Hashtable props = new Hashtable
        {
            { CharacterSelectPhotonKeys.SelectionPhase, (int)phase },
            { CharacterSelectPhotonKeys.PhaseStartTime, startTime }
        };

        // Carry the final map and index together before the synchronized scene transition.
        if (phase == CharacterSelectionPhase.Loading)
        {
            MapSelectionDefinition map = ResolveMap(selectedMapId);
            if (map != null)
            {
                props[CharacterSelectPhotonKeys.SelectedMapId] = map.mapId;
                if (map.legacyStageIndex >= 0)
                    props[CharacterSelectPhotonKeys.LegacyStageIndex] = map.legacyStageIndex;
            }
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        currentPhase = phase;
        phaseStartTime = startTime;
        RefreshAllViews();
        PlayPhaseEnteredAnimation(phase);
    }

    private void PlayPhaseEnteredAnimation(CharacterSelectionPhase phase)
    {
        if (phase == CharacterSelectionPhase.Revealing)
        {
            youPanel?.PlayReveal();
            enemyPanel?.PlayReveal();
            vsAnimation?.PlayTrigger("VSFlash");
        }
    }

    private void ResetRoomPhaseToSelecting(string reason)
    {
        Debug.LogWarning("CharacterSelectionManager: reset to Selecting. Reason: " + reason);
        SetRoomPhase(CharacterSelectionPhase.Selecting);
    }

    private void LoadBattleSceneAsMaster()
    {
        if (!PhotonNetwork.IsMasterClient || isLoadingBattleScene)
            return;

        isLoadingBattleScene = true;
        PhotonNetwork.AutomaticallySyncScene = true;
        SceneTransitionManager.RequestSceneTransition(battleSceneName);
    }

    private void SetLocalReady(bool ready)
    {
        localReady = ready;

        if (PhotonNetwork.LocalPlayer == null)
            return;

        if (!CanSendRoomOperations())
        {
            pendingLocalReadyProperties = true;
            return;
        }

        pendingLocalReadyProperties = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { CharacterSelectPhotonKeys.IsReady, ready },
            { CharacterSelectPhotonKeys.LegacyReady, ready }
        });
    }

    private void WriteLocalSelectionProperties(bool ready)
    {
        if (committedLocalCharacter == null || PhotonNetwork.LocalPlayer == null)
            return;

        int legacyClassIndex = GetLegacyClassIndex(committedLocalCharacter);
        int validSkinIndex = committedLocalCharacter.GetValidSkinIndex(localSkinIndex);
        localSkinIndex = validSkinIndex;
        localReady = ready;

        if (!CanSendRoomOperations())
        {
            pendingLocalSelectionProperties = true;
            return;
        }

        pendingLocalSelectionProperties = false;
        pendingLocalReadyProperties = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
        {
            { CharacterSelectPhotonKeys.SelectedCharacterId, committedLocalCharacter.characterId },
            { CharacterSelectPhotonKeys.SelectedSkinIndex, validSkinIndex },
            { CharacterSelectPhotonKeys.IsReady, ready },
            { CharacterSelectPhotonKeys.LegacyClassIndex, legacyClassIndex },
            { CharacterSelectPhotonKeys.LegacySkinIndex, validSkinIndex },
            { CharacterSelectPhotonKeys.LegacyReady, ready }
        });
    }

    private void SetRoomMap(MapSelectionDefinition definition)
    {
        if (!CanSendRoomOperations() || definition == null)
            return;

        selectedMapId = definition.mapId;
        Hashtable props = new Hashtable
        {
            { CharacterSelectPhotonKeys.SelectedMapId, definition.mapId }
        };
        if (definition.legacyStageIndex >= 0)
            props[CharacterSelectPhotonKeys.LegacyStageIndex] = definition.legacyStageIndex;

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        Debug.Log("CharacterSelectionManager: selected map = " + definition.mapId +
                  ", stage index = " + definition.legacyStageIndex);
        RefreshRoomControls();
    }

    private bool CanModifyRoomSettings()
    {
        return PhotonNetwork.IsMasterClient &&
               CanSendRoomOperations() &&
               currentPhase == CharacterSelectionPhase.Selecting &&
               !HasAnyReadyPlayer();
    }

    private void TryInitializeRoomWhenJoined()
    {
        if (roomReadyInitialized || !CanSendRoomOperations())
            return;

        roomReadyInitialized = true;
        LoadRoomStateFromProperties();
        LoadLocalStateFromProperties();
        EnsureMasterRoomDefaults();
        EnsureLocalSelectionDefaults();
        TryFlushPendingLocalProperties();
        RefreshAllViews();
        TryStartRevealIfReady();
    }

    private void TryFlushPendingLocalProperties()
    {
        if (!CanSendRoomOperations())
            return;

        if (pendingLocalSelectionProperties && committedLocalCharacter != null)
        {
            WriteLocalSelectionProperties(localReady);
            return;
        }

        if (pendingLocalReadyProperties)
            SetLocalReady(localReady);
    }

    private bool CanSendRoomOperations()
    {
        return PhotonNetwork.CurrentRoom != null &&
               PhotonNetwork.InRoom &&
               PhotonNetwork.NetworkClientState == ClientState.Joined;
    }

    private bool HasAnyReadyPlayer()
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (IsPlayerReady(players[i]))
                return true;
        }

        return false;
    }

    private bool IsValidSelection(Player player)
    {
        CharacterSelectionDefinition definition = GetPlayerCharacter(player);
        if (definition == null)
            return false;

        int skinIndex = GetPlayerSkinIndex(player, definition);
        return skinIndex >= 0 && skinIndex < definition.GetSkinCount();
    }

    private bool ShouldShowRemoteSelection(Player remotePlayer)
    {
        if (remotePlayer == null || !IsValidSelection(remotePlayer))
            return false;

        return publicSelection ||
               currentPhase == CharacterSelectionPhase.Revealing ||
               currentPhase == CharacterSelectionPhase.Countdown ||
               currentPhase == CharacterSelectionPhase.Loading;
    }

    private Player GetRemotePlayer()
    {
        Player[] players = PhotonNetwork.PlayerList;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i] != PhotonNetwork.LocalPlayer)
                return players[i];
        }

        return null;
    }

    private CharacterSelectionDefinition GetPlayerCharacter(Player player)
    {
        string id = GetPlayerCharacterId(player);
        return ResolveCharacter(id);
    }

    private string GetPlayerCharacterId(Player player)
    {
        if (player == null || player.CustomProperties == null)
            return string.Empty;

        return TryGetString(player.CustomProperties, CharacterSelectPhotonKeys.SelectedCharacterId, out string id) ? id : string.Empty;
    }

    private int GetPlayerSkinIndex(Player player, CharacterSelectionDefinition definition)
    {
        if (player == null || player.CustomProperties == null)
            return 0;

        int requested = TryGetInt(player.CustomProperties, CharacterSelectPhotonKeys.SelectedSkinIndex, out int skinIndex) ? skinIndex : 0;
        return definition != null ? definition.GetValidSkinIndex(requested) : Mathf.Max(0, requested);
    }

    private bool IsPlayerReady(Player player)
    {
        if (player == null || player.CustomProperties == null)
            return false;

        return TryGetBool(player.CustomProperties, CharacterSelectPhotonKeys.IsReady, out bool ready) && ready;
    }

    private CharacterSelectionDefinition ResolveCharacter(string characterId)
    {
        if (!string.IsNullOrEmpty(characterId) && characterById.TryGetValue(characterId, out CharacterSelectionDefinition definition))
            return definition;

        return null;
    }

    private MapSelectionDefinition ResolveMap(string mapId)
    {
        if (!string.IsNullOrEmpty(mapId) && mapById.TryGetValue(mapId, out MapSelectionDefinition definition))
            return definition;

        return null;
    }

    private bool HasValidMapId(string mapId)
    {
        MapSelectionDefinition definition = ResolveMap(mapId);
        return definition != null && definition.isUnlocked;
    }

    private CharacterSelectionDefinition GetFirstValidCharacter()
    {
        if (characters == null)
            return null;

        for (int i = 0; i < characters.Length; i++)
        {
            if (characters[i] != null && !string.IsNullOrEmpty(characters[i].characterId))
                return characters[i];
        }

        return null;
    }

    private MapSelectionDefinition GetFirstUnlockedMap()
    {
        if (maps == null)
            return null;

        for (int i = 0; i < maps.Length; i++)
        {
            if (maps[i] != null && maps[i].isUnlocked && !string.IsNullOrEmpty(maps[i].mapId))
                return maps[i];
        }

        return null;
    }

    private int GetLegacyClassIndex(CharacterSelectionDefinition definition)
    {
        if (definition == null)
            return 0;

        if (definition.legacyClassIndex >= 0)
            return definition.legacyClassIndex;

        if (characters != null)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] == definition)
                    return i;
            }
        }

        return 0;
    }

    private string GetPhaseLabel(CharacterSelectionPhase phase)
    {
        switch (phase)
        {
            case CharacterSelectionPhase.Revealing:
                return "REVEAL";
            case CharacterSelectionPhase.Countdown:
                return "READY";
            case CharacterSelectionPhase.Loading:
                return "FIGHT";
            default:
                return "SELECT";
        }
    }

    private double GetPhaseElapsed()
    {
        if (phaseStartTime < 0d)
            return 0d;

        return Math.Max(0d, PhotonNetwork.Time - phaseStartTime);
    }

    private bool TryGetString(Hashtable table, string key, out string value)
    {
        value = string.Empty;
        if (table == null || !table.TryGetValue(key, out object raw) || raw == null)
            return false;

        value = raw as string;
        return !string.IsNullOrEmpty(value);
    }

    private bool TryGetInt(Hashtable table, string key, out int value)
    {
        value = 0;
        if (table == null || !table.TryGetValue(key, out object raw) || raw == null)
            return false;

        try
        {
            value = Convert.ToInt32(raw);
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning("CharacterSelectionManager: invalid int property for key " + key);
            return false;
        }
    }

    private bool TryGetBool(Hashtable table, string key, out bool value)
    {
        value = false;
        if (table == null || !table.TryGetValue(key, out object raw) || raw == null)
            return false;

        try
        {
            value = Convert.ToBoolean(raw);
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning("CharacterSelectionManager: invalid bool property for key " + key);
            return false;
        }
    }

    private bool TryGetDouble(Hashtable table, string key, out double value)
    {
        value = -1d;
        if (table == null || !table.TryGetValue(key, out object raw) || raw == null)
            return false;

        try
        {
            value = Convert.ToDouble(raw);
            return true;
        }
        catch (Exception)
        {
            Debug.LogWarning("CharacterSelectionManager: invalid double property for key " + key);
            return false;
        }
    }

    private bool TryGetPhase(Hashtable table, string key, out CharacterSelectionPhase phase)
    {
        phase = CharacterSelectionPhase.Selecting;
        if (!TryGetInt(table, key, out int value))
            return false;

        if (!Enum.IsDefined(typeof(CharacterSelectionPhase), value))
            return false;

        phase = (CharacterSelectionPhase)value;
        return true;
    }
}
