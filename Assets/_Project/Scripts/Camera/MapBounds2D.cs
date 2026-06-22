using UnityEngine;

namespace TinyDragon.Camera
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class MapBounds2D : MonoBehaviour
    {
        private BoxCollider2D boxCollider;

        private void Awake()
        {
            boxCollider = GetComponent<BoxCollider2D>();
            boxCollider.isTrigger = true; // Ensure it doesn't block player if used incorrectly
        }

        public Bounds GetBounds()
        {
            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider2D>();
            }
            return boxCollider.bounds;
        }

        private void OnDrawGizmos()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider2D>();
            if (boxCollider != null)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
                Gizmos.DrawCube(boxCollider.bounds.center, boxCollider.bounds.size);
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(boxCollider.bounds.center, boxCollider.bounds.size);
            }
        }
    }
}
