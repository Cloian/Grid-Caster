using System;
using System.Collections.Generic;
using UnityEngine;

// 공개하기 전에 현재 점유와 실제 이동술로 A→B와 반격을 풀 수 있는지 검사한다.
public sealed class QueenSafeZonePlanner
{
    public sealed class Layout
    {
        public readonly HashSet<Vector3Int> A = new HashSet<Vector3Int>();
        public readonly HashSet<Vector3Int> B = new HashSet<Vector3Int>();
        public string Id;
    }

    private readonly List<Layout> templates = new List<Layout>();
    private readonly Queue<string> recent = new Queue<string>();
    private readonly System.Random random;
    private readonly GridManager grid;
    private readonly MonsterSpawner spawner;
    private readonly Move player;
    private readonly MonsterMovement queen;
    private readonly List<Vector3Int> floor = new List<Vector3Int>();
    private static readonly Vector3Int[] Steps = { Vector3Int.up, Vector3Int.right, Vector3Int.down,
        Vector3Int.left, new Vector3Int(1,1,0), new Vector3Int(1,-1,0), new Vector3Int(-1,1,0), new Vector3Int(-1,-1,0) };

    public QueenSafeZonePlanner(GridManager grid, MonsterSpawner spawner, Move player,
        MonsterMovement queen, int seed)
    {
        this.grid = grid; this.spawner = spawner; this.player = player; this.queen = queen;
        random = new System.Random(seed);
        Refresh();
    }

    // 여왕이 움직이면 바닥·후보를 현재 점유로 다시 만든다(최근 사용 기록과 난수 상태는 유지).
    public void Refresh()
    {
        floor.Clear(); templates.Clear();
        foreach (Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
            if (grid.IsWalkableCell(cell) && !spawner.TryGetMonsterAtCell(cell, out MonsterMovement actor)) floor.Add(cell);
        // 후보는 입력 전에만 고르며 발동 순간 재추첨하지 않는다.
        var ids = new HashSet<string>();
        Vector3Int o = QueenBossBehaviour.MapOrigin(grid);   // 지역 좌표(1~13) → 판 좌표
        for (int y = 1; y <= 12; y++) for (int x = 1; x <= 12; x++) foreach (Vector3Int step in Steps)
        {
            Vector3Int first = new Vector3Int(x, y, 0) + o, delta = step * 2;
            Add(first, new Vector3Int(13-x,13-y,0) + o, delta, -delta, ids);
            Vector3Int offset = new Vector3Int((x-1+6)%12+1,y,0) + o;
            Add(first, offset, delta, delta, ids);
            Add(first, offset, delta, -delta, ids);
        }
    }

    private void Add(Vector3Int first, Vector3Int second, Vector3Int delta, Vector3Int otherDelta, HashSet<string> ids)
    {
        var layout = new Layout(); AddSquare(layout.A, first); AddSquare(layout.A, second);
        AddSquare(layout.B, first + delta); AddSquare(layout.B, second + otherDelta);
        if (layout.A.Count != 8 || layout.B.Count != 8 || layout.A.Overlaps(layout.B)) return;
        foreach (Vector3Int cell in layout.A) if (!ValidCell(cell)) return;
        foreach (Vector3Int cell in layout.B) if (!ValidCell(cell)) return;
        var a = new List<Vector3Int>(layout.A); var b = new List<Vector3Int>(layout.B);
        a.Sort(Compare); b.Sort(Compare); layout.Id = string.Join(";", a) + "|" + string.Join(";", b);
        if (ids.Add(layout.Id)) templates.Add(layout);
    }

    private static int Compare(Vector3Int a, Vector3Int b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y);
    private bool ValidCell(Vector3Int cell) => grid.IsWalkableCell(cell)
        && (!spawner.TryGetMonsterAtCell(cell, out MonsterMovement actor) || actor != queen);
    private static void AddSquare(HashSet<Vector3Int> cells, Vector3Int origin)
    {
        for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) cells.Add(origin + new Vector3Int(x,y,0));
    }

    public bool TryChoose(Vector3Int origin, HashSet<Vector3Int> previousB, out Layout chosen)
    {
        // 근위대는 궁극기 중 정지한다. 현재 점유를 그대로 사용한 두 행동 도달 집합이다.
        var reachable = new HashSet<Vector3Int> { origin };
        var first = new List<Vector3Int> { origin };
        foreach (Vector3Int cell in floor) if (player.CanReachCellFrom(origin,cell)) { reachable.Add(cell); first.Add(cell); }
        foreach (Vector3Int from in first) foreach (Vector3Int cell in floor)
            if (!reachable.Contains(cell) && player.CanReachCellFrom(from,cell)) reachable.Add(cell);
        var candidates = new List<Layout>(templates);
        for (int i = candidates.Count-1; i > 0; i--) { int j = random.Next(i+1); (candidates[i],candidates[j])=(candidates[j],candidates[i]); }
        foreach (bool preferFresh in new[] {true,false}) foreach (Layout layout in candidates)
        {
            if (previousB != null && layout.A.Overlaps(previousB)) continue;
            if (preferFresh && recent.Contains(layout.Id)) continue;
            foreach (Vector3Int a in layout.A)
            {
                if (!reachable.Contains(a) || spawner.IsOccupied(a)) continue;
                foreach (Vector3Int b in layout.B)
                    if (player.CanReachCellFrom(a,b) && CanShootQueen(b))
                    {
                        chosen = layout; recent.Enqueue(layout.Id); while (recent.Count > 6) recent.Dequeue(); return true;
                    }
            }
        }
        chosen = null; return false;
    }

    public bool CanShootQueen(Vector3Int origin)
    {
        foreach (Vector3Int step in Steps)
        {
            TurnProjectile.FindImpact(grid, spawner, origin, step, out MonsterMovement target);
            if (target == queen) return true;
        }
        return false;
    }
}
