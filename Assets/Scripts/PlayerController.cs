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
    // for animator access
    public Rigidbody2D RB => rb;

#region Mono Behaviour

    void Start()
    {
        cl.enabled = true;
        cl_s.enabled = false;
        cl.isTrigger = false;
        rb.gravityScale = GravityScale;
        stat.ResetAll();
    }

    void Update()
    {
        timer.Update();
        CheckSurrounding();
        GetInput();

        #region Check States

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

        if ((stat.Sneaking && (input.RawV >= 0 || !OnGround || stat.WallGrabbing)) || (!stat.Sneaking && cl.enabled == false))
            EndSneak();

        #endregion

        #region Player Movements

        if (stat.LedgeGrabbing || stat.LedgeClimbing)
            goto ledge_grabbing;

        if (!stat.Sneaking && !stat.WallGrabbing && sur.OnGround && input.RawV < 0)
            StartSneak();

        if (!stat.WallGrabbing && !stat.WallClimbing)
            Move();

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

        if (sur.LedgeDetected && stat.CanGrabLedge)
            StartLedgeGrab();

        #endregion

        if (stat.WallGrabbing && !stat.WallClimbing)
            rb.velocity = Vector2.zero;

ledge_grabbing:
        if (stat.LedgeGrabbing || stat.LedgeClimbing)
        {
            cl.isTrigger = true;
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
    private struct Surrounding {
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
        sur.LedgeDetected = (ledgeCheck.Detect(groundLayer) && !ledgeCheckTop.Detect(groundLayer)) || ((stat.WallGrabbing || stat.WallClimbing) && !wallToLedgeCheck.Detect(groundLayer) && wallBottomCheck.Detect(groundLayer));
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
    private struct Input {
        public float H, V, RawH, RawV;
        public bool MoveDown;
        public bool JumpDown, JumpUp;
        public bool WallPress;
    }

    private Input input;
    [Header("Input")]
    [SerializeField][Range(0f, 1f)] private float doubleMoveDownCheckTime = 0.5f;
    private bool _canCheckDoubleMoveDown = true;
    private int _moveDownCount = 0;

    private void GetInput()
    {
        input.H = UnityEngine.Input.GetAxis("Horizontal");
        input.V = UnityEngine.Input.GetAxis("Vertical");
        input.RawH = UnityEngine.Input.GetAxisRaw("Horizontal");
        input.RawV = UnityEngine.Input.GetAxisRaw("Vertical");
        input.MoveDown = UnityEngine.Input.GetButtonDown("Horizontal");
        input.JumpDown = UnityEngine.Input.GetButtonDown("Jump");
        input.JumpUp = UnityEngine.Input.GetButtonUp("Jump");
        input.WallPress = UnityEngine.Input.GetButton("Wall");

        if (input.MoveDown)
            _moveDownCount++;

        if (_moveDownCount == 1 && _canCheckDoubleMoveDown)
        {
            timer.LastPressMove = doubleMoveDownCheckTime;
            StartCoroutine(DetectDoubleMoveDown());
        }

        if (input.RawH == 0)
            stat.Running = false;

        if (input.JumpDown)
            timer.LastPressJump = jumpBufferTime;

        if (input.WallPress && input.JumpDown)
            timer.LastPressWallJump = wallJumpBufferTime;
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

        public void ResetAll()
        {
            FaceRight = true;
            CanGrabLedge = true;
            Running = false;
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
    public bool IsLedgeClimbing => stat.LedgeClimbing;
    public bool IsWallGrabbing => stat.WallGrabbing;
    public bool IsWallClimbing => stat.WallClimbing;
    public bool IsWallJumping => stat.WallJumping;

#endregion

#region Move

    [Header("Move")]
    [SerializeField][Min(0f)] private float maxWalkSpeed = 1.6f;
    [SerializeField][Min(0f)] private float maxRunSpeed = 2.5f;
    [SerializeField][Min(0f)] private float maxSneakSpeed = 1f;
    [SerializeField][Min(0f)] private float moveAcceleration = 1.2f, moveDecceleration = 1.6f;
    [SerializeField][Min(0f)] private float frictionAmount = 0.5f;
    [Space(10)]
    [SerializeField][Min(1f)] private float jumpAirTimeMoveSpeedMult = 1.5f;

    private void Move()
    {
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
        cl.enabled = false;
        cl_s.enabled = true;
    }

    private void EndSneak()
    {
        stat.Sneaking = false;
        cl.enabled = true;
        cl_s.enabled = false;
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
        // jump
        rb.AddForce(jumpForce * Vector2.up, ForceMode2D.Impulse);
        stat.Reset();
        stat.Jumping = true;
        timer.LastOnGround = 0;
    }

    [Space(10)]
    [SerializeField][Range(0f, 1f)] private float jumpCutVelocityMult = 0.5f;
    [SerializeField][Min(1f)] private float jumpCutGravityMult = 1.4f;

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
    [SerializeField][Min(0f)] private float slideGravityMult = 0.5f;
    private const float GravityScale = 1;

    private void SetGravity()
    {
        if (stat.LedgeGrabbing || stat.LedgeClimbing || stat.WallGrabbing || stat.WallClimbing)
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
        // limit max fall/slide speed
        if (!sur.OnWall && rb.velocity.y < -maxFallSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        else if (sur.OnWall && rb.velocity.y < -maxSlideSpeed)
            rb.velocity = new Vector2(rb.velocity.x, -maxSlideSpeed);
    }

#endregion

#region Ledge

    [Header("Ledge")]
    [SerializeField] private float delayForLedgeGrab = 0.5f;
    [SerializeField] private Vector2 offsetBefore = new(0.063f, 0.22f);
    [SerializeField] private Vector2 offsetAfter = new(0.35f, 0.4f);
    [SerializeField] private Vector2 defaultOffsetBefore = new(0.146f, 0.22f);
    [SerializeField] private Vector2 defaultOffsetAfter = new(0.35f, 0.4f);
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
        cl.isTrigger = false;
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
    [SerializeField] private Vector2 wallJumpPower = new(9.5f, 4f);
    [SerializeField] private float wallJumpGravityMult = 1.35f;
    [SerializeField] private float wallJumpMoveAcceleration = 0.5f;
    [SerializeField] private float wallJumpMoveDecceleration = 0.6f;
    // isWallGrabing == isWallCLimbing == true when wall climbing
    [Space(10)]
    [SerializeField] private Vector2 wallGrabOffset = new(-0.2f, 0f);
    [SerializeField] private Vector2 defaultWallGrabOffset = new(-0.25f, 0f);

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
        cl.isTrigger = false;
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
        timer.LastPressJump = 0;
        timer.LastOnGround = 0;

        Vector2 force = new((stat.FaceRight ? -1 : 1) * wallJumpPower.x, wallJumpPower.y);

        yield return new WaitForSeconds(timeBeforeWallJump);
        stat.WallJumping = true;
        stat.WallGrabbing = false;
        stat.WallClimbing = false;
        stat.Jumping = false;
        stat.Running = false;
        stat.Sneaking = false;

        rb.velocity = Vector2.zero;
        rb.AddForce(force, ForceMode2D.Impulse);
    }

#endregion
}