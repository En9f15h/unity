using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Text;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks
{
    private const string RoomPropStageIndex = BackgroundSpriteSync.ROOM_PROP_BG_INDEX;
    [Header("基本UI")]
    [SerializeField] Text RoomName;
    [SerializeField] Text PlayerList;
    [SerializeField] Button ButtonLeave;
    [SerializeField] Button ButtonStartGame;

    [Header("Ready Button UI")]
    [SerializeField] Text ReadyButtonText;
    [SerializeField] Component ReadyButtonTextComponent;
    [SerializeField] GameObject ReadyStateObject;
    [SerializeField] GameObject NotReadyStateObject;

    [Header("選角UI")]
    [SerializeField] Text ClassText;
    [SerializeField] Text SkinText;
    [SerializeField] Image PreviewImage;

    [Header("選角按鈕")]
    [SerializeField] Button PrevClassButton;
    [SerializeField] Button NextClassButton;
    [SerializeField] Button PrevSkinButton;
    [SerializeField] Button NextSkinButton;

    [Header("職業名稱")]
    [SerializeField] string[] classNames;

    [Header("每個職業的造型名稱")]
    [SerializeField] string[] knightSkins;
    [SerializeField] string[] fortuneTellerSkins;
    [SerializeField] string[] warriorSkins;

    [Header("預覽圖")]
    [SerializeField] Sprite[] knightSkinSprites;
    [SerializeField] Sprite[] fortuneTellerSkinSprites;
    [SerializeField] Sprite[] warriorSkinSprites;

    [Header("Stage Selection")]
    [SerializeField] Text StageText;
    [SerializeField] Button PrevStageButton;
    [SerializeField] Button NextStageButton;
    [SerializeField] string[] stageNames;
    [SerializeField] int stageCount = 4;

    private bool ready = false;
    private int selectedClassIndex = 0;
    private int selectedSkinIndex = 0;
    private int selectedStageIndex = 0;
    private GameObject generatedStagePanel;
    private bool roomPropertiesInitialized = false;

    void Start()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            SceneManager.LoadScene("LobbyScene");
            return;
        }

        PhotonNetwork.AutomaticallySyncScene = true;
        ResolveOptionalUIReferences();

        if (RoomName != null)
            RoomName.text = PhotonNetwork.CurrentRoom.Name;

        LoadLocalSelectionFromProperties();
        EnsureStageSelectionUI();

        UpdateSelectionUI();
        UpdateStageUI();
        UpdatePlayerList();
        UpdateReadyButtonText();
        UpdateSelectionButtonsInteractable();

        StartCoroutine(InitializeRoomPropertiesWhenJoined());
    }

    private IEnumerator InitializeRoomPropertiesWhenJoined()
    {
        while (PhotonNetwork.CurrentRoom != null &&
               (!PhotonNetwork.InRoom || PhotonNetwork.NetworkClientState != ClientState.Joined))
        {
            yield return null;
        }

        if (PhotonNetwork.CurrentRoom == null)
            yield break;

        roomPropertiesInitialized = true;

        LoadLocalSelectionFromProperties();
        SetReady(false);
        SetClassAndSkin(selectedClassIndex, selectedSkinIndex);
        EnsureRoomStageSelection();

        UpdateSelectionUI();
        UpdateStageUI();
        UpdatePlayerList();
        UpdateReadyButtonText();
        UpdateSelectionButtonsInteractable();
        CheckAllPlayersReady();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        EnsureRoomStageSelection();
        UpdateStageUI();
        UpdateSelectionButtonsInteractable();
        UpdatePlayerList();
        CheckAllPlayersReady();
    }

    public void UpdatePlayerList()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        if (PlayerList == null)
        {
            ResolveOptionalUIReferences();

            if (PlayerList == null)
            {
                Debug.LogWarning("RoomManager: PlayerList is not assigned, player list text will be skipped.");
                return;
            }
        }

        LoadStageSelectionFromRoomProperties();

        StringBuilder sb = new StringBuilder();

        foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
        {
            Player player = kvp.Value;

            bool isReady = false;
            if (player.CustomProperties.TryGetValue("ready", out object readyObj))
                isReady = (bool)readyObj;

            int classIndex = 0;
            if (player.CustomProperties.TryGetValue("classIndex", out object classObj))
                classIndex = (int)classObj;

            int skinIndex = 0;
            if (player.CustomProperties.TryGetValue("skinIndex", out object skinObj))
                skinIndex = (int)skinObj;

            string className = GetClassName(classIndex);
            string skinName = GetSkinName(classIndex, skinIndex);

            string readyText = isReady ? " READY" : "";
            string masterText = player.IsMasterClient ? " (Host)" : "";

            sb.AppendLine($"- {GetPlayerDisplayName(player)}{readyText}{masterText} | {className} - {skinName}");
        }

        sb.AppendLine();
        sb.AppendLine("Stage: " + GetStageName(selectedStageIndex));

        PlayerList.text = sb.ToString();
    }

    private void ResolveOptionalUIReferences()
    {
        if (RoomName == null)
            RoomName = FindTextByObjectName("RoomName", "RoomTitle", "RoomText");

        if (PlayerList == null)
            PlayerList = FindTextByObjectName("PlayerList", "RoomPlayerList", "PlayersText");
    }

    private Text FindTextByObjectName(params string[] objectNames)
    {
        Text[] texts = FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null)
                continue;

            for (int j = 0; j < objectNames.Length; j++)
            {
                if (texts[i].name == objectNames[j])
                    return texts[i];
            }
        }

        return null;
    }

    public void StartGameButton()
    {
        ready = !ready;
        SetReady(ready);

        UpdateReadyButtonText();
        UpdateSelectionButtonsInteractable();
        UpdatePlayerList();
        CheckAllPlayersReady();
    }

    private string GetPlayerDisplayName(Player player)
    {
        if (player == null)
            return "P?";

        return player.IsMasterClient ? "P1" : "P2";
    }

    private void SetReady(bool value)
    {
        ready = value;

        Hashtable props = new Hashtable();
        props["ready"] = value;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void UpdateReadyButtonText()
    {
        string label = ready ? "Cancel" : "Ready";

        if (ReadyButtonText == null && ButtonStartGame != null)
            ReadyButtonText = ButtonStartGame.GetComponentInChildren<Text>(true);

        if (ReadyButtonTextComponent == null && ButtonStartGame != null)
            ReadyButtonTextComponent = FindReadyButtonTextComponent();

        if (ReadyButtonText != null)
            ReadyButtonText.text = label;

        SetTextOnComponent(ReadyButtonTextComponent, label);

        if (ReadyStateObject != null)
            ReadyStateObject.SetActive(ready);

        if (NotReadyStateObject != null)
            NotReadyStateObject.SetActive(!ready);
    }

    private Component FindReadyButtonTextComponent()
    {
        if (ButtonStartGame == null) return null;

        Component[] components = ButtonStartGame.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null || component is Text) continue;

            string typeName = component.GetType().FullName;
            if (!string.IsNullOrEmpty(typeName) && typeName.StartsWith("TMPro."))
                return component;
        }

        return null;
    }

    private void SetTextOnComponent(Component component, string value)
    {
        if (component == null) return;

        if (component is Text legacyText)
        {
            legacyText.text = value;
            return;
        }

        System.Reflection.PropertyInfo textProperty = component
            .GetType()
            .GetProperty("text", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        if (textProperty != null && textProperty.CanWrite && textProperty.PropertyType == typeof(string))
            textProperty.SetValue(component, value);
    }

    private void UpdateSelectionButtonsInteractable()
    {
        bool canUseRoomButtons = roomPropertiesInitialized &&
                                 PhotonNetwork.InRoom &&
                                 PhotonNetwork.NetworkClientState == ClientState.Joined;
        bool canSelect = canUseRoomButtons && !ready;
        bool canSelectStage = canSelect && PhotonNetwork.IsMasterClient && GetStageCount() > 1;

        if (ButtonStartGame != null) ButtonStartGame.interactable = canUseRoomButtons;
        if (PrevClassButton != null) PrevClassButton.interactable = canSelect;
        if (NextClassButton != null) NextClassButton.interactable = canSelect;
        if (PrevSkinButton != null) PrevSkinButton.interactable = canSelect;
        if (NextSkinButton != null) NextSkinButton.interactable = canSelect;
        if (PrevStageButton != null) PrevStageButton.interactable = canSelectStage;
        if (NextStageButton != null) NextStageButton.interactable = canSelectStage;
    }

    private void CheckAllPlayersReady()
    {
        if (PhotonNetwork.CurrentRoom == null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount < 2) return;

        foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
        {
            Player player = kvp.Value;

            if (!player.CustomProperties.TryGetValue("ready", out object readyObj))
                return;

            if (!(bool)readyObj)
                return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Debug.Log("所有玩家都 READY，進入 GameScene");
            EnsureRoomStageSelection();
            PhotonNetwork.LoadLevel("GameScene");
        }
    }

    public void LeaveRoomButton()
    {
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        SceneManager.LoadScene("LobbyScene");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
        CheckAllPlayersReady();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
        CheckAllPlayersReady();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps.ContainsKey("ready") ||
            changedProps.ContainsKey("classIndex") ||
            changedProps.ContainsKey("skinIndex"))
        {
            UpdatePlayerList();

            if (targetPlayer == PhotonNetwork.LocalPlayer)
            {
                LoadLocalSelectionFromProperties();
                UpdateSelectionUI();
                UpdateReadyButtonText();
                UpdateSelectionButtonsInteractable();
            }

            CheckAllPlayersReady();
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(RoomPropStageIndex))
        {
            LoadStageSelectionFromRoomProperties();
            UpdateStageUI();
            UpdatePlayerList();
        }
    }

    public void NextStage()
    {
        if (!CanLocalPlayerChangeStage())
            return;

        int count = GetStageCount();
        selectedStageIndex++;
        if (selectedStageIndex >= count)
            selectedStageIndex = 0;

        SetRoomStage(selectedStageIndex);
    }

    public void PrevStage()
    {
        if (!CanLocalPlayerChangeStage())
            return;

        int count = GetStageCount();
        selectedStageIndex--;
        if (selectedStageIndex < 0)
            selectedStageIndex = count - 1;

        SetRoomStage(selectedStageIndex);
    }

    public void NextClass()
    {
        if (ready) return;

        selectedClassIndex++;
        if (selectedClassIndex >= classNames.Length)
            selectedClassIndex = 0;

        selectedSkinIndex = 0;
        SetClassAndSkin(selectedClassIndex, selectedSkinIndex);
        UpdateSelectionUI();
        UpdatePlayerList();
    }

    public void PrevClass()
    {
        if (ready) return;

        selectedClassIndex--;
        if (selectedClassIndex < 0)
            selectedClassIndex = classNames.Length - 1;

        selectedSkinIndex = 0;
        SetClassAndSkin(selectedClassIndex, selectedSkinIndex);
        UpdateSelectionUI();
        UpdatePlayerList();
    }

    public void NextSkin()
    {
        if (ready) return;

        string[] skins = GetSkinArray(selectedClassIndex);
        int skinCount = skins.Length;
        if (skinCount == 0) return;

        selectedSkinIndex++;
        if (selectedSkinIndex >= skinCount)
            selectedSkinIndex = 0;

        SetClassAndSkin(selectedClassIndex, selectedSkinIndex);
        UpdateSelectionUI();
        UpdatePlayerList();
    }

    public void PrevSkin()
    {
        if (ready) return;

        string[] skins = GetSkinArray(selectedClassIndex);
        int skinCount = skins.Length;
        if (skinCount == 0) return;

        selectedSkinIndex--;
        if (selectedSkinIndex < 0)
            selectedSkinIndex = skinCount - 1;

        SetClassAndSkin(selectedClassIndex, selectedSkinIndex);
        UpdateSelectionUI();
        UpdatePlayerList();
    }

    private void SetClassAndSkin(int classIndex, int skinIndex)
    {
        selectedClassIndex = classIndex;
        selectedSkinIndex = skinIndex;

        Hashtable props = new Hashtable();
        props["classIndex"] = selectedClassIndex;
        props["skinIndex"] = selectedSkinIndex;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void EnsureRoomStageSelection()
    {
        LoadStageSelectionFromRoomProperties();

        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(RoomPropStageIndex))
            return;

        SetRoomStage(selectedStageIndex);
    }

    private void LoadStageSelectionFromRoomProperties()
    {
        if (PhotonNetwork.CurrentRoom == null)
            return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(RoomPropStageIndex, out object stageObj))
            selectedStageIndex = Mathf.Clamp(System.Convert.ToInt32(stageObj), 0, GetStageCount() - 1);
    }

    private void SetRoomStage(int stageIndex)
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        selectedStageIndex = Mathf.Clamp(stageIndex, 0, GetStageCount() - 1);

        Hashtable props = new Hashtable
        {
            { RoomPropStageIndex, selectedStageIndex }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        UpdateStageUI();
    }

    private bool CanLocalPlayerChangeStage()
    {
        return PhotonNetwork.IsMasterClient && !ready && GetStageCount() > 1;
    }

    private int GetStageCount()
    {
        if (stageNames != null && stageNames.Length > 0)
            return stageNames.Length;

        return Mathf.Max(1, stageCount);
    }

    private string GetStageName(int stageIndex)
    {
        if (stageNames != null &&
            stageIndex >= 0 &&
            stageIndex < stageNames.Length &&
            !string.IsNullOrEmpty(stageNames[stageIndex]))
        {
            return stageNames[stageIndex];
        }

        return "Stage " + (stageIndex + 1);
    }

    private void UpdateStageUI()
    {
        if (StageText != null)
        {
            string ownerText = PhotonNetwork.IsMasterClient ? "" : " (Host only)";
            StageText.text = "Stage: " + GetStageName(selectedStageIndex) + ownerText;
        }

        UpdateSelectionButtonsInteractable();
    }

    private void EnsureStageSelectionUI()
    {
        if (StageText != null && PrevStageButton != null && NextStageButton != null)
            return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (generatedStagePanel != null)
            return;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        generatedStagePanel = new GameObject("StageSelectionPanel", typeof(RectTransform));
        generatedStagePanel.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = generatedStagePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.sizeDelta = new Vector2(520f, 90f);
        panelRect.anchoredPosition = new Vector2(0f, 28f);

        Image panelImage = generatedStagePanel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.45f);

        HorizontalLayoutGroup layout = generatedStagePanel.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 12f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = false;
        layout.childControlHeight = true;

        PrevStageButton = CreateStageButton("PrevStageButton", generatedStagePanel.transform, font, "<");
        StageText = CreateStageText("StageText", generatedStagePanel.transform, font);
        NextStageButton = CreateStageButton("NextStageButton", generatedStagePanel.transform, font, ">");

        PrevStageButton.onClick.AddListener(PrevStage);
        NextStageButton.onClick.AddListener(NextStage);
    }

    private Text CreateStageText(string objectName, Transform parent, Font font)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        LayoutElement layoutElement = obj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 320f;
        layoutElement.preferredHeight = 56f;

        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        return text;
    }

    private Button CreateStageButton(string objectName, Transform parent, Font font, string label)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        LayoutElement layoutElement = obj.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 72f;
        layoutElement.preferredHeight = 56f;

        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.15f, 0.2f, 0.28f, 0.9f);

        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(obj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObj.AddComponent<Text>();
        text.font = font;
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;

        return button;
    }

    private void LoadLocalSelectionFromProperties()
    {
        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("classIndex", out object classObj))
            selectedClassIndex = (int)classObj;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("skinIndex", out object skinObj))
            selectedSkinIndex = (int)skinObj;

        if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("ready", out object readyObj))
            ready = (bool)readyObj;
    }

    private void UpdateSelectionUI()
    {
        if (ClassText != null)
            ClassText.text = "Class : " + GetClassName(selectedClassIndex);

        if (SkinText != null)
            SkinText.text = "Skin : " + GetSkinName(selectedClassIndex, selectedSkinIndex);

        if (PreviewImage != null)
            PreviewImage.sprite = GetSkinSprite(selectedClassIndex, selectedSkinIndex);
    }

    private string GetClassName(int classIndex)
    {
        if (classNames == null || classNames.Length == 0) return "None";
        if (classIndex < 0 || classIndex >= classNames.Length) return "Unknown";
        return classNames[classIndex];
    }

    private string GetSkinName(int classIndex, int skinIndex)
    {
        string[] skins = GetSkinArray(classIndex);
        if (skins == null || skins.Length == 0) return "Default";
        if (skinIndex < 0 || skinIndex >= skins.Length) return skins[0];
        return skins[skinIndex];
    }

    private string[] GetSkinArray(int classIndex)
    {
        switch (classIndex)
        {
            case 0: return knightSkins;
            case 1: return fortuneTellerSkins;
            case 2: return warriorSkins;
            default: return knightSkins;
        }
    }

    private Sprite GetSkinSprite(int classIndex, int skinIndex)
    {
        Sprite[] arr = GetSkinSpriteArray(classIndex);
        if (arr == null || arr.Length == 0) return null;
        if (skinIndex < 0 || skinIndex >= arr.Length) return arr[0];
        return arr[skinIndex];
    }

    private Sprite[] GetSkinSpriteArray(int classIndex)
    {
        switch (classIndex)
        {
            case 0: return knightSkinSprites;
            case 1: return fortuneTellerSkinSprites;
            case 2: return warriorSkinSprites;
            default: return knightSkinSprites;
        }
    }

}
