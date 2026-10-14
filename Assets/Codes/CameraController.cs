using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(100)]
public class CameraController : MonoBehaviour
{
    [Header("보드 참조")]
    [SerializeField] private GridManager gridManager;

    [Header("카메라 설정")]
    [SerializeField] private float cameraZ = -10f;
    // 기존 씬 직렬화 보존용. 화면 여백은 PcBoardLayout / 모바일 어댑터가 결정한다.
    [SerializeField, Min(0f), HideInInspector] private float mapPadding = 0.5f;

    [Header("명중 카메라 반응")]
    [SerializeField, Range(0f, 4f)] private float impactShakePixels = 2f;
    [SerializeField, Min(0.01f)] private float impactShakeSeconds = 0.07f;
    private static CameraController instance;
    private float shakeRemaining;
    private Vector3 shakeDirection = Vector3.right;

    private Camera targetCamera;
    private Vector3 mapCenter;
    private Vector2Int lastScreenSize;
    private Rect lastSafeArea;

    private void Awake()
    {
        instance = this;
        FindReferences();

        if (targetCamera == null)
        {
            Debug.LogError("씬에서 Main Camera를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        if (gridManager == null || !gridManager.IsReady)
        {
            Debug.LogError("전체 보드를 표시할 GridManager를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        DisableOtherCameras();
        ConfigureCamera();
        CenterAndFitWholeMap();
    }

    private void LateUpdate()
    {
        RefreshViewportIfScreenSizeChanged();
        KeepMapCentered();
        ApplyImpactShake();
    }

    public static void PlayImpactShake(Vector3 direction)
    {
        if (instance == null || !instance.isActiveAndEnabled) return;
        // 동시 명중은 강도를 더하지 않고 짧은 반응을 갱신한다. 게임 RNG는 사용하지 않는다.
        instance.shakeRemaining = instance.impactShakeSeconds;
        instance.shakeDirection = direction == Vector3.zero ? Vector3.right : direction.normalized;
    }

    private void ApplyImpactShake()
    {
        if (shakeRemaining <= 0f || targetCamera == null) return;
        shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
        float elapsed = impactShakeSeconds - shakeRemaining;
        float strength = impactShakePixels * shakeRemaining / impactShakeSeconds;
        Vector3 perpendicular = new Vector3(-shakeDirection.y, shakeDirection.x, 0f);
        Vector3 pixels = Vector3.ClampMagnitude(shakeDirection * Mathf.Cos(elapsed * 110f)
            + perpendicular * (0.45f * Mathf.Sin(elapsed * 90f)), 1f) * strength;
        // 화면 픽셀 단위로 옮겨 샘플링 경계와 공통 PC 보드/HUD 배율을 유지한다.
        float unitsPerPixel = targetCamera.orthographicSize * 2f / Mathf.Max(1f, targetCamera.pixelHeight);
        targetCamera.transform.position += new Vector3(Mathf.Round(pixels.x), Mathf.Round(pixels.y), 0f) * unitsPerPixel;
    }

    private void OnDisable()
    {
        shakeRemaining = 0f;
        KeepMapCentered();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

    private void FindReferences()
    {
        targetCamera = GetComponent<Camera>();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (gridManager == null)
        {
            gridManager = FindAnyObjectByType<GridManager>();
        }
    }

    private void DisableOtherCameras()
    {
        Camera[] sceneCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);

        foreach (Camera sceneCamera in sceneCameras)
        {
            if (sceneCamera != targetCamera)
            {
                sceneCamera.enabled = false;
            }
        }
    }

    private void ConfigureCamera()
    {
        targetCamera.enabled = true;
        targetCamera.orthographic = true;
        targetCamera.cullingMask = ~0;
        targetCamera.targetTexture = null;
        targetCamera.targetDisplay = 0;
        targetCamera.transform.rotation = Quaternion.identity;
        UpdateViewportRect();
    }

    private void RefreshViewportIfScreenSizeChanged()
    {
        Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);

        if (currentScreenSize == lastScreenSize && Screen.safeArea == lastSafeArea)
        {
            return;
        }

        UpdateViewportRect();
        CenterAndFitWholeMap();
    }

    private void UpdateViewportRect()
    {
        if (targetCamera == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        // 바둑판처럼 맵 전체를 화면 중앙에 보여 주기 위해 카메라가 전체 화면을 사용한다.
        Rect board = MobilePlaytestController.IsActive
            ? MobilePlaytestController.BoardPixelRect(Screen.width, Screen.height, Screen.safeArea)
            : new Rect(0f, 0f, Screen.width, Screen.height);
        targetCamera.rect = new Rect(board.x / Screen.width, board.y / Screen.height,
            board.width / Screen.width, board.height / Screen.height);
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastSafeArea = Screen.safeArea;
    }

    private void CenterAndFitWholeMap()
    {
        Tilemap groundTilemap = gridManager != null ? gridManager.GroundTilemap : null;

        if (targetCamera == null || groundTilemap == null)
        {
            return;
        }

        BoundsInt bounds = groundTilemap.cellBounds;
        Vector3 worldMin = groundTilemap.CellToWorld(bounds.min);
        Vector3 worldMax = groundTilemap.CellToWorld(bounds.max);
        float mapWidth = Mathf.Abs(worldMax.x - worldMin.x);
        float mapHeight = Mathf.Abs(worldMax.y - worldMin.y);
        mapCenter = (worldMin + worldMax) * 0.5f;
        if (MobilePlaytestController.IsActive)
        {
            float aspect = targetCamera.pixelWidth / Mathf.Max(1f, targetCamera.pixelHeight);
            targetCamera.orthographicSize = Mathf.Max(mapHeight * 0.5f + 0.03f,
                (mapWidth * 0.5f + 0.03f) / Mathf.Max(0.01f, aspect));
        }
        else
        {
            // 외벽을 포함한 전체 맵을 공통 PC 구성의 보드 영역에 맞춘다. UI별 예약 계산은 하지 않는다.
            float boardPixels = PcBoardLayout.BoardPixelRect(Screen.width, Screen.height).height;
            targetCamera.orthographicSize = Mathf.Max(mapWidth, mapHeight) * 0.5f
                * targetCamera.pixelHeight / Mathf.Max(1f, boardPixels);
        }
        KeepMapCentered();
    }

    private void KeepMapCentered()
    {
        if (targetCamera == null)
        {
            return;
        }

        float offset = MobilePlaytestController.IsActive ? 0f
            : PcBoardLayout.BoardCenterY * PcBoardLayout.Scale(Screen.width, Screen.height)
                * targetCamera.orthographicSize * 2f / Mathf.Max(1f, targetCamera.pixelHeight);
        targetCamera.transform.position = new Vector3(mapCenter.x, mapCenter.y - offset, cameraZ);
    }

    private void OnValidate()
    {
        mapPadding = Mathf.Max(0f, mapPadding);

        if (cameraZ >= 0f)
        {
            cameraZ = -10f;
        }
    }
}
