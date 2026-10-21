using System.Collections.Generic;
using UnityEngine;

// 근위병(폰) 머리 왼쪽 위에 "끝 줄에 닿으면 무엇으로 승격하는지"를 작은 말 그림으로 미리 보여 준다.
// 끝 줄까지 남은 칸이 2칸 이하면 깜빡이고, 1칸이면 빠르게 깜빡인다. 판정은 QueenBossBehaviour가 맡는다.
public sealed class PromotionMarker : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    private static readonly Dictionary<MonsterMovementPattern, Sprite> sprites = new Dictionary<MonsterMovementPattern, Sprite>();

    private Transform target;
    private SpriteRenderer icon;
    private int rowsLeft = 99;

    public MonsterMovementPattern Promotion { get; private set; }

    public static PromotionMarker Attach(MonsterMovement guard, MonsterMovementPattern promotion)
    {
        var root = new GameObject("PromotionMarker");
        var marker = root.AddComponent<PromotionMarker>();
        marker.target = guard.transform;
        marker.Promotion = promotion;
        marker.icon = root.AddComponent<SpriteRenderer>();
        marker.icon.sprite = SpriteFor(promotion);
        marker.icon.sortingOrder = 13;   // 캐릭터(10)·상태 아이콘 위
        marker.LateUpdate();
        return marker;
    }

    public void SetRowsLeft(int rows) => rowsLeft = rows;

    private void LateUpdate()
    {
        if (target == null || !target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }
        // 칸 중심 기준 왼쪽 위(HP 칸·행동 아이콘과 겹치지 않는 자리), 정수 픽셀에 맞춘다.
        Vector3 p = target.position + new Vector3(-11f, 13f, 0f) / PixelsPerUnit;
        transform.position = new Vector3(Mathf.Round(p.x * PixelsPerUnit) / PixelsPerUnit,
            Mathf.Round(p.y * PixelsPerUnit) / PixelsPerUnit, 0f);
        float period = rowsLeft <= 1 ? 0.18f : rowsLeft == 2 ? 0.45f : 0f;
        icon.enabled = period <= 0f || Mathf.FloorToInt(Time.unscaledTime / period) % 2 == 0;
    }

    // 9×9 말 그림(금색 + 외곽선). 위에서 아래로 적는다.
    private static readonly Dictionary<MonsterMovementPattern, string[]> Glyphs = new Dictionary<MonsterMovementPattern, string[]>
    {
        [MonsterMovementPattern.Knight] = new[]
        {
            "..####...",
            ".#yyyy#..",
            "#yyg#gy#.",
            "#ygggggy#",
            ".##gg#gG#",
            "..#ggg#..",
            ".#gggGG#.",
            "#yggGGGb#",
            "#########",
        },
        [MonsterMovementPattern.Bishop] = new[]
        {
            "...###...",
            "..#yyy#..",
            ".#yg#gg#.",
            ".#yg#gG#.",
            "..#ggG#..",
            "...#g#...",
            "..#gGG#..",
            ".#ygGGb#.",
            "#########",
        },
        [MonsterMovementPattern.Bomber] = new[]
        {
            "......#r.",
            ".....#b..",
            "...###...",
            "..#nNN#..",
            ".#nwNNN#.",
            ".#nNNNN#.",
            ".#NNNNM#.",
            "..#NMM#..",
            "...###...",
        },
    };

    private static Sprite SpriteFor(MonsterMovementPattern pattern)
    {
        if (sprites.TryGetValue(pattern, out Sprite cached) && cached != null) return cached;
        string[] rows = Glyphs.TryGetValue(pattern, out string[] glyph) ? glyph : Glyphs[MonsterMovementPattern.Knight];
        int h = rows.Length, w = rows[0].Length;
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[(h - 1 - y) * w + x] = ColorOf(x < rows[y].Length ? rows[y][x] : '.');
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PixelsPerUnit);
        sprites[pattern] = sprite;
        return sprite;
    }

    private static Color32 ColorOf(char c) => c switch
    {
        '#' => new Color32(30, 22, 34, 255),
        'y' => new Color32(255, 214, 110, 255),
        'g' => new Color32(240, 170, 50, 255),
        'G' => new Color32(196, 128, 36, 255),
        'b' => new Color32(140, 86, 24, 255),
        'n' => new Color32(110, 120, 140, 255),
        'N' => new Color32(64, 72, 92, 255),
        'M' => new Color32(40, 44, 60, 255),
        'w' => new Color32(220, 228, 240, 255),
        'r' => new Color32(255, 110, 70, 255),
        _ => new Color32(0, 0, 0, 0),
    };
}
