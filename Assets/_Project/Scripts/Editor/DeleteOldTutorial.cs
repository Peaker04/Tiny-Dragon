using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

public class DeleteOldTutorial
{
    [MenuItem("Tools/Delete Old Tutorial")]
    public static void DoDelete()
    {
        Scene langAruScene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/LangAru.unity", OpenSceneMode.Single);
        
        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        GameObject oldTutorialRoot = null;

        foreach (TMP_Text txt in allTexts)
        {
            if (txt.text != null && txt.text.Contains("A & D"))
            {
                // Go up to find the root canvas or the parent that holds Step 1 & Step 2
                Transform curr = txt.transform;
                while (curr.parent != null)
                {
                    if (curr.name.Contains("Tutorial") || curr.name.Contains("Canvas") || curr.GetComponent<Canvas>() != null)
                    {
                        if (curr.GetComponent<TinyDragon.UI.TutorialManager>() == null) // don't delete CongHieu's
                        {
                            oldTutorialRoot = curr.gameObject;
                        }
                    }
                    curr = curr.parent;
                }
                
                // If we didn't find a canvas, just take the highest parent that isn't CongHieu's
                if (oldTutorialRoot == null)
                {
                    curr = txt.transform;
                    while (curr.parent != null) curr = curr.parent;
                    oldTutorialRoot = curr.gameObject;
                }
                break;
            }
        }

        if (oldTutorialRoot != null)
        {
            Debug.Log("Found OLD tutorial root: " + oldTutorialRoot.name + ". Deleting...");
            GameObject.DestroyImmediate(oldTutorialRoot);
            EditorSceneManager.SaveScene(langAruScene);
            Debug.Log("Deleted successfully!");
        }
        else
        {
            Debug.Log("Could not find the old tutorial text!");
        }
    }
}