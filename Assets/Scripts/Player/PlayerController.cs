using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private Transform tf;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D normalCollider; // normal collider
    [SerializeField] private Collider2D sneakCollider; // sneak collider
    [SerializeField] private PlayerAnimator animator;
    // for animator access
    public Rigidbody2D RB => rb;

#region Mono Behaviour

    void Start()
    {
        normalCollider.enabled = true;
        sneakCollider.enabled = false;
        normalCollider.isTrigger = false;
        rb.gravityScale = GravityScale;
        stat.ResetAll();
        _dashCooling = false;
        _canDashCount = 1;
        animator.StopEmitTrail();
    }

    void Update()
    {
        timer.Update();
        CheckSurrounding();
        GetInput();

        #region Check States

        if (input.RawH == 0)
            stat.Running = false;

        if (input.JumpDown)
            timer.LastPressJump = jumpBufferTime;

        if (input.WallPress && input.JumpDown)
            timer.LastPressWallJump = wallJumpBufferTime;

        if (stat.Jumping && rb.velocity.y <= 0)
        {
            stat.Jumping = false;
            stat.JumpCutting = false;
        }

        if (sur.OnGround && !stat.Jumping)
            timer.LastOnGround = coyoteTime;

        if (stat.WallGrabbing && (!sur.OnWall || !input.WallPress || stat.LedgeGrabbing || stat.LedgeClimbing))
        {
            stat.WallGrabbing = false;
            stat.WallClimbing = false;
        }

        if (stat.WallJumping && (rb.velocity.x == 0 || rb.velocity.y <= 0))
            stat.WallJumping = false;

        stat.JumpAirTiming = stat.Jumping && Mathf.Abs(rb.velocity.y) < jumpAirTimeYSpeed;

        if ((stat.Sneaking && (input.RawV >= 0 || !OnGround || stat.WallGrabbing)) || (!stat.Sneaking && normalCollider.enabled == false))
            EndSneak();

        if (sur.OnGround || sur.OnWall)
            _canDashCount = 1;

        if (Mathf.Abs(rb.velocity.x) < 0.01f)
            stat.Dashing = false;

        #endregion

        if (stat.LedgeGrabbing || stat.LedgeClimbing || stat.Dashing)
            goto skip_movement;

        #region Player Movements

        if (input.DashDown && !stat.Dashing && !_dashCooling && _canDashCount > 0)
        {
            StartCoroutine(Dash());
            goto skip_movement;
        }

        if (!stat.Sneaking && !stat.WallGrabbing && sur.OnGround && input.RawV < 0)
            StartSneak();

        if (!stat.WallGrabbing && !stat.WallClimbing && !stat.Jumping && !stat.WallJumping && timer.LastOnGround > 0 && timer.LastPressJump > 0)
            StartCoroutine(Jump());

        if (input.JumpUp && (stat.Jumping || stat.WallJumping) && rb.velocity.y > 0)
            JumpCut();

        if (!stat.WallGrabbing && input.WallPress && !stat.WallJumping)
        {
            if (sur.OnWall)
                StartCoroutine(StartWallGrab());
            else if (sur.BackWallDetected)
            {
                Turn();
                StartCoroutine(StartWallGrab());
            }
        }

        if (stat.WallGrabbing && !stat.WallJumping)
            WallClimb();

        if (stat.WallGrabbing && timer.LastPressWallJump > 0 && !stat.Jumping && !stat.WallJumping)
            StartCoroutine(WallJump());

skip_movement:

        if (sur.LedgeDetected && stat.CanGrabLedge)
            StartLedgeGrab();

        #endregion

        if (stat.WallGrabbing && !stat.WallClimbing)
            rb.velocity = Vector2.zero;

        if (stat.LedgeGrabbing || stat.LedgeClimbing)
        {
            normalCollider.isTrigger = true;
            tf.position = (Vector3)_ledgeClimbPosBefore;
            rb.velocity = Vector2.zero;
            // rb.gravityScale = 0;

            if (stat.LedgeGrabbing)
            {
                if (input.JumpDown)
                    StartLedgeClimb();
                if (input.RawV < 0)
                    StartCoroutine(StartWallGrab());
            }
        }

        SetGravity();
        CheckFaceDir();

        animator.SetAnimation();
    }

    void FixedUpdate()
    {
        if (stat.LedgeGrabbing || stat.LedgeClimbing || stat.Dashing)
            return;

        if (!stat.WallGrabbing && !stat.WallClimbing)
            Move();
    }

#endregion

