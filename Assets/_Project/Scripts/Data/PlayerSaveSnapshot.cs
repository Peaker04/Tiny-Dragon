using UnityEngine;

namespace TinyDragon.Data
{
    public readonly struct PlayerSaveSnapshot
    {
        public PlayerSaveSnapshot(
            string sceneName,
            Vector3 position,
            int facingDirection,
            int currentHealth,
            int maxHealth
        )
        {
            SceneName = sceneName;
            Position = position;
            FacingDirection = facingDirection >= 0 ? 1 : -1;
            CurrentHealth = Mathf.Max(currentHealth, 0);
            MaxHealth = Mathf.Max(maxHealth, 1);
        }

        public string SceneName { get; }
        public Vector3 Position { get; }
        public int FacingDirection { get; }
        public int CurrentHealth { get; }
        public int MaxHealth { get; }
    }
}
