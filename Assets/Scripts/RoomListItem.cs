using UnityEngine;
using UnityEngine.UI;

public class RoomListItem : MonoBehaviour
{
    [SerializeField] private Text roomNameText;
    [SerializeField] private Text passwordStateText;
    [SerializeField] private Button joinButton;
    [SerializeField] private Text joinButtonText;

    public void SetReferences(Text nameText, Text passwordText, Button button, Text buttonLabel)
    {
        roomNameText = nameText;
        passwordStateText = passwordText;
        joinButton = button;
        joinButtonText = buttonLabel;
    }

    public void Bind(string roomName, bool hasPassword, bool canJoin, LobbyManager lobbyManager)
    {
        if (roomNameText != null)
            roomNameText.text = "-" + roomName;

        if (passwordStateText != null)
            passwordStateText.text = hasPassword ? "|Y|" : "|N|";

        if (joinButtonText != null)
            joinButtonText.text = "JOIN";

        if (joinButton == null)
            return;

        joinButton.onClick.RemoveAllListeners();
        joinButton.gameObject.SetActive(!hasPassword);
        joinButton.interactable = !hasPassword && canJoin && lobbyManager != null;

        if (!hasPassword && lobbyManager != null)
        {
            string targetRoomName = roomName;
            joinButton.onClick.AddListener(() => lobbyManager.JoinPublicRoom(targetRoomName));
        }
    }
}
