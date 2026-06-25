using UnityEngine;

namespace TinyDragon.Combat
{
    /// <summary>
    /// Groups all parameters needed to configure a projectile, replacing
    /// long parameter lists in ProjectileShooter and PlayerProjectile.
    /// </summary>
    [System.Serializable]
    public struct ProjectileSpec
    {
        public int damage;
        public float speed;
        public float lifetime;
        public float scale;
        public Vector2 spawnOffset;
        public Sprite sprite;
        public bool facesRightByDefault;
        public string sortingLayerName;
        public int sortingOrder;
    }
}
