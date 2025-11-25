using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class characterMovement : MonoBehaviour
{
    [Header("Horizontal Movement (Ground)")]
    [SerializeField] private float maxSpeed = 10f;
    [SerializeField] private float acceleration = 50f;
    [SerializeField] private float deceleration = 70f;
    [SerializeField] private float turnSpeed = 60f;

    [Header("Horizontal Movement (Air)")]
    [SerializeField] private float airAcceleration = 30f;
    [SerializeField] private float airDeceleration = 40f;
    [SerializeField] private float airTurnSpeed = 25f;

    [Header("Vertical Jump Settings")]
    [SerializeField] private float jumpHeight = 4f;
    [SerializeField] private float timeToApex = 0.4f;
    [SerializeField] private float upwardMovementMultiplier = 1f;
    [SerializeField] private float downwardMovementMultiplier = 1f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private float speedYLimit = 20f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float airJumpHeightMultiplier = 1f;
    [SerializeField] private float hangTime = 0.2f;

    [Header("Wall Jump Settings")]
    [SerializeField] private float wallCheckDistance = 0.1f;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(5f, 9f);
    [SerializeField] private float wallJumpTime = 0.2f;
    [SerializeField] private LayerMask wallLayer;


    private bool enableWallJump;
    private int maxAirJumps;
    private float agility;
    private float strenght;


    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer = 1;

    [Header("Ceiling Check")]
    [SerializeField] private Transform ceilingCheck;
    [SerializeField] private float ceilingCheckRadius = 0.15f;
    [SerializeField] private LayerMask ceilingLayer = 1;

    [Header("Debug")]
    [SerializeField] public float currentHorizontalVelocity;
    [SerializeField] public float horizontalInput;
    public Animator playerAnimator;

    [SerializeField] public float jumpSpeed;
    [SerializeField] private float defaultGravityScale;
    [SerializeField] public float gravMultiplier;

    [SerializeField] public bool canJumpAgain = false;
    [SerializeField] private bool desiredJump;
    [SerializeField] private float jumpBufferCounter;
    [SerializeField] private float coyoteTimeCounter = 0f;
    [SerializeField] private bool pressingJump;
    [SerializeField] public bool onGround;
    [SerializeField] private bool currentlyJumping;
    [SerializeField] private int airJumpsUsed = 0;
    [SerializeField] private float jumpStartTime;

    [SerializeField] private bool isTouchingRightWall;
    [SerializeField] private bool isTouchingLeftWall;
    [SerializeField] public bool isWallSliding;
    public bool isWallJumping;
    private float wallJumpingCounter;

    public Rigidbody2D rb;
    private float originalJumpSpeed;
    private Collider2D playerCollider;

    private float initialJumpY;
    private bool isGroundJump;
    private float hangTimer = 0f;
    private float previousVelocityY;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    public GameController gameController;
    public PlayerData playerData;

    // Henrique: Referência ao script do gancho
    private GrapplingHook grapplingHook;
    // até aqui

    void Awake()
    {
        playerInput = GetComponent<PlayerInput>();

        if (playerInput == null)
        {
            Debug.LogError("PlayerInput component não encontrado!");
            enabled = false;
            return;
        }

        var actionMap = playerInput.actions.FindActionMap("Player");
        moveAction = actionMap.FindAction("Movement");
        jumpAction = actionMap.FindAction("Jump");
    }

    void Start()
    {
        enableWallJump = PlayerData.Instance.canWallJump;
        maxAirJumps = PlayerData.Instance.maxAirJumps;
        agility = PlayerData.Instance.agility;
        strenght = PlayerData.Instance.strenght;
        jumpHeight += 0.1f*strenght;
        maxSpeed += 0.5f*agility;
        acceleration += agility;


        rb = GetComponent<Rigidbody2D>();
        
        // Henrique: Pega o componente do Gancho
        grapplingHook = GetComponent<GrapplingHook>();
        // até aqui

        playerCollider = GetComponent<Collider2D>();
        playerAnimator = GetComponent<Animator>();
        defaultGravityScale = rb.gravityScale;
        CalculateJumpVariables();

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = gc.transform;
        }

        if (ceilingCheck == null)
        {
            GameObject cc = new GameObject("CeilingCheck");
            cc.transform.SetParent(transform);
            cc.transform.localPosition = new Vector3(-0.05f, 0.3f, 0);
            ceilingCheck = cc.transform;
        }
        
        if (ceilingLayer.value == 0) ceilingLayer = groundLayer;
        if (wallLayer.value == 0) wallLayer = groundLayer;

        coyoteTimeCounter = coyoteTime;
        jumpBufferCounter = 0f;
    }

    void Update()
    {
        horizontalInput = moveAction.ReadValue<float>();

        if (jumpAction.WasPressedThisFrame())
        {
            desiredJump = true;
            jumpBufferCounter = jumpBufferTime;
            pressingJump = true;
            jumpStartTime = Time.time;
        }

        if (jumpAction.WasReleasedThisFrame())
        {
            pressingJump = false;
            currentlyJumping = false;
        }


        onGround = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        CheckWalls();

        if (onGround)
        {
            coyoteTimeCounter = coyoteTime;
            airJumpsUsed = 0;
            isWallJumping = false;
            wallJumpingCounter = 0f;
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        if ((isTouchingLeftWall || isTouchingRightWall) && !onGround && rb.linearVelocity.y < 0)
        {
            isWallSliding = true;
        }
        else
        {
            isWallSliding = false;
        }

        bool canCoyoteJump = coyoteTimeCounter > 0f;
        bool canAirJump = airJumpsUsed < maxAirJumps;
        canJumpAgain = onGround || canCoyoteJump || canAirJump;

        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
            if (desiredJump)
            {
                if (enableWallJump && isWallSliding)
                {
                    WallJump();
                    desiredJump = false;
                    jumpBufferCounter = 0f;
                }
                else if (canJumpAgain && !(isWallSliding && !enableWallJump))
                {
                    Jump();
                    desiredJump = false;
                    jumpBufferCounter = 0f;
                }
            }
        }

        if (isWallJumping)
        {
            wallJumpingCounter -= Time.deltaTime;
            if (wallJumpingCounter <= 0f) isWallJumping = false;
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Henrique: Se estiver usando o gancho, trava movimento e gravidade
        if (grapplingHook != null && grapplingHook.IsGrappling)
        {
            rb.gravityScale = 0;
            return; 
        }
        // até aqui

        float currentVelocityX = rb.linearVelocity.x;
        float currentVelocityY = rb.linearVelocity.y;

        previousVelocityY = currentVelocityY;

        // Horizontal Movement
        float accelRate = onGround ? acceleration : airAcceleration;
        float decelRate = onGround ? deceleration : airDeceleration;
        float turnRate = onGround ? turnSpeed : airTurnSpeed;

        float targetSpeed = horizontalInput * maxSpeed;
        float speedDifference = targetSpeed - currentVelocityX;

        float horizontalMovement = 0f;
        float selectedRate;

        if (horizontalInput == 0) selectedRate = decelRate;
        else if (Mathf.Sign(horizontalInput) != Mathf.Sign(currentVelocityX) && currentVelocityX != 0) selectedRate = turnRate;
        else selectedRate = accelRate;

        float movement = selectedRate * Time.fixedDeltaTime;

        if (speedDifference > 0) horizontalMovement = Mathf.Min(movement, speedDifference);
        else if (speedDifference < 0) horizontalMovement = Mathf.Max(-movement, speedDifference);

        if (isWallJumping) horizontalMovement = 0f;

        float newVelocityX = currentVelocityX + horizontalMovement;
        currentHorizontalVelocity = newVelocityX;

        // Vertical Movement
        if (isWallSliding)
        {
            if (currentVelocityY < -wallSlideSpeed) currentVelocityY = -wallSlideSpeed;
        }

        bool hitCeiling = Physics2D.OverlapCircle(ceilingCheck.position, ceilingCheckRadius, ceilingLayer);

        if (currentlyJumping && pressingJump && isGroundJump)
        {
            if (transform.position.y >= initialJumpY + jumpHeight || hitCeiling)
            {
                currentVelocityY = 0;
                currentlyJumping = false;
                rb.gravityScale = defaultGravityScale * gravMultiplier;
            }
            else
            {
                currentVelocityY = jumpSpeed;
                rb.gravityScale = 0;
            }
        }
        else if (currentlyJumping && !pressingJump && isGroundJump)
        {
            currentVelocityY = 0;
            if (hangTimer == 0) hangTimer = hangTime;
        }
        else
        {
            if (currentVelocityY > 0) currentVelocityY *= upwardMovementMultiplier;
            else if (currentVelocityY < 0) currentVelocityY *= downwardMovementMultiplier;

            rb.gravityScale = defaultGravityScale * gravMultiplier;
        }

        if (currentlyJumping && !isGroundJump && previousVelocityY > 0 && currentVelocityY <= 0 && hangTimer == 0)
        {
            hangTimer = hangTime;
        }

        if (hangTimer > 0)
        {
            hangTimer -= Time.fixedDeltaTime;
            currentVelocityY = 0;
            rb.gravityScale = 0;
        }

        currentVelocityY = Mathf.Clamp(currentVelocityY, -speedYLimit, speedYLimit);
        rb.linearVelocity = new Vector2(newVelocityX, currentVelocityY);
    }

    private void CheckWalls()
    {
        if (playerCollider == null) return;

        Vector2 center = playerCollider.bounds.center;
        float halfWidth = playerCollider.bounds.extents.x;

        Vector2 rightOrigin = new Vector2(center.x + halfWidth, center.y);
        Vector2 leftOrigin = new Vector2(center.x - halfWidth, center.y);

        isTouchingRightWall = Physics2D.Raycast(rightOrigin, Vector2.right, wallCheckDistance, wallLayer);
        isTouchingLeftWall = Physics2D.Raycast(leftOrigin, Vector2.left, wallCheckDistance, wallLayer);
    }

    private void WallJump()
    {
        isWallSliding = false;
        isWallJumping = true;
        wallJumpingCounter = wallJumpTime;
        airJumpsUsed = 0;
        currentlyJumping = true;
        isGroundJump = false;

        float jumpDirectionX = isTouchingRightWall ? -1f : 1f;
        rb.linearVelocity = new Vector2(jumpDirectionX * wallJumpForce.x, wallJumpForce.y);
        jumpSpeed = wallJumpForce.y;
    }

    private void Jump()
    {
        initialJumpY = transform.position.y;
        isGroundJump = onGround || coyoteTimeCounter > 0f;
        float thisJumpSpeed = originalJumpSpeed;
        bool isAboutToAirJump = !onGround && coyoteTimeCounter <= 0f;
        bool isDoubleJumpAvailable = isAboutToAirJump && airJumpsUsed < maxAirJumps;

        if (isAboutToAirJump)
        {
            thisJumpSpeed *= airJumpHeightMultiplier;
            isGroundJump = false;
        }
        
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, thisJumpSpeed);
        currentlyJumping = true;

        if (isAboutToAirJump) airJumpsUsed = Mathf.Min(airJumpsUsed + 1, maxAirJumps);

        if (playerAnimator != null)
        {
            if (isDoubleJumpAvailable) playerAnimator.SetTrigger("DoubleJumpTrigger");
            else playerAnimator.SetTrigger("JumpTrigger");
        }

        jumpSpeed = thisJumpSpeed;
    }

    private void CalculateJumpVariables()
    {
        originalJumpSpeed = jumpHeight / timeToApex;
        float calculatedGravity = (2f * jumpHeight) / (Mathf.Pow(timeToApex, 2));
        gravMultiplier = calculatedGravity / Physics2D.gravity.magnitude / defaultGravityScale;
        jumpSpeed = originalJumpSpeed;
    }

    void OnValidate()
    {
        if (rb != null && defaultGravityScale > 0) CalculateJumpVariables();
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = onGround ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
        if (ceilingCheck != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(ceilingCheck.position, ceilingCheckRadius);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Coin"))
        {
            Destroy(other.gameObject);
            gameController.coinCount++;
        }
    }
}