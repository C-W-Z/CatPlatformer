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
        rb.gravityScale = GravityScale;
        _isFaceRight = true;
        _isRunning = false;
        isJumping = false;
        _isJumpCutting = false;
        _ledgeGrabbing = false;
        LedgeClimbing = false;
        IsWallGrabbing = false;
        IsWallClimbing = false;
        IsWallJumping = false;
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
            _isJumpCutting = false;
        }

        if (OnGround && !isJumping)
            _lastOnGroundTimer = coyoteTime;

        if (IsWallGrabbing && (!OnWall || !_input.WallPress || _ledgeGrabbing || LedgeClimbing))
        {
            IsWallGrabbing = false;
            IsWallClimbing = false;
        }

        if (IsWallJumping && (rb.velocity.x == 0 || rb.velocity.y <= 0))
            IsWallJumping = false;

        #endregion

        #region Player Movements

        if (_ledgeGrabbing || LedgeClimbing)
            goto ledge_grabbing;

        if (!IsWallGrabbing && !IsWallClimbing)
            Move();

        if (!IsWallGrabbing && !IsWallClimbing && !isJumping && !IsWallJumping && _lastOnGroundTimer > 0 && _lastPressJumpTimer > 0)
            StartCoroutine(Jump());

        if (_input.JumpUp && (isJumping || IsWallJumping) && rb.velocity.y > 0)
            JumpCut();

        if (!IsWallGrabbing && _input.WallPress && !IsWallJumping)
        {
            if (OnWall)
                StartCoroutine(StartWallGrab());
            else if (_backWallDetected)
            {
                Turn();
                StartCoroutine(StartWallGrab());
            }
        }

        if (IsWallGrabbing && !IsWallJumping)
            WallClimb();

        if (IsWallGrabbing && _lastPressWallJumpTimer > 0 && !isJumping && !IsWallJumping)
            StartCoroutine(WallJump());

        if (_ledgeDetected && _canGrabLedge)
            LedgeGrab();

        #endregion

        if (IsWallGrabbing && !IsWallClimbing)
            rb.velocity = Vector2.zero;

ledge_grabbing:
        if (_ledgeGrabbing || LedgeClimbing)
        {
            cl.isTrigger = true;
            tf.position = (Vector3)_ledgeClimbPosBefore;
            rb.velocity = Vector2.zero;
            // rb.gravityScale = 0;

            if (_ledgeGrabbing)
            {
                if (_input.JumpDown)
                    LedgeClimb();
                if (_input.RawV < 0)
                    StartCoroutine(StartWallGrab());
            }
        }

        SetGravity();
        CheckFaceDir();

        animator.SetAnimation();
    }

#endregion

#region Timer

    private float _lastPressMoveTimer; // for walk speed up to run
    private float _lastPressJumpTimer; // for jump buffer
    private float _lastOnGroundTimer; // for coyote time
    private float _lastPressWallJumpTimer; // for wall jump buffer
    private void TimerUpdate()
    {
        _lastPressMoveTimer -= Time.deltaTime;
        _lastPressJumpTimer -=  Time.deltaTime;
        _lastOnGroundTimer -= Time.deltaTime;
        _lastPressWallJumpTimer -= Time.deltaTime;
    }

#endregion

