using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class StartFromSpecificScene
{
    static StartFromSpecificScene()
    {
        // Tên scene bạn muốn luôn bắt đầu
        string sceneName = "Intro";

        // Tìm scene trong project
        string[] guids = AssetDatabase.FindAssets("t:Scene " + sceneName);

        if (guids.Length > 0)
        {
            // Lấy đường dẫn của scene đầu tiên tìm thấy
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            // Ép Unity luôn chạy scene này khi bấm Play
            EditorSceneManager.playModeStartScene = sceneAsset;
        }
        else
        {
            Debug.LogWarning("Không tìm thấy scene tên là: " + sceneName);
        }
    }
}
