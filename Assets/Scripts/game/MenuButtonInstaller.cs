using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Scene-local setup also includes initially hidden dialogs; no recurring scene scan.
public sealed class MenuButtonInstaller : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "StartScene" && scene.name != "LobbyScene" && scene.name != "CharacterSelectScene") return;
        var obj = new GameObject("Menu Button Presentation");
        SceneManager.MoveGameObjectToScene(obj, scene);
        obj.AddComponent<MenuButtonInstaller>();
        Install(scene);
    }
    static void Install(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var button in root.GetComponentsInChildren<Button>(true)) MenuButtonPresentation.Ensure(button);
    }
    IEnumerator Start()
    {
        yield return null;
        Install(gameObject.scene);
    }
}
