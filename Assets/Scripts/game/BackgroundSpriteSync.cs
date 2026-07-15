using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class BackgroundSpriteSync : MonoBehaviourPunCallbacks
{
    public const string ROOM_PROP_BG_INDEX = "bgSpriteIndex";

    [Header("Background SpriteRenderer")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Header("Floor SpriteRenderer")]
    [SerializeField] private SpriteRenderer floorRenderer;

    [Header("Stage backgrounds. Order must match on every client.")]
    [SerializeField] private Sprite[] backgroundSprites;

    [Header("Stage floors. Same index as backgroundSprites.")]
    [SerializeField] private Sprite[] floorSprites;

    [Header("Auto sync on Start")]
    [SerializeField] private bool syncOnStart = true;

    private Sprite initialFloorSprite;

    private void Awake()
    {
        if (backgroundRenderer == null)
            backgroundRenderer = GetComponent<SpriteRenderer>();

        if (floorRenderer == null)
            floorRenderer = FindRendererByName("ground", "floor");

        if (floorRenderer != null)
            initialFloorSprite = floorRenderer.sprite;
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
            Debug.LogWarning("BackgroundSpriteSync: not in room, cannot sync stage visuals");
            return;
        }

        if (backgroundSprites == null || backgroundSprites.Length == 0)
        {
            Debug.LogWarning("BackgroundSpriteSync: backgroundSprites is empty");
            return;
        }

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(ROOM_PROP_BG_INDEX, out object indexObj))
        {
            int index = System.Convert.ToInt32(indexObj);
            ApplyBackground(index);
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Hashtable props = new Hashtable
            {
                { ROOM_PROP_BG_INDEX, 0 }
            };

            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Debug.Log("BackgroundSpriteSync: Master set default stage index = 0");
        }
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.TryGetValue(ROOM_PROP_BG_INDEX, out object indexObj))
        {
            int index = System.Convert.ToInt32(indexObj);
            ApplyBackground(index);
        }
    }

    private void ApplyBackground(int index)
    {
        if (backgroundRenderer == null)
        {
            Debug.LogWarning("BackgroundSpriteSync: backgroundRenderer is missing");
            return;
        }

        if (backgroundSprites == null || backgroundSprites.Length == 0)
        {
            Debug.LogWarning("BackgroundSpriteSync: backgroundSprites is empty");
            return;
        }

        if (index < 0 || index >= backgroundSprites.Length)
        {
            Debug.LogWarning("BackgroundSpriteSync: index out of range = " + index);
            return;
        }

        backgroundRenderer.sprite = backgroundSprites[index];
        ApplyFloor(index);

        Debug.Log("BackgroundSpriteSync: applied stage index = " + index + " / background = " + backgroundSprites[index].name);
    }

    private void ApplyFloor(int index)
    {
        if (floorRenderer == null)
            return;

        Sprite floorSprite = GetFloorSprite(index);
        if (floorSprite == null)
            return;

        floorRenderer.sprite = floorSprite;
    }

    private Sprite GetFloorSprite(int index)
    {
        if (floorSprites != null && index >= 0 && index < floorSprites.Length && floorSprites[index] != null)
            return floorSprites[index];

        if (floorSprites != null && floorSprites.Length == 1 && floorSprites[0] != null)
            return floorSprites[0];

        return initialFloorSprite;
    }

    private SpriteRenderer FindRendererByName(params string[] nameParts)
    {
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            string lowerName = renderers[i].name.ToLowerInvariant();
            for (int j = 0; j < nameParts.Length; j++)
            {
                if (lowerName.Contains(nameParts[j]))
                    return renderers[i];
            }
        }

        return null;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        TrySyncBackground();
    }

    public override void OnJoinedRoom()
    {
        TrySyncBackground();
    }
}
