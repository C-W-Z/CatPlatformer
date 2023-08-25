using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Transform tf;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController player;
    [SerializeField] private float resetDelayTime = 0.1f;
    private bool startJumpAnimation = false;
    private bool startLedgeGrabAnimation = false;
    [SerializeField] private Vector2 normalPos = new(0, 0);
    [SerializeField] private Vector2 wallGrabPos = new(0, -0.07f);
    [SerializeField] private Vector2 wallClimbUpPos = new(0, -0.07f);
    [SerializeField] private Vector2 wallClimbDownPos = new(0, -0.3f);

    public void FlipY(bool f)
    {
        sr.flipY = f;
    }
    public void SetAnimation()
    {
        animator.SetBool("onGround", player.onGround);
        animator.SetFloat("xSpeed", Mathf.Abs(player.RB.velocity.x));
        animator.SetFloat("yVeloc", player.RB.velocity.y);
        animator.SetBool("startJump", startJumpAnimation);
        animator.SetBool("startLedgeGrab", startLedgeGrabAnimation);
        animator.SetBool("ledgeClimbing", player.ledgeClimbing);
        animator.SetBool("wallGrabbing", player.isWallGrabbing);
        animator.SetBool("wallClimbing", player.isWallClimbing);
        animator.SetBool("wallJumping", player.isWallJumping);
        animator.SetBool("onWall", player.onWall);

        if (startJumpAnimation)
            Invoke(nameof(ResetJumpAnimation), resetDelayTime);
        if (startLedgeGrabAnimation)
            Invoke(nameof(ResetLedgeGrabAnimation), resetDelayTime);

        if (player.isWallClimbing && !sr.flipY)
            tf.localPosition = wallClimbUpPos;
        else if (player.isWallClimbing && sr.flipY)
            tf.localPosition = wallClimbDownPos;
        else if (player.isWallGrabbing)
            tf.localPosition = wallGrabPos;
        else
            tf.localPosition = normalPos;
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
