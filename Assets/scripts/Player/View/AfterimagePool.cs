using UnityEngine;

/// <summary>
/// Afterimages do dash (SPEC §10.3; os anéis de velocidade do RF-60 ficam em P2): pool fixo de SpriteRenderers que
/// copiam sprite, flip e pose e somem num fade linear. Sem alocação por uso.
/// </summary>
public sealed class AfterimagePool : MonoBehaviour
{
    private const int PoolSize = 6;

    [SerializeField] private int sortingOrderOffset = -1;

    private SpriteRenderer[] renderers;
    private float[] age;
    private float[] life;
    private Color[] baseColor;

    private GameObject container;

    private void Awake()
    {
        renderers = new SpriteRenderer[PoolSize];
        age = new float[PoolSize];
        life = new float[PoolSize];
        baseColor = new Color[PoolSize];

        // As cópias ficam paradas no mundo: um contêiner fora da hierarquia do jogador (que se move).
        container = new GameObject("Afterimages (" + name + ")");
        for (int i = 0; i < PoolSize; i++)
        {
            var go = new GameObject("Afterimage" + i);
            go.transform.SetParent(container.transform, false);
            renderers[i] = go.AddComponent<SpriteRenderer>();
            renderers[i].enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (container != null) Destroy(container);
    }

    /// <summary>Copia o <paramref name="source"/> agora (pose de render) e some em <paramref name="fadeSeconds"/>.</summary>
    public void Spawn(SpriteRenderer source, Color tint, float fadeSeconds)
    {
        if (source == null || source.sprite == null || renderers == null) return;

        // Primeiro livre; sem livre, reaproveita o mais apagado.
        int slot = 0;
        float mostFaded = -1f;
        for (int i = 0; i < PoolSize; i++)
        {
            if (!renderers[i].enabled)
            {
                slot = i;
                break;
            }

            float faded = age[i] / life[i];
            if (faded > mostFaded)
            {
                mostFaded = faded;
                slot = i;
            }
        }

        SpriteRenderer r = renderers[slot];
        r.sprite = source.sprite;
        r.flipX = source.flipX;
        r.sortingLayerID = source.sortingLayerID;
        r.sortingOrder = source.sortingOrder + sortingOrderOffset;
        r.sharedMaterial = source.sharedMaterial;
        r.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        r.transform.localScale = source.transform.lossyScale;
        baseColor[slot] = tint;
        r.color = tint;
        r.enabled = true;
        age[slot] = 0f;
        life[slot] = Mathf.Max(0.01f, fadeSeconds);
    }

    private void LateUpdate()
    {
        if (renderers == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < PoolSize; i++)
        {
            SpriteRenderer r = renderers[i];
            if (!r.enabled) continue;
            age[i] += dt;
            float t = age[i] / life[i];
            if (t >= 1f)
            {
                r.enabled = false;
                continue;
            }

            Color c = baseColor[i];
            c.a *= 1f - t;
            r.color = c;
        }
    }
}