#region Timer

    [System.Serializable]
    private struct Timer
    {
        public float LastPressMove; // for walk speed up to run
        public float LastPressJump; // for jump buffer
        public float LastPressWallJump; // for wall jump buffer
        public float LastOnGround; // for coyote time
        public void Update()
        {
            LastPressMove -= Time.deltaTime;
            LastPressJump -= Time.deltaTime;
            LastPressWallJump -= Time.deltaTime;
            LastOnGround -= Time.deltaTime;
        }
    }

    private Timer timer;

#endregion

#region Surrounding

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

    [System.Serializable]
    private struct Surrounding
    {
        public bool OnGround;
        public bool OnWall;
        public bool BackWallDetected;
        public bool LedgeDetected;
    }

    private Surrounding sur;
    // for animator access
    public bool OnGround => sur.OnGround;
    public bool OnWall => sur.OnWall;

    private void CheckSurrounding()
    {
        sur.OnGround = groundCheck.Detect(groundLayer);
        sur.OnWall = wallCheck.Detect(groundLayer);
        sur.BackWallDetected = backWallCheck.Detect(groundLayer);
        sur.LedgeDetected = (ledgeCheck.Detect(groundLayer) && !ledgeCheckTop.Detect(groundLayer)) ||
        ((stat.WallGrabbing || stat.WallClimbing || (sur.OnWall && rb.velocity.y < 0)) &&
          !wallToLedgeCheck.Detect(groundLayer) && wallBottomCheck.Detect(groundLayer));
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
        return new Vector2(GetWallX(), GetPlatformY());
    }

#endregion

#region Input

    [System.Serializable]
    private struct Input
    {
        public float H, V, RawH, RawV;
        public bool MoveDown;
        public bool JumpDown, JumpUp;
        public bool WallPress;
        public bool DashDown;
        public void Update()
        {
            H = UnityEngine.Input.GetAxis("Horizontal");
            V = UnityEngine.Input.GetAxis("Vertical");
            RawH = UnityEngine.Input.GetAxisRaw("Horizontal");
            RawV = UnityEngine.Input.GetAxisRaw("Vertical");
            MoveDown = UnityEngine.Input.GetButtonDown("Horizontal");
            JumpDown = UnityEngine.Input.GetButtonDown("Jump");
            JumpUp = UnityEngine.Input.GetButtonUp("Jump");
            WallPress = UnityEngine.Input.GetButton("Wall");
            DashDown = UnityEngine.Input.GetButtonDown("Dash");
        }
    }

    private Input input;
    [Header("Input")]
    [SerializeField][Range(0f, 1f)] private float doubleMoveDownCheckTime = 0.5f;
    private bool _canCheckDoubleMoveDown = true;
    private int _moveDownCount = 0;

    private void GetInput()
    {
        input.Update();

        if (input.MoveDown)
            _moveDownCount++;

        if (_moveDownCount == 1 && _canCheckDoubleMoveDown)
        {
            timer.LastPressMove = doubleMoveDownCheckTime;
            StartCoroutine(DetectDoubleMoveDown());
        }
    }

    private IEnumerator DetectDoubleMoveDown()
    {
        _canCheckDoubleMoveDown = false;
        while (timer.LastPressMove > 0)
        {
            if (_moveDownCount == 2)
            {
                stat.Running = true;
                break;
            }
            yield return null;
        }
        _moveDownCount = 0;
        _canCheckDoubleMoveDown = true;
    }

#endregion

#region Stat

    [System.Serializable]
    private struct Stat
    {
        public bool FaceRight;
        public bool Running;
        public bool Sneaking;
        public bool Jumping;
        public bool JumpAirTiming;
        public bool JumpCutting;
        public bool CanGrabLedge;
        public bool LedgeGrabbing;
        public bool LedgeClimbing;
        public bool WallGrabbing;
        public bool WallClimbing;
        public bool WallJumping;
        public bool Dashing;

        public void ResetAll()
        {
            FaceRight = true;
            CanGrabLedge = true;
            Running = false;
            Dashing = false;
            Reset();
        }

        public void Reset()
        {
            Sneaking = false;
            Jumping = false;
            JumpAirTiming = false;
            JumpCutting = false;
            LedgeGrabbing = false;
            LedgeClimbing = false;
            WallGrabbing = false;
            WallClimbing = false;
            WallJumping = false;
        }
    }

    private Stat stat;
    // for animator access
    public bool IsSneaking => stat.Sneaking;
    public bool IsLedgeGrabbing => stat.LedgeGrabbing;
    public bool IsLedgeClimbing => stat.LedgeClimbing;
    public bool IsWallGrabbing => stat.WallGrabbing;
    public bool IsWallClimbing => stat.WallClimbing;
    public bool IsWallJumping => stat.WallJumping;
    public bool IsDashing => stat.Dashing;

