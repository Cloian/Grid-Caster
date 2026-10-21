using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 여왕 공격 연출(판정과 무관). 맞는 칸마다 32px 도트 효과를 띄워 "무슨 공격이 어디로 들어오는지"를 보여 준다.
// - 선(왕권 사격·성벽 사격): 칸마다 선 방향(가로·세로·대각) 금빛 광선 조각이 여왕 쪽부터 차례로 켜진다.
// - 포격: 3×3 가운데로 하늘에서 빛기둥이 꽂히고 9칸이 터지며 균열이 남는다.
// - 체크메이트: 맞는 칸이 모두 번쩍이고 일부 칸에 번개가 떨어진다.
public static class QueenAttackFx
{
    private const float Ppu = 32f;
    public const float LineStepSeconds = 0.022f;     // 선이 한 칸 뻗어 나가는 시간
    public const float BombardDelay = 0.06f;          // 여왕 내리꽂기 잔상 뒤 빛기둥
    public const float CheckmateDelay = 0.06f;

    private static readonly Color32 O = new Color32(30, 22, 34, 255);
    private static readonly Color32 White = new Color32(255, 255, 240, 255);
    private static readonly Color32 GoldL = new Color32(255, 214, 110, 255);
    private static readonly Color32 GoldM = new Color32(242, 183, 66, 255);
    private static readonly Color32 Gold = new Color32(206, 144, 41, 255);
    private static readonly Color32 GoldD = new Color32(157, 102, 29, 255);

    private static Sprite[][] beams;      // [방향 0=가로 1=세로 2=/ 3=\][프레임]
    private static Sprite[] bursts;
    private static Sprite[] pillar;
    private static readonly Dictionary<Color32, Sprite[]> flashes = new Dictionary<Color32, Sprite[]>();
    private static readonly Dictionary<Color32, Sprite[][]> bolts = new Dictionary<Color32, Sprite[][]>();

