using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DirectionalActionIndicator : MonoBehaviour
{
    private sealed class ArrowMarker
    {
        public GameObject Root;
        public SpriteRenderer Shadow;
        public SpriteRenderer Arrow;
    }

    [Header("화살표 표시")]
    [SerializeField] private Color moveColor = new Color32(89, 209, 255, 235);
    [SerializeField] private Color attackColor = new Color32(255, 101, 105, 235);
    [SerializeField] private Color attackTargetColor = new Color32(255, 205, 91, 255);
    [SerializeField, Range(0.4f, 1f)] private float markerScale = 0.72f;
    [SerializeField, Range(0f, 0.2f)] private float pulseAmount = 0.07f;
    [SerializeField, Min(0f)] private float pulseSpeed = 4f;
    [SerializeField] private int sortingOrder = 20;

    private readonly List<ArrowMarker> markerPool = new List<ArrowMarker>();

    private Texture2D arrowTexture;
    private Sprite arrowSprite;
    private int activeChoiceCount;

    public int ActiveChoiceCount => activeChoiceCount;

    private void Awake()
    {
        CreateArrowSprite();
    }

    private void Update()
    {
        for (int i = 0; i < activeChoiceCount; i++)
        {
            float phase = Time.unscaledTime * pulseSpeed + i * 0.45f;
            float pulse = 1f + Mathf.Sin(phase) * pulseAmount;
            markerPool[i].Root.transform.localScale = Vector3.one * markerScale * pulse;
        }
    }

    public void ShowChoices(
        Vector3 originWorldPosition,
        IReadOnlyList<Vector3> targetWorldPositions,
        PlayerActionSelectionMode mode,
        IReadOnlyList<bool> emphasizedChoices
    )
    {
        EnsureArrowSprite();
        EnsurePoolSize(targetWorldPositions.Count);
        activeChoiceCount = targetWorldPositions.Count;

        for (int i = 0; i < markerPool.Count; i++)
        {
            bool isActive = i < activeChoiceCount;
            ArrowMarker marker = markerPool[i];
            marker.Root.SetActive(isActive);

            if (!isActive)
                continue;

            Vector3 targetPosition = targetWorldPositions[i];
            targetPosition.z = transform.position.z;
            Vector2 direction = targetPosition - originWorldPosition;
            float angle = Vector2.SignedAngle(Vector2.up, direction.normalized);
            bool isEmphasized = emphasizedChoices != null
                && i < emphasizedChoices.Count
                && emphasizedChoices[i];

            marker.Root.transform.position = targetPosition;
            marker.Root.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            marker.Root.transform.localScale = Vector3.one * markerScale;
            marker.Arrow.color = GetArrowColor(mode, isEmphasized);
        }
    }

    public void ClearChoices()
    {
        activeChoiceCount = 0;

        foreach (ArrowMarker marker in markerPool)
        {
            marker.Root.SetActive(false);
        }
    }

    private void EnsurePoolSize(int requestedSize)
    {
        while (markerPool.Count < requestedSize)
        {
            markerPool.Add(CreateMarker(markerPool.Count));
        }
    }

    private ArrowMarker CreateMarker(int index)
    {
        GameObject root = new GameObject($"ActionArrow_{index + 1}");
        root.transform.SetParent(transform, false);

        SpriteRenderer shadow = root.AddComponent<SpriteRenderer>();
        shadow.sprite = arrowSprite;
        shadow.color = new Color32(4, 9, 19, 210);
        shadow.sortingOrder = sortingOrder - 1;

        GameObject arrowObject = new GameObject("Arrow");
        arrowObject.transform.SetParent(root.transform, false);
        arrowObject.transform.localScale = Vector3.one * 0.82f;

        SpriteRenderer arrow = arrowObject.AddComponent<SpriteRenderer>();
        arrow.sprite = arrowSprite;
        arrow.color = moveColor;
        arrow.sortingOrder = sortingOrder;

        root.SetActive(false);
        return new ArrowMarker
        {
            Root = root,
            Shadow = shadow,
            Arrow = arrow
        };
    }

    private Color GetArrowColor(PlayerActionSelectionMode mode, bool isEmphasized)
    {
        if (mode == PlayerActionSelectionMode.Attack)
        {
            return isEmphasized ? attackTargetColor : attackColor;
        }

        return moveColor;
    }

    private void EnsureArrowSprite()
    {
        if (arrowSprite == null)
        {
            CreateArrowSprite();
        }
    }

    private void CreateArrowSprite()
    {
        const int textureSize = 32;
        Color32 transparent = new Color32(0, 0, 0, 0);
        Color32 white = new Color32(255, 255, 255, 255);
        Color32[] pixels = new Color32[textureSize * textureSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transparent;
        }

        // 32 PPU 기준으로 선명한 위쪽 화살표를 만든 뒤 방향에 맞게 회전한다.
        for (int y = 4; y <= 19; y++)
        {
            for (int x = 14; x <= 17; x++)
            {
                pixels[y * textureSize + x] = white;
            }
        }

        for (int y = 15; y <= 27; y++)
        {
            int halfWidth = Mathf.CeilToInt((27 - y) * 0.62f);

            for (int x = 16 - halfWidth; x <= 15 + halfWidth; x++)
            {
                if (x >= 0 && x < textureSize)
                {
                    pixels[y * textureSize + x] = white;
                }
            }
        }

        arrowTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        arrowTexture.name = "RuntimeActionArrowTexture";
        arrowTexture.filterMode = FilterMode.Point;
        arrowTexture.wrapMode = TextureWrapMode.Clamp;
        arrowTexture.hideFlags = HideFlags.DontSave;
        arrowTexture.SetPixels32(pixels);
        arrowTexture.Apply(false, true);

        arrowSprite = Sprite.Create(
            arrowTexture,
            new Rect(0f, 0f, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            32f,
            0,
            SpriteMeshType.FullRect
        );
        arrowSprite.name = "RuntimeActionArrowSprite";
        arrowSprite.hideFlags = HideFlags.DontSave;
    }

    private void OnDisable()
    {
        ClearChoices();
    }

    private void OnDestroy()
    {
        if (arrowSprite != null)
        {
            Destroy(arrowSprite);
        }

        if (arrowTexture != null)
        {
            Destroy(arrowTexture);
        }
    }

    private void OnValidate()
    {
        markerScale = Mathf.Clamp(markerScale, 0.4f, 1f);
        pulseAmount = Mathf.Clamp(pulseAmount, 0f, 0.2f);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
    }
}
