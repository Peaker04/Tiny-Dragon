using UnityEngine;

namespace TinyDragon.Combat.Projectiles
{
    [System.Serializable]
    public class ProjectileVisualProfile
    {
        public Sprite sprite;
        public Sprite[] animationSprites;
        public float animationFrameRate = 12f;
        public float scale = 0.35f;
        public Vector2 spawnOffset;
        public bool facesRightByDefault = true;
    }
}