    // ---------------- 공개 연출 ----------------
    public static float Lines(GridManager grid, IEnumerable<Vector3Int> cells, Vector3Int origin, Vector3Int playerCell)
    {
        Ensure();
        float playerDelay = 0f;
        foreach (Vector3Int cell in cells)
        {
            int dx = cell.x - origin.x, dy = cell.y - origin.y;
            int dir = Mathf.Abs(dy) <= 1 && Mathf.Abs(dx) > 1 ? 0
                : Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) > 1 ? 1
                : (dx > 0) == (dy > 0) ? 2 : 3;
            float delay = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * LineStepSeconds;
            Spawn(grid.GetCellCenterWorld(cell), beams[dir], new[] { 0.04f, 0.05f, 0.09f, 0.08f, 0.08f }, delay, -3);
            if (cell == playerCell)
            {
                playerDelay = delay + 0.05f;
                Spawn(grid.GetCellCenterWorld(cell), bursts, new[] { 0.05f, 0.06f, 0.07f, 0.08f }, playerDelay, 12);
            }
        }
        CameraController.PlayImpactShake(Vector3.down);
        return playerDelay;
    }

    public static float Bombard(GridManager grid, ICollection<Vector3Int> cells, Vector3Int playerCell)
    {
        Ensure();
        foreach (Vector3Int center in Centers(cells))
        {
            Vector3 c = grid.GetCellCenterWorld(center);
            // 빛기둥: 가운데 칸 바닥(아래 가장자리 조금 위)에 발을 두고 위로 솟는다.
            Spawn(c + new Vector3(0f, 80f - 10f, 0f) / Ppu, pillar, new[] { 0.05f, 0.06f, 0.1f, 0.1f, 0.08f }, BombardDelay, 11);
        }
        float hit = BombardDelay + 0.11f;
        foreach (Vector3Int cell in cells)
            Spawn(grid.GetCellCenterWorld(cell), bursts, new[] { 0.05f, 0.06f, 0.08f, 0.1f }, hit, 12);
        KnightQuakeEffect.PlayCells(grid, cells, false);
        Delayed(grid, hit, () => CameraController.PlayImpactShake(Vector3.down));
        return cells.Contains(playerCell) ? hit : 0f;
    }

    public static float Checkmate(GridManager grid, ICollection<Vector3Int> area, Color32 color, int seed, Vector3Int playerCell)
    {
        Ensure();
        Sprite[] flash = Flash(color);
        foreach (Vector3Int cell in area)
            Spawn(grid.GetCellCenterWorld(cell), flash, new[] { 0.05f, 0.08f, 0.1f, 0.12f }, CheckmateDelay, -3);
        // 번개는 일부 칸(약 1/6)에만, 플레이어 칸이 맞는다면 반드시 그 칸에도.
        var rnd = new System.Random(seed);
        Sprite[][] boltSet = Bolts(color);
        foreach (Vector3Int cell in area)
        {
            bool onPlayer = cell == playerCell;
            if (!onPlayer && rnd.Next(6) != 0) continue;
            float delay = CheckmateDelay + (float)rnd.NextDouble() * 0.12f;
            Vector3 c = grid.GetCellCenterWorld(cell);
            Spawn(c + new Vector3(0f, 80f - 12f, 0f) / Ppu, boltSet[rnd.Next(boltSet.Length)], new[] { 0.05f, 0.06f, 0.06f }, delay, 12);
            Spawn(c, bursts, new[] { 0.04f, 0.05f, 0.06f, 0.07f }, delay + 0.05f, 12);
        }
        Delayed(grid, CheckmateDelay + 0.05f, () => CameraController.PlayImpactShake(Vector3.down));
        return area.Contains(playerCell) ? CheckmateDelay + 0.1f : 0f;
    }

    // 여왕 이동 잔상: 지나온 길에 지금 그림을 금빛으로 물들여 4장 남기고 옅게 사라지게 한다.
    public static void Glide(SpriteRenderer queen, Vector3 from, Vector3 to)
    {
        if (queen == null || queen.sprite == null) return;
        for (int i = 0; i < 4; i++)
        {
            float t = i / 4f;
            var go = new GameObject("QueenGlideGhost");
            Vector3 p = Vector3.Lerp(from, to, t);
            go.transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
            go.transform.localScale = queen.transform.lossyScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = queen.sprite;
            renderer.flipX = queen.flipX;
            renderer.sortingOrder = queen.sortingOrder - 1;
            var fade = go.AddComponent<GhostFade>();
            fade.Delay = i * 0.02f;
            fade.Seconds = 0.22f + i * 0.04f;
        }
    }

    // 돌진: 지나간 줄(몸 3칸 폭)에 금빛 광선을 깔고, 멈춘 앞쪽(플레이어를 들이받았으면 그 칸)에 충격을 터뜨린다.
    public static float Charge(GridManager grid, Vector3Int from, Vector3Int stop, Vector3Int direction, Vector3Int? hitCell)
    {
        Ensure();
        int dir = direction.y == 0 ? 0 : direction.x == 0 ? 1 : (direction.x > 0) == (direction.y > 0) ? 2 : 3;
        int steps = Mathf.Max(Mathf.Abs(stop.x - from.x), Mathf.Abs(stop.y - from.y));
        Vector3Int side = dir == 0 ? Vector3Int.up : dir == 1 ? Vector3Int.right : new Vector3Int(direction.x, -direction.y, 0);
        for (int k = 0; k <= steps; k++)
            for (int lane = -1; lane <= 1; lane++)
            {
                Vector3Int cell = from + direction * k + (dir < 2 ? side * lane : (lane == 0 ? Vector3Int.zero : new Vector3Int(lane, 0, 0)));
                Spawn(grid.GetCellCenterWorld(cell), beams[dir], new[] { 0.03f, 0.04f, 0.06f, 0.07f, 0.07f }, k * 0.012f, -3);
            }
        float impact = steps * 0.012f + 0.05f;
        Vector3Int front = hitCell ?? stop + direction * 2;
        Spawn(grid.GetCellCenterWorld(front), bursts, new[] { 0.05f, 0.06f, 0.08f, 0.1f }, impact, 12);
        var ring = new List<Vector3Int>();
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) ring.Add(front + new Vector3Int(x, y, 0));
        Delayed(grid, impact, () => KnightQuakeEffect.PlayCells(grid, ring, true));
        return impact;
    }

    private sealed class GhostFade : MonoBehaviour
    {
        public float Delay, Seconds;
        private SpriteRenderer renderer;
        private float elapsed;

        private void Start()
        {
            renderer = GetComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 0.8f, 0.35f, 0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = (elapsed - Delay) / Seconds;
            if (t < 0f) return;
            if (t >= 1f) { Destroy(gameObject); return; }
            renderer.color = new Color(1f, 0.8f, 0.35f, 0.55f * (1f - t));
        }
    }

    // 3×3 묶음의 가운데: 자기와 8방향 이웃이 모두 위험 칸인 칸
    private static IEnumerable<Vector3Int> Centers(ICollection<Vector3Int> cells)
    {
        foreach (Vector3Int cell in cells)
        {
            bool full = true;
            for (int y = -1; y <= 1 && full; y++)
                for (int x = -1; x <= 1 && full; x++)
                    full = cells.Contains(cell + new Vector3Int(x, y, 0));
            if (full) yield return cell;
        }
    }

    // ---------------- 재생 ----------------
    private sealed class FxPlayer : MonoBehaviour
    {
        public Sprite[] Frames;
        public float[] Durations;
        public float Delay;
        public System.Action Callback;

        private IEnumerator Start()
        {
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.enabled = false;
            if (Delay > 0f) yield return new WaitForSeconds(Delay);
            Callback?.Invoke();
            if (renderer != null && Frames != null)
            {
                renderer.enabled = true;
                for (int i = 0; i < Frames.Length; i++)
                {
                    renderer.sprite = Frames[i];
                    yield return new WaitForSeconds(Durations[Mathf.Min(i, Durations.Length - 1)]);
                }
            }
            Destroy(gameObject);
        }
    }

    private static void Spawn(Vector3 position, Sprite[] frames, float[] durations, float delay, int order)
    {
        var go = new GameObject("QueenFx");
        go.transform.position = new Vector3(Mathf.Round(position.x * Ppu) / Ppu, Mathf.Round(position.y * Ppu) / Ppu, 0f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        renderer.sprite = frames[0];
        var player = go.AddComponent<FxPlayer>();
        player.Frames = frames;
        player.Durations = durations;
        player.Delay = delay;
    }

    private static void Delayed(GridManager grid, float delay, System.Action action)
    {
        var go = new GameObject("QueenFxTimer");
        var player = go.AddComponent<FxPlayer>();
        player.Delay = delay;
        player.Callback = action;
    }

    // ---------------- 그림(코드로 찍는 도트) ----------------
    private static void Ensure()
    {
        if (beams != null) return;
        beams = new Sprite[4][];
        // 프레임: 가는 섬광 → 굵어짐 → 흰 심지 최대 → 가장자리 끊기며 옅어짐 → 가늘게 사라짐
        var widths = new[] { 3, 6, 7, 6, 3 };
        var alphas = new[] { 1f, 1f, 1f, 0.75f, 0.45f };
        for (int d = 0; d < 4; d++)
        {
            beams[d] = new Sprite[widths.Length];
            for (int f = 0; f < widths.Length; f++)
                beams[d][f] = Make(32, 32, (x, y) => BeamPixel(d, x, y, widths[f], alphas[f], f == 2, f >= 3), new Vector2(0.5f, 0.5f));
        }
        bursts = new Sprite[4];
        for (int f = 0; f < 4; f++)
        {
            int frame = f;
            bursts[f] = Make(32, 32, (x, y) => BurstPixel(x, y, frame), new Vector2(0.5f, 0.5f));
        }
        pillar = new Sprite[5];
        var pw = new[] { 2, 6, 8, 6, 3 };
        for (int f = 0; f < 5; f++)
        {
            int frame = f;
            pillar[f] = Make(32, 160, (x, y) => PillarPixel(x, y, pw[frame], frame), new Vector2(0.5f, 0.5f));
        }
    }

    // d: 0 가로, 1 세로, 2 '/', 3 '\'. 띠 가운데로부터의 거리로 색을 고른다.
    private static Color32 BeamPixel(int d, int x, int y, int half, float alpha, bool hot, bool broken)
    {
        float dist = d == 0 ? Mathf.Abs(y - 15.5f) : d == 1 ? Mathf.Abs(x - 15.5f)
            : d == 2 ? Mathf.Abs((x - y)) / 1.414f : Mathf.Abs((x + y - 31)) / 1.414f;
        if (dist > half + 0.5f) return default;
        float t = dist / Mathf.Max(1f, half);
        // 가장자리는 3px마다 끊겨 일렁인다(옅어지는 프레임은 더 많이 끊긴다).
        float along = d == 0 ? x : d == 1 ? y : (x + (d == 2 ? y : 31 - y)) * 0.5f;
        if (t > 0.75f && ((int)along / 3) % (broken ? 2 : 3) == 0) return default;
        Color32 c = t < 0.2f ? (hot ? White : GoldL) : t < 0.45f ? GoldL : t < 0.7f ? GoldM : t < 0.9f ? Gold : GoldD;
        c.a = (byte)(255 * alpha * (t > 0.85f ? 0.7f : 1f));
        return c;
    }

    private static Color32 BurstPixel(int x, int y, int frame)
    {
        float dx = x - 15.5f, dy = y - 15.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
        float ring = 4f + frame * 3.5f;
        bool onRing = Mathf.Abs(r - ring) < (frame < 2 ? 1.6f : 1f);
        bool ray = (Mathf.Abs(dx) < 1f || Mathf.Abs(dy) < 1f) && r < ring + 4f && r > ring - 6f;
        bool diag = Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) < 1f && r < ring + 1f && r > ring - 4f;
        if (frame == 0 && r < 4f) return White;
        if (onRing) return frame < 2 ? GoldL : new Color32(GoldM.r, GoldM.g, GoldM.b, (byte)(255 - frame * 50));
        if (ray) return frame < 2 ? White : GoldL;
        if (diag && frame < 3) return GoldM;
        return default;
    }

    private static Color32 PillarPixel(int x, int y, int half, int frame)
    {
        // y: 0 = 아래(바닥), 159 = 하늘
        float dist = Mathf.Abs(x - 15.5f);
        if (dist > half + 0.5f)
        {
            // 바닥 착지 섬광(아래 12px에 넓게 퍼진 빛)
            if (frame >= 1 && frame <= 3 && y < 10 && dist < half + 8 - y * 0.6f) return y < 4 ? GoldL : GoldM;
            return default;
        }
        float t = dist / Mathf.Max(1f, half);
        if (t > 0.75f && (y / 3) % 2 == 0) return default;
        Color32 c = t < 0.25f ? (frame == 2 ? White : GoldL) : t < 0.55f ? GoldL : t < 0.8f ? GoldM : Gold;
        float fade = Mathf.Clamp01(1.2f - y / 160f);              // 하늘 쪽으로 옅어진다
        if (frame == 4) fade *= 0.5f;
        c.a = (byte)(255 * fade);
        return c;
    }

    private static Sprite[] Flash(Color32 color)
    {
        if (flashes.TryGetValue(color, out Sprite[] cached)) return cached;
        var frames = new Sprite[4];
        var alphas = new[] { 200, 150, 90, 45 };
        for (int f = 0; f < 4; f++)
        {
            int frame = f;
            frames[f] = Make(32, 32, (x, y) =>
            {
                bool edge = x < 2 || y < 2 || x > 29 || y > 29;
                bool hatch = (x + y + frame * 2) % 6 == 0;
                if (!edge && !hatch && frame > 0) return default;
                Color32 c = frame == 0 && !edge ? White : color;
                c.a = (byte)alphas[frame];
                return c;
            }, new Vector2(0.5f, 0.5f));
        }
        flashes[color] = frames;
        return frames;
    }

    // 번개: 32×160, 2~4px 계단으로 꺾이며 내려오는 3px 줄기(흰 심지) + 어두운 테두리. 모양 3가지.
    private static Sprite[][] Bolts(Color32 color)
    {
        if (bolts.TryGetValue(color, out Sprite[][] cached)) return cached;
        var set = new Sprite[3][];
        Color32 dark = new Color32((byte)(color.r / 2), (byte)(color.g / 2), (byte)(color.b / 2), 255);
        for (int v = 0; v < 3; v++)
        {
            var rnd = new System.Random(17 + v * 31);
            var path = new int[160];
            int x = 16;
            for (int y = 159; y >= 0;)
            {
                int step = rnd.Next(3, 7), shift = rnd.Next(-3, 4);
                for (int i = 0; i < step && y >= 0; i++, y--)
                    path[y] = Mathf.Clamp(x + (i >= step / 2 ? shift : 0), 4, 27);
                x = Mathf.Clamp(x + shift, 4, 27);
            }
            path[0] = path[1] = path[2] = 16;
            set[v] = new Sprite[3];
            for (int f = 0; f < 3; f++)
            {
                int frame = f;
                set[v][f] = Make(32, 160, (px, py) =>
                {
                    int d = Mathf.Abs(px - path[py]);
                    if (frame == 2 && py > 60) return default;                 // 사라질 때는 위에서부터 걷힌다
                    if (d == 0) return White;
                    if (d == 1) return color;
                    if (d == 2) return dark;
                    return default;
                }, new Vector2(0.5f, 0.5f));
            }
        }
        bolts[color] = set;
        return set;
    }

    private static Sprite Make(int w, int h, System.Func<int, int, Color32> pixel, Vector2 pivot)
    {
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = pixel(x, y);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, w, h), pivot, Ppu);
    }
}
