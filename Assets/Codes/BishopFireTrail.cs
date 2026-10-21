using System.Collections.Generic;
using UnityEngine;

// 불길은 지나갈 수 없는 벽이다(플레이어·적 이동 불가, 투사체는 통과). 수명은 적 턴 시작 때만 감소한다.
// 진입 피해 코드는 남아 있지만 이동 판정이 불길 칸을 막으므로 정상 이동으로는 들어갈 수 없다.
public sealed class BishopFireTrail : MonoBehaviour
{
    private readonly Dictionary<Vector3Int, int> expiryTurns = new Dictionary<Vector3Int, int>();
    private readonly List<Vector3Int> expired = new List<Vector3Int>();
    private GridManager grid;
    private Move player;
    private Font font;
    private int turn;
    private int lifetime = 3;
    private int damage = 1;

    // 연출: 칸마다 타오르는 픽셀 불꽃. 규칙(피해·수명)은 즉시 적용하고,
    // 그림은 비숍(아르마딜로)이 그 칸을 굴러 지나갈 때 나타난다.
    // 불꽃 끝만 흔들리는 낮은 불길을 천천히 돌려 깜빡임 피로를 줄인다.
    private const float FireFramesPerSecond = 4f;
    private const float RevealRadius = 0.55f;
    private static Sprite[] fireFrames;
    private readonly Dictionary<Vector3Int, SpriteRenderer> visuals = new Dictionary<Vector3Int, SpriteRenderer>();
    private readonly HashSet<Vector3Int> hidden = new HashSet<Vector3Int>();
    private readonly List<Vector3Int> revealed = new List<Vector3Int>();

    public Font LabelFont => font;
    public int Count => expiryTurns.Count;
    public bool HasFire(Vector3Int cell) => expiryTurns.ContainsKey(cell);
    public int RemainingTurns(Vector3Int cell) => expiryTurns.TryGetValue(cell, out int expiry) ? expiry - turn : 0;

