using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Offset & Suavização")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
    [SerializeField] private float smoothTime = 0.2f;

    [Header("Configuração Inicial")]
    [SerializeField] private bool snapToTargetOnStart = true;
    [SerializeField] private float maxTeleportDistance = 20f;

    private float currentMinX = 0;
    private float currentMaxX = 1000;
    private float currentMinY = 0;
    private float currentMaxY = 1000;

    private Vector3 _velocity = Vector3.zero;
    private bool _shouldSnap = false;

    private void Awake()
    {
        // Garante que apenas uma câmera exista
        CameraFollow[] existingCameras = FindObjectsOfType<CameraFollow>();
        if (existingCameras.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneController.OnPlayerSpawned -= OnPlayerSpawned;
        SceneController.OnPlayerSpawned += OnPlayerSpawned;
    }

    private void OnDisable()
    {
        SceneController.OnPlayerSpawned -= OnPlayerSpawned;
    }

    private void Start()
    {
        // Garante que esta é a câmera principal
        Camera.main.gameObject.tag = "MainCamera";

        if (target == null)
        {
            FindTarget();
        }

        FindLevelBoundaries();

        if (snapToTargetOnStart && target != null)
        {
            _shouldSnap = true;
        }
    }

    private void OnPlayerSpawned(GameObject playerObject)
    {
        target = playerObject.transform;
        Debug.Log("Câmera recebeu referência do jogador via evento");
        FindLevelBoundaries();
        _shouldSnap = true;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTarget();
            if (target == null)
            {
                Debug.LogWarning("Nenhum target encontrado para a câmera");
                return;
            }
        }

        if (_shouldSnap)
        {
            SnapToTarget();
            _shouldSnap = false;
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position + offset);
        if (distance > maxTeleportDistance)
        {
            SnapToTarget();
            return;
        }

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, currentMinX, currentMaxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, currentMinY, currentMaxY);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _velocity,
            smoothTime
        );
    }

    private void FindLevelBoundaries()
    {
        CameraBoundary boundary = FindObjectOfType<CameraBoundary>();

        if (boundary != null)
        {
            currentMinX = boundary.minX;
            currentMaxX = boundary.maxX;
            currentMinY = boundary.minY;
            currentMaxY = boundary.maxY;

            Debug.Log($"Limites da câmera atualizados para o nível atual");
        }
    }

    private void FindTarget()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
            Debug.Log("Câmera encontrou jogador via FindWithTag");
        }
    }

    private void SnapToTarget()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, currentMinX, currentMaxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, currentMinY, currentMaxY);
        transform.position = desiredPosition;
        _velocity = Vector3.zero;
    }
}