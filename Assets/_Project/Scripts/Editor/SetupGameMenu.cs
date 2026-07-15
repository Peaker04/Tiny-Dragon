using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TinyDragon.UI;

public class SetupGameMenu
{
    [MenuItem("Tools/Setup Game Menu Overlay")]
    public static void DoSetup()
    {
        string scenePath = "Assets/_Project/Scenes/Level_02.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Find existing Pause and Settings
        GameObject pauseObj = GameObject.Find("Pause");
        GameObject settingsObj = GameObject.Find("Settings");

        if (pauseObj == null || settingsObj == null)
        {
            Debug.LogError("Could not find Pause or Settings in Level_02");
            return;
        }

        // Create overlay parent
        GameObject overlay = new GameObject("GameMenuOverlay");
        
        // Disconnect from prefabs if they are instances
        if (PrefabUtility.IsPartOfPrefabInstance(pauseObj)) PrefabUtility.UnpackPrefabInstance(pauseObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        if (PrefabUtility.IsPartOfPrefabInstance(settingsObj)) PrefabUtility.UnpackPrefabInstance(settingsObj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        pauseObj.transform.SetParent(overlay.transform, false);
        settingsObj.transform.SetParent(overlay.transform, false);

        PauseManager pm = pauseObj.GetComponent<PauseManager>();
        SettingsManager sm = settingsObj.GetComponent<SettingsManager>();

        if (pm != null)
        {
            SerializedObject so = new SerializedObject(pm);
            so.FindProperty("mainMenuSceneName").stringValue = "Level_01_Origin";
            so.FindProperty("settingsCanvas").objectReferenceValue = settingsObj.GetComponent<Canvas>();
            so.ApplyModifiedProperties();
        }

        if (sm != null)
        {
            SerializedObject so = new SerializedObject(sm);
            so.FindProperty("pauseCanvas").objectReferenceValue = pauseObj.GetComponent<Canvas>();
            so.ApplyModifiedProperties();
        }

        // Save as prefab
        string prefabPath = "Assets/_Project/Prefabs/UI/GameMenuOverlay.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(overlay, prefabPath);
        
        // Remove from scene to apply clean later
        GameObject.DestroyImmediate(overlay);
        EditorSceneManager.SaveScene(scene);

        // Now apply to all gameplay scenes
        string[] gameplayScenes = new string[] {
            "Assets/_Project/Scenes/LangAru.unity",
            "Assets/_Project/Scenes/DoiHoaCuc.unity",
            "Assets/_Project/Scenes/ThungLungTre.unity",
            "Assets/_Project/Scenes/VoDaiXenBoHung.unity",
            "Assets/_Project/Scenes/Level_02.unity",
            "Assets/_Project/Scenes/Level_03.unity"
        };

        foreach (string path in gameplayScenes)
        {
            Scene s = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            
            // Clean up old ones
            foreach (GameObject root in s.GetRootGameObjects())
            {
                if (root.name == "Pause" || root.name == "Settings" || root.name == "GameMenuOverlay")
                {
                    GameObject.DestroyImmediate(root);
                }
            }

            // Instantiate GameMenuOverlay
            GameObject inst = PrefabUtility.InstantiatePrefab(savedPrefab) as GameObject;
            
            EditorSceneManager.SaveScene(s);
            Debug.Log("Added GameMenuOverlay to " + path);
        }

        // Apply the same overlay to Level_01_Origin so Settings/Pause stay available there too.
        Scene menuScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_01_Origin.unity", OpenSceneMode.Single);
        foreach (GameObject root in menuScene.GetRootGameObjects())
        {
            if (root.name == "Pause" || root.name == "Settings" || root.name == "GameMenuOverlay")
            {
                GameObject.DestroyImmediate(root);
            }
        }
        PrefabUtility.InstantiatePrefab(savedPrefab);
        EditorSceneManager.SaveScene(menuScene);

        Debug.Log("Game Menu Overlay Setup Complete!");
    }
}
