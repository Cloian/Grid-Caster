using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)]
public class CameraController : MonoBehaviour
{
    public event Action<bool> FollowModeChanged;

    [Header("추적 대상")]
    [SerializeField] private Transform player;

    [Header("카메라 설정")]
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private float cameraZ = -10f;
    [SerializeField] private float orthographicSize = 3f;
    [SerializeField] private Vector2 viewportAspectRatio = new Vector2(4f, 5f);

    [Header("확대/축소 설정")]
    [SerializeField] private float minimumZoomSize = 1.5f;
    [SerializeField] private float maximumZoomSize = 8f;
    [SerializeField] private float zoomStep = 0.5f;

    [Header("자유 카메라 설정")]
    [SerializeField] private float mouseMoveSpeed = 5f;
    [SerializeField, Range(0f, 0.9f)] private float mouseDeadZone = 0.15f;

    private Camera targetCamera;
    private bool followPlayer = true;
    private Vector2Int lastScreenSize;

    public bool IsFollowingPlayer => followPlayer;

    private void Awake()
    {
        FindCamera();
        FindPlayer();

        if (targetCamera == null)
        {
            Debug.LogError("씬에서 Main Camera를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        if (player == null)
        {
            Debug.LogError("씬에서 Move가 붙은 플레이어를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        DisableOtherCameras();
        ConfigureCamera();
        FollowPlayer();
    }

    private void LateUpdate()
    {
        RefreshViewportIfScreenSizeChanged();
        ToggleCameraMode();
        HandleZoom();

        if (followPlayer)
        {
            FollowPlayer();
        }
        else
        {
            FollowMouseDirection();
        }
    }

    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        float scroll = mouse.scroll.ReadValue().y;

        if (Mathf.Approximately(scroll, 0f))
        {
            return;
        }

        // 휠 위: 확대, 휠 아래: 축소
        float zoomDirection = scroll > 0f ? -1f : 1f;

        targetCamera.orthographicSize = Mathf.Clamp(
            targetCamera.orthographicSize + zoomDirection * zoomStep,
            minimumZoomSize,
            maximumZoomSize
        );
    }

    private void ToggleCameraMode()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.yKey.wasPressedThisFrame)
        {
            followPlayer = !followPlayer;

            if (followPlayer)
            {
                FollowPlayer();
            }

            FollowModeChanged?.Invoke(followPlayer);
        }
    }

    private void FindCamera()
    {
        // Main Camera에 붙였을 때는 자기 자신의 Camera를 사용한다.
        targetCamera = GetComponent<Camera>();

        // 플레이어에 붙였을 때는 Main Camera를 찾아 사용한다.
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void FindPlayer()
    {
        if (player != null)
        {
            return;
        }

        // Move와 CameraController가 같은 플레이어에 붙어 있는 경우.
        if (TryGetComponent(out Move playerMove))
        {
            player = playerMove.transform;
            return;
        }

        // CameraController가 Main Camera에 붙어 있는 경우.
        Move scenePlayer = FindAnyObjectByType<Move>();

        if (scenePlayer != null)
        {
            player = scenePlayer.transform;
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
        targetCamera.orthographicSize = orthographicSize;
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
    }

    private void UpdateViewportRect()
    {
        if (targetCamera == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        // 화면 크기와 관계없이 세로가 살짝 긴 4:5 카메라 영역을 중앙에 유지한다.
        float targetAspect = viewportAspectRatio.x / viewportAspectRatio.y;
        float screenAspect = (float)Screen.width / Screen.height;
        Rect viewportRect = new Rect(0f, 0f, 1f, 1f);

        if (screenAspect > targetAspect)
        {
            float widthScale = targetAspect / screenAspect;
            viewportRect.x = (1f - widthScale) * 0.5f;
            viewportRect.width = widthScale;
        }
        else
        {
            float heightScale = screenAspect / targetAspect;
            viewportRect.y = (1f - heightScale) * 0.5f;
            viewportRect.height = heightScale;
        }

        targetCamera.rect = viewportRect;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
    }

    private void FollowPlayer()
    {
        if (targetCamera == null || player == null)
        {
            return;
        }

        targetCamera.transform.position = new Vector3(
            player.position.x + offset.x,
            player.position.y + offset.y,
            cameraZ
        );
    }

    private void FollowMouseDirection()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        Rect screenRect = targetCamera.pixelRect;

        if (screenRect.width <= 0f || screenRect.height <= 0f)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();
        Vector2 direction = new Vector2(
            (mousePosition.x - screenRect.center.x) / (screenRect.width * 0.5f),
            (mousePosition.y - screenRect.center.y) / (screenRect.height * 0.5f)
        );

        direction = Vector2.ClampMagnitude(direction, 1f);

        if (direction.magnitude <= mouseDeadZone)
        {
            return;
        }

        float strength = Mathf.InverseLerp(mouseDeadZone, 1f, direction.magnitude);
        Vector2 movement = direction.normalized
            * (mouseMoveSpeed * strength * Time.unscaledDeltaTime);
        Vector3 cameraPosition = targetCamera.transform.position;

        cameraPosition.x += movement.x;
        cameraPosition.y += movement.y;
        cameraPosition.z = cameraZ;
        targetCamera.transform.position = cameraPosition;
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        viewportAspectRatio.x = Mathf.Max(0.1f, viewportAspectRatio.x);
        viewportAspectRatio.y = Mathf.Max(0.1f, viewportAspectRatio.y);
        minimumZoomSize = Mathf.Max(0.1f, minimumZoomSize);
        maximumZoomSize = Mathf.Max(minimumZoomSize, maximumZoomSize);
        zoomStep = Mathf.Max(0.01f, zoomStep);
        mouseMoveSpeed = Mathf.Max(0f, mouseMoveSpeed);

        // 2D 오브젝트와 같은 Z 위치에 놓여 화면이 사라지는 것을 방지한다.
        if (cameraZ >= 0f)
        {
            cameraZ = -10f;
        }
    }
}