#endregion

#region Move

    [Header("Move")]
    [SerializeField][Min(0f)] private float maxWalkSpeed = 1.6f;
    [SerializeField][Min(0f)] private float maxRunSpeed = 2.5f;
    [SerializeField][Min(0f)] private float maxSneakSpeed = 1f;
    [SerializeField][Min(0f)] private float moveAcceleration = 12f, moveDecceleration = 16f;
    [SerializeField][Min(0f)] private float frictionAmount = 1f;
    [Space(10)]
    [SerializeField][Min(1f)] private float jumpAirTimeMoveSpeedMult = 1.5f;

    private void Move()
    {
        // calculate move speed
        float targetSpeed, accelerate;
        if (stat.Running)
            targetSpeed = input.H * maxRunSpeed;
        else if (stat.Sneaking)
            targetSpeed = input.H * maxSneakSpeed;
        else
            targetSpeed = input.H * maxWalkSpeed;
        if (stat.WallJumping)
            accelerate = ((input.RawH > 0 && rb.velocity.x > 0) || (input.H < 0 && rb.velocity.x < 0)) ? wallJumpMoveAcceleration : wallJumpMoveDecceleration;
        else
            accelerate = ((input.RawH > 0 && rb.velocity.x > 0) || (input.H < 0 && rb.velocity.x < 0)) ? moveAcceleration : moveDecceleration;
        // faster when air time
        if (stat.JumpAirTiming)
        {
            targetSpeed *= jumpAirTimeMoveSpeedMult;
            accelerate *= jumpAirTimeMoveSpeedMult;
        }
        float speedDiff = targetSpeed - rb.velocity.x;
        float movement = speedDiff * accelerate;
        // friction
        float friction = 0;
        if (timer.LastOnGround > 0 && input.RawH == 0)
            friction = Mathf.Min(Mathf.Abs(rb.velocity.x), frictionAmount) * Mathf.Sign(rb.velocity.x);
        rb.AddForce((movement - friction) * Vector2.right, ForceMode2D.Force);
    }

    private void StartSneak()
    {
        stat.Reset();
        stat.CanGrabLedge = true;
        stat.Running = false;
        stat.Sneaking = true;
        normalCollider.enabled = false;
        sneakCollider.enabled = true;
    }

    private void EndSneak()
    {
        stat.Sneaking = false;
        normalCollider.enabled = true;
        sneakCollider.enabled = false;
    }

    private void CheckFaceDir()
    {
        animator.FlipY(stat.WallClimbing && rb.velocity.y < 0);

        if (stat.LedgeGrabbing || stat.LedgeClimbing || stat.WallGrabbing || stat.WallClimbing)
            return;
        if (sur.OnWall && ((input.RawH > 0 && stat.FaceRight) || (input.RawH < 0 && !stat.FaceRight)))
            return;
        if ((input.RawH < 0 && stat.FaceRight) || (input.RawH > 0 && !stat.FaceRight))
            Turn();
    }

    private void Turn()
    {
        tf.localScale = new Vector2(-tf.localScale.x, tf.localScale.y);
        stat.FaceRight = !stat.FaceRight;
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

    private IEnumerator Jump()
    {
        timer.LastPressJump = 0;
        // reset (falling) velocity for coyote time 
        rb.velocity = new Vector2(rb.velocity.x, 0);
        // start jump animation
        animator.SetJumpAnimation();
        // wait for animation frames before jump up
        yield return new WaitForSeconds(timeBeforeJump);

        if (stat.Dashing)
        {
            _velocityBeforeDash.y = jumpForce;
            yield break;
        }

        Debug.Log("Jump");
        // jump
        rb.AddForce(jumpForce * Vector2.up, ForceMode2D.Impulse);
        stat.Reset();
        stat.Jumping = true;
        timer.LastOnGround = 0;
    }

    [Space(10)]
    [SerializeField][Range(0f, 1f)] private float jumpCutVelocityMult = 0.75f;
    [SerializeField][Min(1f)] private float jumpCutGravityMult = 1.5f;

    private void JumpCut()
    {
        if (rb.velocity.y <= 0)
            return;
        rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * jumpCutVelocityMult);
        stat.Reset();
        stat.JumpCutting = true;
    }

#endregion

