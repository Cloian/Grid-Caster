using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 기사단장 공격 연출(판정과 무관). 여왕의 금빛 광선과 구분되도록 강철·청록·흙먼지 색만 쓴다.
// - 돌진: 청록 잔상이 길게 남고, 지나간 3칸 폭 줄에 진행 방향 속도선과 흙먼지가 일어나며, 멈춘 앞에서 창끝 충격이 터진다.
// - 휩쓸기: 몸 가운데를 축으로 앞쪽 반원(±72°)을 그리는 베기.
public static class KnightCommanderFx
{
    private const float Ppu = 32f;
    public const float StepSeconds = 0.016f;          // 돌진 줄이 한 칸 지나가는 시간

    private static readonly Color32 Steel = new Color32(236, 244, 246, 255);
    private static readonly Color32 TealL = new Color32(112, 214, 196, 255);
    private static readonly Color32 Teal = new Color32(38, 140, 128, 255);
    private static readonly Color32 TealD = new Color32(22, 64, 70, 255);
    private static readonly Color32 DustL = new Color32(196, 160, 112, 255);
    private static readonly Color32 Dust = new Color32(140, 104, 70, 255);

    private static Sprite[] streaks;      // 가로 방향 속도선(세로는 90° 돌려 쓴다)
    private static Sprite[] dust;
    private static Sprite[] lanceBurst;
    private static Sprite[] crescent;     // 오른쪽을 향한 베기(다른 방향은 돌려 쓴다)

    // ---------------- 공개 연출 ----------------
    // 돌진 잔상: 지나온 길에 지금 그림을 옅은 강철빛 청록으로 물들여 4장 남긴다.
    public static void Gallop(SpriteRenderer body, Vector3 from, Vector3 to)
    {
        if (body == null || body.sprite == null) return;
        for (int i = 0; i < 4; i++)
        {
            var go = new GameObject("CommanderGallopGhost");
            Vector3 p = Vector3.Lerp(from, to, i / 4f);
            go.transform.position = Snap(p);
            go.transform.localScale = body.transform.lossyScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = body.sprite;
            renderer.flipX = body.flipX;
            renderer.sortingOrder = body.sortingOrder - 1;
            var fade = go.AddComponent<GhostFade>();
            fade.Delay = i * 0.015f;
            fade.Seconds = 0.2f + i * 0.05f;
        }
    }

    // 돌진 줄: 칸마다 속도선, 양옆 줄에는 두 칸마다 흙먼지. 플레이어를 들이받았으면 그 칸에, 아니면 멈춘 앞에 창끝 충격.
    // 돌려주는 값은 플레이어 피격 표시를 늦출 시간.
    public static float Charge(GridManager grid, IEnumerable<Vector3Int> cells, Vector3Int from, Vector3Int direction,
        Vector3Int impactCell, bool hitPlayer)
    {
        Ensure();
        float angle = Angle(direction);
        int farthest = 0;
        foreach (Vector3Int cell in cells)
        {
            Vector3Int d = cell - from;
            int along = d.x * direction.x + d.y * direction.y;
            int lane = direction.x != 0 ? d.y : d.x;
            farthest = Mathf.Max(farthest, along);
            float delay = Mathf.Max(0, along) * StepSeconds;
            Vector3 center = grid.GetCellCenterWorld(cell);
            Spawn(center, streaks, new[] { 0.03f, 0.04f, 0.06f, 0.07f }, delay, -3, angle);
            if (lane != 0 && (along & 1) == 0)
                Spawn(center - new Vector3(direction.x, direction.y) * 0.3f, dust,
                    new[] { 0.05f, 0.07f, 0.09f, 0.11f }, delay + 0.03f, -2, 0f);
        }
        float impact = farthest * StepSeconds + 0.04f;
        Spawn(grid.GetCellCenterWorld(impactCell), lanceBurst, new[] { 0.04f, 0.05f, 0.07f, 0.09f }, impact, 12, angle);
        Delayed(impact, () => CameraController.PlayImpactShake(new Vector3(direction.x, direction.y)));
        return hitPlayer ? impact + 0.03f : 0f;
    }

