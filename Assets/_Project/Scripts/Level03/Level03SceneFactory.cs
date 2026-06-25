using UnityEngine;

internal static class Level03SceneFactory
{
    public static Transform CreateRoot(string rootName, Transform parent)
    {
        GameObject root = new GameObject(rootName);
        root.transform.SetParent(parent, false);
        return root.transform;
    }
}
