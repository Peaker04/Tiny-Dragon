using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace TinyDragon.UI
{
    public class PersistentEventSystem : MonoBehaviour
    {
        private static PersistentEventSystem instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureEventSystemExists()
        {
            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject("EventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                inputModule.AssignDefaultActions();
            }

            if (eventSystem.GetComponent<PersistentEventSystem>() == null)
            {
                eventSystem.gameObject.AddComponent<PersistentEventSystem>();
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
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            RemoveDuplicateEventSystems();
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RemoveDuplicateEventSystems();
        }

        private void RemoveDuplicateEventSystems()
        {
            EventSystem ownEventSystem = GetComponent<EventSystem>();
            EventSystem[] eventSystems = FindObjectsByType<EventSystem>(FindObjectsInactive.Include);

            foreach (EventSystem eventSystem in eventSystems)
            {
                if (eventSystem == null || eventSystem == ownEventSystem)
                {
                    continue;
                }

                Destroy(eventSystem.gameObject);
            }

            if (ownEventSystem != null && EventSystem.current != ownEventSystem)
            {
                EventSystem.current = ownEventSystem;
            }
        }
    }
}
