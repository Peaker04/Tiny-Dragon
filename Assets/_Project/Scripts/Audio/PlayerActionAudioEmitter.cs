using UnityEngine;

namespace TinyDragon.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerActionAudioEmitter : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 0f;
        [SerializeField] private bool ignoreListenerPause = true;

        private void Awake()
        {
            CacheSource();
            ApplySourceDefaults();
        }

        private void OnValidate()
        {
            CacheSource();
            ApplySourceDefaults();
        }

        public void Play(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                return;
            }

            CacheSource();
            if (audioSource == null)
            {
                return;
            }

            audioSource.PlayOneShot(clip, volumeScale);
        }

        private void CacheSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void ApplySourceDefaults()
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = spatialBlend;
            audioSource.ignoreListenerPause = ignoreListenerPause;
        }
    }
}
