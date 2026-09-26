using System.Collections;
using Photon.Pun;
using UnityEngine;

// One entry snapshot per scene, using the same atomic readiness message as battle startup.
public sealed class ActionUsageMatchHUD : MonoBehaviour
{
    public bool Frozen { get; private set; }
    public ActionUsagePanel MasterPanel { get; private set; }
    public ActionUsagePanel ClientPanel { get; private set; }
    IEnumerator Start()
    {
        while (GameSceneStartSync.Instance == null || !GameSceneStartSync.Instance.AreBothPlayersSceneReady()) yield return null;
        var manager = GetComponent<BattleUIManager>();
        int token = LocalActionUsage.EntryToken(PhotonNetwork.LocalPlayer);
        foreach (var player in PhotonNetwork.PlayerList)
        {
            var bar = manager.GetBarByOwner(player.IsMasterClient); if (bar == null) continue;
            var panel = bar.GetComponent<ActionUsagePanel>() ?? bar.gameObject.AddComponent<ActionUsagePanel>();
            panel.Freeze(LocalActionUsage.ReadPeer(player, token), LocalActionUsage.CharacterId(player));
            if (player.IsMasterClient) MasterPanel = panel; else ClientPanel = panel;
        }
        Frozen = true;
    }
}
