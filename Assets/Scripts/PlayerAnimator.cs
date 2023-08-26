using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Transform tf;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController player;
    [SerializeField] private float resetDelayTime = 0.1f;
    private bool _startJumpAnimation = false;
    private bool _startLedgeGrabAnimation = false;
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
        animator.SetBool("onGround", player.OnGround);
        animator.SetFloat("xSpeed", Mathf.Abs(player.RB.velocity.x));
        animator.SetFloat("yVeloc", player.RB.velocity.y);
        animator.SetBool("startJump", _startJumpAnimation);
        animator.SetBool("startLedgeGrab", _startLedgeGrabAnimation);
        animator.SetBool("ledgeClimbing", player.IsLedgeClimbing);
        animator.SetBool("wallGrabbing", player.IsWallGrabbing);
        animator.SetBool("wallClimbing", player.IsWallClimbing);
        animator.SetBool("wallJumping", player.IsWallJumping);
        animator.SetBool("onWall", player.OnWall);

        if (_startJumpAnimation)
            Invoke(nameof(ResetJumpAnimation), resetDelayTime);
        if (_startLedgeGrabAnimation)
            Invoke(nameof(ResetLedgeGrabAnimation), resetDelayTime);

        if (player.IsWallClimbing && !sr.flipY)
            tf.localPosition = wallClimbUpPos;
        else if (player.IsWallClimbing && sr.flipY)
            tf.localPosition = wallClimbDownPos;
        else if (player.IsWallGrabbing)
            tf.localPosition = wallGrabPos;
        else
            tf.localPosition = normalPos;
    }
    public void SetJumpAnimation()
    {
        _startJumpAnimation = true;
    }
    private void ResetJumpAnimation()
    {
        _startJumpAnimation = false;
    }
    public void SetLedgeGrabAnimation()
    {
        _startLedgeGrabAnimation = true;
    }
    private void ResetLedgeGrabAnimation()
    {
        _startLedgeGrabAnimation = false;
    }
    // for animation event
    public void LedgeClimbEnd()
    {
        player.LedgeClimbOver();
    }
}
