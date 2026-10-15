using System.Collections.Generic;
using UnityEngine;

// 몬스터 머리 위·발밑 정보: 체력 칸, 조준 중 예상 피해(깎일 칸 깜빡임·처치 해골), 다음 행동 아이콘, 피해 숫자.
// 모든 그림은 32 PPU 도트로 런타임에 만든다(타일 1px = 그림 1px). 판정에는 관여하지 않는다.
public sealed class MonsterStatusHud : MonoBehaviour
{
    private const float PixelsPerUnit = 32f;
    // 아이콘·숫자는 타일 픽셀 2배 크기로 그려 작은 칸에서도 읽히게 한다.
    private const float LargePixelsPerUnit = 16f;
    private const int SortingOrder = 30;
    private const int MaximumPips = 10;
    private const float BlinkSeconds = 0.18f;
    private const float NumberRiseUnits = 0.45f;
    private const float NumberSeconds = 0.75f;

    private static readonly Color32 Outline = new Color32(30, 22, 34, 255);
    private static readonly Color32 PipFull = new Color32(224, 69, 58, 255);
    private static readonly Color32 PipShine = new Color32(246, 138, 112, 255);
    private static readonly Color32 PipEmpty = new Color32(70, 48, 60, 255);
    private static readonly Color32 PipDoomed = new Color32(255, 244, 220, 255);
    private static readonly Color32 NumberColor = new Color32(255, 230, 128, 255);
    private static readonly Color32 PlayerNumberColor = new Color32(255, 110, 96, 255);

    public enum Intent { None, Attack, Move, Leap, Wait, Vulnerable, Fire, Crossbow, Blocked }

    // 7×5 이하 아이콘(외곽선은 자동). '#' = 본색.
    private static readonly Dictionary<Intent, (string[] Rows, Color32 Color)> Icons = new Dictionary<Intent, (string[], Color32)>
    {
        [Intent.Attack] = (new[] { "....##", "...###", "#.###.", ".###..", "..#...", ".#.#..", "#....." }, new Color32(240, 92, 72, 255)),
        [Intent.Move] = (new[] { "#..#..", ".#..#.", "..#..#", ".#..#.", "#..#.." }, new Color32(220, 186, 144, 255)),
        [Intent.Leap] = (new[] { "##.##", "##.##", "##.##", "##.##", ".###." }, new Color32(244, 170, 77, 255)),
        [Intent.Wait] = (new[] { "#####", ".#.#.", "..#..", ".#.#.", "#####" }, new Color32(176, 160, 150, 255)),
        [Intent.Vulnerable] = (new[] { "#####", "#.#.#", "##.##", ".#.#.", "..#.." }, new Color32(95, 205, 228, 255)),
        [Intent.Fire] = (new[] { "..#..", ".##..", ".###.", "#####", ".###." }, new Color32(250, 122, 0, 255)),
        [Intent.Crossbow] = (new[] { "..#..", ".###.", "##.##", ".###.", "..#.." }, new Color32(240, 60, 60, 255)),
        [Intent.Blocked] = (new[] { ".###.", ".#.#.", "#####", "##.##", "#####" }, new Color32(150, 170, 190, 255)),
    };
    private static readonly string[] Skull = { ".###.", "#####", "#.#.#", "#####", ".#.#." };

