using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController player;
    private bool startJumpAnimation = false;
    private bool startLedgeGrabAnimation = false;

    public void SetJumpAnimation()
    {
        startJumpAnimation = true;
    }
    public void ResetJumpAnimation()
    {
        startJumpAnimation = false;
    }
    public void SetAnimation()
    {
        animator.SetBool("onGround", player.onGround);
        animator.SetFloat("xSpeed", Mathf.Abs(player.rb.velocity.x));
        animator.SetFloat("yVeloc", player.rb.velocity.y);
        animator.SetBool("startJump", startJumpAnimation);
        animator.SetBool("startLedgeGrab", startLedgeGrabAnimation);
        animator.SetBool("ledgeClimbing", player.ledgeClimbing);
    }
    public void SetLedgeGrabAnimation()
    {
        startLedgeGrabAnimation = true;
    }
    public void ResetLedgeGrabAnimation()
    {
        startLedgeGrabAnimation = false;
    }
    public void LedgeClimbEnd()
    {
        player.LedgeClimbOver();
    }
}
