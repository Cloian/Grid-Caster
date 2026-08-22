using UnityEngine;

[DisallowMultipleComponent]
public sealed class DamageFlash : MonoBehaviour
{
    [Header("피해 색상 피드백")]
    [SerializeField] private Color flashColor = new Color(1f, 0.12f, 0.12f, 1f);
    [SerializeField, Min(0.01f)] private float flashDuration = 0.22f;
    [SerializeField, Range(0f, 1f)] private float flashStrength = 0.8f;

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private float remainingTime;

    public bool IsFlashing => remainingTime > 0f;

    private void Awake()
    {
        CacheSpriteColors();
    }

    public void Play()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            CacheSpriteColors();
        }

        remainingTime = flashDuration;
        ApplyFlashColor(1f);
    }

    private void Update()
    {
        if (remainingTime <= 0f)
            return;

        remainingTime = Mathf.Max(0f, remainingTime - Time.deltaTime);
        float normalizedTime = remainingTime / flashDuration;

        // 붉은색이 처음에는 강하게 보이고 끝으로 갈수록 부드럽게 사그라든다.
        ApplyFlashColor(normalizedTime * normalizedTime);

        if (remainingTime <= 0f)
        {
            RestoreOriginalColors();
        }
    }

    private void CacheSpriteColors()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalColors[i] = spriteRenderers[i].color;
        }
    }

    private void ApplyFlashColor(float intensity)
    {
        float blend = Mathf.Clamp01(intensity) * flashStrength;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null)
                continue;

            Color originalColor = originalColors[i];
            Color targetColor = new Color(
                flashColor.r,
                flashColor.g,
                flashColor.b,
                originalColor.a
            );
            spriteRenderer.color = Color.Lerp(originalColor, targetColor, blend);
        }
    }

    private void RestoreOriginalColors()
    {
        if (spriteRenderers == null || originalColors == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].color = originalColors[i];
            }
        }
    }

    private void OnDisable()
    {
        remainingTime = 0f;
        RestoreOriginalColors();
    }

    private void OnValidate()
    {
        flashDuration = Mathf.Max(0.01f, flashDuration);
        flashStrength = Mathf.Clamp01(flashStrength);
    }
}
