using ExitGames.Client.Photon;
using Photon.Pun;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameResultManager : MonoBehaviourPunCallbacks
{
    private const string CharacterSelectSceneName = "CharacterSelectScene";
    private const string ResultManagerResourcePath = "Prefab/GameResultManager";
    private const string RoomPropGameEnded = "gameEnded";
    private const string RoomPropGameWinnerActor = "gameWinnerActor";
    private const string RoomPropGameEndReason = "gameEndReason";
    private const string VictorySpriteAssetPath = "Assets/Scenes/UI/Victory.png";
    private const string DefeatSpriteAssetPath = "Assets/Scenes/UI/Defeat.png";
    private const string LeaveSpriteAssetPath = "Assets/Scenes/UI/Leave.png";
    private const string VictorySpriteResourcePath = "UI/Result/Victory";
    private const string DefeatSpriteResourcePath = "UI/Result/Defeat";
    private const string LeaveSpriteResourcePath = "UI/Result/Leave";

    private static GameResultManager instance;
    private static Sprite cachedVictorySprite;
    private static Sprite cachedDefeatSprite;
    private static Sprite cachedLeaveSprite;

    [SerializeField] private Sprite victorySprite;
    [SerializeField] private Sprite defeatSprite;
    [SerializeField] private Sprite leaveSprite;
    [SerializeField] private Sprite returnButtonSprite;

    private GameObject overlayRoot;
    private Image resultImage;
    private Text titleText;
    private Button returnButton;
    private ResultPresentation presentation;
    private bool isReturningToCharacterSelect;

    public static GameResultManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<GameResultManager>();

            if (instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>(ResultManagerResourcePath);
                GameObject obj = prefab != null
                    ? Instantiate(prefab)
                    : new GameObject("GameResultManager");

                obj.name = "GameResultManager";
                instance = obj.GetComponent<GameResultManager>();
                if (instance == null)
                    instance = obj.AddComponent<GameResultManager>();
            }

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureSpriteReferences();
    }

    public void ShowResult(int winnerActorNumber, int localActorNumber, string reason)
    {
        EnsureSpriteReferences();
        EnsureOverlay();

        bool isDraw = winnerActorNumber <= 0;
        bool localWon = !isDraw && winnerActorNumber == localActorNumber;
        bool opponentLeft = reason == "OpponentLeft";

        AudioManager.Instance.PlayResult(localWon, isDraw, opponentLeft);

        if (titleText != null)
            titleText.text = BuildTitleText(isDraw, localWon, reason);

        SetResultImage(ResolveResultSprite(isDraw, localWon, reason));

        overlayRoot.SetActive(true);
        presentation.Present(localWon, isDraw, opponentLeft);
    }

    private string BuildTitleText(bool isDraw, bool localWon, string reason)
    {
        if (reason == "OpponentLeft")
            return "OPPONENT LEFT";

        if (isDraw)
            return "DRAW";

        return localWon ? "VICTORY" : "DEFEAT";
    }

    private Sprite ResolveResultSprite(bool isDraw, bool localWon, string reason)
    {
        if (reason == "OpponentLeft")
            return leaveSprite;

        if (isDraw)
            return null;

        return localWon ? victorySprite : defeatSprite;
    }

    private void EnsureSpriteReferences()
    {
        EnsureResultSpritesFromProjectFiles();

        if (victorySprite != null && defeatSprite != null && leaveSprite != null && returnButtonSprite != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(ResultManagerResourcePath);
        if (prefab == null)
            return;

        GameResultManager prefabManager = prefab.GetComponent<GameResultManager>();
        if (prefabManager == null || prefabManager == this)
            return;

        if (victorySprite == null)
            victorySprite = prefabManager.victorySprite;

        if (defeatSprite == null)
            defeatSprite = prefabManager.defeatSprite;

        if (leaveSprite == null)
            leaveSprite = prefabManager.leaveSprite;

        if (returnButtonSprite == null)
            returnButtonSprite = prefabManager.returnButtonSprite;

        EnsureResultSpritesFromProjectFiles();
    }

    private void EnsureResultSpritesFromProjectFiles()
    {
        AssignResourceSprite(ref victorySprite, ref cachedVictorySprite, VictorySpriteResourcePath);
        AssignResourceSprite(ref defeatSprite, ref cachedDefeatSprite, DefeatSpriteResourcePath);
        AssignResourceSprite(ref leaveSprite, ref cachedLeaveSprite, LeaveSpriteResourcePath);

        if (!Application.isEditor)
            return;

        AssignProjectSprite(ref victorySprite, ref cachedVictorySprite, VictorySpriteAssetPath);
        AssignProjectSprite(ref defeatSprite, ref cachedDefeatSprite, DefeatSpriteAssetPath);
        AssignProjectSprite(ref leaveSprite, ref cachedLeaveSprite, LeaveSpriteAssetPath);
    }

    private void AssignResourceSprite(ref Sprite target, ref Sprite cache, string resourcePath)
    {
        if (target != null)
            return;

        if (cache == null)
            cache = LoadSpriteFromResources(resourcePath);

        if (cache != null)
            target = cache;
    }

    private Sprite LoadSpriteFromResources(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
            return null;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
            return sprite;

        Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private void AssignProjectSprite(ref Sprite target, ref Sprite cache, string assetPath)
    {
        if (cache == null)
            cache = LoadSpriteFromProjectFile(assetPath);

        if (cache != null)
            target = cache;
    }

    private Sprite LoadSpriteFromProjectFile(string assetPath)
    {
        string fullPath = GetProjectFilePath(assetPath);
        if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            return null;

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(assetPath);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
    }

    private string GetProjectFilePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return string.Empty;

        string normalizedPath = assetPath.Replace('\\', '/');
        if (normalizedPath.StartsWith("Assets/"))
            normalizedPath = normalizedPath.Substring("Assets/".Length);

        return Path.Combine(Application.dataPath, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private void SetResultImage(Sprite sprite)
    {
        if (resultImage == null)
            return;

        resultImage.sprite = sprite;
        resultImage.color = Color.white;
        resultImage.preserveAspect = true;
        resultImage.enabled = sprite != null;
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null)
            return;

        EnsureEventSystem();

        Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        overlayRoot = new GameObject("GameResultOverlay");

        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        overlayRoot.AddComponent<GraphicRaycaster>();

        GameObject dim = CreateUIObject("Dim", overlayRoot.transform);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.72f);
        Stretch(dim.GetComponent<RectTransform>());

        GameObject panel = CreateUIObject("ResultPanel", dim.transform);
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.09f, 0.94f);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(680f, 560f);
        panelRect.anchoredPosition = Vector2.zero;

        resultImage = CreateImage("ResultImage", panel.transform);
        RectTransform imageRect = resultImage.GetComponent<RectTransform>();
        imageRect.anchorMin = new Vector2(0.5f, 0.73f);
        imageRect.anchorMax = new Vector2(0.5f, 0.73f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.sizeDelta = new Vector2(300f, 300f);
        imageRect.anchoredPosition = Vector2.zero;
        resultImage.enabled = false;

        titleText = CreateText("Title", panel.transform, defaultFont, 64, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.26f);
        titleRect.anchorMax = new Vector2(1f, 0.41f);
        titleRect.offsetMin = new Vector2(40f, 0f);
        titleRect.offsetMax = new Vector2(-40f, 0f);
        titleText.color = Color.white;
        titleText.resizeTextForBestFit = true;
        titleText.resizeTextMinSize = 36;
        titleText.resizeTextMaxSize = 64;

        returnButton = CreateButton("ReturnButton", panel.transform, defaultFont, "Back");
        RectTransform buttonRect = returnButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.12f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.12f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(300f, 72f);
        buttonRect.anchoredPosition = Vector2.zero;
        returnButton.onClick.AddListener(ReturnToCharacterSelect);

        presentation = overlayRoot.AddComponent<ResultPresentation>();
        presentation.Configure(dimImage, panelRect, resultImage, titleText);
        overlayRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (overlayRoot != null) Destroy(overlayRoot);
        if (instance == this) instance = null;
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private Image CreateImage(string objectName, Transform parent)
    {
        GameObject obj = CreateUIObject(objectName, parent);
        Image image = obj.AddComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, Font font, int size, FontStyle style, TextAnchor alignment)
    {
        GameObject obj = CreateUIObject(objectName, parent);
        Text text = obj.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string objectName, Transform parent, Font font, string label)
    {
        GameObject buttonObject = CreateUIObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        if (returnButtonSprite != null)
        {
            image.sprite = returnButtonSprite;
            image.preserveAspect = true;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.16f, 0.32f, 0.62f, 1f);
        }

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.22f, 0.42f, 0.78f, 1f);
        colors.pressedColor = new Color(0.11f, 0.23f, 0.46f, 1f);
        button.colors = colors;

        Text labelText = CreateText("Text", buttonObject.transform, font, 30, FontStyle.Bold, TextAnchor.MiddleCenter);
        labelText.text = label;
        labelText.color = Color.white;
        Stretch(labelText.GetComponent<RectTransform>());

        return button;
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void ReturnToCharacterSelect()
    {
        if (isReturningToCharacterSelect)
            return;

        isReturningToCharacterSelect = true;
        PrepareRoomForCharacterSelect();

        SceneTransitionManager.RequestSceneTransition(CharacterSelectSceneName);
    }

    private void PrepareRoomForCharacterSelect()
    {
        if (!PhotonNetwork.InRoom)
            return;

        if (PhotonNetwork.LocalPlayer != null)
        {
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { CharacterSelectPhotonKeys.IsReady, false },
                { CharacterSelectPhotonKeys.LegacyReady, false }
            });
        }

        if (PhotonNetwork.CurrentRoom != null)
        {
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { CharacterSelectPhotonKeys.SelectionPhase, (int)CharacterSelectionPhase.Selecting },
                { CharacterSelectPhotonKeys.PhaseStartTime, -1d },
                { RoomPropGameEnded, false },
                { RoomPropGameWinnerActor, 0 },
                { RoomPropGameEndReason, string.Empty }
            });
        }
    }

    public override void OnLeftRoom()
    {
        if (!isReturningToCharacterSelect)
            return;

        SceneTransitionManager.RequestSceneTransition(CharacterSelectSceneName);
    }
}
