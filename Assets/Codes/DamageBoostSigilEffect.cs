using System.Collections;
using UnityEngine;

// 강화 폭발 연출: 직격 칸에 낙인 문양을 찍고, 현재 폭발 범위(8·12·16칸)의 바닥 칸마다 작은 문양을 찍는다.
// 판정과 무관한 순수 연출이다. 주변 칸은 잔여 마력 파편이 도착하는 시점에 맞춰 늦게 시작한다.
public sealed class DamageBoostSigilEffect : MonoBehaviour
{
    private const float FrameSeconds = 0.06f;
    private const int SortingOrder = -5;   // 바닥(-10) 위, 캐릭터·그림자 아래

    private static Sprite[] centerFrames;
    private static Sprite[] splashFrames;

    public static int ActiveCount { get; private set; }

    private SpriteRenderer sigil;
    private Sprite[] frames;
    private float delay;

    public static void Play(GridManager grid, Vector3Int center, int areaLevel)
    {
        if (grid == null || !LoadFrames()) return;
        Spawn(grid.GetCellCenterWorld(center), centerFrames, 0f);
        foreach (Vector3Int offset in DamageBoostArea.Offsets(areaLevel))
        {
            Vector3Int cell = center + offset;
            if (!grid.IsWalkableCell(cell)) continue;
            Spawn(grid.GetCellCenterWorld(cell), splashFrames, CombatFeedback.FragmentArrivalSeconds);
        }
    }

    private static bool LoadFrames()
    {
        if (centerFrames == null || centerFrames.Length == 0)
            centerFrames = Sorted(Resources.LoadAll<Sprite>("Effects/damage_boost_sigil_center"));
        if (splashFrames == null || splashFrames.Length == 0)
            splashFrames = Sorted(Resources.LoadAll<Sprite>("Effects/damage_boost_sigil_splash"));
        return centerFrames.Length > 0 && splashFrames.Length > 0;
    }

    private static Sprite[] Sorted(Sprite[] frames)
    {
        System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        return frames;
    }

    private static void Spawn(Vector3 position, Sprite[] frames, float delay)
    {
        GameObject root = new GameObject("DamageBoostSigil");
        root.transform.position = position;
        DamageBoostSigilEffect effect = root.AddComponent<DamageBoostSigilEffect>();
        effect.sigil = root.AddComponent<SpriteRenderer>();
        effect.sigil.sortingOrder = SortingOrder;
        effect.frames = frames;
        effect.delay = delay;
        ActiveCount++;
        effect.StartCoroutine(effect.Run());
    }

    private void OnDestroy() => ActiveCount--;

    // 일시정지(timeScale 0)에서는 파편과 함께 멈추도록 스케일 시간만 쓴다.
    private IEnumerator Run()
    {
        for (float waited = 0f; waited < delay; waited += Time.deltaTime)
            yield return null;
        foreach (Sprite frame in frames)
        {
            sigil.sprite = frame;
            for (float elapsed = 0f; elapsed < FrameSeconds; elapsed += Time.deltaTime)
                yield return null;
        }
        Destroy(gameObject);
    }
}
