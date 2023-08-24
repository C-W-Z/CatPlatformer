using UnityEngine;

public class AnimateController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController player;
    [SerializeField] private float resetDelayTime = 0.1f;
    private bool startJumpAnimation = false;
    private bool startLedgeGrabAnimation = false;

    public void SetAnimation()
    {
        animator.SetBool("onGround", player.onGround);
        animator.SetFloat("xSpeed", Mathf.Abs(player.RB.velocity.x));
        animator.SetFloat("yVeloc", player.RB.velocity.y);
        animator.SetBool("startJump", startJumpAnimation);
        animator.SetBool("startLedgeGrab", startLedgeGrabAnimation);
        animator.SetBool("ledgeClimbing", player.ledgeClimbing);

        if (startJumpAnimation)
            Invoke(nameof(ResetJumpAnimation), resetDelayTime);
        if (startLedgeGrabAnimation)
            Invoke(nameof(ResetLedgeGrabAnimation), resetDelayTime);
    }
    public void SetJumpAnimation()
    {
        startJumpAnimation = true;
    }
    private void ResetJumpAnimation()
    {
        startJumpAnimation = false;
    }
    public void SetLedgeGrabAnimation()
    {
        startLedgeGrabAnimation = true;
    }
    private void ResetLedgeGrabAnimation()
    {
        startLedgeGrabAnimation = false;
    }
    // for animation event
    public void LedgeClimbEnd()
    {
        player.LedgeClimbOver();
    }
}
