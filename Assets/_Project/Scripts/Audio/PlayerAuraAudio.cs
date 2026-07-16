using UnityEngine;
using TinyDragon.UI;

namespace TinyDragon.Audio
{
    [DisallowMultipleComponent]
    public sealed class PlayerAuraAudio : MonoBehaviour
    {
        private const string AuraClipPath = "res/sound/aura";
        private const string AuraAudioSourceName = "AuraAudioSource";

        [SerializeField] private PlayerAttack playerAttack;
        [SerializeField] private AudioClip auraClip;
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;

        private void Reset()
        {
            ResolveReferences(true);
            ResolveClip();
        }

        private void OnValidate()
        {
            ResolveReferences(false);
            ResolveClip();
        }

        private void Awake()
        {
            ResolveReferences(true);
            ResolveClip();
            ApplySourceDefaults();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (playerAttack != null)
            {
                playerAttack.AuraStateChanged += HandleAuraStateChanged;
                HandleAuraStateChanged(playerAttack.IsAuraActive);
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (playerAttack != null)
            {
                playerAttack.AuraStateChanged -= HandleAuraStateChanged;
            }

            StopAura();
        }

        private void HandleAuraStateChanged(bool isActive)
        {
            if (isActive)
            {
                PlayAura();
            }
            else
            {
                StopAura();
            }
        }

        private void PlayAura()
        {
            if (audioSource == null || auraClip == null)
            {
                return;
            }

            if (audioSource.clip != auraClip)
            {
                audioSource.clip = auraClip;
            }

            RefreshVolumeFromSettings();

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        public void RefreshVolumeFromSettings()
        {
            if (audioSource != null)
            {
                audioSource.volume = volume * SettingsManager.GlobalSFXVolume;
            }
        }

        private void StopAura()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private void ResolveReferences(bool createAudioSource)
        {
            if (playerAttack == null)
            {
                playerAttack = GetComponent<PlayerAttack>();
            }

            if (audioSource == null)
            {
                Transform existingSource = transform.Find(AuraAudioSourceName);
                if (existingSource != null)
                {
                    audioSource = existingSource.GetComponent<AudioSource>();
                }
            }

            if (audioSource == null && createAudioSource)
            {
                GameObject sourceObject = new GameObject(AuraAudioSourceName);
                sourceObject.transform.SetParent(transform, false);
                audioSource = sourceObject.AddComponent<AudioSource>();
            }
        }

        private void ResolveClip()
        {
            if (auraClip == null)
            {
                auraClip = Resources.Load<AudioClip>(AuraClipPath);
            }
        }

        private void ApplySourceDefaults()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = spatialBlend;
            audioSource.ignoreListenerPause = true;
            audioSource.clip = auraClip;
        }
    }
}
