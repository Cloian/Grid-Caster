using System.Collections.Generic;
using UnityEngine;

// 피해와 사망은 즉시 확정한다. 파편과 지연 반응은 판정을 가진 투사체가 아니다.
public sealed partial class CombatFeedback
{
    public const float FragmentArrivalSeconds = 0.08f;
    private const float CoreGatherSeconds = 0.025f;
    public static float DamageVisualDelay { get; set; }
    private static bool attackActive, powerAttack, counterOutcome, collisionOutcome;
    private static readonly HashSet<int> attackKills = new HashSet<int>();
    private static string lastOutcome = "";
    private static float outcomeRemaining, outcomeDelay;
    public static string CurrentOutcome => outcomeRemaining > 0f && outcomeDelay <= 0f ? lastOutcome : "";
    public static int ResidualFragmentCount => instance != null ? instance.fragments.Count : 0;
    private sealed class Fragment
    {
        public SpriteRenderer Head, Tail;
        public Vector3 Start, End, Bend;
        public float Elapsed;
    }
    private sealed class DelayedSound
    {
        public CombatSound Kind;
        public bool Lethal;
        public float Remaining;
    }
    private readonly List<Fragment> fragments = new List<Fragment>();
    private readonly List<DelayedSound> delayedSounds = new List<DelayedSound>();

    public static void BeginPlayerAttack(bool empowered)
    {
        ResetAttackOutcome();
        attackActive = true;
        powerAttack = empowered;
        GetOrCreate();
    }
    public static void NoteCounter() { if (attackActive) counterOutcome = true; }
    public static void NoteCollision() { if (attackActive) collisionOutcome = true; }
    public static void CompletePlayerAttack()
    {
        if (!attackActive) return;
        lastOutcome = attackKills.Count >= 2 ? $"{attackKills.Count}체 처치"
            : counterOutcome ? "반격 +1" : collisionOutcome ? "충돌" : "";
        outcomeDelay = ResidualFragmentCount > 0 ? FragmentArrivalSeconds : 0f;
        outcomeRemaining = string.IsNullOrEmpty(lastOutcome) ? 0f : 0.65f + outcomeDelay;
        attackActive = powerAttack = false;
    }
    public static void ResetAttackOutcome()
    {
        attackActive = powerAttack = counterOutcome = collisionOutcome = false;
        attackKills.Clear();
        DamageVisualDelay = outcomeRemaining = outcomeDelay = 0f;
        lastOutcome = "";
    }

    public static void PlayHitSound(CombatSound kind, bool lethal)
    {
        if (DamageVisualDelay > 0f)
        {
            GetOrCreate().delayedSounds.Add(new DelayedSound {
                Kind = kind, Lethal = lethal, Remaining = DamageVisualDelay });
            return;
        }
        GameAudio.PlayImpact(kind, lethal ? 0.84f : powerAttack ? 0.9f : 1f, important: lethal);
    }

    public static void StyleEmpoweredProjectile(SpriteRenderer projectile)
    {
        CombatFeedback feedback = GetOrCreate();
        feedback.EnsurePixelSprite();
        // 투사체 원래 색(흰·청록·파랑)을 유지하고 크기와 꼬리로 강화를 표시한다.
        projectile.color = Color.white;
        projectile.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        SpriteRenderer tail = feedback.CreatePixel("EmpoweredShortTail", projectile);
        tail.transform.SetParent(projectile.transform, false);
        tail.transform.localPosition = new Vector3(-3f / PixelsPerUnit, 0f, 0f);
        tail.transform.localScale = new Vector3(4f, 2f, 1f);
        tail.color = new Color(0.37f, 0.8f, 0.89f, 0.85f);   // #5FCDE4
    }

    public static void EmitResidualFragment(Vector3 center, MonsterMovement target)
    {
        SpriteRenderer source = target.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null) return;
        CombatFeedback feedback = GetOrCreate();
        feedback.EnsurePixelSprite();
        Vector3 endpoint = source.bounds.center;
        // 끝점은 적중 당시의 위치로 고정한다. 이후 적 이동을 추적하지 않는다.
        Vector3 offset = endpoint - center;
        float side = (target.GridPosition.x + target.GridPosition.y) % 2 == 0 ? 1f : -1f;
        Vector3 bend = new Vector3(-offset.y, offset.x, 0f).normalized * (3f / PixelsPerUnit * side);
        SpriteRenderer head = feedback.CreatePixel("ResidualMagicFragment", source);
        SpriteRenderer tail = feedback.CreatePixel("ResidualMagicTail", source);
        head.transform.position = tail.transform.position = PixelOffset(center);
        head.transform.localScale = new Vector3(3f, 2f, 1f);
        tail.transform.localScale = new Vector3(2f, 1f, 1f);
        head.color = new Color(0.37f, 0.8f, 0.89f);          // #5FCDE4
        tail.color = new Color(0.39f, 0.61f, 1f, 0.7f);      // #639BFF
        feedback.fragments.Add(new Fragment { Head = head, Tail = tail, Start = center, End = endpoint, Bend = bend });
    }

    private SpriteRenderer CreatePixel(string name, SpriteRenderer source)
    {
        GameObject visual = new GameObject(name);
        visual.layer = source.gameObject.layer;
        visual.transform.SetParent(transform, false);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = pixelSprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder + 3;
        return renderer;
    }

    private void UpdateFragments(float dt)
    {
        for (int i = fragments.Count - 1; i >= 0; i--)
        {
            Fragment fragment = fragments[i];
            fragment.Elapsed += dt;
            if (fragment.Elapsed >= FragmentArrivalSeconds)
            {
                HideAndDestroy(fragment.Head.gameObject);
                HideAndDestroy(fragment.Tail.gameObject);
                fragments.RemoveAt(i);
                continue;
            }
            float t = Mathf.Clamp01((fragment.Elapsed - CoreGatherSeconds)
                / (FragmentArrivalSeconds - CoreGatherSeconds));
            Vector3 pose = Vector3.Lerp(fragment.Start, fragment.End, t)
                + fragment.Bend * (4f * t * (1f - t));
            Vector3 tangent = fragment.End - fragment.Start + fragment.Bend * (4f - 8f * t);
            float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            fragment.Head.transform.SetPositionAndRotation(PixelOffset(pose), Quaternion.Euler(0f, 0f, angle));
            fragment.Tail.transform.SetPositionAndRotation(PixelOffset(pose - tangent.normalized * 2f / PixelsPerUnit),
                Quaternion.Euler(0f, 0f, angle));
            fragment.Tail.enabled = t > 0f;
        }
        // 동시에 도착하는 파편은 가장 큰 결과의 소리 하나만 짧게 낸다.
        DelayedSound arrival = null;
        for (int i = delayedSounds.Count - 1; i >= 0; i--)
        {
            DelayedSound sound = delayedSounds[i];
            sound.Remaining -= dt;
            if (sound.Remaining > 0f) continue;
            if (arrival == null || sound.Lethal) arrival = sound;
            delayedSounds.RemoveAt(i);
        }
        if (arrival != null) GameAudio.PlayImpact(arrival.Kind, arrival.Lethal ? 0.84f : 1.15f, 0.55f, arrival.Lethal);
    }

    private void ClearFragments()
    {
        foreach (Fragment fragment in fragments)
        {
            HideAndDestroy(fragment.Head.gameObject);
            HideAndDestroy(fragment.Tail.gameObject);
        }
        fragments.Clear();
        delayedSounds.Clear();
    }
}