#region Get Input

    private struct Input {
        public float H, V, RawH, RawV;
        public bool MoveDown;
        public bool JumpDown, JumpUp;
        public bool WallPress;
    }

    private Input _input;
    [Header("Input")]
    [SerializeField][Range(0f, 1f)] private float doubleMoveDownCheckTime = 0.5f;
    private bool _canCheckDoubleMoveDown = true;
    private int _moveDownCount = 0;

    private void GetInput()
    {
        _input.H = UnityEngine.Input.GetAxis("Horizontal");
        _input.V = UnityEngine.Input.GetAxis("Vertical");
        _input.RawH = UnityEngine.Input.GetAxisRaw("Horizontal");
        _input.RawV = UnityEngine.Input.GetAxisRaw("Vertical");
        _input.MoveDown = UnityEngine.Input.GetButtonDown("Horizontal");
        _input.JumpDown = UnityEngine.Input.GetButtonDown("Jump");
        _input.JumpUp = UnityEngine.Input.GetButtonUp("Jump");
        _input.WallPress = UnityEngine.Input.GetButton("Wall");

        if (_input.MoveDown)
            _moveDownCount++;

        if (_moveDownCount == 1 && _canCheckDoubleMoveDown)
        {
            _lastPressMoveTimer = doubleMoveDownCheckTime;
            StartCoroutine(DetectDoubleMoveDown());
        }

        if (_input.RawH == 0)
            _isRunning = false;

        if (_input.JumpDown)
            _lastPressJumpTimer = jumpBufferTime;

        if (_input.WallPress && _input.JumpDown)
            _lastPressWallJumpTimer = wallJumpBufferTime;
    }

    private IEnumerator DetectDoubleMoveDown()
    {
        _canCheckDoubleMoveDown = false;
        while (_lastPressMoveTimer > 0)
        {
            if (_moveDownCount == 2)
            {
                _isRunning = true;
                break;
            }
            yield return null;
        }
        _moveDownCount = 0;
        _canCheckDoubleMoveDown = true;
    }

#endregion

