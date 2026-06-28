using UnityEngine;
using Photon.Pun;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Text;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [Header("基本UI")]
    [SerializeField] Text RoomName;
    [SerializeField] Text PlayerList;
    [SerializeField] Button ButtonLeave;
    [SerializeField] Button ButtonStartGame;

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

    private bool ready = false;
    private int selectedClassIndex = 0;
    private int selectedSkinIndex = 0;

    void Start()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            SceneManager.LoadScene("LobbyScene");
            return;
        }

        PhotonNetwork.AutomaticallySyncScene = true;
        RoomName.text = PhotonNetwork.CurrentRoom.Name;

        LoadLocalSelectionFromProperties();

        SetReady(false);
        SetClassAndSkin(selectedClassIndex, selectedSkinIndex);

        UpdateSelectionUI();
        UpdatePlayerList();
        UpdateReadyButtonText();
        UpdateSelectionButtonsInteractable();
        CheckAllPlayersReady();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        UpdatePlayerList();
        CheckAllPlayersReady();
    }

    public void UpdatePlayerList()
    {
        if (PhotonNetwork.CurrentRoom == null) return;

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

            sb.AppendLine($"- {player.NickName}{readyText}{masterText} | {className} - {skinName}");
        }

        PlayerList.text = sb.ToString();
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

    private void SetReady(bool value)
    {
        ready = value;

        Hashtable props = new Hashtable();
        props["ready"] = value;
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    private void UpdateReadyButtonText()
    {
        Text btnText = ButtonStartGame.GetComponentInChildren<Text>();
        if (btnText != null)
            btnText.text = ready ? "Cancel" : "Ready";
    }

    private void UpdateSelectionButtonsInteractable()
    {
        bool canSelect = !ready;

        if (PrevClassButton != null) PrevClassButton.interactable = canSelect;
        if (NextClassButton != null) NextClassButton.interactable = canSelect;
        if (PrevSkinButton != null) PrevSkinButton.interactable = canSelect;
        if (NextSkinButton != null) NextSkinButton.interactable = canSelect;
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
