using System.Collections.Generic;
using UnityEngine;

// 캐릭터 발밑 픽셀 그림자. 판정과 무관한 표시용이며 스프라이트 시트 이름으로 캐릭터별 크기를 고른다.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CharacterShadow : MonoBehaviour
{
    private struct Profile
    {
        public string Prefix;
        public int Width;       // 그림자 폭 (스프라이트 픽셀)
        public int Height;      // 그림자 높이 (스프라이트 픽셀)
        public float OffsetX;   // 프레임 중심 기준, 오른쪽을 볼 때의 발 중심 (스프라이트 픽셀)
        public float OffsetY;   // 프레임 중심 기준 발 중심 (위가 +)
    }

    // 프레임 크기·기준점(중심)·PPU는 시트마다 다르므로 값은 각 시트의 픽셀 단위로 둔다.
    private static readonly Profile[] Profiles =
    {
        new Profile { Prefix = "wizard", Width = 24, Height = 6, OffsetX = 0f, OffsetY = -16f },
        new Profile { Prefix = "new_wizard", Width = 24, Height = 6, OffsetX = 0f, OffsetY = -16f },
        // 박쥐는 떠 있으므로 몸보다 작은 그림자를 칸 바닥 쪽에 둔다.
        new Profile { Prefix = "bat", Width = 14, Height = 4, OffsetX = 0f, OffsetY = -16f },
        new Profile { Prefix = "snake", Width = 30, Height = 6, OffsetX = 0f, OffsetY = -13f },
        new Profile { Prefix = "armadillo", Width = 54, Height = 9, OffsetX = -1f, OffsetY = -22.5f },
        // 64px 체스 적: 발이 프레임 바닥에서 2px 위(y=62). 룩은 성벽에 붙어 있어 그림자가 없다.
        new Profile { Prefix = "knight", Width = 52, Height = 8, OffsetX = 0f, OffsetY = -29f },
        new Profile { Prefix = "pawn_guard", Width = 34, Height = 7, OffsetX = 0f, OffsetY = -29f },
        new Profile { Prefix = "bomber", Width = 34, Height = 7, OffsetX = 0f, OffsetY = -29f },
        new Profile { Prefix = "bishop_priest", Width = 30, Height = 7, OffsetX = 2f, OffsetY = -29f },
        // 여왕 120px: 기준점이 발(점유 3×3의 아래 가장자리) 위 48px에 있다.
        new Profile { Prefix = "queen", Width = 80, Height = 12, OffsetX = 0f, OffsetY = -47f },
    };

    private static readonly Color Core = new Color(30f / 255f, 22f / 255f, 34f / 255f, 0.5f);
    private static readonly Color Rim = new Color(30f / 255f, 22f / 255f, 34f / 255f, 0.24f);
    private static readonly Dictionary<long, Sprite> SpriteCache = new Dictionary<long, Sprite>();

    private SpriteRenderer body;
    private CharacterHealth health;
    private bool isMonster;
    private SpriteRenderer shadow;
    private MonsterMovement monster;
    private Profile profile;
    private float pixelsPerUnit;

    public static void Attach(GameObject actor)
    {
        if (actor != null && actor.GetComponent<SpriteRenderer>() != null && actor.GetComponent<CharacterShadow>() == null)
            actor.AddComponent<CharacterShadow>();
    }

    // DamageFlash가 Awake에서 자식 렌더러 색을 저장하므로, 그림자는 Start 이후에 만들어 깜빡임 대상에서 뺀다.
    private void Start()
    {
        body = GetComponent<SpriteRenderer>();
        if (body.sprite == null || !TryFindProfile(body.sprite.texture.name, out profile))
        {
            enabled = false;
            return;
        }

        pixelsPerUnit = body.sprite.pixelsPerUnit;
        health = GetComponent<CharacterHealth>();
        monster = GetComponent<MonsterMovement>();
        isMonster = monster != null;
        GameObject child = new GameObject("Shadow");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        shadow = child.AddComponent<SpriteRenderer>();
        shadow.sprite = ShadowSprite(profile.Width, profile.Height, pixelsPerUnit);
        shadow.sharedMaterial = body.sharedMaterial;
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (shadow == null) return;
        shadow.sortingLayerID = body.sortingLayerID;
        shadow.sortingOrder = body.sortingOrder - 1;
        // 피격 연출이 원본을 숨기고 복사본을 그릴 때도 그림자는 남긴다. 사망 시에는 함께 숨긴다.
        shadow.enabled = !isMonster || health == null || !health.IsDead;
        float x = body.flipX ? -profile.OffsetX : profile.OffsetX;
        // 도약 중에는 몸만 뜨고 그림자는 바닥에 남는다. 높이 뜰수록 그림자가 작아진다.
        float lift = monster != null ? monster.VisualLift : 0f;
        float scaleY = Mathf.Max(0.0001f, transform.lossyScale.y);
        float shrink = 1f - Mathf.Clamp01(lift) * 0.35f;
        shadow.transform.localPosition = new Vector3(x / pixelsPerUnit, profile.OffsetY / pixelsPerUnit - lift / scaleY, 0f);
        shadow.transform.localScale = new Vector3(shrink, shrink, 1f);
    }

    private static bool TryFindProfile(string textureName, out Profile found)
    {
        foreach (Profile candidate in Profiles)
        {
            if (textureName.StartsWith(candidate.Prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                found = candidate;
                return true;
            }
        }
        found = default;
        return false;
    }

    // 가장자리 한 줄은 옅게 둔 픽셀 타원. 같은 크기는 공유한다.
    private static Sprite ShadowSprite(int width, int height, float ppu)
    {
        long key = ((long)width << 40) | ((long)height << 20) | (long)ppu;
        if (SpriteCache.TryGetValue(key, out Sprite cached) && cached != null) return cached;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };
        float rx = width / 2f, ry = height / 2f;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x + 0.5f - rx) / rx, dy = (y + 0.5f - ry) / ry;
                float d = dx * dx + dy * dy;
                Color c = d <= 0.55f ? Core : d <= 1f ? Rim : Color.clear;
                texture.SetPixel(x, y, c);
            }
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
            ppu, 0, SpriteMeshType.FullRect);
        sprite.hideFlags = HideFlags.DontSave;
        SpriteCache[key] = sprite;
        return sprite;
    }
}