#region Check Surrounding

    [Header("Surrounding")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private CheckBox groundCheck;
    [SerializeField] private CheckBox wallCheck;
    [SerializeField] private CheckBox backWallCheck;
    [SerializeField] private CheckBox ledgeCheck;
    [SerializeField] private CheckBox ledgeCheckTop;
    [SerializeField] private List<CheckBox> wallRays;
    [SerializeField] private List<CheckBox> platformRays;
    [SerializeField] private CheckBox wallToLedgeCheck;
    [SerializeField] private CheckBox wallBottomCheck;
    public bool OnGround { get; private set; }
    public bool OnWall { get; private set; }
    private bool _ledgeDetected;
    private bool _backWallDetected;

    private void CheckSurrounding()
    {
        OnGround = groundCheck.Detect(groundLayer);
        OnWall = wallCheck.Detect(groundLayer);
        _backWallDetected = backWallCheck.Detect(groundLayer);
        _ledgeDetected = (ledgeCheck.Detect(groundLayer) && !ledgeCheckTop.Detect(groundLayer)) || ((IsWallGrabbing || IsWallClimbing) && !wallToLedgeCheck.Detect(groundLayer) && wallBottomCheck.Detect(groundLayer));
    }

    private float GetWallX()
    {
        foreach (var ray in wallRays)
        {
            float X = ray.GetHitPoint(groundLayer, tf.position).x;
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
    private bool _isRunning = false;
    [SerializeField][Min(0f)] private float moveAcceleration = 1.2f, moveDecceleration = 1.6f;
    [SerializeField][Min(0f)] private float frictionAmount = 0.5f;
    [Space(10)]
    [SerializeField][Min(1f)] private float jumpAirTimeMoveSpeedMult = 1.5f;
    private bool _isFaceRight = true;

    private void Move()
    {
        float targetSpeed = _input.H * (_isRunning ? maxRunSpeed : maxWalkSpeed);
        float accelerate = (_input.RawH != 0) ? moveAcceleration : moveDecceleration;
        if (IsWallJumping)
            accelerate = (_input.RawH != 0) ? wallJumpMoveAcceleration : wallJumpMoveDecceleration;
        // faster when air time
        if (JumpAirTiming)
        {
            targetSpeed *= jumpAirTimeMoveSpeedMult;
            accelerate *= jumpAirTimeMoveSpeedMult;
        }
        float speedDiff = targetSpeed - rb.velocity.x;
        float movement = speedDiff * accelerate;
        // friction
        float friction = 0;
        if (_lastOnGroundTimer > 0 && _input.RawH == 0)
            friction = Mathf.Min(Mathf.Abs(rb.velocity.x), frictionAmount) * Mathf.Sign(rb.velocity.x);
        rb.AddForce((movement - friction) * Vector2.right, ForceMode2D.Force);
    }

    private void CheckFaceDir()
    {
        animator.FlipY(IsWallClimbing && rb.velocity.y < 0);

        if (_ledgeGrabbing || LedgeClimbing || IsWallGrabbing || IsWallClimbing)
            return;
        if (OnWall && ((_input.RawH > 0 && _isFaceRight) || (_input.RawH < 0 && !_isFaceRight)))
            return;
        if ((_input.RawH < 0 && _isFaceRight) || (_input.RawH > 0 && !_isFaceRight))
            Turn();
    }

    private void Turn()
    {
        tf.localScale = new Vector2(-tf.localScale.x, tf.localScale.y);
        _isFaceRight = !_isFaceRight;
        foreach (var ray in wallRays)
            ray.FlipDirX();
        // foreach (var ray in platformRays)
        //     ray.FlipDirX();
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
    private bool JumpAirTiming => isJumping && Mathf.Abs(rb.velocity.y) < jumpAirTimeYSpeed;

    private IEnumerator Jump()
    {
        _lastPressJumpTimer = 0;
        // reset (falling) velocity for coyote time 
        rb.velocity = new Vector2(rb.velocity.x, 0);
        // start jump animation
        animator.SetJumpAnimation();
        // wait for animation frames before jump up
        yield return new WaitForSeconds(timeBeforeJump);
        // jump
        rb.AddForce(jumpForce * Vector2.up, ForceMode2D.Impulse);
        isJumping = true;
        _lastOnGroundTimer = 0;
    }

    [Space(10)]
    [SerializeField][Range(0f, 1f)] private float _jumpCutVelocityMult = 0.5f;
    [SerializeField][Min(1f)] private float _jumpCutGravityMult = 1.4f;
    private bool _isJumpCutting = false;

    private void JumpCut()
    {
        if (rb.velocity.y <= 0)
            return;
        rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * _jumpCutVelocityMult);
        _isJumpCutting = true;
    }

#endregion

#region Fall

    [Header("Fall")]
    [SerializeField][Min(0f)] private float maxFallSpeed = 5f;
    [SerializeField][Min(1f)] private float fallGravityMult = 1.5f;
    [SerializeField][Min(0f)] private float maxSlideSpeed = 1.5f;
    [SerializeField][Min(0f)] private float slideGravityMult = 0.5f;
    private const float GravityScale = 1;

    private void SetGravity()
    {
        if (_ledgeGrabbing || LedgeClimbing || IsWallGrabbing || IsWallClimbing)
            rb.gravityScale = 0;
        else if (IsWallJumping)
            rb.gravityScale = GravityScale * wallJumpGravityMult;
        else if (rb.velocity.y < 0)
            rb.gravityScale = GravityScale * (OnWall ? slideGravityMult : fallGravityMult);
        else if (_isJumpCutting)
            rb.gravityScale = GravityScale * _jumpCutGravityMult;
        else if (JumpAirTiming)
            rb.gravityScale = GravityScale * jumpAirTimeGravityMult;
        else
            rb.gravityScale = GravityScale;

        if (!OnWall && rb.velocity.y < -maxFallSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        else if (OnWall && rb.velocity.y < -maxSlideSpeed)
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
    private Vector2 _ledgeClimbPosBefore;
    private Vector2 _ledgeClimbPosAfter;
    private bool _canGrabLedge = true;
    private bool _ledgeGrabbing = false;
    public bool LedgeClimbing { get; private set; } = false;

    private void LedgeGrab()
    {
        // get corner position
        Vector2 cornerPos = GetLedgeCornerPos();
        // set ledge grab and climb position        
        _ledgeClimbPosBefore.x = cornerPos.x + ((cornerPos.x == tf.position.x) ? defaultOffsetBefore.x : offsetBefore.x) * (_isFaceRight ? 1 : -1);
        _ledgeClimbPosBefore.y = cornerPos.y + ((cornerPos.y == tf.position.y) ? defaultOffsetBefore.y : offsetBefore.y);
        _ledgeClimbPosAfter.x = cornerPos.x + ((cornerPos.x == tf.position.x) ? defaultOffsetAfter.x : offsetAfter.x) * (_isFaceRight ? 1 : -1);
        _ledgeClimbPosAfter.y = cornerPos.y + ((cornerPos.y == tf.position.y) ? defaultOffsetAfter.y : offsetAfter.y);
        // grab
        _canGrabLedge = false;
        _ledgeGrabbing = true;
        isJumping = false;
        _isJumpCutting = false;
        _isRunning = false;
        IsWallClimbing = false;
        IsWallGrabbing = false;
        IsWallJumping = false;
        // start grab animation
        animator.SetLedgeGrabAnimation();
    }

    private void LedgeClimb()
    {
        _ledgeGrabbing = false;
        LedgeClimbing = true;
    }

    // for animation event
    public void LedgeClimbOver()
    {
        tf.position = _ledgeClimbPosAfter;
        cl.isTrigger = false;
        _ledgeGrabbing = false;
        LedgeClimbing = false;
        _canGrabLedge = true;
    }

#endregion

#region Wall

    [Header("Wall")]
    [SerializeField] private float wallClimbSpeed = 1.5f;
    [SerializeField][Range(0f, 1f)] private float wallJumpBufferTime = 0.1f;
    [SerializeField] private float timeBeforeWallJump = 0.05f;
    [SerializeField] private Vector2 wallJumpPower = new(9.8f, 4f);
    [SerializeField] private float wallJumpGravityMult = 1.35f;
    [SerializeField] private float wallJumpMoveAcceleration = 0.5f;
    [SerializeField] private float wallJumpMoveDecceleration = 0.6f;
    public bool IsWallGrabbing { get; private set; } = false;
    public bool IsWallClimbing { get; private set; } = false;
    public bool IsWallJumping { get; private set; } = false;
    [SerializeField] private Vector2 wallGrabOffset = new(-0.2f, 0f);
    [SerializeField] private Vector2 defaultWallGrabOffset = new(-0.25f, 0f);

    private IEnumerator StartWallGrab()
    {
        // get wall grab pos
        Vector2 wallGrabPos = tf.position;
        wallGrabPos.x = GetWallX();
        if (wallGrabPos.x == tf.position.x)
            wallGrabPos += defaultWallGrabOffset * (_isFaceRight ? 1 : -1);
        else
            wallGrabPos += wallGrabOffset * (_isFaceRight ? 1 : -1);

        // start wall grab
        _lastOnGroundTimer = 0;
        IsWallGrabbing = true;
        IsWallJumping = false;
        rb.velocity = Vector2.zero;
        tf.position = wallGrabPos;

        // for back to wall grab from ledge grab
        cl.isTrigger = false;
        _ledgeGrabbing = false;
        LedgeClimbing = false;

        if (!_canGrabLedge)
        {
            yield return new WaitForSeconds(delayForLedgeGrab);
            _canGrabLedge = true;
        }
        else yield break;
    }

    private void WallClimb()
    {
        if (_input.RawV == 0)
        {
            IsWallClimbing = false;
            return;
        }

        IsWallClimbing = true;
        rb.velocity = new Vector2(0, _input.V * wallClimbSpeed);
    }

    private IEnumerator WallJump()
    {
        _lastPressJumpTimer = 0;
        _lastOnGroundTimer = 0;

        Vector2 force = new((_isFaceRight ? -1 : 1) * wallJumpPower.x, wallJumpPower.y);

        yield return new WaitForSeconds(timeBeforeWallJump);
        IsWallJumping = true;
        IsWallGrabbing = false;
        IsWallClimbing = false;
        isJumping = false;
        _isRunning = false;

        rb.velocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

#endregion
}