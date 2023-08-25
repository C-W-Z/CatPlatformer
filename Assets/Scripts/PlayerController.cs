using System.Collections;
using System.Collections.Generic;
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
        isRunning = false;
        isWallJumping = false;
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

        if (isWallGrabbing && (!onWall || !wallPress || ledgeGrabbing || ledgeClimbing))
        {
            isWallGrabbing = false;
            isWallClimbing = false;
        }

        if (isWallJumping && (rb.velocity.x == 0 || rb.velocity.y <= 0))
            isWallJumping = false;

        #endregion

        #region Player Movements

        if (ledgeGrabbing || ledgeClimbing)
            goto ledge_grabbing;

        if (!isWallGrabbing && !isWallClimbing)
            Move();
        if (!isWallGrabbing && !isWallClimbing && !isJumping && !isWallJumping && lastOnGroundTimer > 0 && lastPressJumpTimer > 0)
            StartCoroutine(Jump());
        if (jumpUp && (isJumping || isWallJumping) && rb.velocity.y > 0)
            JumpCut();
        if (!isWallGrabbing && onWall && wallPress && !isWallJumping)
            StartCoroutine(StartWallGrab());
        if (isWallGrabbing && !isWallJumping)
            WallClimb();
        if (isWallGrabbing && jumpDown && !isJumping && !isWallJumping)
            StartCoroutine(WallJump());
        if (ledgeDetected && canGrabLedge)
            LedgeGrab();

        #endregion

        if (isWallGrabbing && !isWallClimbing)
            rb.velocity = Vector2.zero;

