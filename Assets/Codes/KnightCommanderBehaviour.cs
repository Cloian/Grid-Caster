using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 10웨이브 중간보스 · 기사단장(3×3 기마). 규칙은 플레이어 행동이 끝날 때마다 Tick 한 번 진행한다.
// 설계: docs/design/knight-commander-patterns-2026-10-18.md
// - 기병 돌진: 가로·세로 한 방향, 몸 폭(3칸) 경로를 두 행동 전에 공개 → 돌진(줄 위의 플레이어 앞에서 멈추며 피해) → 두 행동 반격 기회
// - 마상 휩쓸기: 바라보는 쪽 앞 4줄 반원(몸 가운데에서 반지름 4.2칸)을 한 행동 전에 공개 → 베기(맞은 자리에 따라 정면·사선·옆으로 2칸 밀려남) → 한 행동 반격 기회
// - 나이트 소환: 생길 칸을 먼저 공개하고 다음 행동에 체력 2 나이트 최대 3마리(생존 상한 3). 새 나이트는 그 행동에 움직이지 않는다.
// 좌표는 13×13 판의 지역 좌표에 판 기준점(QueenBossBehaviour.MapOrigin)을 더한다. 논리 좌표는 몸의 중심 칸이다.
public sealed class KnightCommanderBehaviour : MonoBehaviour, IBossEncounter
{
    public enum Pattern { Charge, Sweep, Summon }
    public const int BodyRadius = 1, BodySize = BodyRadius * 2 + 1;
    public static readonly Vector3Int HomeAnchor = new Vector3Int(7, 11, 0);   // 위쪽 가운데 3×3(지역 6~8, 10~12)
    public const int ChargeDamage = 3, SweepDamage = 2, MaxSummoned = 3, SummonHealth = 2;
    public const float SweepRadius = 4.2f;
    public const int SweepKnockback = 2;
    private static readonly Pattern[] Cycle = { Pattern.Charge, Pattern.Sweep, Pattern.Summon };
    private static readonly Vector3Int[] Orthogonal = { Vector3Int.up, Vector3Int.right, Vector3Int.down, Vector3Int.left };

    public event Action Changed;
    public MonsterMovement Commander { get; private set; }
    public MonsterMovement Body => Commander;
    public Move Player { get; private set; }
    public int Turn { get; private set; }
    public Pattern CurrentPattern { get; private set; }
    public int PatternStep { get; private set; }
    public bool IsFinished { get; private set; }
    public bool Won { get; private set; }
    public int MaximumHealth { get; private set; }
    public string LastOutcome { get; private set; } = "예고 확인";
    public Vector3Int Origin { get; private set; }
    public Vector3Int Anchor => Commander != null ? Commander.GridPosition : HomeAnchor + Origin;
    public HashSet<Vector3Int> Danger { get; } = new HashSet<Vector3Int>();
    public IReadOnlyList<Vector3Int> SummonCells => summonCells;
    public IReadOnlyList<MonsterMovement> Summoned => summoned;
    public bool FacingLeft { get; private set; }

    // 패턴별 발동 차례와 반격 기회 길이(행동 수)
    private int StrikeStep => CurrentPattern == Pattern.Charge ? 1 : 0;
    private int RecoveryActions => CurrentPattern == Pattern.Charge ? 2 : CurrentPattern == Pattern.Sweep ? 1 : 0;
    public bool IsRecovery => PatternStep > StrikeStep;
    public string PatternName => CurrentPattern switch
    {
        Pattern.Charge => "기병 돌진", Pattern.Sweep => "마상 휩쓸기", _ => "나이트 소환"
    };
    public string TimingText => IsFinished ? (Won ? "기사단장 처치" : "플레이어 사망")
        : IsRecovery ? $"반격 기회 · {StrikeStep + RecoveryActions + 1 - PatternStep}행동"
        : $"발동까지 {StrikeStep + 1 - PatternStep}행동";

    private readonly List<MonsterMovement> summoned = new List<MonsterMovement>();
    private readonly List<Vector3Int> summonCells = new List<Vector3Int>();
    private readonly List<GameObject> markers = new List<GameObject>();
    private MonsterSpawner spawner;
    private GridManager grid;
    private System.Random random = new System.Random(0);
    private Vector3Int chargeDirection, sweepDirection;
    private int cycleIndex, serial;
    private static Sprite pixel;
    private GUIStyle style;

