using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    private enum State { Ready, Shooting, Grappling, Cooldown }
    private State currentState = State.Ready;

    [Header("Configurações de Física")]
    [SerializeField] private float grappleRadius = 15f;
    [SerializeField] private float grapplePullSpeed = 20f;
    [SerializeField] private float launchBoostForce = 35f;
    [SerializeField] private float hookTravelSpeed = 60f;
    [SerializeField] private float minDistanceToFinish = 1.0f;

    [Header("Camadas")]
    [SerializeField] private LayerMask whatIsGrappleable;
    [SerializeField] private LayerMask whatIsObstacle;
    [SerializeField] private float grappleCooldown = 0.5f;
    [SerializeField] private float maxGrappleDuration = 3f;
    [SerializeField] private float stuckCheckInterval = 0.3f;
    [SerializeField] private float stuckDistanceThreshold = 0.05f;

    [Header("Visuais (Pixel Art)")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform hookTipTransform;
    [SerializeField] private LineRenderer ropeRenderer;

    [Header("Referências")]
    [SerializeField] private PlayerData playerData; // Referência ao PlayerData

    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction grappleAction;

    private Vector2 grappleTargetPosition;
    private Vector2 launchDirectionVector;
    private float cooldownTimer;

    // Henrique: Controle de timeout e detecção de "gancho travado"
    private float grappleElapsedTime;
    private float stuckCheckTimer;
    private float lastDistanceToTarget;
    private bool missingReferenceWarningLogged;

    public bool IsGrappling => currentState == State.Grappling;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        // Busca automaticamente o PlayerData se não foi atribuído
        if (playerData == null)
        {
            playerData = FindFirstObjectByType<PlayerData>();
            if (playerData == null)
            {
                Debug.LogWarning("PlayerData não encontrado. O gancho estará desativado.");
            }
        }

        if (playerInput != null)
        {
            var actionMap = playerInput.actions.FindActionMap("Player");
            if (actionMap != null) grappleAction = actionMap.FindAction("Grapple");
        }

        // Garante que os visuais comecem invisíveis
        if (hookTipTransform != null) hookTipTransform.gameObject.SetActive(false);
        if (ropeRenderer != null) ropeRenderer.enabled = false;
    }

    void Update()
    {
        // Verifica se o gancho está habilitado pelo PlayerData
        bool canGrapple = playerData != null && playerData.canGrapplingHook;

        if (!canGrapple)
        {
            // Se estava ativo, desativa os visuais
            if (currentState != State.Ready && currentState != State.Cooldown)
            {
                ResetGrapplingHook();
            }
            return;
        }

        // 1. Cooldown
        if (currentState == State.Cooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0) currentState = State.Ready;
        }

        // 2. Input
        if (grappleAction != null && grappleAction.WasPressedThisFrame() && currentState == State.Ready)
        {
            FindTargetAndShoot();
        }

        // 3. Gancho Viajando (Shooting)
        if (currentState == State.Shooting)
        {
            hookTipTransform.position = Vector2.MoveTowards(hookTipTransform.position, grappleTargetPosition, hookTravelSpeed * Time.deltaTime);

            UpdateVisuals();

            // Se chegou ao ponto alvo, mude para o estado Grappling
            if (Vector2.Distance(hookTipTransform.position, grappleTargetPosition) < 0.2f)
            {
                hookTipTransform.position = grappleTargetPosition;
                currentState = State.Grappling;
            }
        }

        // 4. Puxando Jogador (Grappling)
        if (currentState == State.Grappling)
        {
            if (hookTipTransform.position != (Vector3)grappleTargetPosition)
            {
                hookTipTransform.position = grappleTargetPosition;
            }

            UpdateVisuals();

            // Henrique: Timeout de segurança para não travar o jogador presa no gancho para sempre
            grappleElapsedTime += Time.deltaTime;
            if (grappleElapsedTime >= maxGrappleDuration)
            {
                CancelGrapple();
                return;
            }

            // Henrique: Se a distância até o alvo não estiver diminuindo, considera o gancho "travado"
            float currentDistance = rb != null ? Vector2.Distance(rb.position, hookTipTransform.position) : Vector2.Distance(transform.position, hookTipTransform.position);
            stuckCheckTimer += Time.deltaTime;
            if (stuckCheckTimer >= stuckCheckInterval)
            {
                if (Mathf.Abs(lastDistanceToTarget - currentDistance) < stuckDistanceThreshold)
                {
                    CancelGrapple();
                    return;
                }

                lastDistanceToTarget = currentDistance;
                stuckCheckTimer = 0f;
            }
        }
    }

    void FixedUpdate()
    {
        // Verifica se o gancho está habilitado
        bool canGrapple = playerData != null && playerData.canGrapplingHook;
        if (!canGrapple || rb == null) return;

        if (currentState == State.Grappling)
        {
            float distance = Vector2.Distance(rb.position, hookTipTransform.position);

            if (distance <= minDistanceToFinish)
            {
                LaunchPlayer();
            }
            else
            {
                Vector2 direction = (hookTipTransform.position - transform.position).normalized;
                rb.linearVelocity = direction * grapplePullSpeed;
            }
        }
    }

    private void FindTargetAndShoot()
    {
        // Henrique: Sem esses pontos de referência não há como disparar o gancho com segurança
        if (hookTipTransform == null || firePoint == null)
        {
            if (!missingReferenceWarningLogged)
            {
                Debug.LogWarning("GrapplingHook: hookTipTransform ou firePoint não atribuídos no inspetor. Gancho desativado.");
                missingReferenceWarningLogged = true;
            }
            return;
        }

        Collider2D[] targets = Physics2D.OverlapCircleAll(transform.position, grappleRadius, whatIsGrappleable);
        if (targets.Length == 0) return;

        float closestDist = float.MaxValue;
        Vector2 bestPoint = Vector2.zero;
        bool found = false;

        foreach (Collider2D target in targets)
        {
            Vector2 targetPos = target.bounds.center;
            Vector2 dir = (targetPos - (Vector2)transform.position).normalized;
            float dist = Vector2.Distance(transform.position, targetPos);

            if (!Physics2D.Raycast(transform.position, dir, dist, whatIsObstacle))
            {
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestPoint = targetPos;
                    launchDirectionVector = dir;
                    found = true;
                }
            }
        }

        if (found)
        {
            grappleTargetPosition = bestPoint;
            currentState = State.Shooting;

            // Henrique: Reseta os contadores de timeout/stuck para esta nova tentativa de gancho
            grappleElapsedTime = 0f;
            stuckCheckTimer = 0f;
            lastDistanceToTarget = float.MaxValue;

            // ATIVAÇÃO DOS VISUAIS
            if (ropeRenderer != null) ropeRenderer.enabled = true;
            if (hookTipTransform != null) hookTipTransform.gameObject.SetActive(true);

            hookTipTransform.position = firePoint.position;
        }
    }

    private void LaunchPlayer()
    {
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(launchDirectionVector * launchBoostForce, ForceMode2D.Impulse);

        // Garante que os visuais sumam ao final
        ResetVisuals();

        currentState = State.Cooldown;
        cooldownTimer = grappleCooldown;
    }

    private void ResetVisuals()
    {
        if (ropeRenderer != null) ropeRenderer.enabled = false;
        if (hookTipTransform != null) hookTipTransform.gameObject.SetActive(false);
    }

    private void ResetGrapplingHook()
    {
        ResetVisuals();
        currentState = State.Ready;
        cooldownTimer = 0f;
    }

    /// <summary>
    /// Henrique: Cancela o gancho a força (ex: quando o jogador morre) e libera o movimento normal.
    /// </summary>
    public void CancelGrapple()
    {
        if (currentState != State.Shooting && currentState != State.Grappling) return;

        ResetVisuals();
        currentState = State.Cooldown;
        cooldownTimer = grappleCooldown;
    }

    private void UpdateVisuals()
    {
        if (ropeRenderer == null || !ropeRenderer.enabled) return;

        Vector3 startPos = firePoint.position;   // Mão (Player)
        Vector3 endPos = hookTipTransform.position; // Ponto de gancho (FIXO no estado Grappling)

        // 1. Line Renderer: Desenha a linha entre os dois pontos
        ropeRenderer.SetPosition(0, startPos);
        ropeRenderer.SetPosition(1, endPos);

        // 2. Rotação do HookTip
        Vector2 direction = endPos - startPos;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Quaternion targetRotation = Quaternion.Euler(0, 0, angle - 45f);

        // Aplica rotação na ponta do hook.
        hookTipTransform.rotation = targetRotation;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, grappleRadius);
    }
}