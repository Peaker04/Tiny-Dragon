using UnityEngine;

namespace TinyDragon.Shared.Unity
{
    public static class ObjectLookup
    {
        public static T Any<T>() where T : Object
        {
            return Object.FindAnyObjectByType<T>();
        }

        public static T InactiveAny<T>() where T : Object
        {
            return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        public static T[] AllIncludingInactive<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        }

        public static GameObject SceneObject(string objectName)
        {
            return string.IsNullOrWhiteSpace(objectName) ? null : GameObject.Find(objectName);
        }
    }
}
