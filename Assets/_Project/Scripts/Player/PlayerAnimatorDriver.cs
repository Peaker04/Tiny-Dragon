using UnityEngine;

public class PlayerAnimatorDriver : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Animator animator;

    private bool hasIsGroundedParameter;
    private bool hasAttackParameter;
    private bool hasPowerAttackParameter;

    private void Awake()
    {
        if (inputReader == null)
        {
            inputReader = GetComponent<PlayerInputReader>();
        }

        if (movement == null)
        {
            movement = GetComponent<PlayerMovement>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        hasIsGroundedParameter = HasAnimatorParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        hasAttackParameter = HasAnimatorParameter("Attack", AnimatorControllerParameterType.Trigger);
        hasPowerAttackParameter = HasAnimatorParameter("PowerAttack", AnimatorControllerParameterType.Trigger);
    }

    private void Update()
    {
        if (animator == null)
        {
            return;
        }

        float speed = inputReader != null ? Mathf.Abs(inputReader.Horizontal) : 0f;
        animator.SetFloat("Speed", speed);

        if (movement != null && hasIsGroundedParameter)
        {
            animator.SetBool("IsGrounded", movement.IsGrounded);
        }
    }

    public void TriggerAttack()
    {
        if (animator != null && hasAttackParameter)
        {
            animator.SetTrigger("Attack");
        }
    }

    public bool TriggerPowerAttackOrFallback()
    {
        if (animator == null)
        {
            return false;
        }

        if (hasPowerAttackParameter)
        {
            animator.SetTrigger("PowerAttack");
            return false;
        }

        if (hasAttackParameter)
        {
            animator.SetTrigger("Attack");
            return true;
        }

        return false;
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType)
            {
                return true;
            }
        }

        return false;
    }
}
