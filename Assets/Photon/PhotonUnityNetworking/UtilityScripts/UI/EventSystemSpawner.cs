// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EventSystemSpawner.cs" company="Exit Games GmbH">
// </copyright>
// <summary>
// For additive Scene Loading context, eventSystem can't be added to each scene and instead should be instantiated only if necessary.
// https://answers.unity.com/questions/1403002/multiple-eventsystem-in-scene-this-is-not-supporte.html
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Photon.Pun.UtilityScripts
{
    /// <summary>
    /// Event system spawner. Will add an EventSystem GameObject with a compatible input module.
    /// Use this in additive scene loading context where you would otherwise get a "Multiple EventSystem in scene... this is not supported" error from Unity.
    /// </summary>
    public class EventSystemSpawner : MonoBehaviour
    {
        void OnEnable()
        {
            #if UNITY_6000_0_OR_NEWER
            EventSystem sceneEventSystem = FindFirstObjectByType<EventSystem>();
            #else
            EventSystem sceneEventSystem = FindObjectOfType<EventSystem>();
            #endif
            if (sceneEventSystem == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");

                eventSystem.AddComponent<EventSystem>();
                AddCompatibleInputModule(eventSystem);
            }
        }

        static void AddCompatibleInputModule(GameObject eventSystem)
        {
            #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Type inputSystemUiModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiModule != null)
            {
                eventSystem.AddComponent(inputSystemUiModule);
                return;
            }

            Debug.LogError("InputSystemUIInputModule was not found. Add an EventSystem with an InputSystemUIInputModule to the scene.");
            #else
            eventSystem.AddComponent<StandaloneInputModule>();
            #endif
        }
    }
}
