using System.Collections;
using UnityEngine;

public class EnemyAI : MonoBehaviour
{
    [Header("Movimentação")]
    [SerializeField] private float speed = 2f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Detecção e Ataque")]
    [SerializeField] private Transform shootPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float shootCooldown = 1.5f;
    [SerializeField] private float shootDetectionRange = 10f;
    [SerializeField] private LayerMask playerLayer;

    private Rigidbody2D rb;
    private Animator anim;

    private bool isFacingRight = true;
    private bool isDead = false;
    private float shootTimer;
    private bool waitingToShoot = false;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        anim = GetComponentInChildren<Animator>();

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

        transform.position += new Vector3(
            (isFacingRight ? 1 : -1) * speed * Time.deltaTime,
            0,
            0
        );

        RaycastHit2D groundHit = Physics2D.Raycast(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        if (!groundHit)
            TurnAround();
    }

    private void DetectPlayer()
    {
        Vector2 shootDir = isFacingRight ? Vector2.right : Vector2.left;

        RaycastHit2D hit = Physics2D.Raycast(
            shootPoint.position,
            shootDir,
            shootDetectionRange,
            playerLayer
        );

        if (hit.collider != null && shootTimer <= 0 && !waitingToShoot)
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
