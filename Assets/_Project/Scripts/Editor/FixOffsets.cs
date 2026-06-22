using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public class FixOffsets
{
    static FixOffsets()
    {
        EditorApplication.delayCall += DoFix;
    }

    static void DoFix()
    {
        if (EditorPrefs.GetBool("FixOffsets_v2", false)) return;
        EditorPrefs.SetBool("FixOffsets_v2", true);

        string[] scenePaths = new string[] {
            "Assets/_Project/Scenes/Intro.unity",
            "Assets/_Project/Scenes/LangAru.unity",
            "Assets/_Project/Scenes/Level_01_Original.unity",
            "Assets/_Project/Scenes/Level_02.unity",
            "Assets/_Project/Scenes/Level_03.unity",
            "Assets/_Project/Scenes/ThungLung.unity",
            "Assets/_Project/Scenes/VoDaiXenBoHung.unity"
        };

        // Fix Prefab
        string prefabPath = "Assets/_Project/Prefabs/Player/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            ApplyFixes(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
        }

        foreach (string path in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player")
                {
                    ApplyFixes(root);
                    changed = true;
                }
            }
            if (changed)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }
        
        Debug.Log("Offsets fixed!");
    }

    static void ApplyFixes(GameObject player)
    {
        Transform bodySprite = player.transform.Find("bodySprite");
        if (bodySprite == null) bodySprite = player.transform.Find("body/bodySprite");
        
        if (bodySprite != null)
        {
            Vector3 pos = bodySprite.localPosition;
            pos.y = 0.85f;
            bodySprite.localPosition = pos;
            bodySprite.localScale = new Vector3(1.1f, 1.1f, 1f); // slightly larger if needed, but let's stick to 1 for now unless instructed
        }

        var shooter = player.GetComponent<ProjectileShooter>();
        if (shooter != null)
        {
            SerializedObject so = new SerializedObject(shooter);
            so.FindProperty("projectileSpawnOffset").vector2Value = new Vector2(0.6f, 0.82f);
            so.FindProperty("powerShotSpawnOffset").vector2Value = new Vector2(0.8f, 0.87f);
            so.ApplyModifiedProperties();
        }
    }
}