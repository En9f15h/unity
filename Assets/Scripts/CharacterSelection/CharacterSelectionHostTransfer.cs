using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSelectionHostTransfer : MonoBehaviourPunCallbacks
{
    [SerializeField] private Button transferButton;
    [SerializeField] private GameObject myCrown;
    [SerializeField] private GameObject enemyCrown;
    private bool awaitingTransfer;
    private float requestedAt;

    public override void OnEnable()
    {
        base.OnEnable();
        if (transferButton != null) transferButton.onClick.AddListener(TransferHost);
        Refresh();
    }

    public override void OnDisable()
    {
        if (transferButton != null) transferButton.onClick.RemoveListener(TransferHost);
        awaitingTransfer = false;
        base.OnDisable();
    }

    public void TransferHost()
    {
        Player opponent = FindOpponent();
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || opponent == null || awaitingTransfer) return;
        awaitingTransfer = true;
        requestedAt = Time.unscaledTime;
        if (!PhotonNetwork.SetMasterClient(opponent)) awaitingTransfer = false;
        Refresh();
    }

    private void Update()
    {
        // A rejected/racing request may not produce a master-switch callback.
        if (awaitingTransfer && Time.unscaledTime - requestedAt > 5f)
        {
            awaitingTransfer = false;
            Refresh();
        }
    }

    private Player FindOpponent()
    {
        if (!PhotonNetwork.InRoom) return null;
        foreach (Player player in PhotonNetwork.PlayerList)
            if (player.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber && !player.IsInactive) return player;
        return null;
    }

    private void Refresh()
    {
        Player opponent = FindOpponent();
        bool localMaster = PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient;
        if (transferButton != null) transferButton.interactable = localMaster && opponent != null && !awaitingTransfer;
        if (myCrown != null) myCrown.SetActive(localMaster);
        if (enemyCrown != null) enemyCrown.SetActive(opponent != null && opponent.IsMasterClient);
    }

    private void FinishRequest() { awaitingTransfer = false; Refresh(); }
    public override void OnMasterClientSwitched(Player newMasterClient) => FinishRequest();
    public override void OnPlayerEnteredRoom(Player newPlayer) => Refresh();
    public override void OnPlayerLeftRoom(Player otherPlayer) => FinishRequest();
    public override void OnJoinedRoom() => FinishRequest();
    public override void OnLeftRoom() => FinishRequest();
    public override void OnDisconnected(DisconnectCause cause) => FinishRequest();
}
