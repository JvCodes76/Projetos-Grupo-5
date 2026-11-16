using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Movimentação")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("Folga à frente do colisor para detectar parede (somada ao passo do frame).")]
    [SerializeField] private float wallCheckDistance = 0.05f;

    [Header("Detecção e Ataque")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float shootCooldown = 1.5f;
    [SerializeField] private float shootDetectionRange = 10f;
    [SerializeField] private LayerMask playerLayer;

    private Rigidbody2D rb;
    private Animator anim;
    private Collider2D bodyCollider;

    private bool isFacingRight = true;
    private bool isDead = false;
    private float shootTimer;
    private bool waitingToShoot = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        anim = GetComponentInChildren<Animator>();
        bodyCollider = GetComponent<Collider2D>();
        if (bodyCollider == null) bodyCollider = GetComponentInChildren<Collider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Update()
    {
        if (isDead) return;

        shootTimer -= Time.deltaTime;

        Move();
        DetectPlayer();
    }

    private void Move()
    {
        if (waitingToShoot) return;

        // Checa ANTES de andar: o corpo é cinemático e movido por transform, então nada impede de entrar na parede.
        if (!CanWalk(isFacingRight))
        {
            // Só vira se o outro lado tiver caminho; sem caminho dos dois lados fica parado (antes virava todo frame).
            if (CanWalk(!isFacingRight))
                TurnAround();
            return;
        }

        transform.position += new Vector3(
            (isFacingRight ? 1 : -1) * speed * Time.deltaTime,
            0,
            0
        );
    }

    /// <summary>Há chão à frente e nenhuma parede (groundLayer) no passo deste frame na direção pedida.</summary>
    private bool CanWalk(bool towardsRight)
    {
        float dir = towardsRight ? 1f : -1f;

        // O groundCheck é filho e espelha com o scale: para o lado oposto ao atual, reflete em torno do pivô.
        Vector2 groundOrigin = groundCheck.position;
        if (towardsRight != isFacingRight)
            groundOrigin.x = 2f * transform.position.x - groundOrigin.x;

        if (!Physics2D.Raycast(groundOrigin, Vector2.down, groundCheckDistance, groundLayer))
            return false;

        Vector2 wallOrigin = transform.position;
        if (bodyCollider != null)
        {
            Bounds bounds = bodyCollider.bounds;
            wallOrigin = new Vector2(bounds.center.x + dir * bounds.extents.x, bounds.center.y);
        }

        float wallDistance = wallCheckDistance + speed * Time.deltaTime;
        return !Physics2D.Raycast(wallOrigin, new Vector2(dir, 0f), wallDistance, groundLayer);
    }

    private void DetectPlayer()
    {
        Vector2 shootDir = isFacingRight ? Vector2.right : Vector2.left;

        // Chão/parede entram na máscara para bloquear a linha de visão: só atira se o PRIMEIRO acerto for o jogador.
        RaycastHit2D hit = Physics2D.Raycast(
            shootPoint.position,
            shootDir,
            shootDetectionRange,
            playerLayer | groundLayer
        );

        bool seesPlayer = hit.collider != null && (playerLayer.value & (1 << hit.collider.gameObject.layer)) != 0;

        if (seesPlayer && shootTimer <= 0 && !waitingToShoot)
        {
            anim.SetTrigger("IsShoot");
            shootTimer = shootCooldown;

            StartCoroutine(DelayedShoot(0.33f));
        }
    }

    private IEnumerator DelayedShoot(float delay)
    {
        waitingToShoot = true;
        yield return new WaitForSeconds(delay);

        if (!isDead)
            Shoot();

        waitingToShoot = false;
    }

    private void Shoot()
    {
        Vector2 shootDir = isFacingRight ? Vector2.right : Vector2.left;

        GameObject b = Instantiate(bulletPrefab, shootPoint.position, Quaternion.identity);
        b.GetComponent<EnemyBullet>().SetDirection(shootDir);
    }

    private void TurnAround()
    {
        isFacingRight = !isFacingRight;

        // Agora giramos o PAI do inimigo
        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (isFacingRight ? 1 : -1);
        transform.localScale = scale;
    }

    public void Die()
    {
        isDead = true;
        anim.SetBool("IsDead", true);

        Destroy(gameObject, 0.5f);
    }

    private void OnDrawGizmos()
    {
        if (shootPoint == null) return;

        Gizmos.color = Color.red;

        Vector3 dir = (isFacingRight ? Vector3.right : Vector3.left);

        // No editor, enquanto jogo não está rodando, usamos o scale do Transform
        if (!Application.isPlaying && transform.localScale.x < 0)
            dir = Vector3.left;
        else if (!Application.isPlaying)
            dir = Vector3.right;

        Gizmos.DrawLine(
            shootPoint.position,
            shootPoint.position + dir * shootDetectionRange
        );
    }
}
