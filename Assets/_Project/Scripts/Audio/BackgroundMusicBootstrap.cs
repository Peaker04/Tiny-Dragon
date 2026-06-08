using UnityEngine;

public static class BackgroundMusicBootstrap
{
    private const string PlayerName = "BackgroundMusicPlayer";
    private const string ClipPath = "Music/XenoverseTrack16Loop";
    private const float Volume = 0.65f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PlayBackgroundMusic()
    {
        if (GameObject.Find(PlayerName) != null)
        {
            return;
        }

        AudioClip clip = Resources.Load<AudioClip>(ClipPath);
        if (clip == null)
        {
            Debug.LogWarning($"Background music clip not found at Resources/{ClipPath}.");
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
