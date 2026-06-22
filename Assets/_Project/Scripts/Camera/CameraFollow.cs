using UnityEngine;

public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Transform leftBoundary;
    [SerializeField] private Transform rightBoundary;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
    [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;

    private Vector3 followVelocity;
    private Camera sceneCamera;
    private Collider2D terrainBounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AttachToMainCameras()
    {
        foreach (Camera camera in Object.FindObjectsByType<Camera>())
        {
            if (!camera.CompareTag("MainCamera") || camera.GetComponent<CameraFollow>() != null)
            {
                continue;
            }

            camera.gameObject.AddComponent<CameraFollow>();
        }
    }

    private void Awake()
    {
        sceneCamera = GetComponent<Camera>();
    }

    private void OnEnable()
    {
        ResolveTarget();

        if (target != null)
        {
            transform.position = ClampToMapBounds(target.position + offset);
        }
    }

    private void LateUpdate()
    {
        ResolveTarget();

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        desiredPosition = ClampToMapBounds(desiredPosition);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref followVelocity,
            smoothTime
        );
    }

    private void ResolveTarget()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            target = player != null ? player.transform : null;
        }

    }

    private Vector3 ClampToMapBounds(Vector3 desiredPosition)
    {
        if (sceneCamera == null)
        {
            sceneCamera = GetComponent<Camera>();
        }

        if (sceneCamera == null || !sceneCamera.orthographic)
        {
            return desiredPosition;
        }

        ResolveTerrainBounds();

        float minimumMapX;
        float maximumMapX;
        if (leftBoundary != null && rightBoundary != null)
        {
            minimumMapX = leftBoundary.position.x;
            maximumMapX = rightBoundary.position.x;
        }
        else if (terrainBounds != null)
        {
            minimumMapX = terrainBounds.bounds.min.x;
            maximumMapX = terrainBounds.bounds.max.x;
        }
        else
        {
            return desiredPosition;
        }

        float halfCameraWidth = sceneCamera.orthographicSize * sceneCamera.aspect;
        float minimumX = minimumMapX + halfCameraWidth;
        float maximumX = maximumMapX - halfCameraWidth;

        desiredPosition.x = minimumX <= maximumX
            ? Mathf.Clamp(desiredPosition.x, minimumX, maximumX)
            : (minimumMapX + maximumMapX) * 0.5f;

        return desiredPosition;
    }

    private void ResolveTerrainBounds()
    {
        if (terrainBounds != null || (leftBoundary != null && rightBoundary != null))
        {
            return;
        }

        foreach (Collider2D collider in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude))
        {
            if (collider.name.StartsWith("LeftWall"))
            {
                if (leftBoundary == null || collider.bounds.min.x < leftBoundary.position.x)
                {
                    leftBoundary = collider.transform;
                }
            }
            else if (collider.name.StartsWith("RightWall"))
            {
                if (rightBoundary == null || collider.bounds.max.x > rightBoundary.position.x)
                {
                    rightBoundary = collider.transform;
                }
            }
        }

        if (leftBoundary != null && rightBoundary != null)
        {
            return;
        }

        GameObject terrain = GameObject.Find("Terrain_Collision");
        terrain ??= GameObject.Find("Ground_Main_Collider");
        terrain ??= GameObject.Find("Ground_Collider");
        if (terrain != null)
        {
            terrainBounds = terrain.GetComponent<Collider2D>();
        }
    }
}
