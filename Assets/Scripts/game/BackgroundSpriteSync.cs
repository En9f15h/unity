using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class BackgroundSpriteSync : MonoBehaviourPunCallbacks
{
    [Header("背景 SpriteRenderer")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("可隨機背景陣列（所有玩家順序必須完全一樣）")]
    [SerializeField] private Sprite[] backgroundSprites;

    [Header("是否在 Start 自動同步")]
    [SerializeField] private bool syncOnStart = true;

    private const string ROOM_PROP_BG_INDEX = "bgSpriteIndex";

    private void Awake()
    {
        if (backgroundRenderer == null)
            backgroundRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (!syncOnStart)
            return;

        TrySyncBackground();
    }

    public void TrySyncBackground()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            Debug.LogWarning("BackgroundSpriteSync: 尚未進入房間，無法同步背景");
            return;
        }

        if (backgroundSprites == null || backgroundSprites.Length == 0)
        {
            Debug.LogWarning("BackgroundSpriteSync: backgroundSprites 為空");
            return;
        }

        // 房間裡已經有背景索引，直接套用
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BG_INDEX, out object indexObj))
        {
            int index = (int)indexObj;
            ApplyBackground(index);
            return;
        }

        // 只有 Master 負責抽隨機值
        if (PhotonNetwork.IsMasterClient)
        {
            int randomIndex = Random.Range(0, backgroundSprites.Length);

            Hashtable props = new Hashtable
            {
                { ROOM_PROP_BG_INDEX, randomIndex }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);

            Debug.Log("BackgroundSpriteSync: Master 隨機背景 index = " + randomIndex);
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.TryGetValue(ROOM_PROP_BG_INDEX, out object indexObj))
        {
            int index = (int)indexObj;
            ApplyBackground(index);
        }
    }

    private void ApplyBackground(int index)
    {
        if (backgroundRenderer == null)
        {
            Debug.LogWarning("BackgroundSpriteSync: backgroundRenderer 沒有指定");
            return;
        }

        if (backgroundSprites == null || backgroundSprites.Length == 0)
        {
            Debug.LogWarning("BackgroundSpriteSync: backgroundSprites 為空");
            return;
        }

        if (index < 0 || index >= backgroundSprites.Length)
        {
            Debug.LogWarning("BackgroundSpriteSync: index 超出範圍 = " + index);
            return;
        }

        backgroundRenderer.sprite = backgroundSprites[index];
        Debug.Log("BackgroundSpriteSync: 已套用背景 index = " + index + " / sprite = " + backgroundSprites[index].name);
    }

    // 如果 Master 切換，也補一次，避免特殊情況沒寫進去
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TrySyncBackground();
    }

    // 晚進房的玩家，也在加入房間後補檢查一次
    public override void OnJoinedRoom()
    {
        TrySyncBackground();
    }
}