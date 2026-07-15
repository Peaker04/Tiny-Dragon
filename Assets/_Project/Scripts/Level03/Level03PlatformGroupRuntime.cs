using System.Collections.Generic;
using UnityEngine;

internal sealed class Level03PlatformGroupRuntime
{
    public readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    public readonly List<Collider2D> colliders = new List<Collider2D>();

    public static void SetState(Level03PlatformGroupRuntime group, float alpha, bool collidersEnabled)
    {
        SetRendererAlpha(group, alpha);
        SetColliderState(group, collidersEnabled);
    }

    public static void SetRendererAlpha(Level03PlatformGroupRuntime group, float alpha)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.renderers.Count; i++)
        {
            SpriteRenderer renderer = group.renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }
    }

    public static void SetColliderState(Level03PlatformGroupRuntime group, bool enabledState)
    {
        if (group == null)
        {
            return;
        }

        for (int i = 0; i < group.colliders.Count; i++)
        {
            if (group.colliders[i] != null)
            {
                group.colliders[i].enabled = enabledState;
            }
        }
    }
}