#region Fall

    [Header("Fall")]
    [SerializeField][Min(0f)] private float maxFallSpeed = 5f;
    [SerializeField][Min(1f)] private float fallGravityMult = 1.5f;
    [SerializeField][Min(0f)] private float maxSlideSpeed = 1.5f;
    [SerializeField][Min(0f)] private float fasterSlideSpeed = 3.5f;
    [SerializeField][Min(0f)] private float slideGravityMult = 0.5f;
    private const float GravityScale = 1;

    private void SetGravity()
    {
        if (stat.Dashing || stat.LedgeGrabbing || stat.LedgeClimbing || stat.WallGrabbing || stat.WallClimbing)
            rb.gravityScale = 0;
        else if (stat.WallJumping)
            rb.gravityScale = GravityScale * wallJumpGravityMult;
        else if (rb.velocity.y < 0)
            rb.gravityScale = GravityScale * (sur.OnWall ? slideGravityMult : fallGravityMult);
        else if (stat.JumpCutting)
            rb.gravityScale = GravityScale * jumpCutGravityMult;
        else if (stat.JumpAirTiming)
            rb.gravityScale = GravityScale * jumpAirTimeGravityMult;
        else
            rb.gravityScale = GravityScale;
        // faster slide
        if (sur.OnWall && input.RawV < 0 && !input.WallPress)
            rb.velocity = new Vector2(rb.velocity.x, -fasterSlideSpeed);
        // limit max fall/slide speed
        else if (!sur.OnWall && rb.velocity.y < -maxFallSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        else if (sur.OnWall && rb.velocity.y < -maxSlideSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxSlideSpeed);
    }

#endregion

#region Ledge

    [Header("Ledge")]
    [SerializeField] private float delayForLedgeGrab = 0.5f;
    [SerializeField] private Vector2 offsetBefore = new(0.063f, 0.22f);
    [SerializeField] private Vector2 offsetAfter = new(0.25f, 0.4f);
    [SerializeField] private Vector2 defaultOffsetBefore = new(0.146f, -0.1f);
    [SerializeField] private Vector2 defaultOffsetAfter = new(0.2f, 0.2f);
    private Vector2 _ledgeClimbPosBefore;
    private Vector2 _ledgeClimbPosAfter;

    private void StartLedgeGrab()
    {
        // get corner position
        Vector2 cornerPos = GetLedgeCornerPos();
        // set ledge grab and climb position        
        _ledgeClimbPosBefore.x = cornerPos.x + ((cornerPos.x == tf.position.x) ? defaultOffsetBefore.x : offsetBefore.x) * (stat.FaceRight ? 1 : -1);
        _ledgeClimbPosBefore.y = cornerPos.y + ((cornerPos.y == tf.position.y) ? defaultOffsetBefore.y : offsetBefore.y);
        _ledgeClimbPosAfter.x = cornerPos.x + ((cornerPos.x == tf.position.x) ? defaultOffsetAfter.x : offsetAfter.x) * (stat.FaceRight ? 1 : -1);
        _ledgeClimbPosAfter.y = cornerPos.y + ((cornerPos.y == tf.position.y) ? defaultOffsetAfter.y : offsetAfter.y);
        // grab
        stat.CanGrabLedge = false;
        stat.LedgeGrabbing = true;
        stat.Jumping = false;
        stat.JumpCutting = false;
        stat.Running = false;
        stat.Sneaking = false;
        stat.WallClimbing = false;
        stat.WallGrabbing = false;
        stat.WallJumping = false;
        // start grab animation
        animator.SetLedgeGrabAnimation();
    }

    private void StartLedgeClimb()
    {
        stat.LedgeGrabbing = false;
        stat.LedgeClimbing = true;
    }

    // for animation event
    public void LedgeClimbOver()
    {
        tf.position = _ledgeClimbPosAfter;
        normalCollider.isTrigger = false;
        stat.LedgeGrabbing = false;
        stat.LedgeClimbing = false;
        stat.CanGrabLedge = true;
    }

#endregion

#region Wall

    [Header("Wall")]
    [SerializeField] private float wallClimbSpeed = 1.5f;
    [Space(5)]
    [SerializeField][Range(0f, 1f)] private float wallJumpBufferTime = 0.1f;
    [SerializeField] private float timeBeforeWallJump = 0.05f;
    [SerializeField] private Vector2 wallJumpOffset = new(0.03f, 0.01f);
    [SerializeField] private Vector2 wallJumpPower = new(9f, 4f);
    [SerializeField] private float wallJumpGravityMult = 1.4f;
    [SerializeField] private float wallJumpMoveAcceleration = 12f;
    [SerializeField] private float wallJumpMoveDecceleration = 5.5f;
    // isWallGrabing == isWallCLimbing == true when wall climbing
    [Space(10)]
    [SerializeField] private Vector2 wallGrabOffset = new(-0.2f, 0f);
    [SerializeField] private Vector2 defaultWallGrabOffset = Vector2.zero;

    private IEnumerator StartWallGrab()
    {
        // get wall grab pos
        Vector2 wallGrabPos = tf.position;
        wallGrabPos.x = GetWallX();
        if (wallGrabPos.x == tf.position.x)
            wallGrabPos += defaultWallGrabOffset * (stat.FaceRight ? 1 : -1);
        else
            wallGrabPos += wallGrabOffset * (stat.FaceRight ? 1 : -1);

        // start wall grab
        timer.LastOnGround = 0;
        stat.WallGrabbing = true;
        stat.WallJumping = false;
        stat.Running = false;
        stat.Sneaking = false;
        rb.velocity = Vector2.zero;
        tf.position = wallGrabPos;

        // for back to wall grab from ledge grab
        normalCollider.isTrigger = false;
        stat.LedgeGrabbing = false;
        stat.LedgeClimbing = false;

        if (!stat.CanGrabLedge)
        {
            yield return new WaitForSeconds(delayForLedgeGrab);
            stat.CanGrabLedge = true;
        }
        else yield break;
    }

    private void WallClimb()
    {
        if (input.RawV == 0)
        {
            stat.WallClimbing = false;
            return;
        }

        stat.WallGrabbing = true;
        stat.WallClimbing = true;
        rb.velocity = new Vector2(0, input.V * wallClimbSpeed);
    }

    private IEnumerator WallJump()
    {
        timer.LastPressWallJump = 0;
        timer.LastPressJump = 0;
        timer.LastOnGround = 0;

        Vector2 force = new((stat.FaceRight ? -1 : 1) * wallJumpPower.x, wallJumpPower.y);

        tf.position += new Vector3(wallJumpOffset.x * (stat.FaceRight ? -1 : 1), wallJumpOffset.y, 0);
        rb.velocity = Vector2.zero;

        yield return new WaitForSeconds(timeBeforeWallJump);
        stat.Sneaking = false;
        stat.Jumping = false;
        stat.Running = false;
        stat.WallGrabbing = false;
        stat.WallClimbing = false;
        stat.WallJumping = true;

        if (stat.Dashing)
        {
            _velocityBeforeDash = force;
            yield break;
        }

        rb.velocity = Vector2.zero;
        Turn();
        rb.AddForce(force, ForceMode2D.Impulse);

        Debug.Log($"before {rb.velocity}");
        yield return new WaitForSeconds(0.1f);
        Debug.Log($"after {rb.velocity}");
    }

#endregion

#region Dash

    [Header("Dash")]
    [SerializeField] private float timeBeforeDash = 0.02f;
    [SerializeField] private Vector2 wallDashOffset = new(0.03f, 0f);
    [SerializeField] private float dashSpeed = 3.33f;
    [SerializeField] private float wallDashSpeed = 3.33f;
    [SerializeField] private float dashTime = 0.3f;
    [SerializeField] private float runDashTime = 0.5f;
    private bool _dashCooling = false;
    private int _canDashCount = 1;
    private Vector2 _velocityBeforeDash;

    private IEnumerator Dash()
    {
        timer.LastPressWallJump = 0;
        timer.LastPressJump = 0;
        timer.LastPressMove = 0;
        _canDashCount--;
        _dashCooling = true;
        stat.Reset();

        // record origin velocity;
        _velocityBeforeDash = rb.velocity;
        // calculate dash speed
        if (sur.OnWall)
            Turn();
        float speed = stat.FaceRight ? 1 : -1;
        if (sur.OnWall)
            speed *= wallDashSpeed;
        else
            speed *= dashSpeed;

        tf.position += new Vector3(wallDashOffset.x * (stat.FaceRight ? -1 : 1), wallDashOffset.y, 0);
        // start dash animation
        animator.SetDashAnimation();

        yield return new WaitForSeconds(timeBeforeDash);

        // dash
        stat.Dashing = true;
        animator.StartEmitTrail();
        rb.velocity = new Vector2(speed, 0);

        yield return new WaitForSeconds(stat.Running ? runDashTime : dashTime);
        // end dash
        animator.StopEmitTrail();
        rb.velocity = _velocityBeforeDash;
        stat.Dashing = false;
        _dashCooling = false;
    }

#endregion
}