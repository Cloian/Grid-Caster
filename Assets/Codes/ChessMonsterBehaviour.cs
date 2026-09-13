using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterMovement))]
public sealed class ChessMonsterBehaviour : MonoBehaviour
{
    private static readonly Vector3Int[] KnightSteps =
    {
        new Vector3Int(2, 1, 0), new Vector3Int(1, 2, 0),
        new Vector3Int(-1, 2, 0), new Vector3Int(-2, 1, 0),
        new Vector3Int(-2, -1, 0), new Vector3Int(-1, -2, 0),
        new Vector3Int(1, -2, 0), new Vector3Int(2, -1, 0)
    };
    private static readonly Vector3Int[] Diagonals =
    {
        new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0)
    };
    private static readonly Vector3Int[] Cardinals =
        { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };

    private const int OrbitRadiusWeight = 20;
    private const int OrbitDirectionPenalty = 40;
    private const int BacktrackPenalty = 30;
    private MonsterMovement monster;
    private GridManager grid;
    private ProjectileManager projectiles;
    private BishopFireTrail fire;
    private Move player;
    private int knightInterval;
    private int bishopMoveDistance;
    private int bishopOrbitDistance;
    private int actionIndex;
    private Vector3Int previousBishopCell;
    private readonly Queue<Vector3Int> recentBishopCells = new Queue<Vector3Int>();
    private Vector3Int previousKnightCell;
    private Vector3Int lastRookStep;
    private bool showCross;
    private Vector3Int lastCrossCenter;

    public int PatternExecutions { get; private set; }
    public string StatusText => monster.MovementPattern == MonsterMovementPattern.Knight
        ? "N · L자 착지 후 상하좌우 공격"
        : monster.MovementPattern == MonsterMovementPattern.Bishop
            ? "B · 대각선 이동 / 불길 남김" : "R · 테두리 추적 이동 + 사격";

    public void Initialize(MonsterMovement owner, GridManager map, ProjectileManager manager,
        Move target, int knightTurns, int travelDistance, int orbitDistance, BishopFireTrail trail)
    {
        monster = owner;
        grid = map;
        projectiles = manager;
        player = target;
        fire = trail;
        knightInterval = Mathf.Max(1, knightTurns);
        bishopMoveDistance = Mathf.Max(1, travelDistance);
        bishopOrbitDistance = Mathf.Max(1, orbitDistance);
        previousBishopCell = monster.GridPosition;
        previousKnightCell = monster.GridPosition;
        recentBishopCells.Clear();
        lastRookStep = Vector3Int.zero;
    }

    public void TakeTurn(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        switch (monster.MovementPattern)
        {
            case MonsterMovementPattern.Knight: TakeKnightTurn(playerCell, blocked); break;
            case MonsterMovementPattern.Bishop: TakeBishopTurn(playerCell, blocked); break;
            case MonsterMovementPattern.Rook: TakeRookTurn(playerCell, blocked); break;
            default: monster.FinishTurn(); break;
        }
        actionIndex++;
    }

    private Vector3Int KnightLanding(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        Vector3Int destination = monster.GridPosition;
        int bestDistance = int.MaxValue;
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> firstSteps = new Dictionary<Vector3Int, Vector3Int>();
        firstSteps[destination] = destination;
        // L자 이동 그래프에서 '십자 공격 가능한 착지 칸'까지 최단 도약 수를 찾는다.
        // 직선 거리만 탐욕적으로 줄이다 두 칸을 왕복하는 상황을 방지한다.
        Vector3Int[] steps = (Vector3Int[])KnightSteps.Clone();
        Array.Sort(steps, (a, b) =>
        {
            int comparison = Distance(monster.GridPosition + a, playerCell)
                .CompareTo(Distance(monster.GridPosition + b, playerCell));
            if (comparison != 0) return comparison;
            comparison = (monster.GridPosition + a == previousKnightCell)
                .CompareTo(monster.GridPosition + b == previousKnightCell);
            if (comparison != 0) return comparison;
            // 기존 공격 가능 착지의 동률 순서를 유지한다.
            return Array.IndexOf(KnightSteps, a).CompareTo(Array.IndexOf(KnightSteps, b));
        });
        foreach (Vector3Int step in steps)
        {
            Vector3Int candidate = monster.GridPosition + step;
            if (candidate == playerCell || !grid.IsWalkableCell(candidate) || blocked(candidate)) continue;
            int distance = Distance(candidate, playerCell);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                destination = candidate;
            }
            if (distance == 1) return candidate;
            firstSteps[candidate] = candidate;
            queue.Enqueue(candidate);
        }
        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();
            foreach (Vector3Int step in steps)
            {
                Vector3Int next = cell + step;
                if (next == playerCell || !grid.IsWalkableCell(next)
                    || blocked(next) || firstSteps.ContainsKey(next)) continue;
                firstSteps[next] = firstSteps[cell];
                if (Distance(next, playerCell) == 1) return firstSteps[next];
                queue.Enqueue(next);
            }
        }
        // 공격 위치 자체가 봉쇄됐다면 가까워지는 도약만 허용해 무의미한 왕복을 막는다.
        return bestDistance < Distance(monster.GridPosition, playerCell)
            ? destination : monster.GridPosition;
    }

    private void TakeKnightTurn(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        showCross = false;
        if (actionIndex % knightInterval != 0)
        {
            monster.FinishTurn();
            return;
        }
        Vector3Int origin = monster.GridPosition;
        Vector3Int destination = KnightLanding(playerCell, blocked);
        // 착지 연출이 끝난 뒤 십자 4칸에만 한 번 피해를 준다. 중심/대각선에는 피해가 없다.
        if (monster.TryBeginMove(origin, destination, () =>
        {
            showCross = true;
            lastCrossCenter = destination;
            if (player.CurrentHealth > 0 && Distance(destination, player.GridPosition) == 1)
                player.TakeDamage(1);
        }))
        {
            previousKnightCell = origin;
            PatternExecutions++;
        }
        else monster.FinishTurn();
    }

    private List<Vector3Int> BishopPath(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        List<Vector3Int> bestPath = new List<Vector3Int>();
        Vector3Int origin = monster.GridPosition;
        Vector3Int relative = origin - playerCell;
        int bestScore = int.MaxValue;
        foreach (Vector3Int direction in Diagonals)
        {
            List<Vector3Int> path = new List<Vector3Int>();
            for (int step = 1; step <= bishopMoveDistance; step++)
            {
                Vector3Int cell = origin + direction * step;
                if (!grid.IsWalkableCell(cell) || cell == playerCell || blocked(cell)) break;
                path.Add(cell);
                int radius = Mathf.Max(Mathf.Abs(cell.x - playerCell.x), Mathf.Abs(cell.y - playerCell.y));
                int cross = relative.x * direction.y - relative.y * direction.x;
                // 목표 거리 근처에서 한 방향으로 돌도록 선호한다. 거리만 최소화하면 작은 왕복에 갇힌다.
                int score = Mathf.Abs(radius - bishopOrbitDistance) * OrbitRadiusWeight
                    + (cross < 0 ? 0 : OrbitDirectionPenalty)
                    + (cell == previousBishopCell ? BacktrackPenalty : 0)
                    + (recentBishopCells.Contains(cell) ? BacktrackPenalty : 0) - step;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestPath = new List<Vector3Int>(path);
                }
            }
        }
        return bestPath;
    }

    private void TakeBishopTurn(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        Vector3Int origin = monster.GridPosition;
        List<Vector3Int> path = BishopPath(playerCell, blocked);
        if (path.Count == 0 || !monster.TryBeginMove(origin, path[path.Count - 1]))
        {
            monster.FinishTurn();
            return;
        }
        if (monster.IsDead) return;
        previousBishopCell = origin;
        recentBishopCells.Enqueue(origin);
        if (recentBishopCells.Count > 4) recentBishopCells.Dequeue();
        fire.AddFire(origin);
        foreach (Vector3Int cell in path) fire.AddFire(cell);
        PatternExecutions++;
    }

    public bool IsRookBoundaryCell(Vector3Int cell)
    {
        BoundsInt b = grid.GroundTilemap.cellBounds;
        return grid.IsWalkableCell(cell) && (cell.x == b.xMin + 1 || cell.x == b.xMax - 2
            || cell.y == b.yMin + 1 || cell.y == b.yMax - 2);
    }

    private Vector3Int RookNextCell(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        Vector3Int origin = monster.GridPosition;
        Vector3Int result = origin;
        int bestDistance = Distance(origin, playerCell);
        int bestLength = 0;
        Queue<Vector3Int> queue = new Queue<Vector3Int>();
        Dictionary<Vector3Int, Vector3Int> firstSteps = new Dictionary<Vector3Int, Vector3Int> { [origin] = origin };
        Dictionary<Vector3Int, int> lengths = new Dictionary<Vector3Int, int> { [origin] = 0 };
        queue.Enqueue(origin);
        // 점유를 고려한 테두리 경로 중 플레이어에 가까운 위치까지의 첫 한 칸을 선택한다.
        while (queue.Count > 0)
        {
            Vector3Int cell = queue.Dequeue();
            foreach (Vector3Int direction in Cardinals)
            {
                Vector3Int next = cell + direction;
                if (!IsRookBoundaryCell(next) || next == playerCell || blocked(next) || firstSteps.ContainsKey(next)) continue;
                firstSteps[next] = cell == origin ? next : firstSteps[cell];
                lengths[next] = lengths[cell] + 1;
                queue.Enqueue(next);
                int distance = Distance(next, playerCell);
                int length = lengths[next];
                // 거리와 경로 길이를 별도로 비교해 맵 크기에 따라 우선순위가 뒤집히지 않게 한다.
                // 완전히 동률인 경우에만 이전 진행 방향을 유지한다.
                if (distance < bestDistance || (distance == bestDistance && length < bestLength)
                    || (distance == bestDistance && length == bestLength
                        && firstSteps[next] - origin == lastRookStep))
                {
                    bestDistance = distance;
                    bestLength = length;
                    result = firstSteps[next];
                }
            }
        }
        return result;
    }

    private Vector3Int RookShotDirection(Vector3Int cell, Vector3Int playerCell)
    {
        if (cell.x == playerCell.x) return playerCell.y > cell.y ? Vector3Int.up : Vector3Int.down;
        if (cell.y == playerCell.y) return playerCell.x > cell.x ? Vector3Int.right : Vector3Int.left;
        BoundsInt b = grid.GroundTilemap.cellBounds;
        if (cell.y == b.yMax - 2) return Vector3Int.down;
        if (cell.y == b.yMin + 1) return Vector3Int.up;
        return cell.x == b.xMin + 1 ? Vector3Int.right : Vector3Int.left;
    }

    private void TakeRookTurn(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        Vector3Int origin = monster.GridPosition;
        Vector3Int next = RookNextCell(playerCell, blocked);
        bool moving = monster.TryBeginMove(origin, next);
        if (monster.IsDead) return;
        if (moving) lastRookStep = next - origin;
        projectiles.SpawnEnemyProjectile(monster.GridPosition, RookShotDirection(monster.GridPosition, playerCell), player);
        PatternExecutions++;
        if (!moving) monster.FinishTurn();
    }

    public List<Vector3Int> GetThreatCells(Func<Vector3Int, bool> blocked)
    {
        List<Vector3Int> cells = new List<Vector3Int>();
        if (monster.MovementPattern == MonsterMovementPattern.Knight && actionIndex % knightInterval == 0)
        {
            Vector3Int landing = KnightLanding(player.GridPosition, blocked);
            if (landing != monster.GridPosition)
                foreach (Vector3Int direction in Cardinals)
                    if (grid.IsWalkableCell(landing + direction)) cells.Add(landing + direction);
        }
        else if (monster.MovementPattern == MonsterMovementPattern.Bishop)
            cells.AddRange(BishopPath(player.GridPosition, blocked));
        else if (monster.MovementPattern == MonsterMovementPattern.Rook)
        {
            Vector3Int next = RookNextCell(player.GridPosition, blocked);
            cells.Add(next);
            cells.Add(next + RookShotDirection(next, player.GridPosition));
        }
        return cells;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (!showCross || camera == null || fire == null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { font = fire.LabelFont, fontSize = 16, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.cyan;
        foreach (Vector3Int direction in Cardinals)
        {
            Vector3Int cell = lastCrossCenter + direction;
            if (!grid.IsWalkableCell(cell)) continue;
            Vector3 point = camera.WorldToScreenPoint(grid.GetCellCenterWorld(cell));
            GUI.Label(new Rect(point.x - 30, Screen.height - point.y - 24, 60, 24), "십자", style);
        }
    }

    private static int Distance(Vector3Int first, Vector3Int second) =>
        Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);
}
