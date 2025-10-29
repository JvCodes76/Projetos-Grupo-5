using UnityEngine;

public class CharacterMovement : MonoBehaviour
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
    [SerializeField] private int maxAirJumps = 1;               
    [SerializeField] private float upwardMovementMultiplier = 1f; 
    [SerializeField] private float downwardMovementMultiplier = 1f; 
    [SerializeField] private bool variableJumpHeight = true;    
    [SerializeField] private float maxJumpHoldTime = 0.3f;      
    [SerializeField] private float jumpCutoffMultiplier = 2f;   
    [SerializeField] private float coyoteTime = 0.1f;           
    [SerializeField] private float speedYLimit = 20f;           
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float airJumpHeightMultiplier = 1f;

    [Header("Wall Jump Settings")]
    [SerializeField] private float wallCheckDistance = 0.1f;
    [SerializeField] private float wallSlideSpeed = 2f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(5f, 9f);
    [SerializeField] private float wallJumpTime = 0.2f; // Duração do bloqueio de input pós-pulo
    [SerializeField] private LayerMask wallLayer;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;             
    [SerializeField] private float groundCheckRadius = 0.2f;    
    [SerializeField] private LayerMask groundLayer = 1;        

    [Header("Horizontal Debug")]
    [SerializeField] public float currentHorizontalVelocity;
    [SerializeField] public float horizontalInput;

    [Header("Vertical Calculations (Debug)")]
    [SerializeField] public float jumpSpeed;                    
    [SerializeField] private float defaultGravityScale;        
    [SerializeField] public float gravMultiplier;               

    [Header("Vertical Current State (Debug)")]
    [SerializeField] public bool canJumpAgain = false;         
    [SerializeField] private bool desiredJump;                  
    [SerializeField] private float jumpBufferCounter;           
    [SerializeField] private float coyoteTimeCounter = 0f;      
    [SerializeField] private bool pressingJump;                 
    [SerializeField] public bool onGround;                    
    [SerializeField] private bool currentlyJumping;             
    [SerializeField] private int airJumpsUsed = 0;
    [SerializeField] private float jumpHoldTime = 0f;       

    [Header("Wall Jump State (Debug)")]
    [SerializeField] private bool isTouchingRightWall;
    [SerializeField] private bool isTouchingLeftWall;
    [SerializeField] public bool isWallSliding;
    public bool isWallJumping;
    private float wallJumpingCounter;
    public Rigidbody2D rb;
    private float originalJumpSpeed;
    public playerStats stats;
    private Collider2D playerCollider;

    void Start()
    {
        stats = GetComponent<playerStats>();

        if (stats == null)
        {
            Debug.LogError("O componente playerStats não foi encontrado no GameObject!");
            enabled = false; 
            return;
        }
        acceleration += stats.forca * 1;
        maxSpeed += stats.agilidade * 2;
        jumpHeight += stats.forca * 1;
        timeToApex -= stats.agilidade * 0.01f;
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("PlayerMovement2D requer um Rigidbody2D no GameObject!");
            enabled = false;
            return;
        }

        playerCollider = GetComponent<Collider2D>();
        if (playerCollider == null)
        {
            Debug.LogError("PlayerMovement2D requer um Collider2D no GameObject para o Wall Jump!");
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

        if (wallLayer.value == 0)
        {
            wallLayer = groundLayer;
        }
    }

    void Update()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.right, 10f, groundLayer);
        if (hit.collider != null)
        {
            Debug.Log($"Objeto invisível detectado: {hit.collider.name} em {hit.point}");
        }
        horizontalInput = Input.GetAxisRaw("Horizontal");

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
            if (desiredJump) // Se o jogador quer pular
            {
                if (isWallSliding) // Prioridade 1: Pular da Parede
                {
                    WallJump();
                    desiredJump = false;
                    jumpBufferCounter = 0f;
                }
                else if (canJumpAgain) // Prioridade 2: Pulo Padrão (Chão, Coyote, Ar)
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
            if (wallJumpingCounter <= 0f)
            {
                isWallJumping = false;
            }
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        float currentVelocityX = rb.linearVelocity.x;
        float currentVelocityY = rb.linearVelocity.y;

        // ========== MOVIMENTO HORIZONTAL ==========
        float accelRate = onGround ? acceleration : airAcceleration;
        float decelRate = onGround ? deceleration : airDeceleration;
        float turnRate = onGround ? turnSpeed : airTurnSpeed;

        float targetSpeed = horizontalInput * maxSpeed;
        float speedDifference = targetSpeed - currentVelocityX;
        
        float horizontalMovement = 0f;

        // [NOVO] Início da lógica de movimento horizontal simétrica
        float selectedRate;

        if (horizontalInput == 0)
        {
            // Se o input é 0, usamos a desaceleração
            selectedRate = decelRate; 
        }
        else if (Mathf.Sign(horizontalInput) != Mathf.Sign(currentVelocityX) && currentVelocityX != 0)
        {
            // Se o input é oposto à velocidade (virando), usamos a taxa de virada
            selectedRate = turnRate; 
        }
        else
        {
            // Caso contrário (acelerando na mesma direção), usamos a aceleração
            selectedRate = accelRate; 
        }

        // [NOVO] Aplica a taxa selecionada ('selectedRate')
        // 'movement' é a mudança máxima de velocidade que podemos aplicar neste frame
        float movement = selectedRate * Time.fixedDeltaTime;
        
        if (speedDifference > 0)
        {
            // Queremos ir mais rápido para a direita
            // Aplicamos força positiva, limitando ao máximo 'movement' e à 'speedDifference'
            horizontalMovement = Mathf.Min(movement, speedDifference);
        }
        else if (speedDifference < 0)
        {
            // Queremos ir mais rápido para a esquerda
            // Aplicamos força negativa, limitando ao máximo 'movement' e à 'speedDifference'
            horizontalMovement = Mathf.Max(-movement, speedDifference);
        }

        // [NOVO] O bloqueio do pulo na parede (isWallJumping) deve sobrescrever qualquer movimento
        if (isWallJumping)
        {
            horizontalMovement = 0f;
        }
        // [NOVO] Fim da lógica de movimento horizontal simétrica


        float newVelocityX = currentVelocityX + horizontalMovement;
        currentHorizontalVelocity = newVelocityX;

        // ========== MOVIMENTO VERTICAL ==========

        if (isWallSliding)
        {
            if (currentVelocityY < -wallSlideSpeed)
            {
                currentVelocityY = -wallSlideSpeed;
            }
        }
        
        if (currentVelocityY > 0)
        {
            currentVelocityY *= upwardMovementMultiplier;
        }
        else if (currentVelocityY < 0)
        {
            currentVelocityY *= downwardMovementMultiplier;
        }

        currentVelocityY = Mathf.Clamp(currentVelocityY, -speedYLimit, speedYLimit);

        if (currentlyJumping && !pressingJump && currentVelocityY > 0)
        {
            rb.gravityScale = defaultGravityScale * jumpCutoffMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravityScale * gravMultiplier;
        }

        float newVelocityY = currentVelocityY;

        // ========== APLICA VELOCIDADE FINAL (UMA VEZ) ==========
        rb.linearVelocity = new Vector2(newVelocityX, newVelocityY);
    }
    
    // Método para checar as paredes
    private void CheckWalls()
    {
        if (playerCollider == null) return; 

        // Pega o centro e a "metade da largura" (extents) do colisor
        Vector2 center = playerCollider.bounds.center;
        float halfWidth = playerCollider.bounds.extents.x;
        
        // Define os pontos de origem dos raios NAS BORDAS do colisor
        // (Usando o centro vertical 'center.y' do colisor)
        Vector2 rightOrigin = new Vector2(center.x + halfWidth, center.y);
        Vector2 leftOrigin = new Vector2(center.x - halfWidth, center.y);

        // Lança os raios a partir das BORDAS.
        // Agora 'wallCheckDistance' é a distância a partir da borda, corrigindo a assimetria.
        isTouchingRightWall = Physics2D.Raycast(rightOrigin, Vector2.right, wallCheckDistance, wallLayer);
        isTouchingLeftWall = Physics2D.Raycast(leftOrigin, Vector2.left, wallCheckDistance, wallLayer);

        // Desenha raios no editor para debug (opcional)
        Debug.DrawRay(rightOrigin, Vector2.right * wallCheckDistance, isTouchingRightWall ? Color.green : Color.red);
        Debug.DrawRay(leftOrigin, Vector2.left * wallCheckDistance, isTouchingLeftWall ? Color.green : Color.red);
    }

    // Método para executar o pulo da parede
    private void WallJump()
    {
        isWallSliding = false;
        isWallJumping = true;
        wallJumpingCounter = wallJumpTime; // Inicia o timer de bloqueio
        airJumpsUsed = 0; // Pulo na parede reseta os pulos aéreos
        currentlyJumping = true; // Ativa a lógica de 'jump cut-off' se o botão for solto

        // Determina a direção do pulo (oposta à parede)
        float jumpDirectionX = isTouchingRightWall ? -1f : 1f;

        // Aplica a força do pulo (define a velocidade diretamente)
        rb.linearVelocity = new Vector2(jumpDirectionX * wallJumpForce.x, wallJumpForce.y);

        // Atualiza a variável de debug 'jumpSpeed'
        jumpSpeed = wallJumpForce.y;
    }
    
    private void Jump()
    {
        float thisJumpSpeed = originalJumpSpeed;
        if (variableJumpHeight && pressingJump)
        {
            float holdFactor = jumpHoldTime / maxJumpHoldTime;
            thisJumpSpeed *= (1f + holdFactor * 0.5f);
        }

        if (!onGround && coyoteTimeCounter <= 0f)
        {
            thisJumpSpeed *= airJumpHeightMultiplier;
        }

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, thisJumpSpeed);
        currentlyJumping = true;

        if (!onGround && coyoteTimeCounter <= 0f)
        {
            airJumpsUsed = Mathf.Min(airJumpsUsed + 1, maxAirJumps);
        }
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
