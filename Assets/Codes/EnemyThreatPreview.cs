using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// 미래 AI를 실행하지 않고 공개 이동 규칙의 가능한 범위만 읽는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Move))]
public sealed class EnemyThreatPreview : MonoBehaviour
{
    private static readonly Vector3Int[] KnightSteps =
    {
        new Vector3Int(2, 1, 0), new Vector3Int(1, 2, 0),
        new Vector3Int(-1, 2, 0), new Vector3Int(-2, 1, 0),
        new Vector3Int(-2, -1, 0), new Vector3Int(-1, -2, 0),
        new Vector3Int(1, -2, 0), new Vector3Int(2, -1, 0)
    };
    private static readonly Vector3Int[] Cardinals =
        { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };
    private static readonly Vector3Int[] Diagonals =
    {
        new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0)
    };
    private readonly HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();
    private Move player;
    private GridManager grid;
    private MonsterSpawner spawner;
    private Camera worldCamera;
    private Texture2D texture;
    private Sprite sprite;
    private Vector3Int? mobileInspectionCell;
    public string Description { get; private set; } = string.Empty;
    public int VisibleCellCount { get; private set; }

    public void ClearMobileInspection() { mobileInspectionCell = null; }

    public void InspectMobilePoint(Vector2 point)
    {
        mobileInspectionCell = null;
        if (grid == null || !grid.IsReady || worldCamera == null || !worldCamera.pixelRect.Contains(point)) return;
        Ray ray = worldCamera.ScreenPointToRay(point);
        Plane plane = new Plane(grid.GroundTilemap.transform.forward, grid.GetCellCenterWorld(player.GridPosition));
        if (plane.Raycast(ray, out float distance)) mobileInspectionCell = grid.WorldToCell(ray.GetPoint(distance));
    }

    public static string CollectCells(MonsterMovement monster, GridManager map,
        HashSet<Vector3Int> output)
    {
        output.Clear();
        if (monster == null || monster.IsDead || map == null || !map.IsReady) return string.Empty;
        ChessMonsterBehaviour pattern = monster.GetComponent<ChessMonsterBehaviour>();
        if (pattern == null) return string.Empty;
        if (monster.IsActionBlocked) return "봉쇄됨: 다음 적 턴 행동 없음 · 다음 턴 이후에는 다시 위험";
        Vector3Int origin = monster.GridPosition;
        if (monster.MovementPattern == MonsterMovementPattern.Rook)
        {
            if (!pattern.IsRookCharged) return "룩: 무작위 열 장전 → 발사 → 재정비";
            foreach (Vector3Int cell in map.GroundTilemap.cellBounds.allPositionsWithin)
                if (cell.x == pattern.RookTargetColumn && map.IsWalkableCell(cell)) output.Add(cell);
            return "빨강: 고정 사선 · 룩이 살아 있고 봉쇄되지 않으면 다음 적 턴 발사";
        }
        if (monster.MovementPattern == MonsterMovementPattern.Knight)
        {
            string counter = pattern.IsKnightCounterVulnerable
                ? "취약: 이번 행동 8방향 인접 첫 직격 +1 · 다른 행동 시 종료\n" : string.Empty;
            if (!pattern.WillKnightJumpNextTurn) return counter + "나이트: 다음 턴 준비 · 도약 공격 없음";
            // 현재 적 점유는 죽거나 비워질 수 있으므로 여기서 확정 장애물로 취급하지 않는다.
            foreach (Vector3Int step in KnightSteps)
            {
                Vector3Int landing = origin + step;
                if (!map.IsWalkableCell(landing)) continue;
                foreach (Vector3Int direction in Cardinals)
                    if (map.IsWalkableCell(landing + direction)) output.Add(landing + direction);
            }
            return counter + "주황: 착지 십자 후보 · 한 곳만 공격 · 행동 후 점유로 결정";
        }
        if (monster.MovementPattern == MonsterMovementPattern.Bishop)
        {
            if (map.IsWalkableCell(origin)) output.Add(origin);
            foreach (Vector3Int direction in Diagonals)
                for (int distance = 1; distance <= pattern.BishopMoveDistance; distance++)
                {
                    Vector3Int cell = origin + direction * distance;
                    if (!map.IsWalkableCell(cell)) break;
                    output.Add(cell);
                }
            return "주황: 출발점과 대각선 불길 경로 후보(한 경로만 선택) · 생성 즉시 피해 없음 · 진입 시 피해";
        }
        return string.Empty;
    }

    private void LateUpdate()
    {
        if (player == null) player = GetComponent<Move>();
        if (spawner == null) spawner = FindAnyObjectByType<MonsterSpawner>();
        if (grid == null && spawner != null) grid = spawner.GetComponent<GridManager>();
        if (worldCamera == null) worldCamera = Camera.main;
        Description = string.Empty;
        VisibleCellCount = 0;
        if (MobilePlaytestController.IsActive)
        {
            if (player.CanAct && mobileInspectionCell.HasValue && spawner != null
                && grid != null && grid.IsReady
                && spawner.TryGetMonsterAtCell(mobileInspectionCell.Value, out MonsterMovement inspected))
            {
                Description = CollectCells(inspected, grid, cells);
                ShowCells(inspected.MovementPattern == MonsterMovementPattern.Rook);
            }
        }
        else if (player.CanAct && grid != null && grid.IsReady && worldCamera != null
            && Mouse.current != null && (EventSystem.current == null
                || !EventSystem.current.IsPointerOverGameObject()))
        {
            Vector2 pointer = Mouse.current.position.ReadValue();
            Ray ray = worldCamera.ScreenPointToRay(pointer);
            Plane plane = new Plane(grid.GroundTilemap.transform.forward,
                grid.GetCellCenterWorld(player.GridPosition));
            if (worldCamera.pixelRect.Contains(pointer) && plane.Raycast(ray, out float distance)
                && spawner.TryGetMonsterAtCell(grid.WorldToCell(ray.GetPoint(distance)), out MonsterMovement target))
            {
                Description = CollectCells(target, grid, cells);
                ShowCells(target.MovementPattern == MonsterMovementPattern.Rook);
            }
        }
        for (int i = VisibleCellCount; i < markers.Count; i++) markers[i].enabled = false;
    }

    private void ShowCells(bool fixedLane)
    {
        if (sprite == null)
        {
            texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                    if ((x == 1 || x == 30 || y == 1 || y == 30)
                        && x >= 1 && x <= 30 && y >= 1 && y <= 30)
                        pixels[y * 32 + x] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
        }
        foreach (Vector3Int cell in cells)
        {
            if (VisibleCellCount == markers.Count)
            {
                GameObject root = new GameObject("ConditionalThreatBorder");
                root.transform.SetParent(transform, false);
                SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 19;
                markers.Add(renderer);
            }
            SpriteRenderer marker = markers[VisibleCellCount++];
            marker.transform.position = grid.GetCellCenterWorld(cell);
            marker.enabled = true;
            marker.color = fixedLane ? new Color32(255, 75, 85, 255) : new Color32(255, 164, 60, 235);
        }
    }

    private void OnDisable()
    {
        Description = string.Empty;
        VisibleCellCount = 0;
        foreach (SpriteRenderer marker in markers) if (marker != null) marker.enabled = false;
    }

    private void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}
