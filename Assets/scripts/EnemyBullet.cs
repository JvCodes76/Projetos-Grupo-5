using UnityEngine;
using Roguelike.Run;

public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    private Vector2 direction;

    [SerializeField] private LayerMask groundLayer;

    private bool hasHit = false;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    private void Start()
    {
        Destroy(gameObject, 5f);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            if (!hasHit)
            {
                hasHit = true;

                var player = col.GetComponentInParent<characterMovement>();
                if (player != null)
                {
                    player.Die(DeathCause.EnemyProjectile);
                }
                else
                {
                    Debug.LogWarning("[EnemyBullet] - characterMovement não encontrado no Player atingido!");
                }
            }
            Destroy(gameObject);
            return;
        }

        // chão
        if (((1 << col.gameObject.layer) & groundLayer) != 0)
        {
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter2D(Collision2D col)
    {
        if (((1 << col.gameObject.layer) & groundLayer) != 0)
        {
            Destroy(gameObject);
        }
    }
}
