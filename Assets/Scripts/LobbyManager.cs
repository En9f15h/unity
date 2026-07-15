using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    private const string RoomPropPasswordHash = "pwHash";
    private const string RoomPropHasPassword = "hasPw";

    [Header("Basic UI")]
    [SerializeField] private InputField InputRoomName;
    [SerializeField] private InputField InputPlayerName;
    [SerializeField] private InputField InputRoomPassword;
    [SerializeField] private Text TextRoomList;
    [SerializeField] private Text OutPutText;

    [Header("Region UI")]
    [SerializeField] private Dropdown RegionDropdown;
    [SerializeField] private Text CurrentRegionText;

    private readonly Dictionary<string, RoomInfo> cachedRooms = new Dictionary<string, RoomInfo>();

    private readonly List<string> regionDisplayNames = new List<string>
    {
        "Asia / Singapore",
        "Hong Kong",
        "Japan / Tokyo",
        "South Korea / Seoul",
        "USA West / San Jose"
    };

    private readonly List<string> regionCodes = new List<string>
    {
        "asia",
        "hk",
        "jp",
        "kr",
        "usw"
    };

    private bool isChangingRegion = false;
    private bool isLeavingAfterPasswordFailure = false;
    private string pendingJoinPassword = "";

    private void Start()
    {
        Debug.Log("Lobby Start: IsConnected = " + PhotonNetwork.IsConnected);
        Debug.Log("Lobby Start: IsConnectedAndReady = " + PhotonNetwork.IsConnectedAndReady);

        SetupRegionDropdown();
        UpdateRegionText();
        DisablePlayerNameInput();

        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Not connected. Returning to StartScene.");
            SceneManager.LoadScene("StartScene");
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
        {
            Debug.Log("Connected and ready. Joining lobby.");
            PhotonNetwork.JoinLobby();
        }
    }

    private void SetupRegionDropdown()
    {
        if (RegionDropdown == null)
            return;

        RegionDropdown.ClearOptions();
        RegionDropdown.AddOptions(regionDisplayNames);

        int index = GetRegionIndex(PhotonNetwork.CloudRegion);
        if (index < 0)
            index = 0;

        RegionDropdown.value = index;
        RegionDropdown.RefreshShownValue();
    }

    private void UpdateRegionText()
    {
        if (CurrentRegionText == null)
            return;

        string region = string.IsNullOrEmpty(PhotonNetwork.CloudRegion) ? "Not connected" : GetRegionLabel(PhotonNetwork.CloudRegion);
        CurrentRegionText.text = "Current region: " + region;
    }

    private int GetRegionIndex(string regionCode)
    {
        if (string.IsNullOrEmpty(regionCode))
            return -1;

        string normalizedRegionCode = regionCode.Trim().ToLowerInvariant();
        int slashIndex = normalizedRegionCode.IndexOf('/');
        if (slashIndex >= 0)
            normalizedRegionCode = normalizedRegionCode.Substring(0, slashIndex);

        for (int i = 0; i < regionCodes.Count; i++)
        {
            if (string.Equals(regionCodes[i], normalizedRegionCode, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private string GetRegionLabel(string regionCode)
    {
        int index = GetRegionIndex(regionCode);
        if (index < 0)
            return regionCode;

        return regionDisplayNames[index] + " (" + regionCodes[index] + ")";
    }

    public void ChangeRegionButton()
    {
        if (RegionDropdown == null)
        {
            Debug.LogError("RegionDropdown is not assigned.");
            return;
        }

        int index = RegionDropdown.value;
        if (index < 0 || index >= regionCodes.Count)
        {
            Debug.LogError("Invalid region index: " + index);
            return;
        }

        string targetRegion = regionCodes[index];
        string targetRegionLabel = GetRegionLabel(targetRegion);
        Debug.Log("Changing region to: " + targetRegion);

        if (GetRegionIndex(PhotonNetwork.CloudRegion) == index && PhotonNetwork.IsConnectedAndReady)
        {
            SetOutput("Already in region: " + targetRegionLabel);
            UpdateRegionText();
            return;
        }

        isChangingRegion = true;
        SetOutput("Changing region: " + targetRegionLabel);

        cachedRooms.Clear();
        if (TextRoomList != null)
            TextRoomList.text = "";

        PhotonNetwork.Disconnect();
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to master. Region = " + PhotonNetwork.CloudRegion);
        UpdateRegionText();

        if (!PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined lobby. Region = " + PhotonNetwork.CloudRegion);
        UpdateRegionText();
        SetOutput("Joined lobby.");
    }

    public override void OnCreatedRoom()
    {
        Debug.Log("Created room: " + PhotonNetwork.CurrentRoom.Name);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Create room failed: " + message);
        SetOutput("Create room failed: " + message);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Joined room.");

        if (!ValidateJoinedRoomPassword())
            return;

        pendingJoinPassword = "";
        SceneManager.LoadScene("RoomScene");
    }

    public override void OnLeftRoom()
    {
        if (!isLeavingAfterPasswordFailure)
            return;

        isLeavingAfterPasswordFailure = false;
        pendingJoinPassword = "";
        SetOutput("Wrong room password.");

        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InLobby)
            PhotonNetwork.JoinLobby();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        pendingJoinPassword = "";
        Debug.LogError("Join room failed: " + message);
        SetOutput("Join failed: " + message);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        StringBuilder sb = new StringBuilder();

        foreach (RoomInfo roomInfo in roomList)
        {
            if (roomInfo.RemovedFromList)
            {
                cachedRooms.Remove(roomInfo.Name);
                continue;
            }

            cachedRooms[roomInfo.Name] = roomInfo;
        }

        foreach (RoomInfo roomInfo in cachedRooms.Values)
        {
            string lockText = RoomHasPassword(roomInfo) ? " [locked]" : "";
            sb.AppendLine("- " + roomInfo.Name + lockText);
        }

        if (TextRoomList != null)
            TextRoomList.text = sb.ToString();
        else
            Debug.LogError("TextRoomList is not assigned.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarning("Photon disconnected: " + cause);
        UpdateRegionText();

        if (!isChangingRegion)
            return;

        isChangingRegion = false;

        if (RegionDropdown == null)
        {
            Debug.LogError("RegionDropdown is not assigned. Cannot reconnect to selected region.");
            return;
        }

        int index = RegionDropdown.value;
        if (index < 0 || index >= regionCodes.Count)
            index = 0;

        string targetRegion = regionCodes[index];
        Debug.Log("Reconnecting to region: " + targetRegion);
        SetOutput("Reconnecting to region: " + GetRegionLabel(targetRegion));
        PhotonNetwork.ConnectToRegion(targetRegion);
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

    public void CreateRoomButton()
    {
        string roomName = InputRoomName != null ? InputRoomName.text.Trim() : "";
        string roomPassword = InputRoomPassword != null ? InputRoomPassword.text.Trim() : "";

        if (string.IsNullOrEmpty(roomName))
        {
            SetOutput("Room name is empty.");
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
        string roomName = InputRoomName != null ? InputRoomName.text.Trim() : "";
        pendingJoinPassword = InputRoomPassword != null ? InputRoomPassword.text.Trim() : "";

        if (string.IsNullOrEmpty(roomName))
        {
            SetOutput("Room name is empty.");
            pendingJoinPassword = "";
            return;
        }

        ClearPhotonNickname();
        PhotonNetwork.JoinRoom(roomName);
    }

    public void RandomJoinButton()
    {
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
        SetOutput("Random join failed: " + message);
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
        SetOutput("Wrong room password.");
        PhotonNetwork.LeaveRoom();
    }

    private bool RoomHasPassword(RoomInfo roomInfo)
    {
        if (roomInfo == null || roomInfo.CustomProperties == null)
            return false;

        if (!roomInfo.CustomProperties.TryGetValue(RoomPropHasPassword, out object hasPwObj))
            return false;

        return Convert.ToInt32(hasPwObj) == 1;
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

    private void SetOutput(string message)
    {
        if (OutPutText != null)
            OutPutText.text = message;

        Debug.Log(message);
    }
}
