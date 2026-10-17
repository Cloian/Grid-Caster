using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 나이트 착지 내려찍기 연출: 십자 4칸에 균열(바닥 층)과 먼지구름(캐릭터 위 층)을 띄우고 땅을 흔든다.
// 판정과 무관한 순수 연출이며, 균열은 잠시 남았다가 흐려진다.
public sealed class KnightQuakeEffect : MonoBehaviour
{
    private const float FrameSeconds = 0.06f;
    private const float CrackHoldSeconds = 0.5f;
    private const float FadeSeconds = 0.4f;
    // 처음 몇 프레임 동안 칸 전체를 1px씩 떨어 땅이 울리는 느낌을 준다(32 PPU 기준 1px).
    private const float ShakeSeconds = 0.18f;
    private const float PixelUnit = 1f / 32f;

    private static Sprite[] crackFrames;
    private static Sprite[] dustFrames;

    private SpriteRenderer crack;
    private SpriteRenderer dust;
    private Vector3 origin;

    public static void PlayCross(GridManager grid, Vector3Int center)
    {
        if (grid == null) return;
        if (!LoadFrames()) return;
        foreach (Vector3Int direction in new[] { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left })
        {
            Vector3Int cell = center + direction;
            if (!grid.IsWalkableCell(cell)) continue;
            Spawn(grid.GetCellCenterWorld(cell), direction);
        }
        CameraController.PlayImpactShake(Vector3.down);
    }

    // 폭탄병 폭발 등 임의 칸 묶음에 같은 균열·먼지 연출을 낸다.
    public static void PlayCells(GridManager grid, IEnumerable<Vector3Int> cells, bool shake)
    {
        if (grid == null || !LoadFrames()) return;
        foreach (Vector3Int cell in cells)
            if (grid.IsWalkableCell(cell)) Spawn(grid.GetCellCenterWorld(cell), Vector3Int.zero);
        if (shake) CameraController.PlayImpactShake(Vector3.down);
    }

    // 소환 해제 등: 균열 없이 먼지구름만 피어오른다.
    public static void PlayDust(GridManager grid, IEnumerable<Vector3Int> cells)
    {
        if (grid == null || !LoadFrames()) return;
        foreach (Vector3Int cell in cells)
        {
            KnightQuakeEffect effect = Spawn(grid.GetCellCenterWorld(cell), Vector3Int.zero);
            effect.crack.enabled = false;
        }
    }

    private static bool LoadFrames()
    {
        if (crackFrames == null || crackFrames.Length == 0)
            crackFrames = Sorted(Resources.LoadAll<Sprite>("Effects/knight_quake_crack"));
        if (dustFrames == null || dustFrames.Length == 0)
            dustFrames = Sorted(Resources.LoadAll<Sprite>("Effects/knight_quake_dust"));
        return crackFrames.Length > 0 && dustFrames.Length > 0;
    }

    private static Sprite[] Sorted(Sprite[] frames)
    {
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        return frames;
    }

    private static KnightQuakeEffect Spawn(Vector3 position, Vector3Int direction)
    {
        GameObject root = new GameObject("KnightQuake");
        root.transform.position = position;
        KnightQuakeEffect effect = root.AddComponent<KnightQuakeEffect>();
        effect.origin = position;
        effect.crack = AddLayer(root, "Crack", -5);   // 바닥(-10) 위, 캐릭터·그림자 아래
        effect.dust = AddLayer(root, "Dust", 12);     // 캐릭터(10) 위
        // 칸마다 균열이 같은 모양이면 찍어 낸 티가 나므로 방향에 따라 뒤집는다.
        bool flipX = direction.x < 0 || direction.y < 0;
        effect.crack.flipX = flipX;
        effect.dust.flipX = flipX;
        effect.crack.flipY = direction.y < 0;
        effect.StartCoroutine(effect.Play());
        return effect;
    }

    private static SpriteRenderer AddLayer(GameObject root, string name, int order)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = order;
        return renderer;
    }

    private IEnumerator Play()
    {
        int frames = Mathf.Max(crackFrames.Length, dustFrames.Length);
        float elapsed = 0f;
        for (int i = 0; i < frames; i++)
        {
            crack.sprite = crackFrames[Mathf.Min(i, crackFrames.Length - 1)];
            dust.sprite = i < dustFrames.Length ? dustFrames[i] : null;
            float frameEnd = elapsed + FrameSeconds;
            while (elapsed < frameEnd)
            {
                elapsed += Time.deltaTime;
                Shake(elapsed);
                yield return null;
            }
        }
        dust.sprite = null;
        transform.position = origin;

        yield return new WaitForSeconds(CrackHoldSeconds);
        for (float t = 0f; t < FadeSeconds; t += Time.deltaTime)
        {
            crack.color = new Color(1f, 1f, 1f, 1f - t / FadeSeconds);
            yield return null;
        }
        Destroy(gameObject);
    }

    private void Shake(float elapsed)
    {
        if (elapsed >= ShakeSeconds)
        {
            transform.position = origin;
            return;
        }
        // 정수 픽셀만 오가며 좌우·위아래로 번갈아 떤다.
        int step = Mathf.FloorToInt(elapsed / 0.03f);
        Vector3 offset = (step % 4) switch
        {
            0 => new Vector3(PixelUnit, 0f),
            1 => new Vector3(0f, -PixelUnit),
            2 => new Vector3(-PixelUnit, 0f),
            _ => new Vector3(0f, PixelUnit)
        };
        transform.position = origin + offset;
    }
}
