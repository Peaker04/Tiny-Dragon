using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FideBossSkillAnimationOrganizer
{
    private const string Root = "Assets/_Project/Animations/Enemies/FideBoss_Ac3";
    private const string SourceRoot = "Assets/_Project/Resources/Enemies/Fide/FideDaiCa3";
    private const string LibraryPath = "Assets/_Project/Resources/FideBossSkillAnimationLibrary.asset";
    private const float FrameDuration = .05f;

    private sealed class Definition
    {
        public readonly string Folder;
        public readonly int[] Frames;

        public Definition(string folder, params int[] frames)
        {
            Folder = folder;
            Frames = frames;
        }
    }

    private static readonly Definition[] Definitions =
    {
        new Definition("basic_punch", 13, 13, 14, 14, 13, 14, 0),
        new Definition("dragon_rush", 2, 3, 4, 9, 10, 11, 12, 0),
        new Definition("antomic_flurry", 13, 14, 15, 16, 17, 13, 14, 15, 16, 17, 0),
        new Definition("masenko", 18, 19, 20, 21, 22, 21, 31, 32, 0),
        new Definition("galick_cannon", 18, 19, 20, 21, 22, 22, 30, 31, 32, 0),
        new Definition("death_beam", 18, 19, 20, 21, 31, 0),
        new Definition("teleport_punch", 25, 25, 25, 13, 13, 14, 14, 0),
        new Definition("gravity_cage", 18, 19, 20, 21, 22, 30, 31, 0),
        new Definition("meteor_barrage", 18, 19, 20, 21, 22, 26, 27, 28, 29, 30, 0),
        new Definition("planet_breaker", 18, 19, 20, 21, 22, 22, 30, 31, 32, 31, 0),
        new Definition("counter_stance", 15, 16, 17, 16, 17, 13, 14, 0),
        new Definition("aerial_dive", 7, 8, 7, 18, 19, 25, 13, 14, 9, 10, 7, 8),
        new Definition("sky_rush", 7, 8, 18, 19, 25, 13, 14, 9, 25, 13, 14, 10, 7),
        new Definition("vanishing_rush", 7, 8, 18, 19, 25, 13, 14, 9, 25, 13, 14, 10, 25, 13, 14, 11, 7),
        new Definition("death_beam_barrage", 18, 19, 20, 21, 22, 31, 32, 31, 0),
        new Definition("teleport_cross", 25, 13, 14, 25, 13, 14, 25, 13, 14, 9, 0),
        new Definition("emperor_nova", 18, 19, 20, 21, 22, 30, 31, 32, 0),
        new Definition("death_saucer_storm", 18, 19, 20, 21, 22, 26, 27, 28, 29, 30, 31, 0),
        new Definition("solar_bomb", 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21, 21),
        new Definition("kamehameha", Enumerable.Repeat(19, 24).Concat(Enumerable.Repeat(20, 38)).ToArray())
    };

    static FideBossSkillAnimationOrganizer()
    {
        EditorApplication.delayCall += EnsureGenerated;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += EnsureGenerated;
        };
    }

    [MenuItem("Tools/Tiny Dragon/Rebuild Fide Boss Skill Animations")]
    public static void Rebuild()
    {
        Generate(true);
    }

    [MenuItem("Tools/Tiny Dragon/Ensure Fide Boss Skill Animations")]
    public static void EnsureGenerated()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureGenerated;
            return;
        }
        bool missingClip = Definitions.Any(definition =>
            AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Root}/{definition.Folder}/FideBoss_{definition.Folder}.anim") == null);
        if (missingClip || AssetDatabase.LoadAssetAtPath<FideBossAnimationLibrary>(LibraryPath) == null) Generate(false);
    }

    private static void Generate(bool rebuild)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before rebuilding Fide boss animations.");
            return;
        }

        var clips = new List<AnimationClip>(Definitions.Length);
        foreach (Definition definition in Definitions)
        {
            string folderPath = $"{Root}/{definition.Folder}";
            if (rebuild && AssetDatabase.IsValidFolder(folderPath)) AssetDatabase.DeleteAsset(folderPath);
            EnsureFolder(Root, definition.Folder);
            clips.Add(CreateOrLoadClip(definition, folderPath, rebuild));
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        FideBossAnimationLibrary library = AssetDatabase.LoadAssetAtPath<FideBossAnimationLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<FideBossAnimationLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.SetClips(clips.ToArray());
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Fide boss animations ready: {clips.Count} editable skill clips in {Root}.");
    }

    private static AnimationClip CreateOrLoadClip(Definition definition, string folderPath, bool rebuild)
    {
        string clipPath = $"{folderPath}/FideBoss_{definition.Folder}.anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null && !rebuild) return existing;

        var keyframes = new ObjectReferenceKeyframe[definition.Frames.Length];
        for (int i = 0; i < definition.Frames.Length; i++)
        {
            int pose = definition.Frames[i];
            string sourcePath = $"{SourceRoot}/pose_{pose:00}.png";
            string framePath = $"{folderPath}/{definition.Folder}_{i:00}_pose_{pose:00}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(framePath) == null && !AssetDatabase.CopyAsset(sourcePath, framePath))
            {
                Debug.LogError($"Could not copy Fide animation frame: {sourcePath}");
            }
            AssetDatabase.ImportAsset(framePath, ImportAssetOptions.ForceSynchronousImport);
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(framePath).OfType<Sprite>().FirstOrDefault();
            keyframes[i] = new ObjectReferenceKeyframe { time = i * FrameDuration, value = sprite };
        }

        var clip = new AnimationClip { name = $"FideBoss_{definition.Folder}", frameRate = 1f / FrameDuration };
        var binding = new EditorCurveBinding { path = string.Empty, type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AssetDatabase.CreateAsset(clip, clipPath);
        return clip;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }
}
