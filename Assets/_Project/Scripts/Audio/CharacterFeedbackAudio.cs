using TinyDragon.UI;
using UnityEngine;

namespace TinyDragon.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(PlayerActionAudioEmitter))]
    public sealed class CharacterFeedbackAudio : MonoBehaviour
    {
        private const string ImpactClipPath = "Audio/DragonBallAction/punch_low";
        private const string DeathClipPath = "Audio/DragonBallAction/power_shot";
        private const string LandingClipPath = "Audio/DragonBallAction/kick_low";

        [Header("Wiring")]
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private EnemyHealth enemyHealth;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerActionAudioEmitter emitter;

        [Header("Clip Overrides")]
        [SerializeField] private AudioClip impactClip;
        [SerializeField] private AudioClip deathClip;
        [SerializeField] private AudioClip landingClip;

        private AudioClip resolvedImpactClip;
        private AudioClip resolvedDeathClip;
        private AudioClip resolvedLandingClip;

        private void Reset()
        {
            ResolveReferences();
            ResolveClips();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ResolveClips();
        }

        private void Awake()
        {
            ResolveReferences();
            ResolveClips();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Subscribe();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Unsubscribe();
        }

        private void Subscribe()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged += OnPlayerDamaged;
                playerHealth.Died += OnPlayerDied;
            }

            if (enemyHealth != null)
            {
                enemyHealth.Damaged += OnEnemyDamaged;
                enemyHealth.Died += OnEnemyDied;
            }

            if (playerMovement != null)
            {
                playerMovement.Landed += PlayLanding;
            }
        }

        private void Unsubscribe()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerDamaged;
                playerHealth.Died -= OnPlayerDied;
            }

            if (enemyHealth != null)
            {
                enemyHealth.Damaged -= OnEnemyDamaged;
                enemyHealth.Died -= OnEnemyDied;
            }

            if (playerMovement != null)
            {
                playerMovement.Landed -= PlayLanding;
            }
        }

        private void OnPlayerDamaged(int _damage, bool isLethal)
        {
            if (isLethal)
            {
                return;
            }

            PlayLocal(resolvedImpactClip);
        }

        private void OnEnemyDamaged(int _damage, bool isLethal)
        {
            if (isLethal)
            {
                return;
            }

            PlayLocal(resolvedImpactClip);
        }

        private void OnPlayerDied(PlayerHealth _)
        {
            PlayDetached(resolvedDeathClip);
        }

        private void OnEnemyDied(EnemyHealth _)
        {
            PlayDetached(resolvedDeathClip);
        }

        private void PlayLanding()
        {
            PlayLocal(resolvedLandingClip);
        }

        private void ResolveReferences()
        {
            if (playerHealth == null)
            {
                playerHealth = GetComponent<PlayerHealth>();
            }

            if (enemyHealth == null)
            {
                enemyHealth = GetComponent<EnemyHealth>();
            }

            if (playerMovement == null)
            {
                playerMovement = GetComponent<PlayerMovement>();
            }

            if (emitter == null)
            {
                emitter = GetComponent<PlayerActionAudioEmitter>();
            }

            if (emitter == null)
            {
                emitter = GetComponentInChildren<PlayerActionAudioEmitter>(true);
            }
        }

        private void ResolveClips()
        {
            resolvedImpactClip = impactClip != null ? impactClip : Resources.Load<AudioClip>(ImpactClipPath);
            resolvedDeathClip = deathClip != null ? deathClip : Resources.Load<AudioClip>(DeathClipPath);
            resolvedLandingClip = landingClip != null ? landingClip : Resources.Load<AudioClip>(LandingClipPath);
        }

        private void PlayLocal(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            if (emitter == null)
            {
                emitter = GetComponent<PlayerActionAudioEmitter>();

                if (emitter == null)
                {
                    emitter = GetComponentInChildren<PlayerActionAudioEmitter>(true);
                }
            }

            if (emitter != null)
            {
                emitter.Play(clip, SettingsManager.GlobalSFXVolume);
            }
        }

        private void PlayDetached(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            AudioSource.PlayClipAtPoint(clip, transform.position, SettingsManager.GlobalSFXVolume);
        }
    }
}
