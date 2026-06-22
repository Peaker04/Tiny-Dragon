using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class Level03BoundsFixer
{
    [MenuItem("Tools/Fix Level 03 Bounds")]
    public static void Fix()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Level_03") return;

        var bgFar = GameObject.Find("Background_Far");
        if (bgFar != null) {
            bgFar.transform.localScale = new Vector3(1.5f, 2.5f, 1f);
            bgFar.transform.position = new Vector3(bgFar.transform.position.x, 3f, 0f);
        }

        var mapBoundsAuto = GameObject.Find("MapBounds_Auto");
        if (mapBoundsAuto != null) {
            var col = mapBoundsAuto.GetComponent<BoxCollider2D>();
            if (col != null) {
                col.size = new Vector2(col.size.x, 15f);
                col.offset = new Vector2(col.offset.x, 5f);
            }
        }

        var mapBounds = GameObject.Find("MapBounds");
        if (mapBounds != null) {
            var col = mapBounds.GetComponent<BoxCollider2D>();
            if (col != null) {
                col.size = new Vector2(col.size.x, 15f);
                col.offset = new Vector2(col.offset.x, 5f);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Map bounds adjusted for height.");
    }
}
