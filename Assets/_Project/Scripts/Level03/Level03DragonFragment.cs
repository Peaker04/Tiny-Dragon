using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Level03DragonFragment : MonoBehaviour
{
    private Level03Manager manager;
    private SpriteRenderer spriteRenderer;
    private Collider2D triggerCollider;
    private Coroutine moveRoutine;

    public int FragmentIndex { get; private set; }
    public bool IsCollected { get; private set; }
    public SpriteRenderer Renderer => spriteRenderer;

    public void Initialize(
        Level03Manager encounterManager,
        int index,
        Sprite sprite,
        Vector3 startPosition,
        Material spriteMaterial)
    {
        manager = encounterManager;
        FragmentIndex = index;
        IsCollected = false;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = sprite;
        spriteRenderer.sharedMaterial = spriteMaterial;
        spriteRenderer.sortingOrder = 20;

        CircleCollider2D circle = GetComponent<CircleCollider2D>();
        if (circle == null)
        {
            circle = gameObject.AddComponent<CircleCollider2D>();
        }

        circle.isTrigger = true;
        if (sprite != null)
        {
            Bounds bounds = sprite.bounds;
            circle.offset = bounds.center;
            circle.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.y) * 0.4f, 0.25f, 0.5f);
        }
        triggerCollider = circle;
        triggerCollider.enabled = true;

        transform.position = startPosition;
    }

    public void MoveTo(Vector3 destination, float duration)
    {
        if (IsCollected)
        {
            return;
        }

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveToDestination(destination, Mathf.Max(duration, 0.01f)));
    }

    public void PrepareForMerge()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }
    }

    public void Hide()
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider2D other)
    {
        if (IsCollected || manager == null || !manager.CanCollectFragments)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        IsCollected = true;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        Hide();
        manager.CollectFragment(this);
    }

    private IEnumerator MoveToDestination(Vector3 destination, float duration)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.LerpUnclamped(start, destination, t);
            yield return null;
        }

        transform.position = destination;
        moveRoutine = null;
    }
}
