using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Roguelike.Events;
using Roguelike.Run;
using Roguelike.Stats;
using Roguelike.Upgrades;

public class characterMovement : MonoBehaviour
{
    [Header("Stats")]
    [Tooltip("Kit base do jogador (valores upgradáveis). Os stats finais chegam por PlayerStatsChanged.")]
    [SerializeField] private PlayerBaseStats baseStats;

    [Header("Horizontal Movement (Ground)")]
    [SerializeField] private float deceleration = 70f;
    [SerializeField] private float turnSpeed = 60f;

    [Header("Horizontal Movement (Air)")]
    [SerializeField] private float airDeceleration = 40f;
    [SerializeField] private float airTurnSpeed = 25f;

    [Header("Vertical Jump Settings")]
    [SerializeField] private float timeToApex = 0.4f;
    [SerializeField] private float upwardMovementMultiplier = 1f;
    [SerializeField] private float downwardMovementMultiplier = 1f;
    [SerializeField] private float speedYLimit = 20f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float airJumpHeightMultiplier = 1f;
    [SerializeField] private float hangTime = 0.2f;

    [Header("Wall Jump Settings")]
    [SerializeField] private float wallCheckDistance = 0.1f;
    [SerializeField] private Vector2 wallJumpForce = new Vector2(5f, 9f);
    [SerializeField] private float wallJumpTime = 0.2f;
    [SerializeField] private LayerMask wallLayer;

    // Valores upgradáveis: vêm do PlayerStatsSnapshot (ApplyStats); o valor base mora no PlayerBaseStats
    private float maxSpeed;
    private float acceleration;
    private float airAcceleration;
    private float jumpHeight;
    private float coyoteTime;
    private float wallSlideSpeed;
    private int maxAirJumps;
    private bool canWallGrab;

    /// <summary>Último snapshot de stats aplicado ao jogador (verificação e HUD).</summary>
    public PlayerStatsSnapshot Stats { get; private set; }

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

    [SerializeField] public bool isTouchingRightWall;
    [SerializeField] public bool isTouchingLeftWall;
    [SerializeField] public bool isWallSliding;
    public bool isWallJumping;
    private float wallJumpingCounter;

    public Rigidbody2D rb;
    private float originalJumpSpeed;
    private Collider2D playerCollider;
    private bool canMove = true;
    private bool isDead = false;

    private float initialJumpY;
    private bool isGroundJump;
    private float hangTimer = 0f;
    private float previousVelocityY;

    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    public GameController gameController;

    // Henrique: Referência ao script do gancho
    private GrapplingHook grapplingHook;

