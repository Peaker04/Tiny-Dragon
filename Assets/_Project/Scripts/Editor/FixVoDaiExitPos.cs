using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class FixVoDaiExitPos
{
    [MenuItem("Tools/Fix VoDai Exit Pos")]
    public static void DoFix()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/VoDaiXenBoHung.unity", OpenSceneMode.Single);
        GameObject exitObj = GameObject.Find("ExitToLevel03");
        if (exitObj != null)
        {
            GameObject mapBounds = GameObject.Find("MapBounds_Auto");
            if (mapBounds != null)
            {
                BoxCollider2D mapBoundsCol = mapBounds.GetComponent<BoxCollider2D>();
                if (mapBoundsCol != null)
                {
                    float rightEdge = mapBoundsCol.transform.position.x + mapBoundsCol.offset.x + (mapBoundsCol.size.x / 2f);
                    exitObj.transform.position = new Vector3(rightEdge - 0.5f, mapBoundsCol.transform.position.y + mapBoundsCol.offset.y, 0f);
                    BoxCollider2D col = exitObj.GetComponent<BoxCollider2D>();
                    if (col != null) col.size = new Vector2(2f, mapBoundsCol.size.y);
                    
                    Debug.Log("Fixed ExitToLevel03 Position to: " + exitObj.transform.position);
                }
            }
            EditorSceneManager.SaveScene(scene);
        }
        else
        {
            Debug.LogError("ExitToLevel03 not found!");
        }
    }
}