    // 3×5 숫자 글꼴
    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" }, ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
        ['2'] = new[] { "###", "..#", "###", "#..", "###" }, ['3'] = new[] { "###", "..#", "###", "..#", "###" },
        ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" }, ['5'] = new[] { "###", "#..", "###", "..#", "###" },
        ['6'] = new[] { "###", "#..", "###", "#.#", "###" }, ['7'] = new[] { "###", "..#", ".#.", ".#.", ".#." },
        ['8'] = new[] { "###", "#.#", "###", "#.#", "###" }, ['9'] = new[] { "###", "#.#", "###", "..#", "###" },
        ['-'] = new[] { "...", "...", "###", "...", "..." },
    };

    private sealed class View
    {
        public GameObject Root;
        public SpriteRenderer Bar;
        public SpriteRenderer Icon;
        public string BarKey;
        public Intent ShownIntent = (Intent)(-1);
        public bool ShownSkull;
    }

    private sealed class FloatingNumber
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public Vector3 Start;
        public float Elapsed;
    }

    private static MonsterStatusHud instance;
    private readonly Dictionary<MonsterMovement, View> views = new Dictionary<MonsterMovement, View>();
    private readonly List<FloatingNumber> numbers = new List<FloatingNumber>();
    private readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
    private readonly List<MonsterMovement> stale = new List<MonsterMovement>();
    private MonsterSpawner spawner;
    private Move player;

    public static bool IsActive => instance != null && instance.isActiveAndEnabled;

    public static MonsterStatusHud Attach(MonsterSpawner owner, Move target)
    {
        MonsterStatusHud hud = owner.GetComponent<MonsterStatusHud>();
        if (hud == null) hud = owner.gameObject.AddComponent<MonsterStatusHud>();
        hud.spawner = owner;
        hud.player = target;
        return hud;
    }

    private void OnEnable()
    {
        instance = this;
        CharacterHealth.AnyDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        CharacterHealth.AnyDamaged -= HandleDamaged;
        if (instance == this) instance = null;
        foreach (View view in views.Values) if (view.Root != null) Destroy(view.Root);
        views.Clear();
        foreach (FloatingNumber number in numbers) if (number.Root != null) Destroy(number.Root);
        numbers.Clear();
    }

    // 현재 규칙으로 보이는 다음 행동. 미래 AI를 실행하지 않는다.
    public static Intent IntentFor(MonsterMovement monster, Vector3Int playerCell)
    {
        if (monster == null || monster.IsDead) return Intent.None;
        if (monster.IsActionBlocked) return Intent.Blocked;
        if (monster.TryGetComponent(out ChessMonsterBehaviour chess))
        {
            switch (monster.MovementPattern)
            {
                case MonsterMovementPattern.Knight:
                    return chess.IsKnightCounterVulnerable ? Intent.Vulnerable
                        : chess.WillKnightJumpNextTurn ? Intent.Leap : Intent.Wait;
                case MonsterMovementPattern.Bishop:
                    return Intent.Fire;
                case MonsterMovementPattern.Rook:
                    return chess.IsRookCharged ? Intent.Crossbow : Intent.Wait;
            }
        }
        Vector3Int delta = playerCell - monster.GridPosition;
        int dx = Mathf.Abs(delta.x), dy = Mathf.Abs(delta.y);
        bool adjacent = monster.MovementPattern == MonsterMovementPattern.EightDirection
            ? Mathf.Max(dx, dy) == 1 : dx + dy == 1;
        return adjacent ? Intent.Attack : Intent.Move;
    }

    private void LateUpdate()
    {
        if (spawner == null) return;
        IReadOnlyDictionary<MonsterMovement, int> prediction = player != null ? player.AttackPrediction : null;
        bool blinkOn = Mathf.FloorToInt(Time.unscaledTime / BlinkSeconds) % 2 == 0;
        Vector3Int playerCell = player != null ? player.GridPosition : Vector3Int.zero;

        foreach (MonsterMovement monster in spawner.ActiveMonsters)
        {
            if (monster == null || monster.IsDead) continue;
            if (!views.TryGetValue(monster, out View view)) views[monster] = view = CreateView(monster);
            CharacterHealth health = monster.GetComponent<CharacterHealth>();
            int predicted = prediction != null && prediction.TryGetValue(monster, out int value) ? value : 0;
            bool lethal = predicted > 0 && predicted >= health.CurrentHealth;

            string key = $"{health.CurrentHealth}/{health.MaxHealth}/{predicted}/{(predicted > 0 && blinkOn ? 1 : 0)}";
            if (key != view.BarKey)
            {
                view.BarKey = key;
                view.Bar.sprite = BarSprite(health.CurrentHealth, health.MaxHealth, predicted, predicted > 0 && blinkOn);
            }

            Intent intent = IntentFor(monster, playerCell);
            if (lethal != view.ShownSkull || (!lethal && intent != view.ShownIntent))
            {
                view.ShownSkull = lethal;
                view.ShownIntent = intent;
                view.Icon.sprite = lethal ? IconSprite("skull", Skull, PipDoomed)
                    : intent == Intent.None ? null : IconSprite(intent.ToString(), Icons[intent].Rows, Icons[intent].Color);
            }
            view.Icon.enabled = !lethal || blinkOn;

            // 몸을 따라가되 도약 높이는 빼서 바닥 기준에 둔다. 화면 픽셀 격자에 맞춰 반올림한다.
            view.Root.transform.position = Snap(monster.transform.position - Vector3.up * monster.VisualLift);
        }

        stale.Clear();
        foreach (KeyValuePair<MonsterMovement, View> entry in views)
            if (entry.Key == null || entry.Key.IsDead) stale.Add(entry.Key);
        foreach (MonsterMovement monster in stale)
        {
            if (views[monster].Root != null) Destroy(views[monster].Root);
            views.Remove(monster);
        }

        UpdateNumbers();
    }

    private View CreateView(MonsterMovement monster)
    {
        var view = new View { Root = new GameObject("MonsterStatus_" + monster.name) };
        view.Root.transform.SetParent(transform, false);
        view.Bar = AddRenderer(view.Root, "Health", new Vector3(0f, -0.5f + 1f / PixelsPerUnit, 0f), new Vector2(0.5f, 0f));
        // 머리 위 가운데: 아이콘 아래쪽 4px만 칸 안에 걸치고 나머지는 위로 나온다.
        view.Icon = AddRenderer(view.Root, "Intent", new Vector3(0f, 0.5f - 4f / PixelsPerUnit, 0f), new Vector2(0.5f, 0f));
        return view;
    }

    private SpriteRenderer AddRenderer(GameObject root, string name, Vector3 localPosition, Vector2 pivot)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        child.transform.localPosition = localPosition;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = SortingOrder;
        return renderer;
    }

    private static Vector3 Snap(Vector3 position)
    {
        return new Vector3(Mathf.Round(position.x * PixelsPerUnit) / PixelsPerUnit,
            Mathf.Round(position.y * PixelsPerUnit) / PixelsPerUnit, position.z);
    }

    // 체력 칸: 체력 1당 3px 칸. 10칸을 넘으면 같은 너비의 연속 막대로 그린다. 예상 피해 칸은 흰색으로 깜빡인다.
    private Sprite BarSprite(int current, int maximum, int predicted, bool showDoomed)
    {
        string key = $"bar:{current}/{maximum}/{predicted}/{showDoomed}";
        if (spriteCache.TryGetValue(key, out Sprite cached)) return cached;
        maximum = Mathf.Max(1, maximum);
        bool pips = maximum <= MaximumPips;
        int inner = pips ? maximum * 4 - 1 : MaximumPips * 4 - 1;
        int width = inner + 2, height = 5;
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Outline;
        int remaining = Mathf.Max(0, current - predicted);
        for (int x = 0; x < inner; x++)
        {
            int unit;
            if (pips)
            {
                if (x % 4 == 3) continue; // 칸 사이 1px 구분선
                unit = x / 4;
            }
            else unit = Mathf.FloorToInt((x + 0.5f) / inner * maximum);
            Color32 fill = unit < remaining ? PipFull
                : unit < current ? (showDoomed ? PipDoomed : PipFull) : PipEmpty;
            for (int y = 1; y <= 3; y++)
            {
                Color32 color = fill;
                if (y == 3 && fill.Equals(PipFull)) color = PipShine; // 위 줄 하이라이트
                pixels[y * width + x + 1] = color;
            }
        }
        return spriteCache[key] = MakeSprite(key, width, height, pixels, new Vector2(0.5f, 0f), PixelsPerUnit);
    }

    private Sprite IconSprite(string id, string[] rows, Color32 color)
    {
        string key = "icon:" + id;
        if (spriteCache.TryGetValue(key, out Sprite cached)) return cached;
        int w = 0;
        foreach (string row in rows) w = Mathf.Max(w, row.Length);
        int h = rows.Length;
        Color32[] pixels = Outlined(w, h, (x, y) => rows[y][x] == '#', color);
        return spriteCache[key] = MakeSprite(key, w + 2, h + 2, pixels, new Vector2(0.5f, 0f), LargePixelsPerUnit);
    }

    private Sprite NumberSprite(string text, Color32 color)
    {
        string key = "num:" + text + color;
        if (spriteCache.TryGetValue(key, out Sprite cached)) return cached;
        // 글자 사이 2px: 외곽선이 서로 붙지 않게 한다.
        int w = text.Length * 5 - 2, h = 5;
        Color32[] pixels = Outlined(w, h, (x, y) =>
        {
            int glyph = x / 5, column = x % 5;
            return column < 3 && Glyphs.TryGetValue(text[glyph], out string[] rows) && rows[y][column] == '#';
        }, color);
        return spriteCache[key] = MakeSprite(key, w + 2, h + 2, pixels, new Vector2(0.5f, 0f), LargePixelsPerUnit);
    }

    // 1px 외곽선을 두른 비트맵. rows는 위에서 아래 순서라 텍스처(아래에서 위)로 뒤집는다.
    private static Color32[] Outlined(int w, int h, System.Func<int, int, bool> filled, Color32 color)
    {
        int width = w + 2, height = h + 2;
        Color32[] pixels = new Color32[width * height];
        bool Filled(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && filled(x, y);
        for (int y = -1; y <= h; y++)
            for (int x = -1; x <= w; x++)
            {
                int index = (h - y) * width + (x + 1);
                if (Filled(x, y)) pixels[index] = color;
                else if (Filled(x - 1, y) || Filled(x + 1, y) || Filled(x, y - 1) || Filled(x, y + 1))
                    pixels[index] = Outline;
            }
        return pixels;
    }

    private static Sprite MakeSprite(string name, int width, int height, Color32[] pixels, Vector2 pivot, float ppu)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0, 0, width, height), pivot, ppu);
    }

    private void HandleDamaged(CharacterHealth health, int damage)
    {
        if (health == null || damage <= 0) return;
        bool isPlayer = health.GetComponent<Move>() != null;
        if (!isPlayer && health.GetComponent<MonsterMovement>() == null) return;
        GameObject root = new GameObject("DamageNumber");
        root.transform.SetParent(transform, false);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = NumberSprite("-" + damage, isPlayer ? PlayerNumberColor : NumberColor);
        renderer.sortingOrder = SortingOrder + 1;
        // 범위 폭발 조각처럼 피해 연출이 늦게 도착하면 숫자도 그때 뜬다.
        float delay = CombatFeedback.DamageVisualDelay;
        // 같은 칸에 연달아 뜨는 숫자가 겹치지 않게 조금씩 위로 쌓는다.
        int stacked = 0;
        Vector3 start = health.transform.position + Vector3.up * 0.3f;
        foreach (FloatingNumber other in numbers)
            if ((other.Start - start).sqrMagnitude < 0.04f && other.Elapsed < 0.3f) stacked++;
        start += Vector3.up * (stacked * 7f / PixelsPerUnit);
        root.transform.position = Snap(start);
        renderer.enabled = delay <= 0f;
        numbers.Add(new FloatingNumber { Root = root, Renderer = renderer, Start = start, Elapsed = -delay });
    }

    private void UpdateNumbers()
    {
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            FloatingNumber number = numbers[i];
            // 결과 화면처럼 게임 시간이 멈춰도 숫자는 사라진다.
            number.Elapsed += Time.unscaledDeltaTime;
            if (number.Root == null) { numbers.RemoveAt(i); continue; }
            if (number.Elapsed < 0f) continue;
            number.Renderer.enabled = true;
            float t = number.Elapsed / NumberSeconds;
            if (t >= 1f)
            {
                Destroy(number.Root);
                numbers.RemoveAt(i);
                continue;
            }
            // 빠르게 튀어 올랐다가 멈추고, 마지막 30%에서 깜빡이며 사라진다.
            float rise = 1f - (1f - Mathf.Min(1f, t * 2.2f)) * (1f - Mathf.Min(1f, t * 2.2f));
            number.Root.transform.position = Snap(number.Start + Vector3.up * (rise * NumberRiseUnits));
            number.Renderer.enabled = t < 0.7f || Mathf.FloorToInt(number.Elapsed / 0.06f) % 2 == 0;
        }
    }
}
