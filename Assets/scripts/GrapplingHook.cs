using UnityEngine;
using UnityEngine.InputSystem;

public class GrapplingHook : MonoBehaviour
{
    // Henrique: Estados do Gancho para controlar o ciclo Puxar -> Lançar
    private enum State { Ready, Grappling, Cooldown }
    private State currentState = State.Ready;
    // até aqui

    [Header("Configurações do Gancho")]
    [SerializeField] private float grappleRadius = 15f;          // Raio de busca por alvos
    // Henrique: Novos parâmetros para Puxar e Lançar
    [SerializeField] private float grapplePullSpeed = 20f;       // Velocidade de puxada do jogador
    [SerializeField] private float launchBoostForce = 35f;       // Força de lançamento após a puxada
    [SerializeField] private float minDistanceToGrapplePoint = 0.5f; // Distância para considerar que "chegou" ao ponto
    // até aqui
    [SerializeField] private LayerMask whatIsGrappleable;        // Layer dos objetos que podem ser agarrados
    [SerializeField] private LayerMask whatIsObstacle;           // Layer dos objetos que bloqueiam a visão (Paredes, Chão)
    [SerializeField] private float grappleCooldown = 1.0f;       // Tempo de espera entre usos

    [Header("Componentes")]
    private Rigidbody2D rb;
    private PlayerInput playerInput;
    private InputAction grappleAction;
    
    // Estado do Gancho
    private Vector2 grapplePoint; 
    // Henrique: Armazena o vetor de direção do Puxar (usado para o Lançamento)
    private Vector2 launchDirectionVector;
    // até aqui
    
    // Propriedade pública para o CharacterMovement verificar o estado de puxada
    public bool IsGrappling => currentState == State.Grappling;
    private float cooldownTimer;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();

        if (playerInput != null)
        {
            var actionMap = playerInput.actions.FindActionMap("Player");
            if(actionMap != null)
            {
                grappleAction = actionMap.FindAction("Grapple");
            }

            if (grappleAction == null)
            {
                Debug.LogError("Ação 'Grapple' não encontrada no Input Actions! Adicione-a ou verifique o nome.");
            }
        }
    }

    void Update()
    {
        // Gerenciamento do Cooldown
        if (currentState == State.Cooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0)
            {
                currentState = State.Ready;
            }
        }

        // Input e Início do Grapple
        if (grappleAction != null && grappleAction.WasPressedThisFrame() && currentState == State.Ready)
        {
            StartGrapple();
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Lógica de Puxada
        if (currentState == State.Grappling)
        {
            float distance = Vector2.Distance(rb.position, grapplePoint);

            // 1. Verifica se chegou perto o suficiente do ponto
            if (distance <= minDistanceToGrapplePoint)
            {
                LaunchPlayer(); // Lança o jogador
            }
            else
            {
                // 2. Puxa o jogador em direção ao ponto (movimento contínuo)
                Vector2 direction = (grapplePoint - rb.position).normalized;
                
                // Aplica a velocidade de puxada no Rigidbody
                rb.linearVelocity = direction * grapplePullSpeed; 
            }
        }
    }

    private void StartGrapple()
    {
        // 1. Busca todos os alvos dentro do raio
        Collider2D[] targetsInRadius = Physics2D.OverlapCircleAll(transform.position, grappleRadius, whatIsGrappleable);
        
        if (targetsInRadius.Length == 0)
        {
            currentState = State.Cooldown;
            cooldownTimer = grappleCooldown;
            return;
        }

        float closestDistance = float.MaxValue;
        Vector2 bestGrapplePoint = Vector2.zero;
        bool validTargetFound = false;
        Vector2 playerPosition = rb.position;

        // 2. Checa a Linha de Visão (Line of Sight - LOS) para o alvo mais próximo
        foreach (Collider2D target in targetsInRadius)
        {
            Vector2 targetPosition = target.bounds.center; 
            Vector2 direction = (targetPosition - playerPosition).normalized;
            float distance = Vector2.Distance(playerPosition, targetPosition);
            
            // Raycast para verificar se há OBSTÁCULOS bloqueando
            Vector2 rayOrigin = playerPosition + direction * 0.1f;
            RaycastHit2D losCheck = Physics2D.Raycast(rayOrigin, direction, distance - 0.1f, whatIsObstacle);
            
            if (losCheck.collider == null)
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestGrapplePoint = targetPosition;
                    // Henrique: Salva a direção de puxada para uso no lançamento
                    launchDirectionVector = direction; 
                    // até aqui
                    validTargetFound = true;
                }
            }
        }

        // 3. Inicia o estado de Grappling (Puxada)
        if (validTargetFound)
        {
            grapplePoint = bestGrapplePoint;
            currentState = State.Grappling;
        }
        else
        {
            currentState = State.Cooldown;
            cooldownTimer = grappleCooldown;
        }
    }

    private void LaunchPlayer()
    {
        if (rb == null) return;

        // Zera a velocidade da puxada
        rb.linearVelocity = Vector2.zero;
        
        // Henrique: Lançamento do Jogador na mesma angulação da puxada
        // A direção de lançamento é a mesma direção de puxada (do jogador para o gancho)
        Vector2 launchDirection = launchDirectionVector;
        
        // Aplica o impulso
        rb.AddForce(launchDirection * launchBoostForce, ForceMode2D.Impulse); 
        // até aqui

        currentState = State.Cooldown;
        cooldownTimer = grappleCooldown;
    }
    
    void OnDrawGizmosSelected()
    {
        if (grappleRadius > 0)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, grappleRadius);
        }
        
        // Mostra o ponto de agarre atual
        if (currentState == State.Grappling)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(grapplePoint, 0.2f);
        }
    }
}