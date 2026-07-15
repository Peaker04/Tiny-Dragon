using TinyDragon.UI;
using UnityEngine;

namespace TinyDragon.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(PlayerActionAudioEmitter))]
    public sealed class PlayerActionAudio : MonoBehaviour
    {
        private const string JumpClipPath = "Audio/DragonBallAction/jump";
        private const string PunchLowClipPath = "Audio/DragonBallAction/punch_low";
        private const string PunchMediumClipPath = "Audio/DragonBallAction/punch_medium";
        private const string KickLowClipPath = "Audio/DragonBallAction/kick_low";
        private const string KickMediumClipPath = "Audio/DragonBallAction/kick_medium";
        private const string ProjectileClipPath = "Audio/DragonBallAction/projectile";
        private const string PowerShotClipPath = "Audio/DragonBallAction/power_shot";

        [Header("Wiring")]
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerAttack playerAttack;
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerActionAudioEmitter emitter;

        [Header("Clip Overrides")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip punchLowClip;
        [SerializeField] private AudioClip punchMediumClip;
        [SerializeField] private AudioClip kickLowClip;
        [SerializeField] private AudioClip kickMediumClip;
        [SerializeField] private AudioClip projectileClip;
        [SerializeField] private AudioClip powerShotClip;

        private AudioClip resolvedJumpClip;
        private AudioClip resolvedPunchLowClip;
        private AudioClip resolvedPunchMediumClip;
        private AudioClip resolvedKickLowClip;
        private AudioClip resolvedKickMediumClip;
        private AudioClip resolvedProjectileClip;
        private AudioClip resolvedPowerShotClip;

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

        public void PlayJump()
        {
            Play(emitter, resolvedJumpClip);
        }

        public void PlayPunch(int comboStep)
        {
            Play(emitter, comboStep == 1 ? resolvedPunchLowClip : resolvedPunchMediumClip);
        }

        public void PlayKick(int comboStep)
        {
            Play(emitter, comboStep == 1 ? resolvedKickLowClip : resolvedKickMediumClip);
        }

        public void PlayProjectile()
        {
            Play(emitter, resolvedProjectileClip);
        }

        public void PlayPowerShot()
        {
            Play(emitter, resolvedPowerShotClip);
        }

        private void Subscribe()
        {
            if (playerMovement != null)
            {
                playerMovement.JumpPerformed += PlayJump;
            }

            if (playerAttack != null)
            {
                playerAttack.AttackExecuted += PlayProjectile;
                playerAttack.PunchExecuted += PlayPunch;
                playerAttack.KickExecuted += PlayKick;
                playerAttack.PowerShotExecuted += OnPowerShotExecuted;
            }

            if (playerController != null)
            {
                playerController.ProjectileShot += PlayProjectile;
            }
        }

        private void Unsubscribe()
        {
            if (playerMovement != null)
            {
                playerMovement.JumpPerformed -= PlayJump;
            }

            if (playerAttack != null)
            {
                playerAttack.AttackExecuted -= PlayProjectile;
                playerAttack.PunchExecuted -= PlayPunch;
                playerAttack.KickExecuted -= PlayKick;
                playerAttack.PowerShotExecuted -= OnPowerShotExecuted;
            }

            if (playerController != null)
            {
                playerController.ProjectileShot -= PlayProjectile;
            }
        }

        private void OnPowerShotExecuted()
        {
            PlayPowerShot();
        }

        private void ResolveReferences()
        {
            if (playerMovement == null)
            {
                playerMovement = GetComponent<PlayerMovement>();
            }

            if (playerAttack == null)
            {
                playerAttack = GetComponent<PlayerAttack>();
            }

            if (playerController == null)
            {
                playerController = GetComponent<PlayerController>();
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
            resolvedJumpClip = jumpClip != null ? jumpClip : Resources.Load<AudioClip>(JumpClipPath);
            resolvedPunchLowClip = punchLowClip != null ? punchLowClip : Resources.Load<AudioClip>(PunchLowClipPath);
            resolvedPunchMediumClip = punchMediumClip != null ? punchMediumClip : Resources.Load<AudioClip>(PunchMediumClipPath);
            resolvedKickLowClip = kickLowClip != null ? kickLowClip : Resources.Load<AudioClip>(KickLowClipPath);
            resolvedKickMediumClip = kickMediumClip != null ? kickMediumClip : Resources.Load<AudioClip>(KickMediumClipPath);
            resolvedProjectileClip = projectileClip != null ? projectileClip : Resources.Load<AudioClip>(ProjectileClipPath);
            resolvedPowerShotClip = powerShotClip != null ? powerShotClip : Resources.Load<AudioClip>(PowerShotClipPath);
        }

        private static void Play(PlayerActionAudioEmitter currentEmitter, AudioClip clip)
        {
            if (currentEmitter == null || clip == null)
            {
                return;
            }

            currentEmitter.Play(clip, SettingsManager.GlobalSFXVolume);
        }
    }
}
