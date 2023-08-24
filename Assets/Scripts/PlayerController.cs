using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform tf;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D cl;
    [SerializeField] private AnimateController animator;
    public Rigidbody2D RB => rb;

#region Mono Behaviour

    void Start()
    {
        cl.isTrigger = false;
        rb.gravityScale = gravityScale;
        isFaceRight = true;
        isJumping = false;
        isJumpCutting = false;
    }

    void Update()
    {
        TimerUpdate();
        CheckSurrounding();
        GetInput();

        #region Check States

        if (isJumping && rb.velocity.y <= 0)
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

        if (ledgeGrabbing)
            goto ledge_grabbing;

        Run();
        if (!isJumping && lastOnGroundTimer > 0 && lastPressJumpTimer > 0)
            StartCoroutine(Jump());
        if (jumpUp && isJumping && rb.velocity.y > 0)
            JumpCut();
        if (ledgeDetected && canGrabLedge)
            LedgeGrab();

        #endregion

        SetGravity();
        CheckFaceDir();

ledge_grabbing:
        if (ledgeGrabbing || ledgeClimbing)
        {
            cl.isTrigger = true;
            tf.position = (Vector3)ledgeClimbPosBefore;
            rb.velocity = Vector2.zero;
            rb.gravityScale = 0;

            if (jumpDown && ledgeGrabbing)
                LedgeClimb();
        }

        animator.SetAnimation();
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
    [SerializeField] private CheckBox wallCheck;
    [SerializeField] private CheckBox ledgeCheck;
    [SerializeField] private CheckBox ledgeCheckTop;
    public bool onGround { get; private set; }
    private bool wallDetected;
    private bool ledgeDetected;

    private void CheckSurrounding()
    {
        onGround = groundCheck.Detect(groundLayer);
        wallDetected = wallCheck.Detect(groundLayer);
        ledgeDetected = !ledgeCheckTop.Detect(groundLayer) && ledgeCheck.Detect(groundLayer);
    }

#endregion

#region Move

    [Header("Move")]
    [SerializeField] private float maxMoveSpeed = 1.6f;
    [SerializeField] private float moveAcceleration = 1.2f, moveDecceleration = 1.6f;
    [Space(10)]
    [SerializeField][Min(1f)] private float jumpAirTimeMoveSpeedMult = 1.5f;
    private bool isFaceRight = true;

    private void Run()
    {
        float targetSpeed = inputH * maxMoveSpeed;
        float accelerate = (Mathf.Abs(inputH) > 0.01f) ? moveAcceleration : moveDecceleration;
        if (jumpAirTiming)
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
        if (wallDetected && ((inputH > 0 && isFaceRight) || (inputH < 0 && !isFaceRight)))
            return;
        if ((rb.velocity.x > 0 && !isFaceRight) ||
            (rb.velocity.x < 0 && isFaceRight))
        {
            tf.localScale = new Vector2(-tf.localScale.x, tf.localScale.y);
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
    [Space(10)]
    [SerializeField] private float jumpAirTimeYSpeed = 0.5f;
    [SerializeField][Range(0f, 1f)] private float jumpAirTimeGravityMult = 0.3f;
    private bool isJumping = false;
    private bool jumpAirTiming => isJumping && Mathf.Abs(rb.velocity.y) < jumpAirTimeYSpeed;

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
        rb.AddForce(jumpForce * Vector2.up, ForceMode2D.Impulse);
        isJumping = true;
        lastOnGroundTimer = 0;
    }

    [Space(10)]
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
    [Space(10)]
    [SerializeField][Min(1f)] private float fallGravityMult = 1.5f;

    private void SetGravity()
    {
        if (ledgeGrabbing || ledgeClimbing)
            rb.gravityScale = 0;
        else if (rb.velocity.y < 0)
            rb.gravityScale = gravityScale * fallGravityMult;
        else if (isJumpCutting)
            rb.gravityScale = gravityScale * jumpCutGravityMult;
        else if (jumpAirTiming)
            rb.gravityScale = gravityScale * jumpAirTimeGravityMult;
        else
            rb.gravityScale = gravityScale;
    }

#endregion

#region Ledge

    [Header("Ledge Info")]
    [SerializeField] private Vector2 offsetBefore = new(0.14f, 0.22f);
    [SerializeField] private Vector2 offsetAfter = new(0.35f, 0.4f);
    [SerializeField] private Vector2 rayRightOffset = new(0f, -0.18f);
    [SerializeField] private Vector2 rayDownOffset = new(0.27f, 0f);
    private Vector2 ledgeClimbPosBefore;
    private Vector2 ledgeClimbPosAfter;
    private bool canGrabLedge = true;
    private bool ledgeGrabbing = false;
    public bool ledgeClimbing { get; private set; } = false;

    private void LedgeGrab()
    {
        // get corner position
        Vector2 cornerPos = tf.position;
        RaycastHit2D hit;
        hit = Physics2D.Raycast((Vector2)tf.position + rayRightOffset, isFaceRight ? Vector2.right : Vector2.left);
        if (hit.collider != null)
            cornerPos.x = hit.point.x;
        else  Debug.Log("no front hit");
        hit = Physics2D.Raycast((Vector2)tf.position + rayDownOffset * (isFaceRight ? 1 : -1), Vector2.down);
        if (hit.collider != null)
            cornerPos.y = hit.point.y;
        else Debug.Log("no down hit");

        // set ledge grab and climb position
        ledgeClimbPosBefore = cornerPos + new Vector2(offsetBefore.x * (isFaceRight ? 1 : -1), offsetBefore.y);
        ledgeClimbPosAfter = cornerPos + new Vector2(offsetAfter.x * (isFaceRight ? 1 : -1), offsetAfter.y);
        // grab
        canGrabLedge = false;
        ledgeGrabbing = true;
        // start grab animation
        animator.SetLedgeGrabAnimation();
    }

    private void LedgeClimb()
    {
        ledgeGrabbing = false;
        ledgeClimbing = true;
    }

    public void LedgeClimbOver()
    {
        tf.position = ledgeClimbPosAfter;
        cl.isTrigger = false;
        ledgeGrabbing = false;
        ledgeClimbing = false;
        canGrabLedge = true;
    }

    private void DrawRayForLedgePos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(tf.position + (Vector3)rayRightOffset, tf.position + (Vector3)rayRightOffset + new Vector3(0.5f * (isFaceRight ? 1 : -1), 0, 0));
        Gizmos.DrawLine(tf.position + (Vector3)rayDownOffset * (isFaceRight ? 1 : -1), tf.position + (Vector3)rayDownOffset * (isFaceRight ? 1 : -1) + new Vector3(0, -0.5f, 0));
    }

#endregion

    void OnDrawGizmos()
    {
        DrawRayForLedgePos();
    }
}