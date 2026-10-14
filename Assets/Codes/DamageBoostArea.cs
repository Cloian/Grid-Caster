using System.Collections.Generic;
using UnityEngine;

// 피해 판정과 조준이 같은 정수 타일 모양을 사용한다. 중심은 직격만 받는다.
public static class DamageBoostArea
{
    private static readonly IReadOnlyList<Vector3Int>[] shapes = BuildShapes();
    public static IReadOnlyList<Vector3Int> Offsets(int level) => shapes[Mathf.Clamp(level, 0, 2)];
    public static bool Contains(Vector3Int offset, int level)
    {
        if (offset.z != 0) return false;
        int x = Mathf.Abs(offset.x), y = Mathf.Abs(offset.y);
        int radius = Mathf.Max(x, y);
        if (radius == 1) return true;
        if (radius != 2 || level < 1) return false;
        return x == 0 || y == 0 || (level >= 2 && x == 2 && y == 2);
    }
    private static IReadOnlyList<Vector3Int>[] BuildShapes()
    {
        var inner = new List<Vector3Int>();
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                if (x != 0 || y != 0) inner.Add(new Vector3Int(x, y, 0));
        var cross = new List<Vector3Int>(inner) { new Vector3Int(0,2,0), new Vector3Int(2,0,0),
            new Vector3Int(0,-2,0), new Vector3Int(-2,0,0) };
        var full = new List<Vector3Int>(cross) { new Vector3Int(2,2,0), new Vector3Int(2,-2,0),
            new Vector3Int(-2,2,0), new Vector3Int(-2,-2,0) };
        return new IReadOnlyList<Vector3Int>[] { inner.AsReadOnly(), cross.AsReadOnly(), full.AsReadOnly() };
    }
}