    void Awake()
    {
        // Henrique: Pega o componente do Gancho (antes do snapshot inicial, que também configura o gancho)
        grapplingHook = GetComponent<GrapplingHook>();

        // Kit base: o jogador se inicializa sozinho até o RunManager emitir PlayerStatsChanged (ADR-16)
        if (baseStats == null)
        {
            Debug.LogWarning("[characterMovement] - PlayerBaseStats não atribuído; usando os valores padrão do kit base.");
            baseStats = ScriptableObject.CreateInstance<PlayerBaseStats>();
        }
        ApplyStats(new PlayerStats(baseStats).CreateSnapshot());

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

    private void OnEnable()
    {
        EventBus<LevelTimeExpired>.Subscribe(HandleLevelTimeExpired);
        EventBus<PlayerStatsChanged>.Subscribe(HandlePlayerStatsChanged);
    }

    private void OnDisable()
    {
        EventBus<LevelTimeExpired>.Unsubscribe(HandleLevelTimeExpired);
        EventBus<PlayerStatsChanged>.Unsubscribe(HandlePlayerStatsChanged);
    }

    private void HandlePlayerStatsChanged(PlayerStatsChanged evt)
    {
        ApplyStats(evt.Stats);
    }

    /// <summary>
    /// Único ponto de entrada de stats no jogador: copia o snapshot para os campos de movimento,
    /// repassa ao GrapplingHook e recalcula o pulo. Idempotente (aplica o snapshot inteiro).
    /// </summary>
    private void ApplyStats(PlayerStatsSnapshot s)
    {
        if (!s.IsValid)
        {
            Debug.LogWarning("[characterMovement] - Snapshot de stats inválido recebido; ignorado.");
            return;
        }

        Stats = s;

        maxSpeed = s.Get(StatType.MaxSpeed);
        acceleration = s.Get(StatType.Acceleration);
        airAcceleration = s.Get(StatType.AirAcceleration);
        jumpHeight = s.Get(StatType.JumpHeight);
        coyoteTime = s.Get(StatType.CoyoteTime);
        wallSlideSpeed = s.Get(StatType.WallSlideSpeed);
        maxAirJumps = s.GetInt(StatType.MaxAirJumps);
        canWallGrab = s.HasAbility(AbilityFlags.WallGrab);

        // Comando direto dentro do domínio do jogador (mesmo GameObject)
        if (grapplingHook != null) grapplingHook.ApplyStats(s);

        // Antes do Start o Rigidbody ainda não existe: o Start calcula o pulo
        if (rb != null && defaultGravityScale > 0f) CalculateJumpVariables();
    }

    private void HandleLevelTimeExpired(LevelTimeExpired evt)
    {
        // Tempo esgotado trava o jogador como a morte, mas NÃO emite PlayerDied (ADR-10)
        if (isDead) return;
        LockPlayer();
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

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
        // Enquanto morto ou com o movimento desabilitado, ignora input e não avança a lógica de pulo
        if (isDead || !canMove)
        {
            horizontalInput = 0f;
            desiredJump = false;
            pressingJump = false;
            currentlyJumping = false;
            return;
        }

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

        // Só considera "no chão" quando não estiver subindo (evita reconceder coyote/pulo do chão logo após pular)
        onGround = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) && rb.linearVelocity.y <= 0.1f;

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

        // Sem a habilidade WallGrab o jogador não desliza na parede (ADR-19)
        if (canWallGrab && (isTouchingLeftWall || isTouchingRightWall) && !onGround && rb.linearVelocity.y < 0)
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
                if (canWallGrab && isWallSliding)
                {
                    WallJump();
                    desiredJump = false;
                    jumpBufferCounter = 0f;
                }
                // Sem WallGrab não há deslize (ADR-19), então não existe mais o caso "deslizando sem poder pular da parede"
                else if (canJumpAgain)
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

        // Enquanto morto ou com o movimento desabilitado, mantém o corpo parado
        if (isDead || !canMove)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Henrique: Se estiver usando o gancho, trava movimento e gravidade
        if (grapplingHook != null && grapplingHook.IsGrappling)
        {
            rb.gravityScale = 0;
            return;
        }

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

        // Consome o coyote time para evitar um pulo extra do chão logo após este pulo
        coyoteTimeCounter = 0f;

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
        // Mesmas fórmulas de antes, agora centralizadas (e testadas) no JumpPhysics
        originalJumpSpeed = JumpPhysics.JumpSpeed(jumpHeight, timeToApex);
        gravMultiplier = JumpPhysics.GravityMultiplier(jumpHeight, timeToApex, Physics2D.gravity.magnitude, defaultGravityScale);
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

            // Legado (D2): moedas saem na 4.1
            PlayerData data = PlayerData.Instance;
            if (data != null)
            {
                data.coinCount++;
                data.SaveData(); // Salva as alterações
            }
            else
            {
                Debug.LogWarning("Coin coletada, mas PlayerData não encontrado!");
            }
        }
    }

    public void ResetToSpawnPoint()
    {
        // Encontra o spawn point na cena
        GameObject spawnPoint = GameObject.FindGameObjectWithTag("SpawnPoint");

        if (spawnPoint != null)
        {
            // Reposiciona o jogador no spawn point
            transform.position = spawnPoint.transform.position;

            // Reseta a velocidade do rigidbody
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }
    // Trava o jogador (usado pela morte e pelo tempo esgotado): cancela o gancho e zera a velocidade
    private void LockPlayer()
    {
        isDead = true;

        // Henrique: Cancela o gancho, se estiver ativo, e libera o controle do movimento
        if (grapplingHook != null)
        {
            grapplingHook.CancelGrapple();
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void Die(DeathCause cause)
    {
        // Die() pode ser chamado mais de uma vez (várias balas no mesmo frame): garante idempotência
        if (isDead) return;
        LockPlayer();

        Debug.Log($"[characterMovement] - Jogador morreu: {cause}");

        EventBus<PlayerDied>.Raise(new PlayerDied(cause));
    }


    public void DisableMovement()
    {
        canMove = false;
    }
    public void EnableMovement()
    {
        canMove = true;
    }
}