using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class FixTutorialLocation
{
    [MenuItem("Tools/Fix Tutorial Location")]
    public static void DoFix()
    {
        // 1. Remove from Level_01_Origin
        Scene originScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Level_01_Origin.unity", OpenSceneMode.Single);
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool originModified = false;
        foreach (GameObject go in allObjects)
        {
            if (go.GetComponent<TinyDragon.UI.TutorialManager>() != null)
            {
                Debug.Log("Removing Tutorial from Level_01_Origin...");
                GameObject.DestroyImmediate(go);
                originModified = true;
            }
        }
        if (originModified) EditorSceneManager.SaveScene(originScene);

        // 2. Add to LangAru
        Scene langAruScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/LangAru.unity", OpenSceneMode.Single);
        allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject go in allObjects)
        {
            if (go.GetComponent<TinyDragon.UI.TutorialManager>() != null)
            {
                Debug.Log("Removing OLD Tutorial from LangAru...");
                GameObject.DestroyImmediate(go);
            }
        }

        // Instantiate new one
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/TutorialCanvas_CongHieu.prefab");
        if (prefab != null)
        {
            PrefabUtility.InstantiatePrefab(prefab);
            Debug.Log("Added NEW Tutorial to LangAru!");
        }
        else
        {
            Debug.LogError("Could not find TutorialCanvas_CongHieu.prefab!");
        }

        EditorSceneManager.SaveScene(langAruScene);
        Debug.Log("Tutorial Location Fix Complete!");
    }
}