    public IEnumerable<Vector3Int> BodyCells => CellsAt(Anchor);

    private static IEnumerable<Vector3Int> CellsAt(Vector3Int center)
    {
        for (int y = -BodyRadius; y <= BodyRadius; y++)
            for (int x = -BodyRadius; x <= BodyRadius; x++)
                yield return center + new Vector3Int(x, y, 0);
    }

    public bool ContainsBodyCell(Vector3Int cell) => Commander != null && !Commander.IsDead
        && Mathf.Abs(cell.x - Anchor.x) <= BodyRadius && Mathf.Abs(cell.y - Anchor.y) <= BodyRadius;

    public void Initialize(MonsterSpawner spawner, GridManager grid, Move player, Sprite sprite,
        RuntimeAnimatorController motion, int health, int seed)
    {
        this.spawner = spawner; this.grid = grid; Player = player; MaximumHealth = health;
        random = new System.Random(seed);
        Turn = cycleIndex = PatternStep = 0; IsFinished = Won = false;
        summoned.Clear(); Danger.Clear(); summonCells.Clear();
        Origin = QueenBossBehaviour.MapOrigin(grid);
        Commander = spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour, HomeAnchor + Origin, sprite,
            new Color32(200, 170, 90, 255), health, 900, 2, 4, 2, motion);
        Commander.name = "Knight Commander · 3×3";
        // 128px 시트는 32 PPU 원래 크기(3×3 안에 맞춤). 그림이 없으면 3×3 크기 단색으로.
        Commander.transform.localScale = motion != null ? Vector3.one : new Vector3(BodySize, BodySize, 1f);
        MonsterStatusHud.IntentOverride = IntentOf;
        PrepareNext();
    }

    // ---------------- 패턴 준비 ----------------
    private void PrepareNext()
    {
        Danger.Clear(); summonCells.Clear(); PatternStep = 0;
        summoned.RemoveAll(m => m == null || m.IsDead);
        CurrentPattern = Cycle[cycleIndex++ % Cycle.Length];
        if (CurrentPattern == Pattern.Summon && !ChooseSummonCells())
            CurrentPattern = Cycle[cycleIndex++ % Cycle.Length];     // 소환할 수 없으면 다음(돌진)
        if (CurrentPattern == Pattern.Charge) BuildCharge();
        else if (CurrentPattern == Pattern.Sweep) BuildSweep();
        Face(Player.GridPosition.x < Anchor.x);
        PlayPose(CurrentPattern switch { Pattern.Charge => "Charge_Windup", Pattern.Sweep => "Sweep_Windup", _ => "Summon_Windup" });
        LastOutcome = "새 예고 · " + PatternName;
        Redraw();
        Changed?.Invoke();
    }

    private bool FootprintFree(Vector3Int anchor, bool playerBlocks, bool traverseSummoned = false)
    {
        foreach (Vector3Int cell in CellsAt(anchor))
        {
            if (!grid.IsWalkableCell(cell)) return false;
            if (playerBlocks && cell == Player.GridPosition) return false;
            if (spawner.TryGetMonsterAtCell(cell, out MonsterMovement other) && other != Commander
                && !(traverseSummoned && summoned.Contains(other))) return false;
        }
        return true;
    }

    private bool FootprintHasPlayer(Vector3Int anchor)
    {
        Vector3Int p = Player.GridPosition;
        return Mathf.Abs(p.x - anchor.x) <= BodyRadius && Mathf.Abs(p.y - anchor.y) <= BodyRadius;
    }

    private List<Vector3Int> ChargePath(Vector3Int direction)
    {
        var path = new List<Vector3Int>();
        for (Vector3Int a = Anchor + direction; FootprintFree(a, false, true); a += direction) path.Add(a);
        return path;
    }

    private void BuildCharge()
    {
        Vector3Int best = Vector3Int.down; double bestScore = double.MinValue;
        foreach (Vector3Int direction in Orthogonal)
        {
            List<Vector3Int> path = ChargePath(direction);
            if (path.Count == 0) continue;
            bool hits = path.Any(FootprintHasPlayer);
            double closest = path.Min(a => Mathf.Max(Mathf.Abs(a.x - Player.GridPosition.x), Mathf.Abs(a.y - Player.GridPosition.y)));
            double score = (hits ? 100 : 0) - closest + path.Count * 0.1 + random.NextDouble() * 0.3;
            if (score > bestScore) { bestScore = score; best = direction; }
        }
        chargeDirection = best;
        foreach (Vector3Int a in ChargePath(best))
            foreach (Vector3Int cell in CellsAt(a))
            {
                if (!ContainsBodyCell(cell)) Danger.Add(cell);
            }
        if (best.x != 0) Face(best.x < 0);
    }

    // 정면 반원: 플레이어가 있는 쪽(가로·세로 중 차이가 큰 축)을 바라본다.
    private void BuildSweep()
    {
        Vector3 center = new Vector3(Anchor.x, Anchor.y);
        Vector3 d = new Vector3(Player.GridPosition.x - center.x, Player.GridPosition.y - center.y);
        sweepDirection = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x < 0 ? Vector3Int.left : Vector3Int.right)
            : (d.y < 0 ? Vector3Int.down : Vector3Int.up);
        foreach (Vector3Int cell in SweepCells(sweepDirection)) if (grid.IsWalkableCell(cell)) Danger.Add(cell);
        if (sweepDirection.x != 0) Face(sweepDirection.x < 0);
    }

    // 몸 가운데를 축으로 한쪽 옆에서 정면을 지나 반대쪽 옆까지 휘두른다.
    // 몸보다 앞쪽 4줄에서 가운데로부터 반지름 안의 칸(몸 제외). 몸 옆 줄과 뒤쪽은 안전하다.
    // 줄별 칸 수: 앞 1줄 6 · 2줄 7 · 3줄 5 · 4줄 3 = 21칸
    private IEnumerable<Vector3Int> SweepCells(Vector3Int direction)
    {
        Vector3Int side = new Vector3Int(direction.y, direction.x, 0);
        int reach = Mathf.FloorToInt(SweepRadius);
        for (int along = 1; along <= reach; along++)
            for (int lateral = -reach; lateral <= reach; lateral++)
            {
                if (along * along + lateral * lateral > SweepRadius * SweepRadius) continue;
                Vector3Int cell = Anchor + direction * along + side * lateral;
                if (!ContainsBodyCell(cell)) yield return cell;
            }
    }

    // 소환 칸: 빈 바닥, 몸과 플레이어 둘레 1칸 밖, 플레이어와 2~4칸 거리, 기사단장 가까운 쪽 우선.
    private bool ChooseSummonCells()
    {
        summonCells.Clear();
        summoned.RemoveAll(m => m == null || m.IsDead);
        int count = MaxSummoned - summoned.Count;
        if (count <= 0) return false;
        Vector3Int p = Player.GridPosition;
        var options = new List<(Vector3Int cell, double score)>();
        for (int y = 1; y <= 13; y++)
            for (int x = 1; x <= 13; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0) + Origin;
                if (!grid.IsWalkableCell(cell) || spawner.IsOccupied(cell) || ContainsBodyCell(cell)) continue;
                int toPlayer = Mathf.Max(Mathf.Abs(cell.x - p.x), Mathf.Abs(cell.y - p.y));
                if (toPlayer <= 1) continue;
                int toBoss = Mathf.Max(Mathf.Abs(cell.x - Anchor.x), Mathf.Abs(cell.y - Anchor.y));
                options.Add((cell, -Mathf.Abs(toPlayer - 3) * 2 - toBoss * 0.3 + random.NextDouble()));
            }
        summonCells.AddRange(options.OrderByDescending(o => o.score).Take(count).Select(o => o.cell));
        return summonCells.Count > 0;
    }

    // ---------------- 진행 ----------------
    public void Tick()
    {
        if (IsFinished) return;
        Turn++;
        if (Commander == null || Commander.IsDead) { Finish(true); return; }
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        RestoreHeldPose();
        summoned.RemoveAll(m => m == null || m.IsDead);
        // 소환한 나이트는 기존 규칙으로 행동(이번 행동에 막 생긴 나이트는 다음 행동부터)
        spawner.RunBossAllyTurn(new List<MonsterMovement>(summoned));
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        if (Commander.ConsumeBlockedTurn()) { LastOutcome = "기사단장 봉쇄 · 발동 지연"; Changed?.Invoke(); return; }

        LastOutcome = IsRecovery ? "반격 기회" : "준비";
        if (PatternStep == StrikeStep)
        {
            if (CurrentPattern == Pattern.Charge) ExecuteCharge();
            else if (CurrentPattern == Pattern.Sweep) ExecuteSweep();
            else ExecuteSummon();
            Danger.Clear(); summonCells.Clear();
        }
        PatternStep++;
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        if (Commander == null || Commander.IsDead) { Finish(true); return; }
        if (PatternStep > StrikeStep + RecoveryActions) PrepareNext();
        else { Redraw(); Changed?.Invoke(); }
    }

    private void ExecuteCharge()
    {
        PlayPose("Charge_Release");
        Vector3Int from = Anchor, stop = from;
        bool hit = false;
        for (Vector3Int a = from + chargeDirection; FootprintFree(a, false, true); a += chargeDirection)
        {
            if (FootprintHasPlayer(a)) { hit = true; break; }
            stop = a;
        }
        var swept = new List<Vector3Int>();
        for (Vector3Int a = from; ; a += chargeDirection)
        {
            swept.AddRange(CellsAt(a));
            if (a == stop) break;
        }
        // 휩쓸기처럼 아군도 피해를 받는다. 경로를 막지 않는 소환 나이트를 개체당 한 번 들이받는다.
        var sweptCells = new HashSet<Vector3Int>(swept);
        foreach (MonsterMovement ally in summoned.ToArray())
            if (ally != null && !ally.IsDead && sweptCells.Contains(ally.GridPosition)) ally.TakeDamage(ChargeDamage);
        // 소환수의 체력이 추후 달라져 살아남아도 최종 몸체와 겹치지 않는다.
        while (stop != from && !FootprintFree(stop, false)) stop -= chargeDirection;
        Vector3 start = Commander.transform.position;
        if (stop != from && Commander.ApplyKnockback(stop))
            KnightCommanderFx.Gallop(Commander.GetComponent<SpriteRenderer>(), start, Commander.transform.position);
        Vector3Int front = stop + chargeDirection * (BodyRadius + 1);
        Vector3Int side = new Vector3Int(chargeDirection.y, chargeDirection.x, 0);
        // 지나간 3칸 폭 줄에 속도선·흙먼지, 들이받은 칸(없으면 멈춘 앞)에 창끝 충격
        float delay = KnightCommanderFx.Charge(grid, swept, from, chargeDirection, hit ? Player.GridPosition : front, hit);
        KnightQuakeEffect.PlayCells(grid, Enumerable.Range(-BodyRadius, BodySize).Select(i => front + side * i), true);
        if (hit) Damage(ChargeDamage, delay, chargeDirection);
        LastOutcome = hit ? $"기병 돌진 피격 −{ChargeDamage}" : "돌진 회피";
    }

    private void ExecuteSweep()
    {
        PlayPose("Sweep_Release");
        var cells = Danger.ToList();
        float delay = KnightCommanderFx.Sweep(grid, Anchor, sweepDirection);
        KnightQuakeEffect.PlayCells(grid, cells, true);
        bool hit = cells.Contains(Player.GridPosition);
        int pushed = 0;
        if (hit)
        {
            Vector3Int push = SweepPushDirection(Player.GridPosition);
            Damage(SweepDamage, delay, push);
            if (Player.CurrentHealth > 0) pushed = KnockPlayer(push, SweepKnockback, delay);
        }
        foreach (MonsterMovement ally in summoned.ToArray())
            if (ally != null && !ally.IsDead && cells.Contains(ally.GridPosition)) ally.TakeDamage(SweepDamage);
        LastOutcome = hit ? $"마상 휩쓸기 피격 −{SweepDamage}" + (pushed > 0 ? $" · {pushed}칸 밀려남" : "") : "휩쓸기 회피";
    }

    private void ExecuteSummon()
    {
        PlayPose("Summon_Release");
        summoned.RemoveAll(m => m == null || m.IsDead);
        var spawnedCells = new List<Vector3Int>();
        foreach (Vector3Int cell in summonCells)
        {
            if (summoned.Count >= MaxSummoned) break;
            // 공개한 칸만 사용한다. 발동 전에 막힌 칸은 다른 위치로 옮기지 않고 생략한다.
            if (!grid.IsWalkableCell(cell) || spawner.IsOccupied(cell) || cell == Player.GridPosition) continue;
            MonsterMovement knight = spawner.SpawnPlaytestMonster(MonsterMovementPattern.Knight, cell, Commander.GetComponent<SpriteRenderer>().sprite,
                ChessWaveDisplay.ColorFor(MonsterMovementPattern.Knight), SummonHealth, serial++ + 901, 2, 4, 2);
            summoned.Add(knight);
            spawnedCells.Add(cell);
        }
        if (spawnedCells.Count > 0) KnightQuakeEffect.PlayDust(grid, spawnedCells);
        LastOutcome = spawnedCells.Count > 0 ? $"나이트 {spawnedCells.Count}마리 소환" : "소환 실패(칸이 막힘)";
    }

    // 맞은 자리에 따라 미는 방향: 몸 폭 정면(옆 거리 ≤ 1)은 뒤로, 앞보다 옆으로 더 먼 칸은 옆으로, 나머지는 사선으로.
    private Vector3Int SweepPushDirection(Vector3Int cell)
    {
        Vector3Int side = new Vector3Int(sweepDirection.y, sweepDirection.x, 0);
        Vector3Int d = cell - Anchor;
        int along = d.x * sweepDirection.x + d.y * sweepDirection.y;
        int lateral = d.x * side.x + d.y * side.y;
        Vector3Int outward = side * Math.Sign(lateral);
        if (Mathf.Abs(lateral) <= BodyRadius) return sweepDirection;
        if (Mathf.Abs(lateral) > along) return outward;
        return sweepDirection + outward;
    }

    // 베기에 맞은 플레이어를 direction 쪽(사선 포함)으로 최대 distance칸 민다. 벽·적·기사단장 몸 앞에서 멈춘다.
    // 판정 위치는 바로 옮기고, 그림은 피격 표시 뒤에 미끄러지듯 따라간다.
    private int KnockPlayer(Vector3Int direction, int distance, float delay)
    {
        Vector3Int to = Player.GridPosition;
        for (int i = 0; i < distance; i++)
        {
            Vector3Int next = to + direction;
            if (!grid.IsWalkableCell(next) || spawner.IsOccupied(next) || ContainsBodyCell(next)) break;
            to = next;
        }
        Vector3Int from = Player.GridPosition;
        if (to == from) return 0;
        Vector3 start = Player.transform.position;
        Player.RelocateForEncounter(to);
        StartCoroutine(SlidePlayer(Player.transform, start, Player.transform.position, delay));
        return Mathf.Max(Mathf.Abs(to.x - from.x), Mathf.Abs(to.y - from.y));
    }

    private static System.Collections.IEnumerator SlidePlayer(Transform body, Vector3 from, Vector3 to, float delay)
    {
        body.position = from;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        const float seconds = 0.15f;
        for (float t = 0f; t < seconds && body != null; t += Time.deltaTime)
        {
            float k = 1f - (1f - t / seconds) * (1f - t / seconds);     // 빠르게 밀려났다가 멈춘다
            body.position = Vector3.Lerp(from, to, k);
            yield return null;
        }
        if (body != null) body.position = to;
    }

    private void Damage(int amount, float delay, Vector3Int direction)
    {
        float previous = CombatFeedback.DamageVisualDelay;
        CombatFeedback.DamageVisualDelay = delay;
        try { Player.TakeDamage(amount, PlayerDamageKind.Hazard, direction); }
        finally { CombatFeedback.DamageVisualDelay = previous; }
    }

    private void Finish(bool won)
    {
        IsFinished = true; Won = won;
        LastOutcome = won ? "기사단장 처치" : "사망";
        Danger.Clear(); summonCells.Clear(); Redraw();
        Changed?.Invoke();
    }

    // ---------------- 모션 ----------------
    private void Face(bool left) => FacingLeft = left;

    private Animator BodyAnimator => Commander != null && Commander.TryGetComponent(out Animator a)
        && a.isActiveAndEnabled && a.runtimeAnimatorController != null ? a : null;

    private void PlayPose(string state, float normalizedTime = 0f)
    {
        Animator animator = BodyAnimator;
        if (animator == null) return;
        string name = state + (FacingLeft ? "_L" : "");
        int hash = Animator.StringToHash(name);
        if (!animator.HasState(0, hash)) hash = Animator.StringToHash(state);
        if (!animator.HasState(0, hash)) return;
        animator.ResetTrigger(ActorMotion.Attack);
        animator.ResetTrigger(ActorMotion.Hit);
        animator.Play(hash, 0, normalizedTime);
    }

    // 피격 모션이 끝나면 대기로 돌아가므로 보스 턴 시작 때 지금 단계의 자세(예고 유지·숨고르기)를 되돌린다.
    private void RestoreHeldPose()
    {
        Animator animator = BodyAnimator;
        if (animator == null) return;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName("Idle") && !info.IsName("Idle_L") && !info.IsName("Hit")) return;
        if (IsRecovery) { if (CurrentPattern == Pattern.Charge) PlayPose("Recover"); return; }
        PlayPose(CurrentPattern switch { Pattern.Charge => "Charge_Windup", Pattern.Sweep => "Sweep_Windup", _ => "Summon_Windup" }, 1f);
    }

    private MonsterStatusHud.Intent? IntentOf(MonsterMovement actor)
    {
        if (actor == null || IsFinished || actor != Commander) return null;
        if (Commander.IsActionBlocked) return MonsterStatusHud.Intent.Blocked;
        if (IsRecovery) return MonsterStatusHud.Intent.Vulnerable;
        return CurrentPattern == Pattern.Summon ? MonsterStatusHud.Intent.Summon : MonsterStatusHud.Intent.Attack;
    }

    // ---------------- 예고 표시 ----------------
    private void Redraw()
    {
        foreach (GameObject marker in markers) if (marker != null) Destroy(marker);
        markers.Clear();
        if (IsFinished || IsRecovery) return;
        foreach (Vector3Int cell in Danger) Marker(cell, new Color(1f, .18f, .25f, .55f));
        foreach (Vector3Int cell in summonCells) Marker(cell, new Color(1f, .78f, .25f, .75f));
    }

    private void Marker(Vector3Int cell, Color color)
    {
        if (pixel == null)
        {
            var texture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            pixel = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }
        var go = new GameObject("CommanderTelegraph " + cell);
        go.transform.position = grid.GetCellCenterWorld(cell);
        go.transform.localScale = Vector3.one * 0.94f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = pixel;
        renderer.color = color;
        renderer.sortingOrder = 2;
        markers.Add(go);
    }

    private void OnGUI()
    {
        if (Commander == null || Commander.IsDead || IsFinished) return;
        if (style == null)
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter, fontSize = 22, fontStyle = FontStyle.Bold, richText = true,
                font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 22)
            };
        int hp = Commander.GetComponent<CharacterHealth>().CurrentHealth;
        string text = $"기사단장 {hp}/{MaximumHealth} — <color=#ffd66e>{PatternName}</color>\n{TimingText}  ·  {LastOutcome}";
        var rect = new Rect(0, 8, Screen.width, 60);
        style.normal.textColor = new Color(0f, 0f, 0f, .8f);
        GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), System.Text.RegularExpressions.Regex.Replace(text, "<.*?>", ""), style);
        style.normal.textColor = Color.white;
        GUI.Label(rect, text, style);
    }

    private void OnDestroy()
    {
        foreach (GameObject marker in markers) if (marker != null) Destroy(marker);
        if (MonsterStatusHud.IntentOverride == (Func<MonsterMovement, MonsterStatusHud.Intent?>)IntentOf)
            MonsterStatusHud.IntentOverride = null;
    }
}
