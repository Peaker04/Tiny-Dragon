using UnityEngine;

namespace TinyDragon.Shared.Unity
{
    public static class ResourceLoader
    {
        public static T Load<T>(string path) where T : Object
        {
            return string.IsNullOrWhiteSpace(path) ? null : Resources.Load<T>(path);
        }

        public static Sprite LoadSprite(string path, string spriteName = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(spriteName))
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(path);
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] != null && sprites[i].name == spriteName)
                    {
                        return sprites[i];
                    }
                }
            }

            return Resources.Load<Sprite>(path);
        }
    }
}
