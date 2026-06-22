using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Text;

public class Level03Dumper
{
    [MenuItem("Tools/Dump Level 03 Hierarchy")]
    public static void Dump()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Level_03")
        {
            Debug.LogError("Please open Level_03 scene first.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (GameObject go in rootObjects)
        {
            DumpObject(go, "", sb);
        }

        File.WriteAllText("Level03Hierarchy.txt", sb.ToString());
        Debug.Log("Dumped to Level03Hierarchy.txt");
    }

    private static void DumpObject(GameObject go, string indent, StringBuilder sb)
    {
        sb.AppendLine($"{indent}- {go.name} (pos: {go.transform.position})");
        var anim = go.GetComponent<Animator>();
        if (anim != null) sb.AppendLine($"{indent}    [Animator: {anim.runtimeAnimatorController?.name}]");

        foreach (Transform child in go.transform)
        {
            DumpObject(child.gameObject, indent + "  ", sb);
        }
    }
}
