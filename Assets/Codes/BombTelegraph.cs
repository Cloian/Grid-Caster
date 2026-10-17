using System.Collections.Generic;
using UnityEngine;

// 폭탄병이 던진 3×3 폭탄의 예고. 칸마다 주황 테두리와 사선 무늬를 깔고, 가운데에 폭탄과 남은 행동 수(점)를 둔다.
// 마지막 행동이 남으면 빨갛게 빨리 깜빡인다. 판정은 ChessMonsterBehaviour가 맡고 여기는 그림만 그린다.
public sealed class BombTelegraph : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private const int SortingOrder = -4;           // 바닥(-10)·균열(-5) 위, 그림자·캐릭터 아래
    private const float ThrowSeconds = 0.35f;

    private static Sprite tileSprite;
    private static Sprite bombSprite;
    private static Sprite fusePipSprite;

    private readonly List<SpriteRenderer> tiles = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
    private GridManager grid;
    private SpriteRenderer bomb;
    private Vector3 throwFrom, landAt;
    private float throwElapsed = ThrowSeconds;
    private int fuse;

    public static BombTelegraph Create(GridManager grid)
    {
        var root = new GameObject("BombTelegraph");
        var telegraph = root.AddComponent<BombTelegraph>();
        telegraph.grid = grid;
        EnsureSprites();
        telegraph.bomb = NewRenderer(root.transform, "Bomb", bombSprite, 11);
        root.SetActive(false);
        return telegraph;
    }

    public void Show(IEnumerable<Vector3Int> cells, Vector3Int center, Vector3 from, int remaining)
    {
        gameObject.SetActive(true);
        int index = 0;
        foreach (Vector3Int cell in cells)
        {
            if (index >= tiles.Count) tiles.Add(NewRenderer(transform, "Tile", tileSprite, SortingOrder));
            tiles[index].transform.position = grid.GetCellCenterWorld(cell);
            tiles[index].gameObject.SetActive(true);
            index++;
        }
        for (; index < tiles.Count; index++) tiles[index].gameObject.SetActive(false);
        throwFrom = from;
        // 칸 한가운데는 플레이어가 서 있으므로 폭탄은 오른쪽 아래로 비켜 놓는다.
        landAt = grid.GetCellCenterWorld(center) + new Vector3(9f, -9f, 0f) / PixelsPerUnit;
        throwElapsed = 0f;
        SetFuse(remaining);
    }

    public void SetFuse(int remaining)
    {
        fuse = Mathf.Max(0, remaining);
        while (pips.Count < fuse) pips.Add(NewRenderer(bomb.transform, "Fuse", fusePipSprite, 12));
        for (int i = 0; i < pips.Count; i++)
        {
            pips[i].gameObject.SetActive(i < fuse);
            // 폭탄 위에 남은 행동 수만큼 점을 3px 간격으로 놓는다.
            pips[i].transform.localPosition = new Vector3((i - (fuse - 1) * 0.5f) * 3f / PixelsPerUnit, 9f / PixelsPerUnit, 0f);
        }
    }

    public void Hide()
    {
        if (this != null) gameObject.SetActive(false);
    }

    private void Update()
    {
        // 던지는 포물선: 폭탄병 위치에서 예고 중심으로 날아가 떨어진다.
        if (throwElapsed < ThrowSeconds)
        {
            throwElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(throwElapsed / ThrowSeconds);
            Vector3 position = Vector3.Lerp(throwFrom, landAt, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.2f);
            bomb.transform.position = Snap(position);
        }
        else bomb.transform.position = Snap(landAt);

        // 마지막 행동이면 빨갛고 빠르게, 아니면 주황으로 천천히 깜빡인다.
        bool urgent = fuse <= 1;
        float period = urgent ? 0.18f : 0.4f;
        bool bright = Mathf.FloorToInt(Time.unscaledTime / period) % 2 == 0;
        Color color = urgent ? new Color(1f, 0.32f, 0.25f, bright ? 0.95f : 0.55f)
            : new Color(1f, 0.62f, 0.2f, bright ? 0.85f : 0.5f);
        foreach (SpriteRenderer tile in tiles) tile.color = color;
    }

    private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, int order)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, false);
        var renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static Vector3 Snap(Vector3 p) =>
        new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit, Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, p.z);

    private static void EnsureSprites()
    {
        if (tileSprite != null) return;
        // 32px 칸: 2px 테두리 + 4px 간격 사선 무늬(흰색, 렌더러 색으로 물들인다)
        var tile = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                bool border = x < 2 || y < 2 || x > 29 || y > 29;
                bool hatch = !border && x > 3 && y > 3 && x < 28 && y < 28 && (x + y) % 6 == 0;
                tile[y * 32 + x] = border || hatch ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        tileSprite = Make(tile, 32, 32, new Vector2(0.5f, 0.5f));

        // 11px 폭탄: 외곽선, 어두운 몸, 왼쪽 위 하이라이트, 심지
        var b = new Color32[11 * 13];
        Color32 outline = new Color32(30, 22, 34, 255), body = new Color32(58, 64, 82, 255),
            shine = new Color32(150, 160, 176, 255), fuseColor = new Color32(157, 102, 29, 255);
        for (int y = 0; y < 11; y++)
            for (int x = 0; x < 11; x++)
            {
                float d = Mathf.Sqrt((x - 5f) * (x - 5f) + (y - 5f) * (y - 5f));
                if (d <= 5.2f) b[y * 11 + x] = d > 4.2f ? outline : (x < 5 && y > 6 ? shine : body);
            }
        b[11 * 11 + 7] = fuseColor; b[12 * 11 + 8] = fuseColor;
        bombSprite = Make(b, 11, 13, new Vector2(0.5f, 0.4f));

        var pip = new Color32[2 * 2];
        for (int i = 0; i < pip.Length; i++) pip[i] = new Color32(255, 230, 128, 255);
        fusePipSprite = Make(pip, 2, 2, new Vector2(0.5f, 0.5f));
    }

    private static Sprite Make(Color32[] pixels, int w, int h, Vector2 pivot)
    {
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, w, h), pivot, PixelsPerUnit);
    }
}
