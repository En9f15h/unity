#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuButtonProbe
{
    static void SuppressLobbyConnection(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "LobbyScene")
            foreach (var root in scene.GetRootGameObjects())
                foreach (var manager in root.GetComponentsInChildren<LobbyManager>(true)) manager.enabled = false;
    }
    public static IEnumerator Menus(Action<string> capture, Action<bool, string> check)
    {
        SceneManager.sceneLoaded += SuppressLobbyConnection;
        try
        {
            foreach (string scene in new[] { "StartScene", "LobbyScene" })
            {
                yield return SceneManager.LoadSceneAsync(scene);
                yield return Wait(.6f);
                yield return CheckScene(capture, check);
                if (scene == "LobbyScene")
                {
                    var manager = UnityEngine.Object.FindFirstObjectByType<LobbyManager>();
                    var canvas = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.isRootCanvas && c.isActiveAndEnabled);
                    var method = typeof(LobbyManager).GetMethod("CreateRoomJoinButton", BindingFlags.NonPublic | BindingFlags.Instance);
                    object[] args = { canvas.transform, null };
                    var dynamicButton = (Button)method.Invoke(manager, args);
                    check(dynamicButton.GetComponent<MenuButtonPresentation>() != null, "Runtime room JOIN button receives menu presentation");
                    check(dynamicButton.GetComponentInChildren<PlanningFocusFrame>().raycastTarget == false, "Dynamic JOIN decoration cannot intercept clicks");
                    UnityEngine.Object.Destroy(dynamicButton.gameObject);
                }
            }
        }
        finally { SceneManager.sceneLoaded -= SuppressLobbyConnection; }
    }
    public static IEnumerator CheckScene(Action<string> capture, Action<bool, string> check)
    {
        string scene = SceneManager.GetActiveScene().name;
        var all = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Button>(true))
            .Where(b => b.targetGraphic is Image && b.GetComponent<CharacterSelectionCard>() == null && b.GetComponent<MapSelectionCard>() == null).ToArray();
        check(all.Length > 0 && all.All(b => b.GetComponent<MenuButtonPresentation>() != null), scene + " styles active and initially hidden buttons");
        var button = all.FirstOrDefault(b => b.isActiveAndEnabled && b.IsInteractable() && (b.name == "ConfirmButton" || b.name == "RoomButton"))
            ?? all.First(b => b.isActiveAndEnabled && b.IsInteractable());
        var view = button.GetComponent<MenuButtonPresentation>();
        var frame = button.GetComponentInChildren<PlanningFocusFrame>(true);
        var rect = (RectTransform)button.transform;
        var position = rect.anchoredPosition; var scale = rect.localScale; var size = rect.sizeDelta;
        int callbacks = button.onClick.GetPersistentEventCount();
        var sprite = button.image.sprite;
        if (scene == "StartScene") check(button.GetComponentInChildren<TMPro.TMP_Text>().color.r > .9f, "Start label has readable warm light text");
        check(!frame.raycastTarget && frame.GetComponent<LayoutElement>().ignoreLayout, scene + " decoration preserves hit testing and layout");
        capture(scene + "-Idle.png");
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        float timeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            ExecuteEvents.Execute<IPointerEnterHandler>(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return Wait(.18f);
            check(frame.enabled && frame.color.a > .75f, scene + " hover animates with paused game time");
            capture(scene + "-Hover.png");
            ExecuteEvents.Execute<IPointerDownHandler>(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            check(frame.rectTransform.offsetMin.x == 6f, scene + " press draws an inset frame");
            capture(scene + "-Pressed.png");
            ExecuteEvents.Execute<IPointerUpHandler>(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute<IPointerExitHandler>(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            EventSystem.current.SetSelectedGameObject(null);
            yield return Wait(.2f);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return Wait(.18f);
            check(frame.enabled && frame.color.a > .75f, scene + " keyboard selection has visible focus");
            // Exercise only visual submit; never connect, create rooms or invoke Quit.
            view.OnSubmit(new BaseEventData(EventSystem.current));
            check(frame.rectTransform.offsetMin.x == 6f, scene + " keyboard submit has press feedback");
            button.interactable = false;
            yield return Wait(.2f);
            check(!frame.enabled, scene + " disabled state clears hover and press feedback");
            capture(scene + "-Disabled.png");
            button.interactable = true;
            EventSystem.current.SetSelectedGameObject(null);
            view.enabled = false;
            check(!frame.enabled, scene + " disabling presentation removes decorations");
            view.enabled = true;
            yield return Wait(.2f);
            check(rect.anchoredPosition == position && rect.localScale == scale && rect.sizeDelta == size, scene + " interaction leaves button geometry unchanged");
            check(button.image.sprite == sprite && button.onClick.GetPersistentEventCount() == callbacks, scene + " original artwork and click callbacks preserved");
        }
        finally { Time.timeScale = timeScale; }
    }
    static IEnumerator Wait(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
}
#endif
