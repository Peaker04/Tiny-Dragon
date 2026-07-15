using TinyDragon.UI;
using UnityEngine;

namespace TinyDragon.Audio
{
    public static class UiSoundPlayer
    {
        private const string PlayerName = "UiSoundPlayer";
        private const string ClickClipPath = "Audio/UI/click";
        private const string InventoryClipPath = "Audio/UI/inventoryClick";

        private static AudioSource audioSource;
        private static AudioClip clickClip;
        private static AudioClip inventoryClip;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureReady()
        {
            EnsureAudioSource();
        }

        public static void PlayClick()
        {
            Play(GetClickClip());
        }

        public static void PlayInventoryOpen()
        {
            Play(GetInventoryClip());
        }

        private static AudioClip GetClickClip()
        {
            if (clickClip == null)
            {
                clickClip = Resources.Load<AudioClip>(ClickClipPath);
            }

            return clickClip;
        }

        private static AudioClip GetInventoryClip()
        {
            if (inventoryClip == null)
            {
                inventoryClip = Resources.Load<AudioClip>(InventoryClipPath);
            }

            return inventoryClip;
        }

        private static void Play(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            EnsureAudioSource();
            if (audioSource == null)
            {
                return;
            }

            audioSource.PlayOneShot(clip, SettingsManager.GlobalSFXVolume);
        }

        private static void EnsureAudioSource()
        {
            if (audioSource != null)
            {
                return;
            }

            GameObject existing = GameObject.Find(PlayerName);
            if (existing != null)
            {
                audioSource = existing.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    return;
                }
            }

            GameObject player = new GameObject(PlayerName);
            Object.DontDestroyOnLoad(player);

            audioSource = player.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
        }
    }
}
