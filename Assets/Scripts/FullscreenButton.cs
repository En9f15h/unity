using UnityEngine;
using UnityEngine.UI;

// Both menu scenes operate on the same player window.
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class FullscreenButton : MonoBehaviour
{
    static int windowWidth, windowHeight;
    static float nextSwitchTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        windowWidth = windowHeight = 0;
        nextSwitchTime = 0;
    }

    void OnEnable() => GetComponent<Button>().onClick.AddListener(Toggle);
    void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Toggle);

    public void Toggle()
    {
        // Resolution changes complete at the end of a frame; ignore rapid repeat clicks.
        if (Time.unscaledTime < nextSwitchTime) return;
        nextSwitchTime = Time.unscaledTime + .5f;
        var desktop = Screen.currentResolution;
        if (!Screen.fullScreen)
        {
            windowWidth = Screen.width;
            windowHeight = Screen.height;
            Screen.SetResolution(desktop.width, desktop.height, FullScreenMode.FullScreenWindow);
        }
        else
        {
            // Leave room for the window border and taskbar on the current display.
            int maxWidth = Mathf.Max(1, Mathf.RoundToInt(desktop.width * .85f));
            int maxHeight = Mathf.Max(1, Mathf.RoundToInt(desktop.height * .85f));
            int width = windowWidth > 0 ? windowWidth : Mathf.Min(1280, maxWidth);
            int height = windowHeight > 0 ? windowHeight : Mathf.RoundToInt(width * (Screen.height / (float)Screen.width));
            float scale = Mathf.Min(1f, maxWidth / (float)width, maxHeight / (float)height);
            Screen.SetResolution(Mathf.Max(1, Mathf.RoundToInt(width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(height * scale)), FullScreenMode.Windowed);
        }
    }
}
