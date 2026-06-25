using UnityEngine;
using TinyDragon.Camera;
using TinyDragon.Shared.Unity;

public sealed class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.75f, -10f);
    [SerializeField, Min(0.01f)] private float smoothTime = 0.12f;

    private Vector3 followVelocity;
    private UnityEngine.Camera sceneCamera;
    private MapBounds2D mapBounds;
    private Collider2D targetCollider;

    private void Awake()
    {
        sceneCamera = GetComponent<UnityEngine.Camera>();
    }

    private void OnEnable()
    {
        ResolveTarget();
        ResolveMapBounds();
        SnapToTarget();
    }

    public void SnapToTarget()
    {
        if (target != null)
        {
            Vector3 targetPos = GetTargetPosition();
            transform.position = ClampToMapBounds(targetPos + offset);
            followVelocity = Vector3.zero;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            targetCollider = target.GetComponent<Collider2D>();
        }
    }

    public void ClearTarget()
    {
        target = null;
        targetCollider = null;
        ResolveTarget();
    }

    private void LateUpdate()
    {
        ResolveTarget();
        ResolveMapBounds();

        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = GetTargetPosition() + offset;
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
        
        if (target != null && targetCollider == null)
        {
            targetCollider = target.GetComponent<Collider2D>();
        }
    }

    private void ResolveMapBounds()
    {
        if (mapBounds == null)
        {
            mapBounds = ObjectLookup.Any<MapBounds2D>();
        }
    }

    private Vector3 GetTargetPosition()
    {
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }
        return target.position;
    }

    private Vector3 ClampToMapBounds(Vector3 desiredPosition)
    {
        if (sceneCamera == null || !sceneCamera.orthographic || mapBounds == null)
        {
            return desiredPosition;
        }

        Bounds bounds = mapBounds.GetBounds();
        
        float halfCameraHeight = sceneCamera.orthographicSize;
        float halfCameraWidth = halfCameraHeight * sceneCamera.aspect;

        float minimumX = bounds.min.x + halfCameraWidth;
        float maximumX = bounds.max.x - halfCameraWidth;
        
        float minimumY = bounds.min.y + halfCameraHeight;
        float maximumY = bounds.max.y - halfCameraHeight;

        desiredPosition.x = minimumX <= maximumX
            ? Mathf.Clamp(desiredPosition.x, minimumX, maximumX)
            : bounds.center.x;

        desiredPosition.y = minimumY <= maximumY
            ? Mathf.Clamp(desiredPosition.y, minimumY, maximumY)
            : bounds.center.y;

        return desiredPosition;
    }
}
