using UnityEngine;

namespace TinyDragon.Shared.Gameplay
{
    public static class SpawnGeometry2D
    {
        public static void GetSpawnXBounds(
            Bounds groundBounds,
            UnityEngine.Camera spawnCamera,
            float spawnXPadding,
            out float minX,
            out float maxX)
        {
            minX = groundBounds.min.x + spawnXPadding;
            maxX = groundBounds.max.x - spawnXPadding;

            if (spawnCamera != null)
            {
                Bounds cameraBounds = GetCameraBounds(spawnCamera, groundBounds);
                minX = Mathf.Max(minX, cameraBounds.min.x + spawnXPadding);
                maxX = Mathf.Min(maxX, cameraBounds.max.x - spawnXPadding);
            }

            if (minX <= maxX)
            {
                return;
            }

            minX = groundBounds.min.x;
            maxX = groundBounds.max.x;
        }

        public static Bounds GetCameraBounds(UnityEngine.Camera camera, Bounds groundBounds)
        {
            if (camera.orthographic)
            {
                float height = camera.orthographicSize * 2f;
                float width = height * camera.aspect;
                return new Bounds(camera.transform.position, new Vector3(width, height, 0f));
            }

            float depth = Mathf.Abs(camera.transform.position.z - groundBounds.center.z);
            Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
            Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
            Bounds bounds = new Bounds();
            bounds.SetMinMax(bottomLeft, topRight);
            return bounds;
        }
    }
}
