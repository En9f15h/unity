#if UNITY_EDITOR || CODEX_SHADER_BENCHMARK
using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ResultPresentationProbe
{
    public static IEnumerator Run(int map, Action<string> capture, Action<bool,string> check)
    {
        var manager = GameResultManager.Instance;
        int local = PhotonNetwork.LocalPlayer.ActorNumber;
        int other = local == 2 ? 1 : 2;
        float oldScale = Time.timeScale;
        // The result intro must finish even if combat is paused.
        Time.timeScale = 0;
        try
        {
            manager.ShowResult(local, local, "HPZero");
            var view = UnityEngine.Object.FindFirstObjectByType<ResultPresentation>();
            check(view != null, "Result presentation created on map " + map);
            var root = (RectTransform)view.transform;
            var panel = (RectTransform)root.Find("Dim/ResultPanel");
            var group = panel.GetComponent<CanvasGroup>();
            var title = panel.Find("Title").GetComponent<Text>();
            var emblem = panel.Find("ResultImage").GetComponent<Image>();
            var button = panel.Find("ReturnButton").GetComponent<Button>();
            var dim = root.Find("Dim").GetComponent<Image>();
            check(group.alpha == 0 && dim.raycastTarget && button.interactable,
                "Intro starts transparent with immediate modal blocking and usable return button " + map);
            float until = Time.realtimeSinceStartup + .6f;
            while (Time.realtimeSinceStartup < until) yield return null;
            check(group.alpha == 1 && panel.anchoredPosition == Vector2.zero,
                "Intro settles while timeScale is zero " + map);
            check(title.text == "VICTORY" && emblem.enabled && emblem.sprite != null,
                "Victory retains title and authored emblem " + map);
            check(Mathf.Approximately(dim.color.a,.52f), "Result-only dim settles at restrained opacity " + map);
            CheckBounds(root, panel, check, "actual map " + map);

            if (map == 0)
            {
                capture("Result-victory.png");
                check(emblem.materialForRendering.shader.name == "Combat/Result Emblem UI" && emblem.materialForRendering.shader.isSupported,
                    "Result emblem shader is supported in Player");
                var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(10,10) };
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer,hits);
                check(hits.Count > 0 && hits[0].gameObject == dim.gameObject,
                    "Outside-panel clicks hit result backdrop before gameplay UI");
                int count = root.GetComponentsInChildren<Graphic>(true).Length;
                foreach (string state in new[] {"DEFEAT", "DRAW", "OPPONENT LEFT", "VICTORY"})
                {
                    bool draw = state == "DRAW";
                    manager.ShowResult(draw ? 0 : state == "DEFEAT" ? other : local, local,
                        state == "OPPONENT LEFT" ? "OpponentLeft" : "HPZero");
                    until = Time.realtimeSinceStartup + .6f;
                    while (Time.realtimeSinceStartup < until) yield return null;
                    check(title.text == state && emblem.enabled == !draw && (draw || emblem.sprite != null),
                        "Repeated result updates correct title and emblem: " + state);
                    check(root.GetComponentsInChildren<Graphic>(true).Length == count,
                        "Repeated result reuses existing UI without extra decorations: " + state);
                    if (state != "VICTORY") capture("Result-" + state.Replace(' ','-').ToLowerInvariant() + ".png");
                }

                // Logical canvas sizes cover desktop aspect ratios and a small window.
                Canvas canvas = root.GetComponent<Canvas>();
                var mode = canvas.renderMode;
                var size = root.sizeDelta;
                try
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    foreach (var dimensions in new[] {new Vector2(1920,1080),new Vector2(1821,1138),new Vector2(1662,1247),new Vector2(640,480)})
                    {
                        root.sizeDelta = dimensions;
                        yield return null;
                        CheckBounds(root,panel,check,"canvas " + dimensions);
                    }
                }
                finally { root.sizeDelta = size; canvas.renderMode = mode; }
            }
            if (map == 2) capture("Result-victory-dark-map.png");
            // On the final route exercise the existing return callback and room reset.
            if (map == 6)
            {
                Time.timeScale = oldScale;
                button.onClick.Invoke();
                until = Time.realtimeSinceStartup + 30;
                while (SceneManager.GetActiveScene().name != "CharacterSelectScene" && Time.realtimeSinceStartup < until)
                    yield return null;
                check(SceneManager.GetActiveScene().name == "CharacterSelectScene", "Result button returns to CharacterSelectScene");
                check(!(bool)PhotonNetwork.LocalPlayer.CustomProperties[CharacterSelectPhotonKeys.IsReady], "Return clears local readiness");
                check(!(bool)PhotonNetwork.CurrentRoom.CustomProperties["gameEnded"], "Return clears room result state");
                yield return null;
                check(UnityEngine.Object.FindObjectsByType<ResultPresentation>(FindObjectsSortMode.None).Length == 0,
                    "Returning to selection releases the result overlay");
            }
            else view.gameObject.SetActive(false);
        }
        finally { Time.timeScale = oldScale; }
    }

    private static void CheckBounds(RectTransform root, RectTransform panel, Action<bool,string> check, string label)
    {
        var corners = new Vector3[4];
        panel.GetWorldCorners(corners);
        check(corners.All(c => root.rect.Contains(root.InverseTransformPoint(c))), "Result panel stays inside " + label);
    }
}
#endif
