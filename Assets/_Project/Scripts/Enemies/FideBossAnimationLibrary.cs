using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Tiny-Dragon/Fide Boss Animation Library", fileName = "FideBossSkillAnimationLibrary")]
public sealed class FideBossAnimationLibrary : ScriptableObject
{
    [SerializeField] private AnimationClip[] clips = Array.Empty<AnimationClip>();

    public AnimationClip GetClip(int index)
    {
        return index >= 0 && index < clips.Length ? clips[index] : null;
    }

    public void SetClips(AnimationClip[] newClips)
    {
        clips = newClips ?? Array.Empty<AnimationClip>();
    }
}
