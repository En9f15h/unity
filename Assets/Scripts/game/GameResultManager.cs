using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameResultManager : MonoBehaviourPunCallbacks
{
    private const string LobbySceneName = "LobbyScene";

    private static GameResultManager instance;

    private GameObject overlayRoot;
    private Text titleText;
    private Text detailText;
    private Button returnButton;
    private bool isReturningToLobby;

    public static GameResultManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<GameResultManager>();

            if (instance == null)
            {
                GameObject obj = new GameObject("GameResultManager");
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
    }

    public void ShowResult(int winnerActorNumber, int localActorNumber, string reason)
    {
        EnsureOverlay();

        bool isDraw = winnerActorNumber <= 0;
        bool localWon = !isDraw && winnerActorNumber == localActorNumber;

        if (titleText != null)
        {
            if (isDraw)
                titleText.text = "\u5e73\u624b";
            else
                titleText.text = localWon ? "\u52dd\u5229" : "\u5931\u6557";
        }

        if (detailText != null)
            detailText.text = BuildDetailText(isDraw, localWon, reason);

        overlayRoot.SetActive(true);
    }

    private string BuildDetailText(bool isDraw, bool localWon, string reason)
    {
        if (reason == "OpponentLeft")
            return "\u5c0d\u624b\u5df2\u96e2\u7dda\uff0c\u6bd4\u8cfd\u7d50\u675f\u3002";

        if (isDraw)
            return "\u96d9\u65b9 HP \u540c\u6642\u6b78\u96f6\u3002";

        return localWon ? "\u5c0d\u624b HP \u6b78\u96f6\u3002" : "\u4f60\u7684 HP \u6b78\u96f6\u3002";
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
        panelRect.sizeDelta = new Vector2(620f, 360f);
        panelRect.anchoredPosition = Vector2.zero;

        titleText = CreateText("Title", panel.transform, defaultFont, 72, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.58f);
        titleRect.anchorMax = new Vector2(1f, 0.92f);
        titleRect.offsetMin = new Vector2(40f, 0f);
        titleRect.offsetMax = new Vector2(-40f, 0f);
        titleText.color = Color.white;

        detailText = CreateText("Detail", panel.transform, defaultFont, 30, FontStyle.Normal, TextAnchor.MiddleCenter);
        RectTransform detailRect = detailText.GetComponent<RectTransform>();
        detailRect.anchorMin = new Vector2(0f, 0.34f);
        detailRect.anchorMax = new Vector2(1f, 0.58f);
        detailRect.offsetMin = new Vector2(48f, 0f);
        detailRect.offsetMax = new Vector2(-48f, 0f);
        detailText.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        returnButton = CreateButton("ReturnButton", panel.transform, defaultFont, "\u8fd4\u56de\u5927\u5ef3");
        RectTransform buttonRect = returnButton.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.12f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.12f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(240f, 72f);
        buttonRect.anchoredPosition = Vector2.zero;
        returnButton.onClick.AddListener(ReturnToLobby);

        overlayRoot.SetActive(false);
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject obj = new GameObject(objectName, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
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
        image.color = new Color(0.16f, 0.32f, 0.62f, 1f);

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

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private void ReturnToLobby()
    {
        if (isReturningToLobby)
            return;

        isReturningToLobby = true;

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            return;
        }

        SceneManager.LoadScene(LobbySceneName);
    }

    public override void OnLeftRoom()
    {
        if (!isReturningToLobby)
            return;

        SceneManager.LoadScene(LobbySceneName);
    }
}
