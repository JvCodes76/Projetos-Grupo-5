using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [SerializeField] private float speed = 6f;
    private Vector2 direction;

    [SerializeField] private LayerMask groundLayer;

    public void SetDirection(Vector2 dir)
    {
        direction = dir.normalized;
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime);

        Destroy(gameObject, 5f);
    }

    private void OnTriggerEnter2D(Collider2D col)
    {
        // --- Detecta PLAYER por tag OU layer ---
        if (col.CompareTag("Player") || col.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            Debug.Log("jogador morreu");
            Destroy(gameObject);
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