using UnityEngine;

namespace TinyDragon.Shared.Animation
{
    public interface IProjectileEffectSource
    {
        bool TryGetProjectileEffect(out Sprite[] sprites, out float frameRate);
    }
}
