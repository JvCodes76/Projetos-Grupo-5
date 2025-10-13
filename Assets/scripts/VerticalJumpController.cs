using UnityEngine;

public class VerticalJumpController : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpHeight = 4f;              
    [SerializeField] private float timeToApex = 0.4f;            
    [SerializeField] private int maxAirJumps = 1;                
    [SerializeField] private float upwardMovementMultiplier = 1f; 
    [SerializeField] private float downwardMovementMultiplier = 1f; 
    [SerializeField] private bool variableJumpHeight = true;     
    [SerializeField] private float maxJumpHoldTime = 0.3f;       
    [SerializeField] private float jumpCutoffMultiplier = 2f;    
    [SerializeField] private float coyoteTime = 0.1f;            
    [SerializeField] private float speedYLimit = 20f;            
    [SerializeField] private float jumpBufferTime = 0.1f;        

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;              
    [SerializeField] private float groundCheckRadius = 0.2f;     
    [SerializeField] private LayerMask groundLayer = 1;          

    [Header("Calculations (Debug)")]
    [SerializeField] public float jumpSpeed;                     
    [SerializeField] private float defaultGravityScale;          
    [SerializeField] public float gravMultiplier;                

    [Header("Current State (Debug)")]
    [SerializeField] public bool canJumpAgain = false;           
    [SerializeField] private bool desiredJump;                   
    [SerializeField] private float jumpBufferCounter;            
    [SerializeField] private float coyoteTimeCounter = 0f;       
    [SerializeField] private bool pressingJump;                  
    [SerializeField] public bool onGround;                       
    [SerializeField] private bool currentlyJumping;              
    [SerializeField] private int airJumpsUsed = 0;               
    [SerializeField] private float jumpHoldTime = 0f;            

    private Rigidbody2D rb;
    private float originalJumpSpeed;                             

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("VerticalJumpController requer um Rigidbody2D no GameObject!");
            enabled = false;
            return;
        }

        
        defaultGravityScale = rb.gravityScale;
        CalculateJumpVariables();

        
        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0, -0.5f, 0);
            groundCheck = gc.transform;
        }

        
        coyoteTimeCounter = coyoteTime;
        jumpBufferCounter = 0f;
    }

    void Update()
    {
        
        if (Input.GetButtonDown("Jump"))
        {
            desiredJump = true;
            jumpBufferCounter = jumpBufferTime;
            pressingJump = true;
            jumpHoldTime = 0f;
        }

        if (Input.GetButtonUp("Jump"))
        {
            pressingJump = false;
            currentlyJumping = false; 
        }

        if (pressingJump && variableJumpHeight)
        {
            jumpHoldTime += Time.deltaTime;
            jumpHoldTime = Mathf.Clamp(jumpHoldTime, 0f, maxJumpHoldTime);
        }

        
        onGround = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        
        if (onGround)
        {
            coyoteTimeCounter = coyoteTime;
            airJumpsUsed = 0; 
        }
        else
        {
            coyoteTimeCounter -= Time.deltaTime;
        }

        
        bool canCoyoteJump = coyoteTimeCounter > 0f;
        bool canAirJump = airJumpsUsed < maxAirJumps;
        canJumpAgain = onGround || canCoyoteJump || canAirJump;

       
        if (jumpBufferCounter > 0f)
        {
            jumpBufferCounter -= Time.deltaTime;
            if (canJumpAgain && desiredJump)
            {
                Jump();
                desiredJump = false;
                jumpBufferCounter = 0f;
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        
        float currentVelY = rb.linearVelocity.y;
        if (currentVelY > 0)
        {
            
            currentVelY *= upwardMovementMultiplier;
        }
        else if (currentVelY < 0)
        {
            
            currentVelY *= downwardMovementMultiplier;
        }

        
        currentVelY = Mathf.Clamp(currentVelY, -speedYLimit, speedYLimit);

        
        if (currentlyJumping && !pressingJump && currentVelY > 0)
        {
            rb.gravityScale = defaultGravityScale * jumpCutoffMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravityScale * gravMultiplier;
        }

        
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, currentVelY);
    }

    private void Jump()
    {
        
        float thisJumpSpeed = originalJumpSpeed;
        if (variableJumpHeight && pressingJump)
        {
            
            float holdFactor = jumpHoldTime / maxJumpHoldTime;
            thisJumpSpeed *= (1f + holdFactor * 0.5f); 
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, thisJumpSpeed);
        currentlyJumping = true;
        airJumpsUsed++; 
        if (!onGround && !(coyoteTimeCounter > 0f)) airJumpsUsed = Mathf.Min(airJumpsUsed, maxAirJumps + 1); 

        
        jumpSpeed = thisJumpSpeed;
    }

    private void CalculateJumpVariables()
    {
        
        originalJumpSpeed = (2f * jumpHeight) / timeToApex;
        float calculatedGravity = (2f * jumpHeight) / (Mathf.Pow(timeToApex, 2));
        gravMultiplier = calculatedGravity / Physics2D.gravity.magnitude / defaultGravityScale;

        
        jumpSpeed = originalJumpSpeed;
    }

    
    void OnValidate()
    {
        if (rb != null && defaultGravityScale > 0)
        {
            CalculateJumpVariables();
        }
    }

    
    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = onGround ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}

