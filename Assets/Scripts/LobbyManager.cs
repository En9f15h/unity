using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Debug = UnityEngine.Debug;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    private const string LocalizationTable = "StringTable";
    private const string RoomPropPasswordHash = "pwHash";
    private const string RoomPropHasPassword = "hasPw";
    private const string LegacyRoomPropPassword = "pw";

    [Header("Basic UI")]
    [SerializeField] private InputField InputRoomName;
    [SerializeField] private InputField InputPlayerName;
    [SerializeField] private InputField InputRoomPassword;
    [SerializeField] private GameObject RoomsPanel;
    [SerializeField] private GameObject RoomCanva;
    [SerializeField] private GameObject TutorialCanva;
    [SerializeField] private Text TextRoomList;
    [SerializeField] private Text OutPutText;


    [Header("Room List UI")]
    [SerializeField] private ScrollRect RoomListScrollRect;
    [SerializeField] private RectTransform RoomListContent;
    [SerializeField] private Scrollbar RoomListVerticalScrollbar;

    [Header("Region UI")]
    [SerializeField] private Dropdown RegionDropdown;
    [SerializeField] private Text CurrentRegionText;
    [SerializeField] private Text RegionRoomCountText;
    [SerializeField] private Button RegionApplyButton;

    [SerializeField] private MonoBehaviour DoomManager;

    private readonly Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();
    private readonly List<RoomListItem> activeRoomItems = new List<RoomListItem>();

    private readonly Dictionary<string, string> regionCodeByDropdownText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Asia / Singapore", "asia" },
        { "Hong Kong", "hk" },
        { "Japan / Tokyo", "jp" },
        { "South Korea / Seoul", "kr" },
        { "USA West / San Jose", "usw" }
    };

    private readonly List<string> fallbackRegionDisplayNames = new List<string>
    {
        "Asia / Singapore",
        "Hong Kong",
        "Japan / Tokyo",
        "South Korea / Seoul",
        "USA West / San Jose"
    };

    private bool isChangingRegion;
    private bool regionSwitchConnectAttemptStarted;
    private bool suppressRegionDropdownEvent;
    private bool isLeavingAfterPasswordFailure;
    private string pendingRegionCode = "";
    private string pendingJoinPassword = "";

    private void Start()
    {
        AudioManager.Instance.PlayLobbyBGM();

        Debug.Log("Lobby Start: IsConnected = " + PhotonNetwork.IsConnected);
        Debug.Log("Lobby Start: IsConnectedAndReady = " + PhotonNetwork.IsConnectedAndReady);

        SetupRegionDropdown();
        DisableRegionApplyButton();
        EnsureRegionRoomCountText();
        EnsureRoomListUI();
        ClearRoomCacheAndUI(false);
        UpdateRegionText();
        DisablePlayerNameInput();

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Not connected. Returning to StartScene.");
            SceneTransitionManager.RequestSceneTransition("StartScene");
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
        {
            Debug.Log("Connected and ready. Joining lobby.");
            PhotonNetwork.JoinLobby();
        }
    }

    private void OnDestroy()
    {
        if (RegionDropdown != null)
            RegionDropdown.onValueChanged.RemoveListener(OnRegionDropdownValueChanged);
    }

    private void SetupRegionDropdown()
    {
        if (RegionDropdown == null)
            return;

        RegionDropdown.ClearOptions();
        RegionDropdown.AddOptions(fallbackRegionDisplayNames);

        RegionDropdown.onValueChanged.RemoveListener(OnRegionDropdownValueChanged);
        RegionDropdown.onValueChanged.AddListener(OnRegionDropdownValueChanged);

        SyncRegionDropdownToConnectedRegion();
    }

    private void DisableRegionApplyButton()
    {
        if (RegionApplyButton == null)
        {
            GameObject applyButtonObject = GameObject.Find("ChangeRegionButton");
            if (applyButtonObject != null)
                RegionApplyButton = applyButtonObject.GetComponent<Button>();
        }

        if (RegionApplyButton == null)
            return;

        RegionApplyButton.onClick.RemoveListener(ChangeRegionButton);
        RegionApplyButton.gameObject.SetActive(false);
    }

    private void EnsureRegionRoomCountText()
    {
        if (RegionRoomCountText == null)
            RegionRoomCountText = CurrentRegionText;

        if (RegionRoomCountText == null && RegionDropdown != null)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject countObject = new GameObject("RegionRoomCountText", typeof(RectTransform));
            countObject.transform.SetParent(RegionDropdown.transform.parent, false);

            RegionRoomCountText = countObject.AddComponent<Text>();
            RegionRoomCountText.font = font;
            RegionRoomCountText.fontSize = 30;
            RegionRoomCountText.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        }

        if (RegionRoomCountText == null || RegionDropdown == null)
            return;

        RegionRoomCountText.alignment = TextAnchor.MiddleLeft;
        RegionRoomCountText.raycastTarget = false;
        PositionRoomCountTextBesideDropdown();
    }
    public void ExitGame()=>  Application.Quit();
    private void PositionRoomCountTextBesideDropdown()
    {
        if (RegionRoomCountText == null || RegionDropdown == null)
            return;

        RectTransform dropdownRect = RegionDropdown.GetComponent<RectTransform>();
        RectTransform countRect = RegionRoomCountText.GetComponent<RectTransform>();
        if (dropdownRect == null || countRect == null)
            return;

        if (countRect.parent != dropdownRect.parent)
            countRect.SetParent(dropdownRect.parent, false);

        countRect.anchorMin = dropdownRect.anchorMin;
        countRect.anchorMax = dropdownRect.anchorMax;
        countRect.pivot = new Vector2(0f, 0.5f);
        countRect.sizeDelta = new Vector2(260f, dropdownRect.sizeDelta.y);

        float dropdownRight = dropdownRect.anchoredPosition.x + dropdownRect.sizeDelta.x * (1f - dropdownRect.pivot.x);
        countRect.anchoredPosition = new Vector2(dropdownRight + 24f, dropdownRect.anchoredPosition.y);
    }

    private void UpdateRegionText()
    {
        SyncRegionDropdownToConnectedRegion();
        UpdateRoomCountText(isChangingRegion);
    }

    private void UpdateRoomCountText(bool showSwitchingState)
    {
        EnsureRegionRoomCountText();

        if (RegionRoomCountText == null)
            return;

        RegionRoomCountText.text = showSwitchingState
            ? GetLocalizedText("LobbyRoomsLoading", "ROOMS: --")
            : string.Format(GetLocalizedText("LobbyRoomsCount", "ROOMS: {0}"), cachedRooms.Count);
    }

    private void SyncRegionDropdownToConnectedRegion()
    {
        if (RegionDropdown == null || isChangingRegion)
            return;

        int index = GetRegionIndex(PhotonNetwork.CloudRegion);
        if (index < 0 && PhotonNetwork.PhotonServerSettings != null)
            index = GetRegionIndex(PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion);

        if (index < 0)
            index = 0;

        suppressRegionDropdownEvent = true;
        RegionDropdown.SetValueWithoutNotify(index);
        RegionDropdown.RefreshShownValue();
        suppressRegionDropdownEvent = false;
    }

    private int GetRegionIndex(string regionCode)
    {
        string normalizedRegionCode = NormalizePhotonRegionCode(regionCode);
        if (string.IsNullOrEmpty(normalizedRegionCode) || RegionDropdown == null)
            return -1;

        for (int i = 0; i < RegionDropdown.options.Count; i++)
        {
            if (TryGetRegionCodeForDropdownIndex(i, out string optionRegionCode) &&
                string.Equals(optionRegionCode, normalizedRegionCode, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private string GetRegionLabel(string regionCode)
    {
        string normalizedRegionCode = NormalizePhotonRegionCode(regionCode);
        int index = GetRegionIndex(normalizedRegionCode);

        if (RegionDropdown != null && index >= 0 && index < RegionDropdown.options.Count)
            return RegionDropdown.options[index].text + " (" + normalizedRegionCode + ")";

        return string.IsNullOrEmpty(normalizedRegionCode) ? "Not connected" : normalizedRegionCode;
    }

    private bool TryGetRegionCodeForDropdownIndex(int index, out string regionCode)
    {
        regionCode = "";

        if (RegionDropdown == null || index < 0 || index >= RegionDropdown.options.Count)
            return false;

        string optionText = RegionDropdown.options[index].text;
        if (string.IsNullOrWhiteSpace(optionText))
            return false;

        if (regionCodeByDropdownText.TryGetValue(optionText.Trim(), out regionCode))
            return true;

        string trimmedText = optionText.Trim().ToLowerInvariant();
        if (regionCodeByDropdownText.ContainsValue(trimmedText))
        {
            regionCode = trimmedText;
            return true;
        }

        return false;
    }

    private string NormalizePhotonRegionCode(string regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
            return "";

        string normalizedRegionCode = regionCode.Trim().ToLowerInvariant();
        int slashIndex = normalizedRegionCode.IndexOf('/');
        if (slashIndex >= 0)
            normalizedRegionCode = normalizedRegionCode.Substring(0, slashIndex);

        return normalizedRegionCode;
    }

    private void OnRegionDropdownValueChanged(int index)
    {
        if (suppressRegionDropdownEvent)
            return;

        if (!TryGetRegionCodeForDropdownIndex(index, out string targetRegion))
        {
            SetOutput("LobbyRegionUnavailable", "Region is not available.");
            SyncRegionDropdownToConnectedRegion();
            return;
        }

        RequestRegionChange(targetRegion);
    }

    public void ChangeRegionButton()
    {
        PlayLobbyButtonSound();
        RequestSelectedRegionChange();
    }

    private void RequestSelectedRegionChange()
    {
        if (RegionDropdown == null)
        {
            Debug.LogError("RegionDropdown is not assigned.");
            return;
        }

        if (!TryGetRegionCodeForDropdownIndex(RegionDropdown.value, out string targetRegion))
        {
            SetOutput("LobbyRegionUnavailable", "Region is not available.");
            return;
        }

        RequestRegionChange(targetRegion);
    }

    private void RequestRegionChange(string targetRegion)
    {
        string normalizedTargetRegion = NormalizePhotonRegionCode(targetRegion);
        if (string.IsNullOrEmpty(normalizedTargetRegion))
            return;

        if (isChangingRegion)
        {
            SetOutput("LobbyChangingRegion", "Changing region...");
            return;
        }

        string currentRegion = NormalizePhotonRegionCode(PhotonNetwork.CloudRegion);
        if (PhotonNetwork.IsConnectedAndReady &&
            string.Equals(currentRegion, normalizedTargetRegion, StringComparison.OrdinalIgnoreCase))
        {
            SetOutput("LobbyAlreadyInRegion", "Already in this region.");
            UpdateRegionText();
            return;
        }

        pendingRegionCode = normalizedTargetRegion;
        isChangingRegion = true;
        regionSwitchConnectAttemptStarted = false;
        SetRegionDropdownInteractable(false);
        ClearRoomCacheAndUI(true);
        SetOutput("LobbyChangingRegion", "Changing region...");

        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        else
            ConnectToPendingRegion();
    }

    private void ConnectToPendingRegion()
    {
        if (string.IsNullOrEmpty(pendingRegionCode))
        {
            FinishRegionSwitchFailure("Missing pending region.");
            return;
        }

        if (PhotonNetwork.PhotonServerSettings == null)
        {
            FinishRegionSwitchFailure("Photon server settings are missing.");
            return;
        }

        PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = pendingRegionCode;
        regionSwitchConnectAttemptStarted = true;

        Debug.Log("[PhotonRegion] Reconnecting to region: " + pendingRegionCode);
        SetOutput("LobbyConnecting", "Connecting...");

        bool connectStarted = PhotonNetwork.ConnectUsingSettings();
        if (!connectStarted)
            FinishRegionSwitchFailure("ConnectUsingSettings did not start.");
    }

    private void FinishRegionSwitchFailure(string message)
    {
        isChangingRegion = false;
        regionSwitchConnectAttemptStarted = false;
        pendingRegionCode = "";
        SetRegionDropdownInteractable(true);
        ClearRoomCacheAndUI(false);
        SyncRegionDropdownToConnectedRegion();
        SetOutput("LobbyChangeRegionFailed", "Could not change region.");
    }

    private void SetRegionDropdownInteractable(bool value)
    {
        if (RegionDropdown != null)
            RegionDropdown.interactable = value;
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("[PhotonRegion] Connected to master. Region = " + PhotonNetwork.CloudRegion);
        UpdateRegionText();

        if (!PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[PhotonRegion] Joined lobby. Region = " + PhotonNetwork.CloudRegion);

        bool completedRegionSwitch = isChangingRegion;
        isChangingRegion = false;
        regionSwitchConnectAttemptStarted = false;
        pendingRegionCode = "";
        SetRegionDropdownInteractable(true);

        ClearRoomCacheAndUI(false);
        UpdateRegionText();

        if (completedRegionSwitch)
            SetOutput("LobbyRegionChanged", "Region changed.");
        else
            SetOutput("LobbyJoined", "Lobby joined.");
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("Created room: " + PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Create room failed: " + message);
        SetOutput("LobbyCreateRoomFailed", "Could not create room.");
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room.");

        if (!ValidateJoinedRoomPassword())
            return;

        pendingJoinPassword = "";
        SceneTransitionManager.RequestSceneTransition("CharacterSelectScene");
    }

    public override void OnLeftRoom()
    {
        if (!isLeavingAfterPasswordFailure)
            return;

        isLeavingAfterPasswordFailure = false;
        pendingJoinPassword = "";
        SetOutput("LobbyWrongPassword", "Wrong password.");

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        pendingJoinPassword = "";
        Debug.LogError("Join room failed: " + message);
        SetOutput("LobbyJoinRoomFailed", "Could not join room.");
    }

    public void showRoomsPanel()
    {
        PlayLobbyButtonSound();

        if (RoomsPanel != null)
            RoomsPanel.SetActive(true);

        EnsureRoomListUI();
        RefreshRoomListUI();
    }

    public void hideRoomsPanel()
    {
        PlayLobbyButtonSound();

        if (RoomsPanel != null)
            RoomsPanel.SetActive(false);
    }

    public void showRoomsCanva()
    {
        PlayLobbyButtonSound();

        if (RoomCanva != null)
            RoomCanva.SetActive(true);
    }

    public void hideRoomsCanva()
    {
        PlayLobbyButtonSound();

        if (RoomCanva != null)
            RoomCanva.SetActive(false);
    }

    public void showTutorialCanva()
    {
        PlayLobbyButtonSound();

        if (RoomCanva != null)
            TutorialCanva.SetActive(true);
    }

    public void hideTutorialCanva()
    {
        PlayLobbyButtonSound();

        if (RoomCanva != null)
            TutorialCanva.SetActive(false);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo roomInfo in roomList)
        {
            if (roomInfo.RemovedFromList)
            {
                cachedRooms.Remove(roomInfo.Name);
                continue;
            }

            cachedRooms[roomInfo.Name] = roomInfo;
        }

        RefreshRoomListUI();
        UpdateRoomCountText(false);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("Photon disconnected: " + cause);

        if (isChangingRegion)
        {
            if (!regionSwitchConnectAttemptStarted)
            {
                ConnectToPendingRegion();
                return;
            }

            FinishRegionSwitchFailure(cause.ToString());
            return;
        }

        SetRegionDropdownInteractable(true);
        UpdateRegionText();
    }

    private void EnsureRoomListUI()
    {
        if (RoomListScrollRect == null)
            RoomListScrollRect = ResolveRoomListScrollRect();

        if (RoomListScrollRect == null)
            return;

        RoomListScrollRect.horizontal = false;
        RoomListScrollRect.vertical = true;
        RoomListScrollRect.movementType = ScrollRect.MovementType.Clamped;

        if (RoomListScrollRect.horizontalScrollbar != null)
        {
            RoomListScrollRect.horizontalScrollbar.gameObject.SetActive(false);
            RoomListScrollRect.horizontalScrollbar = null;
        }

        if (RoomListVerticalScrollbar == null)
            RoomListVerticalScrollbar = RoomListScrollRect.verticalScrollbar != null
                ? RoomListScrollRect.verticalScrollbar
                : FindVerticalScrollbar(RoomListScrollRect.transform);

        if (RoomListVerticalScrollbar != null)
        {
            RoomListVerticalScrollbar.gameObject.SetActive(true);
            RoomListScrollRect.verticalScrollbar = RoomListVerticalScrollbar;
        }

        RectTransform viewport = EnsureRoomListViewport();
        if (viewport == null)
            return;

        RoomListContent = EnsureRoomListContent(viewport);
        ConfigureRoomListContent(RoomListContent);

        if (TextRoomList != null)
        {
            TextRoomList.text = "";
            TextRoomList.enabled = false;
        }
    }

    private ScrollRect ResolveRoomListScrollRect()
    {
        if (TextRoomList != null)
        {
            ScrollRect scrollRect = TextRoomList.GetComponentInParent<ScrollRect>(true);
            if (scrollRect != null && scrollRect.GetComponentInParent<Dropdown>(true) == null)
                return scrollRect;
        }

        ScrollRect[] scrollRects = FindObjectsByType<ScrollRect>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < scrollRects.Length; i++)
        {
            ScrollRect scrollRect = scrollRects[i];
            if (scrollRect == null || scrollRect.GetComponentInParent<Dropdown>(true) != null)
                continue;

            if (scrollRect.name == "Scroll View")
                return scrollRect;
        }

        return null;
    }

    private Scrollbar FindVerticalScrollbar(Transform root)
    {
        if (root == null)
            return null;

        Scrollbar[] scrollbars = root.GetComponentsInChildren<Scrollbar>(true);
        for (int i = 0; i < scrollbars.Length; i++)
        {
            Scrollbar scrollbar = scrollbars[i];
            if (scrollbar == null)
                continue;

            if (scrollbar.direction == Scrollbar.Direction.BottomToTop ||
                scrollbar.direction == Scrollbar.Direction.TopToBottom ||
                scrollbar.name.IndexOf("Vertical", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return scrollbar;
            }
        }

        return null;
    }

    private RectTransform EnsureRoomListViewport()
    {
        if (RoomListScrollRect == null)
            return null;

        RectTransform existingContent = RoomListScrollRect.content;
        RectTransform viewport = RoomListScrollRect.viewport;
        if (viewport == null)
        {
            GameObject viewportObject = new GameObject("RoomListViewport", typeof(RectTransform));
            viewportObject.transform.SetParent(RoomListScrollRect.transform, false);
            viewportObject.transform.SetSiblingIndex(0);

            viewport = viewportObject.GetComponent<RectTransform>();
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = new Vector2(RoomListVerticalScrollbar != null ? -24f : 0f, 0f);
            viewport.pivot = new Vector2(0f, 1f);

            viewportObject.AddComponent<RectMask2D>();

            RoomListScrollRect.viewport = viewport;
        }

        Mask legacyMask = viewport.GetComponent<Mask>();
        if (legacyMask != null)
            legacyMask.enabled = false;

        Image viewportImage = viewport.GetComponent<Image>();
        if (viewportImage != null)
            viewportImage.enabled = false;

        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        if (existingContent != null &&
            existingContent != viewport &&
            existingContent.parent == RoomListScrollRect.transform)
        {
            existingContent.SetParent(viewport, false);
            RoomListScrollRect.content = existingContent;
        }

        return viewport;
    }

    private RectTransform EnsureRoomListContent(RectTransform viewport)
    {
        if (RoomListContent != null && RoomListContent.IsChildOf(viewport))
            return RoomListContent;

        RectTransform oldTextRect = TextRoomList != null ? TextRoomList.GetComponent<RectTransform>() : null;
        if (oldTextRect != null)
        {
            oldTextRect.gameObject.SetActive(true);

            if (!oldTextRect.IsChildOf(viewport))
                oldTextRect.SetParent(viewport, false);

            RoomListScrollRect.content = oldTextRect;
            return oldTextRect;
        }

        if (RoomListScrollRect.content != null && RoomListScrollRect.content.IsChildOf(viewport))
        {
            return RoomListScrollRect.content;
        }

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);

        RectTransform content = contentObject.GetComponent<RectTransform>();
        RoomListScrollRect.content = content;
        return content;
    }

    private void ConfigureRoomListContent(RectTransform content)
    {
        if (content == null)
            return;

        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, content.sizeDelta.y);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = content.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void ClearRoomCacheAndUI(bool showSwitchingState)
    {
        cachedRooms.Clear();
        RefreshRoomListUI();
        UpdateRoomCountText(showSwitchingState);
    }

    private void RefreshRoomListUI()
    {
        EnsureRoomListUI();

        if (RoomListContent == null)
            return;

        for (int i = RoomListContent.childCount - 1; i >= 0; i--)
            Destroy(RoomListContent.GetChild(i).gameObject);

        activeRoomItems.Clear();

        List<RoomInfo> rooms = new List<RoomInfo>(cachedRooms.Values);
        rooms.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        for (int i = 0; i < rooms.Count; i++)
        {
            RoomListItem item = CreateRoomListItem(rooms[i]);
            if (item != null)
                activeRoomItems.Add(item);
        }

        Canvas.ForceUpdateCanvases();
        if (RoomListScrollRect != null)
            RoomListScrollRect.verticalNormalizedPosition = 1f;
    }

    private RoomListItem CreateRoomListItem(RoomInfo roomInfo)
    {
        if (roomInfo == null || RoomListContent == null)
            return null;

        GameObject rowObject = new GameObject("RoomItem_" + SanitizeObjectName(roomInfo.Name), typeof(RectTransform));
        rowObject.transform.SetParent(RoomListContent, false);

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = new Color(1f, 1f, 1f, 0.86f);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = 68f;
        rowLayout.preferredHeight = 72f;

        Text roomNameText = CreateRoomNameText(rowObject.transform);
        Text passwordStateText = CreateRoomPasswordStateText(rowObject.transform);
        Button joinButton = CreateRoomJoinButton(rowObject.transform, out Text joinButtonText);

        RoomListItem item = rowObject.AddComponent<RoomListItem>();
        item.SetReferences(roomNameText, passwordStateText, joinButton, joinButtonText);
        item.Bind(roomInfo.Name, RoomHasPassword(roomInfo), IsRoomJoinable(roomInfo), this);
        return item;
    }

    private Text CreateRoomNameText(Transform parent)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject textObject = new GameObject("RoomNameText", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.48f, 1f);
        rect.offsetMin = new Vector2(30f, 0f);
        rect.offsetMax = new Vector2(-12f, 0f);

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 34;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Text CreateRoomPasswordStateText(Transform parent)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject textObject = new GameObject("PasswordStateText", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(150f, 56f);
        rect.anchoredPosition = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = 34;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.black;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Button CreateRoomJoinButton(Transform parent, out Text buttonText)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        GameObject buttonObject = new GameObject("JoinButton", typeof(RectTransform));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0.5f);
        buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.sizeDelta = new Vector2(180f, 56f);
        buttonRect.anchoredPosition = new Vector2(-30f, 0f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.12f, 0.10f, 0.07f, 1f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.22f, 0.42f, 0.78f, 1f);
        colors.pressedColor = new Color(0.11f, 0.23f, 0.46f, 1f);
        colors.disabledColor = new Color(0.18f, 0.18f, 0.18f, 0.35f);
        button.colors = colors;

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        buttonText = textObject.AddComponent<Text>();
        buttonText.font = font;
        buttonText.fontSize = 28;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.color = Color.white;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.text = "JOIN";
        buttonText.raycastTarget = false;

        MenuButtonPresentation.Ensure(button);
        return button;
    }

    private string SanitizeObjectName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "Unnamed";

        StringBuilder sb = new StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return sb.ToString();
    }

    private bool IsRoomJoinable(RoomInfo roomInfo)
    {
        if (roomInfo == null || !roomInfo.IsOpen)
            return false;

        return roomInfo.MaxPlayers <= 0 || roomInfo.PlayerCount < roomInfo.MaxPlayers;
    }

    public void JoinPublicRoom(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return;

        PlayLobbyButtonSound();
        pendingJoinPassword = "";
        ClearPhotonNickname();
        PhotonNetwork.JoinRoom(roomName);
    }

    private void DisablePlayerNameInput()
    {
        if (InputPlayerName != null)
            InputPlayerName.gameObject.SetActive(false);
    }

    private void ClearPhotonNickname()
    {
        if (PhotonNetwork.LocalPlayer != null)
            PhotonNetwork.LocalPlayer.NickName = "";
    }

    public void LaunchDOOM()
    {
        DoomManager.enabled = true;
    }

    public void CreateRoomButton()
    {
        PlayLobbyButtonSound();

        string roomName = InputRoomName != null ? InputRoomName.text.Trim() : "";
        string roomPassword = InputRoomPassword != null ? InputRoomPassword.text.Trim() : "";

        if (string.IsNullOrEmpty(roomName))
        {
            SetOutput("LobbyEnterRoomName", "Enter a room name.");
            return;
        }

        if ((string.Equals(roomName, "DOOM", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(roomName, "playdoom", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(roomName, "playingdoom", StringComparison.OrdinalIgnoreCase)) &&
            SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows)
        {
            LaunchDOOM();
            return;
        }

        ClearPhotonNickname();
        pendingJoinPassword = roomPassword;

        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = 2,
            IsVisible = true,
            IsOpen = true,
            EmptyRoomTtl = 0
        };

        Hashtable table = new Hashtable
        {
            { RoomPropPasswordHash, HashPassword(roomPassword) },
            { RoomPropHasPassword, string.IsNullOrEmpty(roomPassword) ? 0 : 1 }
        };

        roomOptions.CustomRoomProperties = table;
        roomOptions.CustomRoomPropertiesForLobby = new[] { RoomPropHasPassword };

        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoomButton()
    {
        PlayLobbyButtonSound();

        string roomName = InputRoomName != null ? InputRoomName.text.Trim() : "";
        pendingJoinPassword = InputRoomPassword != null ? InputRoomPassword.text.Trim() : "";

        if (string.IsNullOrEmpty(roomName))
        {
            SetOutput("LobbyEnterRoomName", "Enter a room name.");
            pendingJoinPassword = "";
            return;
        }

        ClearPhotonNickname();
        PhotonNetwork.JoinRoom(roomName);
    }

    public void RandomJoinButton()
    {
        PlayLobbyButtonSound();

        ClearPhotonNickname();
        pendingJoinPassword = "";

        Hashtable expectedProperties = new Hashtable
        {
            { RoomPropHasPassword, 0 }
        };

        PhotonNetwork.JoinRandomRoom(expectedProperties, 2);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        pendingJoinPassword = "";
        Debug.LogError("Random join failed: " + message);
        SetOutput("LobbyNoOpenRoom", "No open room found.");
    }

    private bool ValidateJoinedRoomPassword()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return false;

        if (!RoomHasPassword(PhotonNetwork.CurrentRoom))
            return true;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomPropPasswordHash, out object hashObj))
        {
            RejectJoinedRoomForPassword();
            return false;
        }

        string expectedHash = hashObj as string;
        string enteredHash = HashPassword(pendingJoinPassword);

        if (!string.Equals(expectedHash, enteredHash, StringComparison.OrdinalIgnoreCase))
        {
            RejectJoinedRoomForPassword();
            return false;
        }

        return true;
    }

    private void RejectJoinedRoomForPassword()
    {
        isLeavingAfterPasswordFailure = true;
        SetOutput("LobbyWrongPassword", "Wrong password.");
        PhotonNetwork.LeaveRoom();
    }

    private bool RoomHasPassword(RoomInfo roomInfo)
    {
        if (roomInfo == null || roomInfo.CustomProperties == null)
            return false;

        if (roomInfo.CustomProperties.TryGetValue(RoomPropHasPassword, out object hasPwObj))
            return IsTruthyPhotonProperty(hasPwObj);

        return HasNonEmptyPhotonProperty(roomInfo, LegacyRoomPropPassword) ||
               HasNonEmptyPhotonProperty(roomInfo, RoomPropPasswordHash);
    }

    private bool IsTruthyPhotonProperty(object value)
    {
        if (value == null)
            return false;

        if (value is bool boolValue)
            return boolValue;

        if (value is byte byteValue)
            return byteValue != 0;

        if (value is int intValue)
            return intValue != 0;

        string stringValue = value.ToString();
        return string.Equals(stringValue, "1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stringValue, "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stringValue, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private bool HasNonEmptyPhotonProperty(RoomInfo roomInfo, string key)
    {
        if (roomInfo == null || roomInfo.CustomProperties == null)
            return false;

        if (!roomInfo.CustomProperties.TryGetValue(key, out object value) || value == null)
            return false;

        return !string.IsNullOrWhiteSpace(value.ToString());
    }

    private string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return "";

        using (SHA256 sha = SHA256.Create())
        {
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            StringBuilder sb = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));

            return sb.ToString();
        }
    }

    private void SetOutput(string key, string fallback)
    {
        string message = GetLocalizedText(key, fallback);
        SetOutputRaw(message);
    }

    private void SetOutputRaw(string message)
    {
        if (OutPutText != null)
            OutPutText.text = message;

        Debug.Log(message);
    }

    private string GetLocalizedText(string key, string fallback)
    {
        if (string.IsNullOrEmpty(key))
            return fallback;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(LocalizationTable, key);
        return string.IsNullOrEmpty(localized) ? fallback : localized;
    }

    private void PlayLobbyButtonSound()
    {
        AudioManager.Instance.PlayLobbyButton();
    }
}
