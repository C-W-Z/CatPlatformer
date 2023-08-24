using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimateController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    private bool startJumpAnimation = false;

    public void SetJumpAnimation()
    {
        startJumpAnimation = true;
    }
    public void ResetJumpAnimation()
    {
        startJumpAnimation = false;
    }
    public void SetAnimation(bool onGround, float xSpeed, float yVeloc)
    {
        animator.SetBool("onGround", onGround);
        animator.SetFloat("xSpeed", xSpeed);
        animator.SetFloat("yVeloc", yVeloc);
        animator.SetBool("startJump", startJumpAnimation);
    }
}
