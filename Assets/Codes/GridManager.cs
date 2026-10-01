using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class GridManager : MonoBehaviour
{
    private static readonly Color DarkFloorColor = new Color(0.8f, 0.85f, 0.9f);
    [Header("맵 설정")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField, Min(1)] private int boundaryThickness = 1;

    public Tilemap GroundTilemap => groundTilemap;
    public bool IsReady => groundTilemap != null;

    private void Start()
    {
        // Tile 에셋의 색/잠금이 씬 로드 중 복구되므로 첫 렌더 전에 다시 적용한다.
        if (groundTilemap != null) ApplyCheckerboard();
    }

    public void Initialize(Tilemap targetTilemap, int outerBoundaryThickness)
    {
        groundTilemap = targetTilemap;
        boundaryThickness = Mathf.Max(1, outerBoundaryThickness);

        if (groundTilemap != null)
        {
            // 삭제된 타일 때문에 남은 빈 Bounds가 벽 안쪽 계산에 포함되지 않게 한다.
            groundTilemap.CompressBounds();
            ApplyCheckerboard();
        }
    }

    private void ApplyCheckerboard()
    {
        // 셀별 색을 표시하도록 렌더링하며 원본 에셋/외벽/셀 Transform은 보존한다.
        TilemapRenderer renderer = groundTilemap.GetComponent<TilemapRenderer>();
        if (renderer != null) renderer.mode = TilemapRenderer.Mode.Individual;
        foreach (Vector3Int cell in groundTilemap.cellBounds.allPositionsWithin)
        {
            if (!IsWalkableCell(cell)) continue;
            groundTilemap.RemoveTileFlags(cell, TileFlags.LockColor);
            groundTilemap.SetColor(cell, (cell.x + cell.y) % 2 == 0 ? Color.white : DarkFloorColor);
        }
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        return groundTilemap.WorldToCell(worldPosition);
    }

    public Vector3 GetCellCenterWorld(Vector3Int cellPosition)
    {
        return groundTilemap.GetCellCenterWorld(cellPosition);
    }

    public bool IsWalkableCell(Vector3Int cellPosition)
    {
        if (groundTilemap == null || !groundTilemap.HasTile(cellPosition))
            return false;

        BoundsInt bounds = groundTilemap.cellBounds;

        // Ground의 가장 바깥쪽 한 줄은 시각 에셋과 관계없이 벽으로 취급한다.
        return cellPosition.x >= bounds.xMin + boundaryThickness
            && cellPosition.x < bounds.xMax - boundaryThickness
            && cellPosition.y >= bounds.yMin + boundaryThickness
            && cellPosition.y < bounds.yMax - boundaryThickness;
    }

    public bool IsInsideMapCell(Vector3Int cellPosition)
    {
        return groundTilemap != null && groundTilemap.HasTile(cellPosition);
    }

    public bool IsTopBoundaryWallCell(Vector3Int cellPosition)
    {
        if (groundTilemap == null || !groundTilemap.HasTile(cellPosition))
            return false;

        BoundsInt bounds = groundTilemap.cellBounds;
        int innerWallY = bounds.yMax - boundaryThickness;
        return cellPosition.y == innerWallY
            && cellPosition.x >= bounds.xMin + boundaryThickness
            && cellPosition.x < bounds.xMax - boundaryThickness;
    }

    private void OnValidate()
    {
        boundaryThickness = Mathf.Max(1, boundaryThickness);
    }
}
