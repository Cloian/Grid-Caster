using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
public sealed class GridManager : MonoBehaviour
{
    [Header("맵 설정")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField, Min(1)] private int boundaryThickness = 1;

    public Tilemap GroundTilemap => groundTilemap;
    public bool IsReady => groundTilemap != null;

    public void Initialize(Tilemap targetTilemap, int outerBoundaryThickness)
    {
        groundTilemap = targetTilemap;
        boundaryThickness = Mathf.Max(1, outerBoundaryThickness);

        if (groundTilemap != null)
        {
            // 삭제된 타일 때문에 남은 빈 Bounds가 벽 안쪽 계산에 포함되지 않게 한다.
            groundTilemap.CompressBounds();
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

    private void OnValidate()
    {
        boundaryThickness = Mathf.Max(1, boundaryThickness);
    }
}
