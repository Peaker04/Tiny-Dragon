using TinyDragon.Config;
using TinyDragon.Shared.Unity;
using UnityEngine;

public static class BackgroundMusicBootstrap
{
    private const string PlayerName = "BackgroundMusicPlayer";
    private const float Volume = 0.65f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PlayBackgroundMusic()
    {
        if (ObjectLookup.SceneObject(PlayerName) != null)
        {
            return;
        }

        string clipPath = TinyDragonRuntimeConfigProvider.Resolve().Resources.backgroundMusicClipPath;
        AudioClip clip = ResourceLoader.Load<AudioClip>(clipPath);
        if (clip == null)
        {
            Debug.LogWarning($"Background music clip not found at Resources/{clipPath}.");
            return;
        }

        GameObject player = new GameObject(PlayerName);
        Object.DontDestroyOnLoad(player);

        AudioSource source = player.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = Volume;
        source.spatialBlend = 0f;
        source.Play();
    }
}
