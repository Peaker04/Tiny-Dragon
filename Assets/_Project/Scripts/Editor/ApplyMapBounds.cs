using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TinyDragon.Camera;

public class ApplyMapBounds
{
    [MenuItem("Tools/Setup Map Bounds")]
    public static void DoSetup()
    {
        string[] scenePaths = new string[] {
            "Assets/_Project/Scenes/LangAru.unity",
            "Assets/_Project/Scenes/DoiHoaCuc.unity",
            "Assets/_Project/Scenes/ThungLungTre.unity",
            "Assets/_Project/Scenes/VoDaiXenBoHung.unity",
            "Assets/_Project/Scenes/Level_02.unity",
            "Assets/_Project/Scenes/Level_03.unity"
        };

        foreach (string path in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;

            // 1. Standardize Main Camera to 3.55
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Camera cam = root.GetComponentInChildren<UnityEngine.Camera>(true);
                if (cam != null && (cam.CompareTag("MainCamera") || cam.name == "Main Camera"))
                {
                    if (Mathf.Abs(cam.orthographicSize - 3.55f) > 0.01f)
                    {
                        cam.orthographicSize = 3.55f;
                        changed = true;
                    }
                }
            }

            // 2. Automatically calculate Background Bounds
            Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool initializedBounds = false;

            SpriteRenderer[] allSprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude);
            foreach (var sr in allSprites)
            {
                string n = sr.name.ToLower();
                if (n.Contains("background") || n.Contains("bg") || n.Contains("map"))
                {
                    if (!initializedBounds)
                    {
                        combinedBounds = sr.bounds;
                        initializedBounds = true;
                    }
                    else
                    {
                        combinedBounds.Encapsulate(sr.bounds);
                    }
                }
            }

            // If we didn't find by name, try to find the absolute largest sprite in the scene
            if (!initializedBounds && allSprites.Length > 0)
            {
                SpriteRenderer largest = allSprites[0];
                float maxArea = 0f;
                foreach (var sr in allSprites)
                {
                    float area = sr.bounds.size.x * sr.bounds.size.y;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        largest = sr;
                    }
                }
                if (maxArea > 10f) // Must be reasonably large
                {
                    combinedBounds = largest.bounds;
                    initializedBounds = true;
                }
            }

            // 3. Attach MapBounds2D to an object and set BoxCollider2D size
            if (initializedBounds)
            {
                // Find or create MapBounds object
                GameObject boundsObj = GameObject.Find("MapBounds_Auto");
                if (boundsObj == null)
                {
                    boundsObj = new GameObject("MapBounds_Auto");
                    changed = true;
                }
                
                // Remove old MapBounds2D from terrain if they exist
                MapBounds2D[] oldBounds = Object.FindObjectsByType<MapBounds2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var ob in oldBounds)
                {
                    if (ob.gameObject != boundsObj)
                    {
                        GameObject.DestroyImmediate(ob, true);
                        changed = true;
                    }
                }

                BoxCollider2D bc = boundsObj.GetComponent<BoxCollider2D>();
                if (bc == null) bc = boundsObj.AddComponent<BoxCollider2D>();
                
                if (boundsObj.GetComponent<MapBounds2D>() == null) boundsObj.AddComponent<MapBounds2D>();

                // Set coordinates
                boundsObj.transform.position = Vector3.zero;
                bc.isTrigger = true;
                bc.offset = combinedBounds.center;
                bc.size = combinedBounds.size;
                changed = true;
                
                Debug.Log("Calculated bounds for " + path + ": size = " + combinedBounds.size);
            }
            else
            {
                Debug.LogWarning("Could not calculate background bounds for " + path);
            }

            if (changed)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Updated Camera and Map Bounds in " + path);
            }
        }

        Debug.Log("Camera Size and Map Bounds Sync Complete!");
    }
}