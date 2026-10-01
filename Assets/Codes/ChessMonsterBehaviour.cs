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
    private const int RookFireDamage = 2;
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
    private bool rookCharged;
    private bool rookRecovering;
    private int rookTargetColumn;
    private float rookShotFlashUntil;
    private bool showCross;
    private Vector3Int lastCrossCenter;

    public int PatternExecutions { get; private set; }
    public bool IsRookCharged => monster != null
        && monster.MovementPattern == MonsterMovementPattern.Rook && rookCharged;
    public int RookTargetColumn => rookTargetColumn;
    public bool IsRookRecovering => monster != null
        && monster.MovementPattern == MonsterMovementPattern.Rook && rookRecovering;
    public bool WillKnightJumpNextTurn => monster != null
        && monster.MovementPattern == MonsterMovementPattern.Knight
        && actionIndex % knightInterval == 0;
    public string StatusText => monster.MovementPattern == MonsterMovementPattern.Knight
        ? $"N · {knightInterval}턴마다 도약 / 다음: {(WillKnightJumpNextTurn ? "도약" : "준비")}"
        : monster.MovementPattern == MonsterMovementPattern.Bishop
            ? "B · 대각선 이동 / 불길 남김"
            : rookCharged
                ? $"R · 성벽 쇠뇌 / {rookTargetColumn}열 발사 준비"
                : rookRecovering ? "R · 성벽 쇠뇌 / 한 턴 재정비 · 반격 기회"
                : "R · 성벽 쇠뇌 / 무작위 열 장전 대기";

    public void Initialize(MonsterMovement owner, GridManager map, ProjectileManager manager,
        Move target, int knightTurns, int travelDistance, int orbitDistance, BishopFireTrail trail)
    {
        monster = owner;
        grid = map;
        projectiles = manager;
        player = target;
        fire = trail;
        knightInterval = Mathf.Max(1, knightTurns);
        // 비숍은 짧게 왕복하지 않고 보드를 가로지르며 플레이어 근처를 압박한다.
        bishopMoveDistance = Mathf.Max(4, travelDistance);
        bishopOrbitDistance = 2;
        previousBishopCell = monster.GridPosition;
        previousKnightCell = monster.GridPosition;
        recentBishopCells.Clear();
        rookCharged = false;
        rookRecovering = false;
        rookTargetColumn = monster.GridPosition.x;
        rookShotFlashUntil = 0f;
    }

    public void TakeTurn(Vector3Int playerCell, Func<Vector3Int, bool> blocked)
    {
        switch (monster.MovementPattern)
        {
            case MonsterMovementPattern.Knight: TakeKnightTurn(playerCell, blocked); break;
            case MonsterMovementPattern.Bishop: TakeBishopTurn(playerCell, blocked); break;
            case MonsterMovementPattern.Rook: TakeRookTurn(blocked); break;
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
        AddBishopOrbitFire(playerCell, path[path.Count - 1]);
        PatternExecutions++;
    }

    private void AddBishopOrbitFire(Vector3Int playerCell, Vector3Int bishopCell)
    {
        int x = Math.Sign(bishopCell.x - playerCell.x);
        int y = Math.Sign(bishopCell.y - playerCell.y);
        if (x == 0) x = bishopCell.x <= playerCell.x ? -1 : 1;
        if (y == 0) y = bishopCell.y <= playerCell.y ? -1 : 1;
        Vector3Int cutoffCell = playerCell + new Vector3Int(x, y, 0);
        if (cutoffCell != playerCell && grid.IsWalkableCell(cutoffCell))
            fire.AddFire(cutoffCell);
    }

    public bool IsRookWallCell(Vector3Int cell)
    {
        return grid != null && grid.IsTopBoundaryWallCell(cell);
    }

    private Vector3Int RookAimCell(Func<Vector3Int, bool> blocked)
    {
        Vector3Int origin = monster.GridPosition;
        List<Vector3Int> candidates = new List<Vector3Int>();
        BoundsInt bounds = grid.GroundTilemap.cellBounds;
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            Vector3Int candidate = new Vector3Int(x, origin.y, 0);
            if (candidate == origin || !IsRookWallCell(candidate) || blocked(candidate)) continue;
            candidates.Add(candidate);
        }
        // 플레이어 좌표를 참조하지 않는다. 다른 룩의 현재/예약 열을 피해 새 사선을 고른다.
        return candidates.Count == 0 ? origin : candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void ChargeRook(Func<Vector3Int, bool> blocked)
    {
        Vector3Int origin = monster.GridPosition;
        Vector3Int aimCell = RookAimCell(blocked);
        rookTargetColumn = aimCell.x;
        rookCharged = true;

        // 성벽 레일 위에서 목표 열까지 이동하며 장전한다. 지상 점유에는 참여하지 않는다.
        if (aimCell != origin && monster.TryBeginMountedMove(origin, aimCell))
            return;

        monster.FinishTurn();
    }

    private void FireRook()
    {
        rookCharged = false;
        rookRecovering = true;
        rookShotFlashUntil = Time.unscaledTime + 0.16f;
        // 장전 때 고정한 세로 열을 즉시 관통 사격한다. 발사 순간 플레이어를 재조준하지 않는다.
        if (player.CurrentHealth > 0 && player.GridPosition.x == rookTargetColumn)
            player.TakeDamage(RookFireDamage);
        PatternExecutions++;
        monster.FinishTurn();
    }

    private void TakeRookTurn(Func<Vector3Int, bool> blocked)
    {
        if (rookCharged)
            FireRook();
        else if (rookRecovering)
        {
            // 발사 직후 한 턴 동안 자리를 지켜 같은 사선에서 무피해 반격할 틈을 준다.
            rookRecovering = false;
            monster.FinishTurn();
        }
        else
            ChargeRook(blocked);
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        bool showRookLane = monster != null
            && monster.MovementPattern == MonsterMovementPattern.Rook
            && (rookCharged || Time.unscaledTime < rookShotFlashUntil);
        if ((!showCross && !showRookLane) || camera == null || grid == null) return;
        Rect viewport = camera.pixelRect;
        GUI.BeginGroup(new Rect(viewport.xMin, Screen.height - viewport.yMax,
            viewport.width, viewport.height));
        if (showRookLane)
        {
            bool firing = !rookCharged;
            Color previous = GUI.color;
            GUI.color = firing
                ? new Color(1f, 0.8f, 0.25f, 0.78f)
                : new Color(1f, 0.12f, 0.12f, 0.28f);
            foreach (Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
            {
                if (cell.x != rookTargetColumn || !grid.IsWalkableCell(cell)) continue;
                Vector3 center = grid.GetCellCenterWorld(cell);
                Vector3 minimum = camera.WorldToScreenPoint(center + new Vector3(-0.46f, -0.46f));
                Vector3 maximum = camera.WorldToScreenPoint(center + new Vector3(0.46f, 0.46f));
                if (center.z < camera.transform.position.z) continue;
                Rect cellRect = new Rect(
                    minimum.x - viewport.xMin,
                    viewport.yMax - maximum.y,
                    maximum.x - minimum.x,
                    maximum.y - minimum.y);
                GUI.DrawTexture(cellRect, Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }

        if (showCross && fire != null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
                { font = fire.LabelFont, fontSize = 16, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.cyan;
            foreach (Vector3Int direction in Cardinals)
            {
                Vector3Int cell = lastCrossCenter + direction;
                if (!grid.IsWalkableCell(cell)) continue;
                Vector3 point = camera.WorldToScreenPoint(grid.GetCellCenterWorld(cell));
                if (point.z <= 0) continue;
                GUI.Label(new Rect(point.x - viewport.xMin - 30, viewport.yMax - point.y - 24, 60, 24), "십자", style);
            }
        }
        GUI.EndGroup();
    }

    private static int Distance(Vector3Int first, Vector3Int second) =>
        Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);
}
