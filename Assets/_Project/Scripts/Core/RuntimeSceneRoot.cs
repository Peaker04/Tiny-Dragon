using UnityEngine;

public static class RuntimeSceneRoot
{
    private const string RootName = "RuntimeObjects";

    private static Transform root;

    public static Transform GetChild(string childName)
    {
        Transform rootTransform = GetRoot();
        Transform child = rootTransform.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(rootTransform, false);
        return childObject.transform;
    }

    private static Transform GetRoot()
    {
        if (root != null)
        {
            return root;
        }

        GameObject rootObject = GameObject.Find(RootName);
        if (rootObject == null)
        {
            rootObject = new GameObject(RootName);
        }

        root = rootObject.transform;
        return root;
    }
}
