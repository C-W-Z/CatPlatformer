using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private Transform tf;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Animator animator;
    [SerializeField] private TrailRenderer tr;
    [SerializeField] private PlayerController player;
    [SerializeField] private float resetDelayTime = 0.1f;
    private bool _startJumpAnimation = false;
    private bool _startLedgeGrabAnimation = false;
    private bool _startDashAnimation = false;
    [SerializeField] private Vector2 normalPos = new(0, 0);
    [SerializeField] private Vector2 wallGrabPos = new(0, -0.07f);
    [SerializeField] private Vector2 wallClimbUpPos = new(0, -0.07f);
    [SerializeField] private Vector2 wallClimbDownPos = new(0, -0.3f);

    public void StartEmitTrail() {
        tr.emitting = true;
    }
    public void StopEmitTrail() {
        tr.emitting = false;
    }
    public void FlipY(bool f) {
        sr.flipY = f;
    }
    public void SetAnimation()
    {
        animator.SetBool("onGround", player.OnGround);
        animator.SetBool("onWall", player.OnWall);
        animator.SetFloat("xSpeed", Mathf.Abs(player.RB.velocity.x));
        animator.SetFloat("yVeloc", player.RB.velocity.y);
        animator.SetBool("sneaking", player.IsSneaking);
        animator.SetBool("startJump", _startJumpAnimation);
        animator.SetBool("startLedgeGrab", _startLedgeGrabAnimation);
        animator.SetBool("ledgeGrabbing", player.IsLedgeGrabbing);
        animator.SetBool("ledgeClimbing", player.IsLedgeClimbing);
        animator.SetBool("wallGrabbing", player.IsWallGrabbing);
        animator.SetBool("wallClimbing", player.IsWallClimbing);
        animator.SetBool("wallJumping", player.IsWallJumping);
        animator.SetBool("startDash", _startDashAnimation);
        animator.SetBool("dashing", player.IsDashing);

        if (_startJumpAnimation)
            Invoke(nameof(ResetJumpAnimation), resetDelayTime);
        if (_startLedgeGrabAnimation)
            Invoke(nameof(ResetLedgeGrabAnimation), resetDelayTime);
        if (_startDashAnimation)
            Invoke(nameof(ResetDashAnimation), resetDelayTime);

        if (player.IsWallClimbing && !sr.flipY)
            tf.localPosition = wallClimbUpPos;
        else if (player.IsWallClimbing && sr.flipY)
            tf.localPosition = wallClimbDownPos;
        else if (player.IsWallGrabbing)
            tf.localPosition = wallGrabPos;
        else
            tf.localPosition = normalPos;
    }
    public void SetJumpAnimation() {
        _startJumpAnimation = true;
    }
    private void ResetJumpAnimation() {
        _startJumpAnimation = false;
    }
    public void SetLedgeGrabAnimation() {
        _startLedgeGrabAnimation = true;
    }
    private void ResetLedgeGrabAnimation() {
        _startLedgeGrabAnimation = false;
    }
    public void SetDashAnimation() {
        _startDashAnimation = true;
    }
    private void ResetDashAnimation() {
        _startDashAnimation = false;
    }
    // for animation event
    public void LedgeClimbEnd()
    {
        player.LedgeClimbOver();
    }
}
