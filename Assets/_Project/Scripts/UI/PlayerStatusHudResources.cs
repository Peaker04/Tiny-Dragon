using UnityEngine;
using TinyDragon.Shared.Unity;

namespace TinyDragon.UI
{
    internal static class PlayerStatusHudResources
    {
        public static Sprite LoadSprite(string path, string spriteName)
        {
            return ResourceLoader.LoadSprite(path, spriteName);
        }
    }
}
