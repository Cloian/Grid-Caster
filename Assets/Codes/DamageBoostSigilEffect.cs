using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 강화 폭발 연출: 직격 칸에 낙인 문양을 찍고, 현재 폭발 범위(8·12·16칸)의 바닥 칸마다 작은 문양을 찍는다.
// 판정과 무관한 순수 연출이다. 주변 칸은 잔여 마력 파편이 도착하는 시점에 맞춰 늦게 시작한다.
public sealed class DamageBoostSigilEffect : MonoBehaviour
{
    private const float FrameSeconds = 0.06f;
    // 캐릭터(10) 위에 찍어 직격 칸 문양이 맞은 적에 가려지지 않게 한다.
    // 파편(13)·투사체(15)·적 상태 HUD(30)보다는 아래라 체력 칸과 피해 숫자를 가리지 않는다.
    private const int SortingOrder = 12;

    private static Sprite[] centerFrames;
    private static Sprite[] splashFrames;

    private static readonly HashSet<DamageBoostSigilEffect> active = new HashSet<DamageBoostSigilEffect>();
    public static int ActiveCount => active.Count;

    public static void ClearAll()
    {
        // 일시정지 중에도 즉시 숨기고, 지연 재생 코루틴까지 취소한다.
        foreach (DamageBoostSigilEffect effect in new List<DamageBoostSigilEffect>(active))
        {
            if (effect == null) continue;
            effect.gameObject.SetActive(false);
            Destroy(effect.gameObject);
        }
        active.Clear();
    }

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
        active.Add(effect);
        effect.StartCoroutine(effect.Run());
    }

    private void OnDisable() => active.Remove(this);
    private void OnDestroy() => active.Remove(this);

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
