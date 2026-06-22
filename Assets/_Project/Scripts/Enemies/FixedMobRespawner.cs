using System.Collections.Generic;
using UnityEngine;

public sealed class FixedMobRespawner : MonoBehaviour
{
    [SerializeField] private Transform[] mobMarkers;
    [SerializeField, Min(0f)] private float respawnDelay = 3f;

    private readonly List<MobSlot> slots = new();

    private void Awake()
    {
        foreach (Transform marker in mobMarkers)
        {
            if (marker == null || !marker.TryGetComponent(out EnemyPatrol template))
            {
                continue;
            }

            slots.Add(new MobSlot(template, marker.position, marker.rotation));
            marker.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        foreach (MobSlot slot in slots)
        {
            Spawn(slot);
        }
    }

    private void Update()
    {
        foreach (MobSlot slot in slots)
        {
            if (slot.ActiveEnemy != null)
            {
                continue;
            }

            if (!slot.WaitingForRespawn)
            {
                slot.RespawnAt = Time.time + respawnDelay;
                slot.WaitingForRespawn = true;
                continue;
            }

            if (Time.time >= slot.RespawnAt)
            {
                Spawn(slot);
            }
        }
    }

    private static void Spawn(MobSlot slot)
    {
        slot.ActiveEnemy = Instantiate(slot.Template, slot.Position, slot.Rotation);
        slot.ActiveEnemy.gameObject.SetActive(true);
        slot.WaitingForRespawn = false;
    }

    private sealed class MobSlot
    {
        public EnemyPatrol Template { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public EnemyPatrol ActiveEnemy { get; set; }
        public bool WaitingForRespawn { get; set; }
        public float RespawnAt { get; set; }

        public MobSlot(EnemyPatrol template, Vector3 position, Quaternion rotation)
        {
            Template = template;
            Position = position;
            Rotation = rotation;
        }
    }
}
