using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;

#region Mono Behaviour

    void Update()
    {
        CheckSurrounding();
        GetInput();


        Run();
        if (Input.GetKeyDown(KeyCode.Space))
            StartCoroutine(Jump());

        CheckFaceDir();
        SetAnimation();
    }

#endregion

#region Get Input

    private float inputH, inputV;

    private void GetInput()
    {
        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");
    }

#endregion

#region Check Surrounding

    [Header("Surrounding")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private CheckBox groundCheck;
    private bool onGround;

    private void CheckSurrounding()
    {
        onGround = groundCheck.Detect(groundLayer);
    }

#endregion

#region Move

    [Header("Move")]
    [SerializeField] private float maxMoveSpeed;
    [SerializeField] private float acceleration, decceleration;
    private bool isFaceRight = true;

    private void Run()
    {
        float targetSpeed = inputH * maxMoveSpeed;
        float speedDiff = targetSpeed - rb.velocity.x;
        float accelerate = (Mathf.Abs(inputH) > 0.01f) ? acceleration : decceleration;
        float movement = speedDiff * accelerate;
        rb.AddForce(movement * Vector2.right, ForceMode2D.Force);
    }

    private void CheckFaceDir()
    {
        if (inputH == 0)
            return;
        if ((rb.velocity.x > 0 && !isFaceRight) ||
            (rb.velocity.x < 0 && isFaceRight))
        {
            transform.localScale = new Vector2(-transform.localScale.x, transform.localScale.y);
            isFaceRight = !isFaceRight;
        }
    }

#endregion

#region Jump & Fall

    [Header("Jump & Fall")]
    [SerializeField] private float frameBeforeJump;
    [SerializeField] private float jumpForce;
    private bool isJumping = false;

    private IEnumerator Jump()
    {
        animator.SetTrigger("startJump");
        for (int i = 0; i < frameBeforeJump; i++)
            yield return null;
        animator.ResetTrigger("startJump");
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        isJumping = true;
    }

#endregion

#region Animation

    private void SetAnimation()
    {
        animator.SetBool("onGround", onGround);
        animator.SetFloat("xSpeed", Mathf.Abs(rb.velocity.x));
        animator.SetFloat("ySpeed", rb.velocity.y);
    }

#endregion
}