    public void Initialize(GridManager map, Move target, int turns, int hitDamage)
    {
        if (player != null) player.PlayerMoved -= OnPlayerMoved;
        grid = map;
        player = target;
        lifetime = Mathf.Max(1, turns);
        damage = Mathf.Max(1, hitDamage);
        player.PlayerMoved += OnPlayerMoved;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 16);
    }

    public void AddFire(Vector3Int cell)
    {
        if (!grid.IsWalkableCell(cell)) return;
        // 플레이어를 완전히 가두는 불은 놓지 않는다(주변 8칸 중 하나는 늘 비워 둔다).
        if (!expiryTurns.ContainsKey(cell) && WouldSealPlayer(cell)) return;
        expiryTurns[cell] = turn + lifetime;
        if (!visuals.ContainsKey(cell)) CreateVisual(cell);
    }

    private bool WouldSealPlayer(Vector3Int candidate)
    {
        if (player == null || grid == null) return false;
        Vector3Int center = player.GridPosition;
        if (candidate == center) return false;
        int exits = 0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                Vector3Int cell = center + new Vector3Int(x, y, 0);
                if (cell != candidate && grid.IsWalkableCell(cell) && !expiryTurns.ContainsKey(cell)) exits++;
            }
        return exits == 0;
    }

    public bool RemoveFire(Vector3Int cell)
    {
        RemoveVisual(cell);
        return expiryTurns.Remove(cell);
    }

    private void CreateVisual(Vector3Int cell)
    {
        if (fireFrames == null)
        {
            fireFrames = Resources.LoadAll<Sprite>("Effects/fire_trail");
            System.Array.Sort(fireFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        }
        if (fireFrames == null || fireFrames.Length == 0) return;
        GameObject visual = new GameObject("FireTrail_" + cell.x + "_" + cell.y);
        visual.transform.SetParent(transform, false);
        visual.transform.position = grid.GetCellCenterWorld(cell);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = fireFrames[0];
        renderer.sortingOrder = -5; // 바닥(-10) 위, 캐릭터·그림자 아래
        renderer.enabled = false;
        visuals[cell] = renderer;
        hidden.Add(cell);
    }

    private void RemoveVisual(Vector3Int cell)
    {
        hidden.Remove(cell);
        if (!visuals.TryGetValue(cell, out SpriteRenderer renderer)) return;
        visuals.Remove(cell);
        if (renderer != null) Destroy(renderer.gameObject);
    }

    private void Update()
    {
        if (visuals.Count == 0) return;
        if (hidden.Count > 0) RevealPassedCells();
        if (fireFrames == null || fireFrames.Length == 0) return;
        foreach (var pair in visuals)
        {
            if (pair.Value == null || !pair.Value.enabled) continue;
            int phase = (pair.Key.x * 3 + pair.Key.y * 5) & 3;
            pair.Value.sprite = fireFrames[(Mathf.FloorToInt(Time.time * FireFramesPerSecond) + phase) % fireFrames.Length];
        }
    }

    // 굴러가는 비숍이 지나간 칸부터 불꽃을 켠다. 움직이는 비숍이 없으면 남은 칸을 모두 켠다.
    private void RevealPassedCells()
    {
        bool anyMoving = false;
        revealed.Clear();
        foreach (MonsterMovement monster in FindObjectsByType<MonsterMovement>(FindObjectsSortMode.None))
        {
            if (monster.MovementPattern != MonsterMovementPattern.Bishop || !monster.IsMovingVisual) continue;
            anyMoving = true;
            Vector3 position = monster.transform.position;
            foreach (Vector3Int cell in hidden)
            {
                Vector3 center = grid.GetCellCenterWorld(cell);
                if (new Vector2(position.x - center.x, position.y - center.y).sqrMagnitude <= RevealRadius * RevealRadius)
                    revealed.Add(cell);
            }
        }
        if (!anyMoving) revealed.AddRange(hidden);
        foreach (Vector3Int cell in revealed)
        {
            hidden.Remove(cell);
            if (visuals.TryGetValue(cell, out SpriteRenderer renderer) && renderer != null) renderer.enabled = true;
        }
    }

    private void OnPlayerMoved()
    {
        ResolvePlayerEntry();
    }

    public void ResolvePlayerEntry()
    {
        if (player.CurrentHealth > 0 && HasFire(player.GridPosition))
            player.TakeDamage(damage, PlayerDamageKind.Hazard);
    }

    public void AdvanceTurn()
    {
        turn++;
        expired.Clear();
        foreach (var fire in expiryTurns) if (fire.Value <= turn) expired.Add(fire.Key);
        foreach (Vector3Int cell in expired)
        {
            expiryTurns.Remove(cell);
            RemoveVisual(cell);
        }
    }

    public void Clear()
    {
        expiryTurns.Clear();
        foreach (SpriteRenderer renderer in visuals.Values) if (renderer != null) Destroy(renderer.gameObject);
        visuals.Clear();
        hidden.Clear();
        turn = 0;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (camera == null || grid == null) return;
        Rect viewport = camera.pixelRect;
        GUI.BeginGroup(new Rect(viewport.xMin, Screen.height - viewport.yMax,
            viewport.width, viewport.height));
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            font = font, fontSize = 14, alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold
        };
        // 남은 턴만 칸 오른쪽 아래에 작게 표시한다(불꽃 그림이 칸을 대표한다).
        float cellPixels = camera.WorldToScreenPoint(grid.GetCellCenterWorld(Vector3Int.right)).x
            - camera.WorldToScreenPoint(grid.GetCellCenterWorld(Vector3Int.zero)).x;
        foreach (var fire in expiryTurns)
        {
            if (hidden.Contains(fire.Key)) continue;
            Vector3 point = camera.WorldToScreenPoint(grid.GetCellCenterWorld(fire.Key));
            if (point.z <= 0) continue;
            Rect cell = new Rect(point.x - viewport.xMin - cellPixels * 0.5f, viewport.yMax - point.y - cellPixels * 0.5f,
                cellPixels - 4f, cellPixels - 2f);
            style.normal.textColor = new Color(0.12f, 0.08f, 0.1f);
            GUI.Label(new Rect(cell.x + 1f, cell.y + 1f, cell.width, cell.height), (fire.Value - turn).ToString(), style);
            style.normal.textColor = new Color(1f, 0.92f, 0.45f);
            GUI.Label(cell, (fire.Value - turn).ToString(), style);
        }
        GUI.EndGroup();
    }

    private void OnDestroy()
    {
        foreach (SpriteRenderer renderer in visuals.Values) if (renderer != null) Destroy(renderer.gameObject);
        if (player != null) player.PlayerMoved -= OnPlayerMoved;
        if (font != null) Destroy(font);
    }
}
