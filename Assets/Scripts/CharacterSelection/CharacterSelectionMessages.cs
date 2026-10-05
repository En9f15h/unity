using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

// Scene-local, latest-message display. No history is cached or persisted in the room.
public sealed class CharacterSelectionMessages : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [SerializeField] private InputField messageField;
    [SerializeField] private Button sendButton;
    [SerializeField] private Text myMessageText;
    [SerializeField] private Text enemyMessageText;
    [SerializeField, Min(1)] private int maxMessageLength = 200;

    public override void OnEnable()
    {
        base.OnEnable();
        if (sendButton != null) sendButton.onClick.AddListener(SendMessageText);
        // Treat player messages as text, including any literal rich-text tags.
        if (myMessageText != null) myMessageText.supportRichText = false;
        if (enemyMessageText != null) enemyMessageText.supportRichText = false;
        if (messageField != null)
            messageField.characterLimit = Mathf.Max(1, maxMessageLength);
    }

    public override void OnDisable()
    {
        if (sendButton != null) sendButton.onClick.RemoveListener(SendMessageText);
        base.OnDisable();
    }

    public void SendMessageText()
    {
        if (!PhotonNetwork.InRoom || messageField == null || myMessageText == null) return;
        string message = Normalize(messageField.text);
        if (message.Length == 0) return;

        bool queued = PhotonNetwork.RaiseEvent(GamePhotonEventCodes.CharacterSelectionMessage, message,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others, CachingOption = EventCaching.DoNotCache },
            SendOptions.SendReliable);
        if (!queued) return; // Preserve the draft if the connection cannot queue it.

        myMessageText.text = message;
        messageField.SetTextWithoutNotify(string.Empty);
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code != GamePhotonEventCodes.CharacterSelectionMessage || !PhotonNetwork.InRoom ||
            photonEvent.Sender == PhotonNetwork.LocalPlayer.ActorNumber ||
            !PhotonNetwork.CurrentRoom.Players.ContainsKey(photonEvent.Sender) ||
            !(photonEvent.CustomData is string text) || enemyMessageText == null) return;

        string message = Normalize(text);
        if (message.Length > 0) enemyMessageText.text = message;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (enemyMessageText != null) enemyMessageText.text = string.Empty;
    }

    public override void OnLeftRoom()
    {
        if (myMessageText != null) myMessageText.text = string.Empty;
        if (enemyMessageText != null) enemyMessageText.text = string.Empty;
    }

    private string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = text.Trim();
        int length = Mathf.Min(text.Length, Mathf.Max(1, maxMessageLength));
        // Avoid cutting an emoji's UTF-16 surrogate pair at the length limit.
        if (char.IsHighSurrogate(text[length - 1])) length--;
        return text.Substring(0, length);
    }
}