ledge_grabbing:
        if (ledgeGrabbing || ledgeClimbing)
        {
            cl.isTrigger = true;
            tf.position = (Vector3)ledgeClimbPosBefore;
            rb.velocity = Vector2.zero;
            // rb.gravityScale = 0;

            if (ledgeGrabbing)
            {
                if (jumpDown)
                    LedgeClimb();
                if (rawInputV < 0)
                    StartCoroutine(StartWallGrab());
            }
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
    private bool wallDown, wallPress;

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
        wallDown = Input.GetButtonDown("Wall");
        wallPress = Input.GetButton("Wall");

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
    [SerializeField] private List<CheckBox> wallRays;
    [SerializeField] private List<CheckBox> platformRays;
    [SerializeField] private CheckBox wallToLedgeCheck;
    [SerializeField] private CheckBox wallBottomCheck;
    public bool onGround;
    public bool onWall;
    private bool ledgeDetected;

    private void CheckSurrounding()
    {
        onGround = groundCheck.Detect(groundLayer);
        onWall = wallCheck.Detect(groundLayer);
        ledgeDetected = (ledgeCheck.Detect(groundLayer) && !ledgeCheckTop.Detect(groundLayer)) || ((isWallGrabbing || isWallClimbing) && !wallToLedgeCheck.Detect(groundLayer) && wallBottomCheck.Detect(groundLayer));
    }

    private float GetWallX()
    {
        foreach (var ray in wallRays)
        {
            float X = ray.GetHitPoint(groundLayer, tf.position, isFaceRight ? 1 : -1).x;
            if (X != tf.position.x)
                return X;
        }
        Debug.Log("All No Hit");
        return tf.position.x;
    }

    private float GetPlatformY()
    {
        foreach (var ray in platformRays)
        {
            float Y = ray.GetHitPoint(groundLayer, tf.position).y;
            if (Y != tf.position.y)
                return Y;
        }
        Debug.Log("All No Hit");
        return tf.position.y;
    }

    private Vector2 GetLedgeCornerPos()
    {
        Vector2 pos;
        pos.x = GetWallX();
        pos.y = GetPlatformY();
        return pos;
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
        float accelerate = (rawInputH != 0) ? moveAcceleration : moveDecceleration;
        if (isWallJumping && rawInputH != 0)
            accelerate = wallJumpMoveDecceleration;
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
        animator.FlipY(isWallClimbing && rb.velocity.y < 0);

        if (ledgeGrabbing || ledgeClimbing || isWallGrabbing || isWallClimbing)
            return;
        if (onWall && ((rawInputH > 0 && isFaceRight) || (rawInputH < 0 && !isFaceRight)))
            return;
        if ((rawInputH < 0 && isFaceRight) || (rawInputH > 0 && !isFaceRight))
            Turn();
    }

    private void Turn()
    {
        tf.localScale = new Vector2(-tf.localScale.x, tf.localScale.y);
        isFaceRight = !isFaceRight;
    }

#endregion

#region Jump

    [Header("Jump")]
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

#endregion

#region Fall

    [Header("Fall")]
    [SerializeField][Min(0f)] private float maxFallSpeed = 5f;
    [SerializeField][Min(1f)] private float fallGravityMult = 1.5f;
    [SerializeField][Min(0f)] private float maxSlideSpeed = 1.5f;
    [SerializeField][Min(0f)] private float slideGravityMult = 0.5f;
    private const float gravityScale = 1;

    private void SetGravity()
    {
        if (ledgeGrabbing || ledgeClimbing || isWallGrabbing || isWallClimbing)
            rb.gravityScale = 0;
        else if (isWallJumping)
            rb.gravityScale = gravityScale * wallJumpGravityMult;
        else if (rb.velocity.y < 0)
            rb.gravityScale = gravityScale * (onWall ? slideGravityMult : fallGravityMult);
        else if (isJumpCutting)
            rb.gravityScale = gravityScale * jumpCutGravityMult;
        else if (jumpAirTiming)
            rb.gravityScale = gravityScale * jumpAirTimeGravityMult;
        else
            rb.gravityScale = gravityScale;

        if (!onWall && rb.velocity.y < -maxFallSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        else if (onWall && rb.velocity.y < -maxSlideSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxSlideSpeed);
    }

#endregion

#region Ledge

    [Header("Ledge Info")]
    [SerializeField] private float delayForLedgeGrab = 0.5f;
    [SerializeField] private Vector2 offsetBefore = new(0.063f, 0.22f);
    [SerializeField] private Vector2 offsetAfter = new(0.35f, 0.4f);
    [SerializeField] private Vector2 defaultOffsetBefore = new(0.146f, 0.22f);
    [SerializeField] private Vector2 defaultOffsetAfter = new(0.35f, 0.4f);
    private Vector2 ledgeClimbPosBefore;
    private Vector2 ledgeClimbPosAfter;
    private bool canGrabLedge = true;
    private bool ledgeGrabbing = false;
    public bool ledgeClimbing { get; private set; } = false;

    private void LedgeGrab()
    {
        // get corner position
        Vector2 cornerPos = GetLedgeCornerPos();
        // set ledge grab and climb position        
        ledgeClimbPosBefore.x = cornerPos.x + ((cornerPos.x == tf.position.x) ? defaultOffsetBefore.x : offsetBefore.x) * (isFaceRight ? 1 : -1);
        ledgeClimbPosBefore.y = cornerPos.y + ((cornerPos.y == tf.position.y) ? defaultOffsetBefore.y : offsetBefore.y);
        ledgeClimbPosAfter.x = cornerPos.x + ((cornerPos.x == tf.position.x) ? defaultOffsetAfter.x : offsetAfter.x) * (isFaceRight ? 1 : -1);
        ledgeClimbPosAfter.y = cornerPos.y + ((cornerPos.y == tf.position.y) ? defaultOffsetAfter.y : offsetAfter.y);
        // grab
        canGrabLedge = false;
        ledgeGrabbing = true;
        isJumping = false;
        isJumpCutting = false;
        isRunning = false;
        isWallClimbing = false;
        isWallGrabbing = false;
        isWallJumping = false;
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

#region Wall

    [Header("Wall")]
    [SerializeField] private float wallClimbSpeed = 1.5f;
    [SerializeField] private float timeBeforeWallJump = 0.05f;
    [SerializeField] private Vector2 wallJumpPower = new(9f, 4f);
    [SerializeField] private float wallJumpGravityMult = 1.4f;
    [SerializeField] private float wallJumpMoveDecceleration = 0.6f;
    public bool isWallGrabbing = false;
    public bool isWallClimbing = false;
    public bool isWallJumping = false;
    [SerializeField] private Vector2 wallGrabOffset = new(-0.2f, 0f);
    [SerializeField] private Vector2 defaultWallGrabOffset = new(-0.25f, 0f);

    private IEnumerator StartWallGrab()
    {
        // get wall grab pos
        Vector2 wallGrabPos = tf.position;
        wallGrabPos.x = GetWallX();
        if (wallGrabPos.x == tf.position.x)
            wallGrabPos += defaultWallGrabOffset * (isFaceRight ? 1 : -1);
        else
            wallGrabPos += wallGrabOffset * (isFaceRight ? 1 : -1);
        
        // start wall grab
        lastOnGroundTimer = 0;
        isWallGrabbing = true;
        isWallJumping = false;
        rb.velocity = Vector2.zero;
        tf.position = wallGrabPos;

        // for back to wall grab from ledge grab
        cl.isTrigger = false;
        ledgeGrabbing = false;
        ledgeClimbing = false;

        if (!canGrabLedge)
        {
            yield return new WaitForSeconds(delayForLedgeGrab);
            canGrabLedge = true;
        }
        else yield break;
    }

    private void WallClimb()
    {
        if (rawInputV == 0)
        {
            isWallClimbing = false;
            return;
        }

        isWallClimbing = true;
        rb.velocity = new Vector2(0, inputV * wallClimbSpeed);
    }

    private IEnumerator WallJump()
    {
        lastPressJumpTimer = 0;
        lastOnGroundTimer = 0;

        Vector2 force = new((isFaceRight ? -1 : 1) * wallJumpPower.x, wallJumpPower.y);

        yield return new WaitForSeconds(timeBeforeWallJump);
        isWallJumping = true;
        isWallGrabbing = false;
        isWallClimbing = false;
        isJumping = false;

        rb.velocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

#endregion
}