    // 휩쓸기: 몸 가운데(center)에서 direction 쪽으로 초승달 베기.
    public static float Sweep(GridManager grid, Vector3Int center, Vector3Int direction)
    {
        Ensure();
        Spawn(grid.GetCellCenterWorld(center), crescent, new[] { 0.035f, 0.035f, 0.05f, 0.07f, 0.08f }, 0f, 12, Angle(direction));
        Delayed(0.07f, () => CameraController.PlayImpactShake(new Vector3(direction.x, direction.y)));
        return 0.08f;
    }

    private static float Angle(Vector3Int d) => d.x > 0 ? 0f : d.x < 0 ? 180f : d.y > 0 ? 90f : -90f;
    private static Vector3 Snap(Vector3 p) => new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);

    // ---------------- 재생 ----------------
    private sealed class GhostFade : MonoBehaviour
    {
        public float Delay, Seconds;
        private SpriteRenderer renderer;
        private float elapsed;
        private static readonly Color Tint = new Color(0.62f, 1f, 0.95f, 1f);

        private void Start()
        {
            renderer = GetComponent<SpriteRenderer>();
            renderer.color = new Color(Tint.r, Tint.g, Tint.b, 0f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = (elapsed - Delay) / Seconds;
            if (t < 0f) return;
            if (t >= 1f) { Destroy(gameObject); return; }
            renderer.color = new Color(Tint.r, Tint.g, Tint.b, 0.42f * (1f - t));
        }
    }

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

    private static void Spawn(Vector3 position, Sprite[] frames, float[] durations, float delay, int order, float angle)
    {
        var go = new GameObject("CommanderFx");
        go.transform.position = Snap(position);
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        renderer.sprite = frames[0];
        var player = go.AddComponent<FxPlayer>();
        player.Frames = frames;
        player.Durations = durations;
        player.Delay = delay;
    }

    private static void Delayed(float delay, System.Action action)
    {
        var player = new GameObject("CommanderFxTimer").AddComponent<FxPlayer>();
        player.Delay = delay;
        player.Callback = action;
    }

    // ---------------- 그림(코드로 찍는 도트) ----------------
    private static void Ensure()
    {
        if (streaks != null) return;
        streaks = new Sprite[4];
        for (int f = 0; f < 4; f++) { int frame = f; streaks[f] = Make(32, 32, (x, y) => StreakPixel(x, y, frame)); }
        dust = new Sprite[4];
        for (int f = 0; f < 4; f++) { int frame = f; dust[f] = Make(32, 32, (x, y) => DustPixel(x, y, frame)); }
        lanceBurst = new Sprite[4];
        for (int f = 0; f < 4; f++) { int frame = f; lanceBurst[f] = Make(64, 64, (x, y) => LancePixel(x, y, frame)); }
        crescent = new Sprite[5];
        for (int f = 0; f < 5; f++) { int frame = f; crescent[f] = Make(256, 256, (x, y) => CrescentPixel(x, y, frame)); }
    }

    // 속도선: 세 줄(위·가운데·아래)이 진행 방향(오른쪽)으로 뻗었다가 꼬리부터 걷힌다. 가운데 줄은 강철색 심지.
    private static Color32 StreakPixel(int x, int y, int frame)
    {
        int[] rows = { 7, 16, 25 };
        int[] shift = { 6, 0, 10 };
        for (int i = 0; i < rows.Length; i++)
        {
            int dy = Mathf.Abs(y - rows[i]);
            if (dy > (i == 1 ? 1 : 0)) continue;
            int head = Mathf.Min(31, 10 + frame * 9 + shift[i] / 2);
            int tail = frame < 2 ? shift[i] : shift[i] + (frame - 1) * 10;
            if (x < tail || x > head) continue;
            bool hot = i == 1 && dy == 0 && frame < 3;
            Color32 c = hot ? Steel : x > head - 4 ? TealL : Teal;
            c.a = (byte)(frame == 3 ? 150 : 255);
            return c;
        }
        return default;
    }

    // 흙먼지: 바닥에서 뭉게뭉게 퍼지는 세 덩이, 점점 옅어지며 끊긴다.
    private static Color32 DustPixel(int x, int y, int frame)
    {
        float r = 4f + frame * 2.2f;
        (float cx, float cy)[] puffs = { (12f, 10f), (20f, 11f), (16f, 15f + frame) };
        foreach ((float cx, float cy) in puffs)
        {
            float dx = x - cx, dy = (y - cy) * 1.3f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d > r) continue;
            if (frame >= 2 && (x + y) % (frame == 2 ? 3 : 2) == 0) return default;
            Color32 c = d < r - 2.5f && dy > -1f ? DustL : Dust;
            c.a = (byte)(new[] { 230, 200, 150, 100 }[frame]);
            return c;
        }
        return default;
    }

    // 창끝 충격(64px, 오른쪽으로 들이받는 모양): 앞쪽으로 긴 쐐기 섬광 + 청록 반원 충격파 + 위아래로 튀는 파편.
    private static Color32 LancePixel(int x, int y, int frame)
    {
        float dx = x - 31.5f, dy = y - 31.5f, r = Mathf.Sqrt(dx * dx + dy * dy);
        float reach = 12f + frame * 7f;
        bool wedge = dx > -6f && dx < reach && Mathf.Abs(dy) < Mathf.Max(0.6f, (reach - dx) * 0.3f) && frame < 3;
        if (wedge) return Mathf.Abs(dy) < 1.6f || frame == 0 ? Steel : TealL;
        float ring = 7f + frame * 6f;
        if (dx > -4f && Mathf.Abs(r - ring) < (frame < 2 ? 2f : 1.2f))
        {
            Color32 c = frame < 2 ? TealL : Teal;
            c.a = (byte)(255 - frame * 45);
            return c;
        }
        bool shard = Mathf.Abs(Mathf.Abs(dy) - (dx + 2f) * 1.1f) < 1f && r > ring - 3f && r < ring + 2f && frame >= 1;
        if (shard) return frame < 3 ? Steel : TealD;
        if (frame == 0 && r < 7f) return Steel;
        return default;
    }

    // 반원 베기(오른쪽): 몸 가운데에서 반지름 약 2.9칸(몸 밖 1.5~4.2칸 띠 가운데)을 지나는 호.
    // 맞는 칸이 몸보다 앞쪽 4줄이라 호는 -72°에서 정면을 지나 +72°까지 그어지고, 가운데가 두껍고 양 끝이 가는 초승달이 된다.
    // 바깥 날은 강철색, 안쪽은 청록, 마지막 프레임은 성글게 흩어진다.
    private static Color32 CrescentPixel(int x, int y, int frame)
    {
        float dx = x - 127.5f, dy = y - 127.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        float lead = Mathf.Min(-72f + (Mathf.Min(frame, 3) + 1) * 36f, 72f);   // 칼날이 지금 있는 각도
        if (angle < -72f || angle > lead) return default;
        float u = (angle + 72f) / 144f;                                         // 호 전체에서의 위치
        float thick = 3f + 26f * Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.8f);
        if (frame == 4) thick *= 0.6f;
        float s = (r - 92f) / (thick * 0.5f);                                  // -1 안쪽 … +1 바깥
        if (s < -1f || s > 1f) return default;
        if (frame == 4 && (x + y) % 2 == 0) return default;
        bool tip = lead - angle < 14f && frame < 4;
        Color32 c = s > 0.7f ? Teal : s > 0.05f || tip ? Steel : s > -0.55f ? TealL : Teal;
        c.a = (byte)(frame == 4 ? 150 : 255);
        return c;
    }

    private static Sprite Make(int w, int h, System.Func<int, int, Color32> pixel)
    {
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = pixel(x, y);
        var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu);
    }
}
