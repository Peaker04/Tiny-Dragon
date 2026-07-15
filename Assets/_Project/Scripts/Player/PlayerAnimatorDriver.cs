using UnityEngine;

public class PlayerAnimatorDriver : MonoBehaviour
{
    [SerializeField] private PlayerInputReader inputReader;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Animator animator;

    private bool hasIsGroundedParameter;
    private bool hasAttackParameter;
    private bool hasPowerAttackParameter;
    private bool hasPunch1Parameter;
    private bool hasPunch2Parameter;
    private bool hasKick1Parameter;
    private bool hasKick2Parameter;

    private void Awake()
    {
        if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (animator == null) animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        hasIsGroundedParameter = HasAnimatorParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        hasAttackParameter = HasAnimatorParameter("Attack", AnimatorControllerParameterType.Trigger);
        hasPowerAttackParameter = HasAnimatorParameter("PowerAttack", AnimatorControllerParameterType.Trigger);
        hasPunch1Parameter = HasAnimatorParameter("Punch1", AnimatorControllerParameterType.Trigger);
        hasPunch2Parameter = HasAnimatorParameter("Punch2", AnimatorControllerParameterType.Trigger);
        hasKick1Parameter = HasAnimatorParameter("Kick1", AnimatorControllerParameterType.Trigger);
        hasKick2Parameter = HasAnimatorParameter("Kick2", AnimatorControllerParameterType.Trigger);
    }

    private void Update()
    {
        if (animator == null) return;

        float speed = inputReader != null ? Mathf.Abs(inputReader.Horizontal) : 0f;
        animator.SetFloat("Speed", speed);

        if (movement != null && hasIsGroundedParameter)
        {
            animator.SetBool("IsGrounded", movement.IsGrounded);
        }
    }

    public void TriggerAttack()
    {
        if (animator != null && hasAttackParameter) animator.SetTrigger("Attack");
    }

    public void TriggerPunch(int comboStep)
    {
        if (animator == null) return;
        if (comboStep == 1 && hasPunch1Parameter) animator.SetTrigger("Punch1");
        else if (comboStep == 2 && hasPunch2Parameter) animator.SetTrigger("Punch2");
        else TriggerAttack();
    }

    public void TriggerKick(int comboStep)
    {
        if (animator == null) return;
        if (comboStep == 1 && hasKick1Parameter) animator.SetTrigger("Kick1");
        else if (comboStep == 2 && hasKick2Parameter) animator.SetTrigger("Kick2");
        else TriggerAttack();
    }

    public bool TriggerPowerAttackOrFallback()
    {
        if (animator == null) return false;

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
        if (animator == null) return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == parameterType) return true;
        }

        return false;
    }
}
