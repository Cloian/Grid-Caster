using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(100)]
public class CameraController : MonoBehaviour
{
    [Header("보드 참조")]
    [SerializeField] private GridManager gridManager;

    [Header("카메라 설정")]
    [SerializeField] private float cameraZ = -10f;
    [SerializeField, Min(0f)] private float mapPadding = 0.5f;

    private Camera targetCamera;
    private Vector3 mapCenter;
    private Vector2Int lastScreenSize;
    private float sideHudReferenceWidth;
    private float verticalHudReferenceHeight;

    private void Awake()
    {
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
    }

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

        if (currentScreenSize == lastScreenSize)
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
        targetCamera.rect = new Rect(0f, 0f, 1f, 1f);
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
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
        float cameraAspect = targetCamera.pixelHeight > 0
            ? (float)targetCamera.pixelWidth / targetCamera.pixelHeight
            : targetCamera.aspect;

        mapCenter = (worldMin + worldMax) * 0.5f;
        // 상하단 전투 HUD와 좌우 빌드 HUD가 사용하는 영역을 제외한 중앙 공간에 보드를 맞춘다.
        float referenceScale = targetCamera.pixelHeight / 1080f;
        float verticalHudPixels = verticalHudReferenceHeight * referenceScale;
        float usableHeightRatio = targetCamera.pixelHeight > 0
            ? Mathf.Clamp01((targetCamera.pixelHeight - verticalHudPixels * 2f)
                / targetCamera.pixelHeight)
            : 1f;
        float verticalSize = (mapHeight * 0.5f + mapPadding)
            / Mathf.Max(0.01f, usableHeightRatio);
        float sideHudPixels = sideHudReferenceWidth * referenceScale;
        float usableWidthRatio = targetCamera.pixelWidth > 0
            ? Mathf.Clamp01((targetCamera.pixelWidth - sideHudPixels * 2f)
                / targetCamera.pixelWidth)
            : 1f;
        float horizontalSize = (mapWidth * 0.5f + mapPadding)
            / Mathf.Max(0.01f, cameraAspect * usableWidthRatio);
        float wholeMapSize = Mathf.Max(verticalSize, horizontalSize);

        targetCamera.orthographicSize = wholeMapSize;
        KeepMapCentered();
    }

    public void SetSideHudReservation(float referenceWidth)
    {
        sideHudReferenceWidth = Mathf.Max(0f, referenceWidth);
        CenterAndFitWholeMap();
    }

    public void SetVerticalHudReservation(float referenceHeight)
    {
        verticalHudReferenceHeight = Mathf.Max(0f, referenceHeight);
        CenterAndFitWholeMap();
    }

    private void KeepMapCentered()
    {
        if (targetCamera == null)
        {
            return;
        }

        targetCamera.transform.position = new Vector3(mapCenter.x, mapCenter.y, cameraZ);
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
