using System.Collections;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform tf;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D cl; // normal collider
    [SerializeField] private Collider2D cl_s; // sneak collider
    [SerializeField] private PlayerAnimator animator;
    public Rigidbody2D RB => rb;

#region Mono Behaviour

    void Start()
    {
        cl.isTrigger = false;
        cl_s.enabled = false;
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

        if (onGround && !isJumping)
            lastOnGroundTimer = coyoteTime;

        #endregion

        #region Player Movements

        if (ledgeGrabbing || ledgeClimbing)
            goto ledge_grabbing;

        Move();
        if (!isJumping && lastOnGroundTimer > 0 && lastPressJumpTimer > 0)
            StartCoroutine(Jump());
        if (jumpUp && isJumping && rb.velocity.y > 0)
            JumpCut();
        if (ledgeDetected && canGrabLedge)
            LedgeGrab();

        #endregion

ledge_grabbing:
        if (ledgeGrabbing || ledgeClimbing)
        {
            cl.isTrigger = true;
            tf.position = (Vector3)ledgeClimbPosBefore;
            rb.velocity = Vector2.zero;
            // rb.gravityScale = 0;

            if (jumpDown && ledgeGrabbing)
                LedgeClimb();
        }

        SetGravity();
        CheckFaceDir();

        animator.SetAnimation();
    }

#endregion

#region Timer

    private float lastPressMoveTimer; // for walk speed up to run
    private float lastPressJumpTimer; // for jump buffer
    private float lastOnGroundTimer; // for coyote time
    private void TimerUpdate()
    {
        lastPressMoveTimer -= Time.deltaTime;
        lastPressJumpTimer -=  Time.deltaTime;
        lastOnGroundTimer -= Time.deltaTime;
    }

#endregion

#region Get Input

    private float inputH, inputV, rawInputH, rawInputV;
    private bool moveDown, canCheckDoubleMoveDown = true;
    private int moveDownCount = 0;
    [Header("Input")]
    [SerializeField][Range(0f, 1f)] private float doubleMoveDownCheckTime = 0.5f;
    private bool jumpDown, jumpPress, jumpUp;

    private void GetInput()
    {
        inputH = Input.GetAxis("Horizontal");
        inputV = Input.GetAxis("Vertical");
        rawInputH = Input.GetAxisRaw("Horizontal");
        rawInputV = Input.GetAxisRaw("Vertical");
        moveDown = Input.GetButtonDown("Horizontal");
        jumpDown = Input.GetButtonDown("Jump");
        jumpPress = Input.GetButton("Jump");
        jumpUp = Input.GetButtonUp("Jump");

        if (moveDown)
            moveDownCount++;

        if (moveDownCount == 1 && canCheckDoubleMoveDown)
        {
            lastPressMoveTimer = doubleMoveDownCheckTime;
            StartCoroutine(DetectDoubleMoveDown());
        }

        if (rawInputH == 0)
            isRunning = false;

        if (jumpDown)
            lastPressJumpTimer = jumpBufferTime;
    }

    private IEnumerator DetectDoubleMoveDown()
    {
        canCheckDoubleMoveDown = false;
        while (lastPressMoveTimer > 0)
        {
            if (moveDownCount == 2)
            {
                isRunning = true;
                break;
            }
            yield return null;
        }
        moveDownCount = 0;
        canCheckDoubleMoveDown = true;
    }

#endregion

#region Check Surrounding

    [Header("Surrounding")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private CheckBox groundCheck;
    [SerializeField] private CheckBox wallCheck;
    [SerializeField] private CheckBox ledgeCheck;
    [SerializeField] private CheckBox ledgeCheckTop;
    [SerializeField] private CheckBox ledgeRayFront;
    [SerializeField] private CheckBox ledgeRayDown;
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
    [SerializeField][Min(0f)] private float maxWalkSpeed = 1.6f;
    [SerializeField][Min(0f)] private float maxRunSpeed = 2.5f;
    private bool isRunning = false;
    [SerializeField][Min(0f)] private float moveAcceleration = 1.2f, moveDecceleration = 1.6f;
    [SerializeField][Min(0f)] private float frictionAmount = 0.5f;
    [Space(10)]
    [SerializeField][Min(1f)] private float jumpAirTimeMoveSpeedMult = 1.5f;
    private bool isFaceRight = true;

    private void Move()
    {
        float targetSpeed = inputH * (isRunning ? maxRunSpeed : maxWalkSpeed);
        float accelerate = (Mathf.Abs(rawInputH) > 0) ? moveAcceleration : moveDecceleration;
        // faster when air time
        if (jumpAirTiming)
        {
            targetSpeed *= jumpAirTimeMoveSpeedMult;
            accelerate *= jumpAirTimeMoveSpeedMult;
        }
        float speedDiff = targetSpeed - rb.velocity.x;
        float movement = speedDiff * accelerate;
        // friction
        float friction = 0;
        if (lastOnGroundTimer > 0 && rawInputH == 0)
            friction = Mathf.Min(Mathf.Abs(rb.velocity.x), frictionAmount) * Mathf.Sign(rb.velocity.x);
        rb.AddForce((movement - friction) * Vector2.right, ForceMode2D.Force);
    }

    private void CheckFaceDir()
    {
        if (rawInputH == 0 || ledgeGrabbing || ledgeClimbing)
            return;
        if (wallDetected && ((rawInputH > 0 && isFaceRight) || (rawInputH < 0 && !isFaceRight)))
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
    [SerializeField] private float jumpForce = 4.5f;
    [SerializeField][Range(0f, 0.5f)] private float coyoteTime = 0.15f;
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
    private Vector2 ledgeClimbPosBefore;
    private Vector2 ledgeClimbPosAfter;
    private bool canGrabLedge = true;
    private bool ledgeGrabbing = false;
    public bool ledgeClimbing { get; private set; } = false;

    private void LedgeGrab()
    {
        // get corner position
        Vector2 cornerPos = tf.position;
        cornerPos.x = ledgeRayFront.GetHitPoint(cornerPos).x;
        cornerPos.y = ledgeRayDown.GetHitPoint(cornerPos).y;
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

#endregion
}