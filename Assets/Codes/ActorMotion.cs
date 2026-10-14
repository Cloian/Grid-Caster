using System.Collections.Generic;
using UnityEngine;

// 판정과 무관한 캐릭터 모션 재생 도우미. 컨트롤러에 해당 파라미터가 없으면 아무것도 하지 않는다.
public static class ActorMotion
{
    public const string DeathState = "Death";
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int Hit = Animator.StringToHash("Hit");
    public static readonly int Die = Animator.StringToHash("Die");

    private static readonly Dictionary<RuntimeAnimatorController, HashSet<int>> parameterCache =
        new Dictionary<RuntimeAnimatorController, HashSet<int>>();

    public static void Trigger(Component actor, int trigger)
    {
        Animator animator = Find(actor, trigger);
        if (animator == null) return;
        if (trigger != Hit) animator.ResetTrigger(Hit);
        animator.SetTrigger(trigger);
    }

    public static void SetMoving(Component actor, bool value)
    {
        Animator animator = Find(actor, IsMoving);
        if (animator != null) animator.SetBool(IsMoving, value);
    }

    public static bool HasDeath(Component actor) => Find(actor, Die) != null;

    // 공격 모션이 실제로 내지르는 프레임까지의 시간. 판정(투사체·피해)은 이 시점에 맞춰 낸다.
    // 값 = lively 공격 클립의 앞 두 프레임 길이 합 (Art/Sources/Lively 원본 ms).
    public static float AttackReleaseSeconds(Component actor)
    {
        Animator animator = Find(actor, Attack);
        if (animator == null) return 0f;
        string controller = animator.runtimeAnimatorController.name;
        if (controller == "idle-run") return 0.37f;  // wizard_attack 150 + 220ms
        if (controller == "Bat") return 0.28f;       // bat_attack 130 + 150ms
        if (controller == "Snake") return 0.30f;     // snake_attack 140 + 160ms
        return 0f;
    }

    // 원본이 곧 사라지는 몬스터용: 같은 컨트롤러로 사망 모션만 재생할 Animator를 붙인다.
    public static float PlayDeathOn(Component source, GameObject visual)
    {
        Animator sourceAnimator = Find(source, Die);
        if (sourceAnimator == null) return 0f;
        Animator animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Play(DeathState, 0, 0f);
        return DeathSeconds(sourceAnimator.runtimeAnimatorController);
    }

    public static float DeathSeconds(RuntimeAnimatorController controller)
    {
        if (controller == null) return 0f;
        foreach (AnimationClip clip in controller.animationClips)
            if (clip != null && clip.name.EndsWith(DeathState, System.StringComparison.OrdinalIgnoreCase)) return clip.length;
        return 0f;
    }

    private static Animator Find(Component actor, int parameter)
    {
        if (actor == null || !actor.TryGetComponent(out Animator animator)) return null;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        if (controller == null || !animator.isActiveAndEnabled) return null;
        if (!parameterCache.TryGetValue(controller, out HashSet<int> parameters))
        {
            parameters = new HashSet<int>();
            foreach (AnimatorControllerParameter p in animator.parameters) parameters.Add(p.nameHash);
            parameterCache.Add(controller, parameters);
        }
        return parameters.Contains(parameter) ? animator : null;
    }
}
