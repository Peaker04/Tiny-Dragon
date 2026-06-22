using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class SetupVoDaiExit
{
    [MenuItem("Tools/Setup VoDaiXenBoHung Exit to Level_03")]
    public static void DoSetup()
    {
        string scenePath = "Assets/_Project/Scenes/VoDaiXenBoHung.unity";
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Find right boundary of the map
        float rightEdge = 15f; // Default guess
        SpriteRenderer[] allSprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var sr in allSprites)
        {
            string n = sr.name.ToLower();
            if (n.Contains("background") || n.Contains("bg") || n.Contains("map"))
            {
                rightEdge = sr.bounds.max.x;
            }
        }

        GameObject exitObj = GameObject.Find("ExitToLevel03");
        if (exitObj == null)
        {
            exitObj = new GameObject("ExitToLevel03");
            exitObj.transform.position = new Vector3(rightEdge - 0.5f, -2f, 0f);
            
            BoxCollider2D col = exitObj.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 20f);

            SceneExitOnPlayerContact exitScript = exitObj.AddComponent<SceneExitOnPlayerContact>();
            SerializedObject so = new SerializedObject(exitScript);
            so.FindProperty("targetSceneName").stringValue = "Level_03";
            so.FindProperty("useTargetSpawnPosition").boolValue = true;
            so.FindProperty("targetSpawnPosition").vector3Value = new Vector3(-12f, -3.5f, 0f);
            so.FindProperty("targetFacingDirection").floatValue = 1f;
            so.ApplyModifiedProperties();

            Debug.Log("Created ExitToLevel03 at X: " + exitObj.transform.position.x);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log("VoDaiXenBoHung -> Level_03 transition created!");
    }
}