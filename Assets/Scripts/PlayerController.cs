using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private AnimateController animator;

#region Mono Behaviour

    void Start()
    {
        isFaceRight = true;
    }

    void Update()
    {
        TimerUpdate();
        CheckSurrounding();
        GetInput();

        #region Check States

        if (isJumping && rb.velocity.y < 0)
        {
            isJumping = false;
            isJumpCutting = false;
        }

        if (jumpDown)
            lastPressJumpTimer = jumpBufferTime;

        if (onGround && !isJumping)
            lastOnGroundTimer = coyoteTime;

        #endregion

        #region Player Movements

        Run();
        if (!isJumping && lastOnGroundTimer > 0 && lastPressJumpTimer > 0)
            StartCoroutine(Jump());
        if (jumpUp && isJumping && rb.velocity.y > 0)
            JumpCut();

        #endregion

        SetGravity();
        CheckFaceDir();
        animator.SetAnimation(onGround, Mathf.Abs(rb.velocity.x), rb.velocity.y);
    }

#endregion

#region Timer

    private float lastPressJumpTimer; // for jump buffer
    private float lastOnGroundTimer; // for coyote time
    private void TimerUpdate()
    {
        lastPressJumpTimer -=  Time.deltaTime;
        lastOnGroundTimer -= Time.deltaTime;
    }

#endregion

#region Get Input

    private float inputH, inputV;
    private bool jumpDown, jumpUp;

    private void GetInput()
    {
        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");
        jumpDown = Input.GetKeyDown(KeyCode.Space);
        jumpUp = Input.GetKeyUp(KeyCode.Space);
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
    [SerializeField] private float maxMoveSpeed = 1.6f;
    [SerializeField] private float moveAcceleration = 1.2f, moveDecceleration = 1.6f;
    [SerializeField][Min(1f)] private float jumpAirTimeMoveSpeedMult = 1.5f;
    private bool isFaceRight = true;

    private void Run()
    {
        float targetSpeed = inputH * maxMoveSpeed;
        float accelerate = (Mathf.Abs(inputH) > 0.01f) ? moveAcceleration : moveDecceleration;
        if (JumpAirTiming)
        {
            targetSpeed *= jumpAirTimeMoveSpeedMult;
            accelerate *= jumpAirTimeMoveSpeedMult;
        }
        float speedDiff = targetSpeed - rb.velocity.x;
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
    [SerializeField] private float timeBeforeJump = 0.06f;
    [SerializeField] private float jumpForce = 4f;
    [SerializeField][Range(0f, 0.5f)] private float coyoteTime = 0.1f;
    [SerializeField][Range(0f, 0.5f)] private float jumpBufferTime = 0.1f; // jump input buffer
    [SerializeField] private float jumpAirTimeYSpeed = 0.5f;
    [SerializeField][Range(0f, 1f)] private float jumpAirTimeGravityMult = 0.3f;
    private bool isJumping = false;
    private bool JumpAirTiming => isJumping && Mathf.Abs(rb.velocity.y) < jumpAirTimeYSpeed;

    private IEnumerator Jump()
    {
        lastPressJumpTimer = 0;
        // reset (falling) velocity for coyote time 
        rb.velocity = new Vector2(rb.velocity.x, 0);
        // start jump animation
        animator.SetJumpAnimation();
        // wait for animation frames before jump up
        yield return new WaitForSeconds(timeBeforeJump);
        // jump
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        animator.ResetJumpAnimation();
        isJumping = true;
        lastOnGroundTimer = 0;
    }

    [SerializeField][Range(0f, 1f)] private float jumpCutVelocityMult = 0.5f;
    [SerializeField][Min(1f)] private float jumpCutGravityMult = 1.4f;
    private bool isJumpCutting = false;

    private void JumpCut()
    {
        if (rb.velocity.y <= 0)
            return;
        rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutVelocityMult);
        isJumpCutting = true;
    }

    private const float gravityScale = 1;
    [SerializeField][Min(1f)] private float fallGravityMult = 1.5f;

    private void SetGravity()
    {
        if (rb.velocity.y < 0)
            rb.gravityScale = gravityScale * fallGravityMult;
        else if (isJumpCutting)
            rb.gravityScale = gravityScale * jumpCutGravityMult;
        else if (JumpAirTiming)
            rb.gravityScale = gravityScale * jumpAirTimeGravityMult;
        else
            rb.gravityScale = gravityScale;
    }

#endregion
}