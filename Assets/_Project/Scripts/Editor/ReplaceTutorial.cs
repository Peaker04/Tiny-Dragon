using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class ReplaceTutorial
{
    [MenuItem("Tools/Replace Tutorial")]
    public static void DoReplace()
    {
        // 1. Open Level_01_guide
        Scene guideScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_01_guide.unity", OpenSceneMode.Single);
        GameObject tutorialManagerGuide = null;
        
        foreach (GameObject root in guideScene.GetRootGameObjects())
        {
            if (root.name == "TutorialManager" || root.GetComponent<TinyDragon.UI.TutorialManager>() != null)
            {
                tutorialManagerGuide = root;
                break;
            }
        }

        if (tutorialManagerGuide == null)
        {
            Debug.LogError("Could not find TutorialManager in Level_01_guide!");
            return;
        }

        // Save as prefab
        string prefabPath = "Assets/_Project/Prefabs/UI/TutorialCanvas_CongHieu.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(tutorialManagerGuide, prefabPath);
        Debug.Log("Saved Tutorial from CongHieu as Prefab.");

        // 2. Open Level_01_Origin
        Scene originScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_01_Origin.unity", OpenSceneMode.Single);
        
        // Find and delete old TutorialManager(s)
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go.GetComponent<TinyDragon.UI.TutorialManager>() != null)
            {
                Debug.Log("Deleting old TutorialManager: " + go.name);
                GameObject.DestroyImmediate(go);
            }
        }

        // Instantiate new one
        PrefabUtility.InstantiatePrefab(savedPrefab);
        EditorSceneManager.SaveScene(originScene);
        Debug.Log("Replaced TutorialManager in Level_01_Origin!");

        // 3. Open LangAru and remove old tutorial if it exists there
        Scene langAruScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/LangAru.unity", OpenSceneMode.Single);
        allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool modifiedLangAru = false;
        foreach (GameObject go in allObjects)
        {
            if (go.GetComponent<TinyDragon.UI.TutorialManager>() != null)
            {
                Debug.Log("Deleting old TutorialManager from LangAru: " + go.name);
                GameObject.DestroyImmediate(go);
                modifiedLangAru = true;
            }
        }
        if (modifiedLangAru) EditorSceneManager.SaveScene(langAruScene);
        FileUtil.DeleteFileOrDirectory("Assets/_Project/Scenes/Level_01_guide.unity");
        FileUtil.DeleteFileOrDirectory("Assets/_Project/Scenes/Level_01_guide.unity.meta");
        AssetDatabase.Refresh();
        Debug.Log("Deleted Level_01_guide scene.");

    }
}