using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Realtime;
using System.Collections.Generic;
using System.Text;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("基本UI")]
    [SerializeField] InputField InputRoomName;
    [SerializeField] InputField InputPlayerName;
    [SerializeField] InputField InputRoomPassword;
    [SerializeField] Text TextRoomList;
    [SerializeField] Text OutPutText;

    [Header("區域UI")]
    [SerializeField] Dropdown RegionDropdown;
    [SerializeField] Text CurrentRegionText;

    private Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();

    // Dropdown 顯示名稱
    private readonly List<string> regionDisplayNames = new List<string>()
{
    "Asia (Singapore)",
    "Australia (Sydney)",
    "Canada East (Montreal)",
    "China Mainland (Shanghai)",
    "Europe (Amsterdam)",
    "Hong Kong",
    "India (Chennai)",
    "Japan (Tokyo)",
    "South Africa (Johannesburg)",
    "South America (Sao Paulo)",
    "South Korea (Seoul)",
    "Turkey (Istanbul)",
    "UAE (Dubai)",
    "USA East (Washington D.C.)",
    "USA West (San José)",
    "USA South Central (Dallas)"
};

    private readonly List<string> regionCodes = new List<string>()
{
    "asia",
    "au",
    "cae",
    "cn",
    "eu",
    "hk",
    "in",
    "jp",
    "za",
    "sa",
    "kr",
    "tr",
    "uae",
    "us",
    "usw",
    "ussc"
};

    private bool isChangingRegion = false;

    void Start()
    {
        Debug.Log("Start: IsConnected = " + PhotonNetwork.IsConnected);
        Debug.Log("Start: IsConnectedAndReady = " + PhotonNetwork.IsConnectedAndReady);
        Debug.Log("TextRoomList 是否為空: " + (TextRoomList == null));

        SetupRegionDropdown();
        UpdateRegionText();

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("未連線，回 StartScene");
            SceneManager.LoadScene("StartScene");
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
        {
            Debug.Log("Start 時已 ready，加入 Lobby");
            PhotonNetwork.JoinLobby();
        }
    }

    private void SetupRegionDropdown()
    {
        if (RegionDropdown == null) return;

        RegionDropdown.ClearOptions();
        RegionDropdown.AddOptions(regionDisplayNames);

        string currentRegion = PhotonNetwork.CloudRegion;
        int index = regionCodes.IndexOf(currentRegion);

        if (index < 0)
            index = 0;

        RegionDropdown.value = index;
        RegionDropdown.RefreshShownValue();
    }

    private void UpdateRegionText()
    {
        if (CurrentRegionText != null)
        {
            string region = string.IsNullOrEmpty(PhotonNetwork.CloudRegion) ? "未連線" : PhotonNetwork.CloudRegion;
            CurrentRegionText.text = "目前區域: " + region;
        }
    }

    public void ChangeRegionButton()
    {
        if (RegionDropdown == null)
        {
            Debug.LogError("RegionDropdown 沒有綁定");
            return;
        }

        int index = RegionDropdown.value;
        if (index < 0 || index >= regionCodes.Count)
        {
            Debug.LogError("選到無效區域 index: " + index);
            return;
        }

        string targetRegion = regionCodes[index];
        Debug.Log("準備切換區域到: " + targetRegion);

        if (PhotonNetwork.CloudRegion == targetRegion && PhotonNetwork.IsConnectedAndReady)
        {
            OutPutText.text = "已經在此區域: " + targetRegion;
            UpdateRegionText();
            return;
        }

        isChangingRegion = true;
        OutPutText.text = "切換區域中: " + targetRegion;

        cachedRooms.Clear();
        if (TextRoomList != null)
            TextRoomList.text = "";

        PhotonNetwork.Disconnect();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("連線至MASTER");
        Debug.Log("CloudRegion = " + PhotonNetwork.CloudRegion);
        Debug.Log("GameVersion = " + PhotonNetwork.GameVersion);
        Debug.Log("AppVersion = " + PhotonNetwork.NetworkingClient.AppVersion);

        UpdateRegionText();

        if (!PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("加入大廳成功");
        Debug.Log("CloudRegion = " + PhotonNetwork.CloudRegion);
        Debug.Log("GameVersion = " + PhotonNetwork.GameVersion);
        Debug.Log("AppVersion = " + PhotonNetwork.NetworkingClient.AppVersion);

        UpdateRegionText();
        OutPutText.text = "已進入Lobby";
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("房間建立成功: " + PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("建立房間失敗: " + message);
        OutPutText.text = "建立房間失敗: " + message;
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("成功加入房間");
        SceneManager.LoadScene("RoomScene");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("加入房間失敗: " + message);
        OutPutText.text = "加入失敗: " + message;
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        Debug.Log("房間列表更新，Count = " + roomList.Count);
        Debug.Log("CloudRegion = " + PhotonNetwork.CloudRegion);
        Debug.Log("GameVersion = " + PhotonNetwork.GameVersion);

        StringBuilder sb = new StringBuilder();

        foreach (RoomInfo roomInfo in roomList)
        {
            Debug.Log("房間: " + roomInfo.Name + " Removed=" + roomInfo.RemovedFromList);

            if (roomInfo.RemovedFromList)
            {
                cachedRooms.Remove(roomInfo.Name);
                continue;
            }

            cachedRooms[roomInfo.Name] = roomInfo;
            sb.AppendLine("- " + roomInfo.Name);
        }

        Debug.Log("顯示房間列表:\n" + sb.ToString());

        if (TextRoomList != null)
            TextRoomList.text = sb.ToString();
        else
            Debug.LogError("TextRoomList 沒有綁定！");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError("Photon 斷線: " + cause);
        UpdateRegionText();

        if (isChangingRegion)
        {
            isChangingRegion = false;

            if (RegionDropdown == null)
            {
                Debug.LogError("RegionDropdown 沒有綁定，無法切換區域");
                return;
            }

            int index = RegionDropdown.value;
            string targetRegion = regionCodes[index];

            Debug.Log("重新連線到區域: " + targetRegion);

            PhotonNetwork.ConnectToRegion(targetRegion);
            return;
        }
    }

    public string GetPlayerName()
    {
        return InputPlayerName.text.Trim();
    }

    public void CreateRoomButton()
    {
        string roomName = InputRoomName.text.Trim();
        string playerName = GetPlayerName();
        string roomPassword = InputRoomPassword.text.Trim();

        Debug.Log("建立房間: [" + roomName + "]");

        if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(playerName))
        {
            Debug.Log("房間名稱或玩家名稱錯誤");
            OutPutText.text = "房間名稱或玩家名稱錯誤";
            return;
        }

        PhotonNetwork.LocalPlayer.NickName = playerName;

        RoomOptions roomOptions = new RoomOptions();
        roomOptions.MaxPlayers = 2;
        roomOptions.IsVisible = true;
        roomOptions.IsOpen = true;
        roomOptions.EmptyRoomTtl = 0;

        Hashtable table = new Hashtable();
        table.Add("pw", roomPassword);
        table.Add("hasPw", string.IsNullOrEmpty(roomPassword) ? 0 : 1);

        roomOptions.CustomRoomProperties = table;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { "pw", "hasPw" };

        PhotonNetwork.CreateRoom(roomName, roomOptions);
    }

    public void JoinRoomButton()
    {
        string roomName = InputRoomName.text.Trim();
        string playerName = GetPlayerName();

        Debug.Log("嘗試加入房間: [" + roomName + "]");
        Debug.Log("cachedRooms.Count = " + cachedRooms.Count);

        foreach (var kv in cachedRooms)
        {
            Debug.Log("快取房間: " + kv.Key);
        }

        if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(playerName))
        {
            OutPutText.text = "房間名稱或玩家名稱錯誤";
            return;
        }

        PhotonNetwork.LocalPlayer.NickName = playerName;
        PhotonNetwork.JoinRoom(roomName);
    }

    public void RandomJoinButton()
    {
        string playerName = GetPlayerName();

        if (string.IsNullOrEmpty(playerName))
        {
            OutPutText.text = "玩家名稱錯誤";
            return;
        }

        PhotonNetwork.LocalPlayer.NickName = playerName;

        Hashtable expectedProperties = new Hashtable();
        expectedProperties.Add("hasPw", 0);

        PhotonNetwork.JoinRandomRoom(expectedProperties, 2);
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogError("隨機加入失敗: " + message);
        OutPutText.text = "隨機加入失敗: " + message;
    }
}