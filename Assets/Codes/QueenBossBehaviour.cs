using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 보스 규칙은 플레이어 행동 완료 뒤에만 Tick으로 진행한다. 렌더링은 별도 UI가 맡는다.
public sealed class QueenBossBehaviour : MonoBehaviour, IBossEncounter
{
    // 여왕·근위병 그림과 모션. 비어 있으면 단색 그레이박스로 그린다. 판정 크기는 그림과 무관하다.
    [Serializable]
    public struct Visuals
    {
        public Sprite QueenSprite;
        public RuntimeAnimatorController QueenMotion;
        public Sprite GuardSprite;
        public RuntimeAnimatorController GuardMotion;
    }

    public enum Pattern { RoyalLines, Bombardment, Guards, Fortress, Checkmate, Charge }
    // 시작 위치. 여왕은 패턴을 시작할 때마다 퀸처럼 미끄러져 움직이므로 현재 위치는 Center를 쓴다.
    // 좌표는 13×13 판의 지역 좌표(1~13)로 적고 판 기준점(MapOrigin)을 더한다.
    // 보스 테스트 씬은 기준점 (0,0), 본 게임 맵은 (-8,-8)이다.
    public static readonly Vector3Int QueenCenter = new Vector3Int(7,11,0);
    public Vector3Int Origin { get; private set; }
    public Vector3Int Home => QueenCenter + Origin;
    public Vector3Int Center => Queen != null ? Queen.GridPosition : Home;

    // 판 왼쪽 아래 벽 칸: 걸을 수 있는 칸 중 가장 작은 x·y에서 1을 뺀다.
    public static Vector3Int MapOrigin(GridManager grid)
    {
        int minX = int.MaxValue, minY = int.MaxValue;
        foreach (Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
            if (grid.IsWalkableCell(cell)) { minX = Mathf.Min(minX, cell.x); minY = Mathf.Min(minY, cell.y); }
        return minX == int.MaxValue ? Vector3Int.zero : new Vector3Int(minX - 1, minY - 1, 0);
    }
    public const int FootprintRadius = 1;
    public event Action Changed;
    // 보스 테스트 씬은 끝나면 입력을 닫는다. 본 게임 마지막 웨이브는 StageFlow가 결과 화면과 입력을 맡는다.
    public bool DisableInputOnFinish { get; set; } = true;
    public MonsterMovement Queen { get; private set; }
    public Move Player { get; private set; }
    public int Turn { get; private set; }
    public int Phase => Queen != null && Queen.GetComponent<CharacterHealth>().CurrentHealth <= maximumHealth / 2 ? 2 : 1;
    public Pattern CurrentPattern { get; private set; }
    public int PatternStep { get; private set; }
    public bool IsFinished { get; private set; }
    public bool Won { get; private set; }
    public int CheckmateCount { get; private set; }
    public int MaximumHealth => maximumHealth;
    public string LastOutcome { get; private set; } = "예고 확인";
    public string PatternName => CurrentPattern switch {
        Pattern.RoyalLines => "왕권 사격", Pattern.Bombardment => "왕실 포격",
        Pattern.Guards => "근위대 전진", Pattern.Fortress => "성벽 봉쇄 · 사격", Pattern.Charge => "여왕 돌진", _ => "체크메이트" };
    public HashSet<Vector3Int> Danger { get; } = new HashSet<Vector3Int>();
    public HashSet<Vector3Int> Walls { get; } = new HashSet<Vector3Int>();
    public QueenSafeZonePlanner.Layout SafeZones { get; private set; }
    public IReadOnlyList<MonsterMovement> Guards => guards;
    public IReadOnlyList<MonsterMovement> Promoted => promoted;
    public IReadOnlyDictionary<MonsterMovement, PromotionMarker> PromotionMarkers => markers;
    public bool IsRecovery => PatternStep >= (CurrentPattern == Pattern.Checkmate ? 3 : 2);
    public int RecoveryActionsLeft => CurrentPattern == Pattern.Checkmate ? 5 - PatternStep : 3 - PatternStep;
    public string TimingText => IsFinished ? (Won ? "여왕 처치" : "플레이어 사망")
        : IsRecovery ? $"반격 기회 · {RecoveryActionsLeft}행동"
        : CurrentPattern == Pattern.Checkmate ? $"A까지 {Mathf.Max(0,2-PatternStep)}행동 / B까지 {3-PatternStep}행동"
        : CurrentPattern == Pattern.Guards ? $"정면 전진·공격 · {2-PatternStep}행동"
        : $"발동까지 {2-PatternStep}행동";

    private readonly List<MonsterMovement> guards = new List<MonsterMovement>();
    // 승격: 근위병은 소환될 때 승격할 말이 정해지고(머리 위 아이콘), 맨 아래 줄에 닿으면 그 말로 바뀐다.
    private readonly List<MonsterMovement> promoted = new List<MonsterMovement>();
    private readonly Dictionary<MonsterMovement, PromotionMarker> markers = new Dictionary<MonsterMovement, PromotionMarker>();
    private System.Random promotionRandom = new System.Random(0);
    private readonly HashSet<Vector3Int> previousB = new HashSet<Vector3Int>();
    private MonsterSpawner spawner;
    private GridManager grid;
    private Sprite square;
    private QueenSafeZonePlanner safePlanner;
    private Visuals visuals;
    private int maximumHealth, serial, normalIndex, phaseTwoNormals, lineVariant;
    // 근위병 방패 밀치기 모션의 발동 프레임(4번째, 70+80+150ms)에 맞춰 피해 연출을 늦춘다. 판정은 즉시다.
    private const float GuardStrikeVisualDelay = 0.3f;
    private const float GuardStepSeconds = 0.36f;
    private const int LineDamage = 3, BombardDamage = 4, UltimateDamage = 8, GuardHealth = 4, ChargeDamage = 5;
    // 패턴 시작 이동은 판 위쪽(중심 y ≥ 8, 몸은 위 7줄) 안에서만. 돌진만 아래로 내려올 수 있다.
    private const int TopCenterMinY = 8;
    // 룩은 성벽 레일 위 전용 적이라 바닥 승격 후보에서 뺀다. 퀸도 보스와 겹쳐 제외.
    public static readonly MonsterMovementPattern[] PromotionPool =
        { MonsterMovementPattern.Knight, MonsterMovementPattern.Bishop, MonsterMovementPattern.Bomber };
    public const int MaxPromoted = 2;            // 동시에 살아 있는 승격 적 상한. 넘으면 근위병이 무너지며 주변 1칸 피해 1
    public const int CrumbleDamage = 1;
    private static readonly Pattern[] NormalCycle = { Pattern.RoyalLines, Pattern.Bombardment, Pattern.Guards, Pattern.Fortress, Pattern.Charge };
    private static readonly Vector3Int[] Orthogonal = { Vector3Int.up,Vector3Int.right,Vector3Int.down,Vector3Int.left };
    private static readonly Vector3Int[] Diagonal = { new Vector3Int(1,1,0),new Vector3Int(1,-1,0),new Vector3Int(-1,1,0),new Vector3Int(-1,-1,0) };
    private static readonly Vector3Int[] Queenwise = { Vector3Int.up,Vector3Int.right,Vector3Int.down,Vector3Int.left,
        new Vector3Int(1,1,0),new Vector3Int(1,-1,0),new Vector3Int(-1,1,0),new Vector3Int(-1,-1,0) };
    private System.Random moveRandom = new System.Random(0);
    private Vector3Int chargeDirection;
    public Vector3Int ChargeDirection => chargeDirection;

    public void Initialize(MonsterSpawner spawner, GridManager grid, Move player, Sprite square, int health, int seed, bool phaseTwo,
        Visuals visuals = default)
    {
        this.visuals = visuals;
        MonsterStatusHud.IntentOverride = IntentOf;
        this.spawner = spawner; this.grid = grid; Player = player; this.square = square; maximumHealth = health;
        Turn = normalIndex = phaseTwoNormals = lineVariant = CheckmateCount = 0; serial = 0;
        IsFinished = Won = false; guards.Clear(); promoted.Clear(); previousB.Clear();
        foreach (PromotionMarker marker in markers.Values) if (marker != null) Destroy(marker.gameObject);
        markers.Clear(); promotionRandom = new System.Random(seed); moveRandom = new System.Random(seed * 31 + 7); Danger.Clear(); Walls.Clear(); grid.SetTemporaryObstacles(null);
        Queen = null;
        Origin = MapOrigin(grid);
        Queen = Spawn(Home, maximumHealth, new Color32(80,55,110,255), "Queen · 3×3",
            visuals.QueenSprite, visuals.QueenMotion);
        // 그림은 32 PPU 원래 크기(120px)로 둬 화면 픽셀 격자에 맞춘다. 발이 3×3 점유의 아래 가장자리에 온다.
        Queen.transform.localScale = visuals.QueenSprite != null ? Vector3.one : new Vector3(3f,3f,1f);
        if (phaseTwo) Queen.GetComponent<CharacterHealth>().TakeDamage(maximumHealth / 2);
        // 초기 좌표와 3×3 점유가 확정된 뒤 후보를 만든다.
        safePlanner = new QueenSafeZonePlanner(grid, spawner, Player, Queen, seed);
        PrepareNext();
    }

    private MonsterMovement Spawn(Vector3Int cell, int health, Color color, string label,
        Sprite sprite = null, RuntimeAnimatorController motion = null)
    {
        MonsterMovement actor = spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,
            cell, sprite != null ? sprite : square, color, health, serial++, 2, 4, 2, sprite != null ? motion : null);
        actor.name = label;
        return actor;
    }

    public MonsterMovement Body => Queen;
    public bool ContainsBodyCell(Vector3Int cell) => ContainsQueenCell(cell);
    public IEnumerable<Vector3Int> BodyCells
    {
        get { for (int y=-1;y<=1;y++) for (int x=-1;x<=1;x++) yield return Center + new Vector3Int(x,y,0); }
    }

    public bool ContainsQueenCell(Vector3Int cell) => Queen != null && Queen.gameObject.activeInHierarchy && !Queen.IsDead
        && Mathf.Abs(cell.x-Center.x) <= FootprintRadius && Mathf.Abs(cell.y-Center.y) <= FootprintRadius;

    private void PrepareNext()
    {
        grid.SetTemporaryObstacles(null); Walls.Clear(); Danger.Clear(); SafeZones = null; PatternStep = 0;
        bool ultimate = Phase == 2 && phaseTwoNormals >= 3;
        if (ultimate) safePlanner.Refresh();     // 여왕이 움직였으므로 안전지대 후보를 현재 점유로 다시 만든다
        if (ultimate && safePlanner.TryChoose(Player.GridPosition, previousB, out QueenSafeZonePlanner.Layout layout))
        {
            CurrentPattern = Pattern.Checkmate; SafeZones = layout; phaseTwoNormals = 0; CheckmateCount++;
        }
        else
        {
            // 현재 점유로 해법을 만들 수 없다면 궁극기를 강행하지 않고 일반 패턴 후 다시 검사한다.
            do { CurrentPattern = NormalCycle[normalIndex++ % NormalCycle.Length]; }
                while (Phase == 1 && (CurrentPattern == Pattern.Fortress || CurrentPattern == Pattern.Charge));
            if (Phase == 2) phaseTwoNormals++;
            if (Turn > 0) Glide(CurrentPattern);      // 첫 예고는 시작 자리에서
            if (CurrentPattern == Pattern.Guards) SummonGuards();
            else if (CurrentPattern == Pattern.Charge) BuildCharge();
            else if (CurrentPattern == Pattern.Bombardment) BuildBombardment();
            else
            {
                if (CurrentPattern == Pattern.Fortress) BuildWalls();
                BuildLines();
            }
        }
        PlayQueenPose(WindupState);
        LastOutcome = "새 예고 · " + PatternName; Changed?.Invoke();
    }

    // ---------------- 여왕 기믹 모션 ----------------
    // 준비 클립은 예고 자세에서 멈추고(예고 두 행동 동안 유지), 발동 클립은 발사부터 대기 복귀까지 재생한다.
    // 체크메이트는 A·B 타격 자세를 이어 가다 탈진(무릎 꿇음)을 반격 기회 동안 반복한다. 판정과 무관한 연출이다.
    private string WindupState => CurrentPattern switch {
        Pattern.RoyalLines => "RoyalLines_Windup", Pattern.Bombardment => "Bombardment_Windup",
        Pattern.Guards => "Guards_Windup", Pattern.Fortress => "Fortress_Windup", Pattern.Charge => "Fortress_Windup", _ => "Checkmate_Windup" };
    private string ReleaseState => CurrentPattern switch {
        Pattern.RoyalLines => "RoyalLines_Release", Pattern.Bombardment => "Bombardment_Release",
        Pattern.Guards => "Guards_Release", Pattern.Charge => "Bombardment_Release", _ => "Fortress_Release" };
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int HitState = Animator.StringToHash("Hit");

    private Animator QueenAnimator => Queen != null && Queen.TryGetComponent(out Animator animator)
        && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null ? animator : null;

    private void PlayQueenPose(string state, float normalizedTime = 0f)
    {
        Animator animator = QueenAnimator;
        int hash = Animator.StringToHash(state);
        if (animator == null || !animator.HasState(0, hash)) return;
        // 이전에 쌓여 아직 소비되지 않은 시전·피격 트리거가 기믹 자세를 늦게 덮어쓰지 않게 지운다.
        animator.ResetTrigger(ActorMotion.Attack);
        animator.ResetTrigger(ActorMotion.Hit);
        animator.Play(hash, 0, normalizedTime);
    }

    // 피격 모션은 끝나면 대기로 돌아가므로, 보스 턴 시작 때 지금 단계의 유지 자세를 되돌린다.
    private void RestoreHeldPose()
    {
        Animator animator = QueenAnimator;
        if (animator == null || IsFinished) return;
        int current = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (current != IdleState && current != HitState) return;
        if (CurrentPattern == Pattern.Checkmate)
            PlayQueenPose(IsRecovery ? "Checkmate_Exhausted" : PatternStep >= 2 ? "Checkmate_StrikeA" : WindupState,
                IsRecovery ? 0f : 1f);
        else if (!IsRecovery)
            PlayQueenPose(WindupState, 1f);
    }

    // ---------------- 이동: 패턴 시작 때 퀸처럼 한 번 미끄러진다 ----------------
    // 가로·세로·대각선 중 한 방향으로, 3×3 몸이 지나갈 수 있는 데까지. 플레이어·다른 적은 통과하지 않는다.
    // 다음 패턴이 플레이어를 노리기 좋은 자리를 고른다(사격: 플레이어가 선 안에, 돌진: 플레이어가 돌진 줄 위에, 그 밖: 플레이어 열 쪽으로).
    private bool FootprintFree(Vector3Int center, bool playerBlocks = true)
    {
        for (int y=-1;y<=1;y++) for (int x=-1;x<=1;x++)
        {
            Vector3Int cell = center + new Vector3Int(x,y,0);
            if (!grid.IsWalkableCell(cell)) return false;
            if (playerBlocks && cell == Player.GridPosition) return false;
            if (spawner.TryGetMonsterAtCell(cell, out MonsterMovement other) && other != Queen) return false;
        }
        return true;
    }

    private void Glide(Pattern next)
    {
        if (Queen == null || Queen.IsDead) return;
        Vector3Int from = Center;
        var options = new List<(Vector3Int cell, int distance)> { (from, 0) };
        foreach (Vector3Int direction in Queenwise)
            for (int d = 1; d <= 12; d++)
            {
                Vector3Int cell = from + direction * d;
                if (!FootprintFree(cell)) break;
                options.Add((cell, d));
            }
        // 돌진 뒤처럼 아래에 있으면 위쪽 영역으로 돌아갈 수 있는 자리만, 없으면 가장 위쪽 자리.
        var top = options.Where(o => o.cell.y >= Origin.y + TopCenterMinY).ToList();
        if (top.Count == 0) top = options.Where(o => o.cell.y == options.Max(m => m.cell.y)).ToList();
        Vector3Int player = Player.GridPosition;
        double Score((Vector3Int cell, int distance) o)
        {
            double score = -Mathf.Abs(o.distance - 3) * 0.5 + moveRandom.NextDouble();
            if (o.distance == 0) score -= 2;                      // 제자리보다는 움직이는 쪽
            int dx = player.x - o.cell.x, dy = player.y - o.cell.y;
            if (next == Pattern.RoyalLines || next == Pattern.Fortress)
            {
                bool orthogonal = lineVariant % 2 == 0;
                bool inLane = orthogonal ? Mathf.Abs(dx) <= 1 || Mathf.Abs(dy) <= 1 : Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) <= 1;
                if (inLane) score += 10;
            }
            else if (next == Pattern.Charge)
            {
                if (Mathf.Abs(dx) <= 1 || Mathf.Abs(dy) <= 1 || Mathf.Abs(Mathf.Abs(dx) - Mathf.Abs(dy)) <= 1) score += 10;
            }
            else score -= Mathf.Abs(dx) * 0.6;
            return score;
        }
        (Vector3Int cell, int distance) best = top.OrderByDescending(Score).First();
        if (best.cell == from) return;
        Vector3 start = Queen.transform.position;
        if (!Queen.ApplyKnockback(best.cell)) return;
        QueenAttackFx.Glide(Queen.GetComponent<SpriteRenderer>(), start, Queen.transform.position);
        KnightQuakeEffect.PlayDust(grid, new[] { best.cell + new Vector3Int(-1,-1,0), best.cell + Vector3Int.down, best.cell + new Vector3Int(1,-1,0) });
        LastOutcome = "여왕 이동";
    }

    // ---------------- 돌진(2페이즈) ----------------
    // 플레이어 쪽 한 방향을 골라 그 줄을 끝까지(벽·다른 적 앞까지) 예고하고, 두 번째 행동 뒤 그 줄을 미끄러져 지나간다.
    // 플레이어가 줄 위에 있으면 바로 앞에서 멈추며 피해 5. 몸이 겹치지 않도록 플레이어를 통과하지는 않는다.
    private List<Vector3Int> ChargePath(Vector3Int direction)
    {
        var path = new List<Vector3Int>();
        for (Vector3Int c = Center + direction; FootprintFree(c, false); c += direction) path.Add(c);
        return path;
    }

    private void BuildCharge()
    {
        Vector3Int player = Player.GridPosition;
        Vector3Int best = Vector3Int.down; double bestScore = double.MinValue;
        foreach (Vector3Int direction in Queenwise)
        {
            List<Vector3Int> path = ChargePath(direction);
            if (path.Count == 0) continue;
            bool hits = path.Any(c => Mathf.Abs(c.x - player.x) <= 1 && Mathf.Abs(c.y - player.y) <= 1);
            double closest = path.Min(c => Mathf.Max(Mathf.Abs(c.x - player.x), Mathf.Abs(c.y - player.y)));
            double score = (hits ? 100 : 0) - closest + path.Count * 0.1 + moveRandom.NextDouble() * 0.5;
            if (score > bestScore) { bestScore = score; best = direction; }
        }
        chargeDirection = best;
        foreach (Vector3Int c in ChargePath(best))
            for (int y=-1;y<=1;y++) for (int x=-1;x<=1;x++)
            {
                Vector3Int cell = c + new Vector3Int(x,y,0);
                if (!ContainsQueenCell(cell)) Danger.Add(cell);
            }
    }

    private void ExecuteCharge()
    {
        Vector3Int from = Center;
        Vector3Int stop = from;
        bool hit = false;
        // 예고 뒤 근위병 등이 들어왔을 수 있으므로 지금 점유로 다시 걷는다.
        for (Vector3Int c = from + chargeDirection; FootprintFree(c, false); c += chargeDirection)
        {
            if (Mathf.Abs(c.x - Player.GridPosition.x) <= 1 && Mathf.Abs(c.y - Player.GridPosition.y) <= 1) { hit = true; break; }
            stop = c;
        }
        Vector3 start = Queen.transform.position;
        if (stop != from && Queen.ApplyKnockback(stop))
            QueenAttackFx.Glide(Queen.GetComponent<SpriteRenderer>(), start, Queen.transform.position);
        float delay = QueenAttackFx.Charge(grid, from, stop, chargeDirection, hit ? Player.GridPosition : (Vector3Int?)null);
        CameraController.PlayImpactShake(new Vector3(chargeDirection.x, chargeDirection.y, 0f));
        if (hit && Player.CurrentHealth > 0)
        {
            float previousDelay = CombatFeedback.DamageVisualDelay;
            CombatFeedback.DamageVisualDelay = delay;
            try { Player.TakeDamage(ChargeDamage, PlayerDamageKind.Hazard, chargeDirection); }
            finally { CombatFeedback.DamageVisualDelay = previousDelay; }
        }
        LastOutcome = hit ? $"여왕 돌진 피격 −{ChargeDamage}" : "돌진 회피";
    }

    private void BuildBombardment()
    {
        AddBombardment(Player.GridPosition);
        if (Phase == 2) AddBombardment(new Vector3Int(2*Origin.x+14-Player.GridPosition.x,Player.GridPosition.y+2,0));
    }
    private void AddBombardment(Vector3Int center)
    {
        for (int y=-1;y<=1;y++) for(int x=-1;x<=1;x++)
        { Vector3Int cell=center+new Vector3Int(x,y,0); if(grid.IsWalkableCell(cell)&&!ContainsQueenCell(cell))Danger.Add(cell); }
    }
    private void BuildWalls()
    {
        foreach (int offset in new[] {-1,1})
        {
            Vector3Int cell = Player.GridPosition + new Vector3Int(offset,1,0);
            if (grid.IsWalkableCell(cell) && !spawner.IsOccupied(cell)) Walls.Add(cell);
        }
        grid.SetTemporaryObstacles(Walls);
    }
    private void BuildLines()
    {
        Vector3Int[] directions = lineVariant++ % 2 == 0 ? Orthogonal : Diagonal;
        foreach (Vector3Int direction in directions) for (int lane=-1;lane<=1;lane++)
        {
            Vector3Int origin = Center + (direction.x == 0 ? new Vector3Int(lane,0,0) : new Vector3Int(0,lane,0));
            for (Vector3Int cell=origin+direction; grid.IsInsideMapCell(cell); cell+=direction)
            {
                if (!grid.IsWalkableCell(cell)) break;
                if (!ContainsQueenCell(cell)) Danger.Add(cell);
            }
        }
    }
    private void SummonGuards()
    {
        guards.RemoveAll(g=>g==null||g.IsDead);
        foreach (int offset in new[] {0,-3,3})
        {
            if (guards.Count >= 3) break;
            Vector3Int cell = new Vector3Int(Mathf.Clamp(Player.GridPosition.x+offset,Origin.x+1,Origin.x+13),Mathf.Min(Origin.y+9,Player.GridPosition.y+3),0);
            if(cell==Player.GridPosition||!grid.IsWalkableCell(cell)||spawner.IsOccupied(cell))continue;
            MonsterMovement guard=Spawn(cell,GuardHealth,new Color32(115,135,151,255),"Guard ↓",
                visuals.GuardSprite,visuals.GuardMotion);
            // 64px 근위병 시트는 나이트·룩처럼 정확히 1/2 배율로 한 칸에 맞춘다.
            guard.transform.localScale = Vector3.one*(visuals.GuardSprite != null ? .5f : .75f); guards.Add(guard);
            MonsterMovementPattern promotion = PromotionPool[promotionRandom.Next(PromotionPool.Length)];
            PromotionMarker marker = PromotionMarker.Attach(guard, promotion);
            marker.SetRowsLeft(RowsToPromotion(cell));
            markers[guard] = marker;
            guard.Defeated += OnGuardDefeated;
        }
    }

    public void Tick()
    {
        if (IsFinished) return;
        Turn++;
        if (Queen == null || Queen.IsDead) { Finish(true); return; }
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        LastOutcome = "준비";
        RestoreHeldPose();
        bool blocked = Queen.ConsumeBlockedTurn();
        promoted.RemoveAll(m => m == null || m.IsDead);
        // 이번 턴에 막 승격한 적은 다음 턴부터 움직인다.
        var acting = new List<MonsterMovement>(promoted);
        if (CurrentPattern != Pattern.Checkmate)
        {
            AdvanceGuards();
            spawner.RunBossAllyTurn(acting);
        }
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        if (blocked) { LastOutcome = "여왕 봉쇄 · 발동 지연"; Changed?.Invoke(); return; }
        if (!IsRecovery)
        {
            if (CurrentPattern == Pattern.Checkmate)
            {
                if (PatternStep == 1) { PlayQueenPose("Checkmate_StrikeA"); StrikeOutside(SafeZones.A,"A"); }
                else if (PatternStep == 2) { PlayQueenPose("Checkmate_StrikeB"); StrikeOutside(SafeZones.B,"B"); previousB.Clear(); previousB.UnionWith(SafeZones.B); }
            }
            else if (PatternStep == 1 && CurrentPattern == Pattern.Charge)
            {
                PlayQueenPose(ReleaseState);
                ExecuteCharge();
            }
            else if (PatternStep == 1 && CurrentPattern != Pattern.Guards)
            {
                PlayQueenPose(ReleaseState);
                // 공격 연출: 맞는 칸에 광선/빛기둥을 띄우고, 피해 숫자는 효과가 플레이어 칸에 닿을 때 보인다.
                float delay = CurrentPattern == Pattern.Bombardment
                    ? QueenAttackFx.Bombard(grid, Danger, Player.GridPosition)
                    : QueenAttackFx.Lines(grid, Danger, Center, Player.GridPosition);
                Strike(Danger,CurrentPattern == Pattern.Bombardment ? BombardDamage : LineDamage, delay);
            }
            // 근위대는 공격이 없으므로 예고가 끝나는 차례에 전진 지휘 → 휴식 모션으로 넘어간다.
            else if (PatternStep == 1 && CurrentPattern == Pattern.Guards) PlayQueenPose(ReleaseState);
        }
        else LastOutcome = "반격 기회";
        PatternStep++;
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        if (Queen == null || Queen.IsDead) { Finish(true); return; }
        if (PatternStep >= (CurrentPattern == Pattern.Checkmate ? 5 : 3)) PrepareNext();
        Changed?.Invoke();
    }
    private void AdvanceGuards()
    {
        guards.RemoveAll(g=>g==null||g.IsDead);
        foreach (MonsterMovement guard in guards.OrderBy(g=>Mathf.Abs(g.GridPosition.x-Player.GridPosition.x)+Mathf.Abs(g.GridPosition.y-Player.GridPosition.y)).ToArray())
        {
            if (Player.CurrentHealth <= 0) break;
            if (guard.ConsumeBlockedTurn()) continue;
            Vector3Int next=guard.GridPosition+Vector3Int.down;
            // 맨 아래 줄(아래가 벽)에 닿아 있으면 이번 턴에 승격한다.
            if(!grid.IsWalkableCell(next)){ Promote(guard); continue; }
            if(next==Player.GridPosition)
            {
                ActorMotion.Trigger(guard,ActorMotion.Attack);
                float previousDelay=CombatFeedback.DamageVisualDelay;
                CombatFeedback.DamageVisualDelay=visuals.GuardMotion!=null?GuardStrikeVisualDelay:0f;
                try{Player.TakeDamage(1,PlayerDamageKind.Normal,Vector3Int.down);}
                finally{CombatFeedback.DamageVisualDelay=previousDelay;}
                LastOutcome="근위병 정면 공격 −1";
            }
            else if(grid.IsWalkableCell(next)&&!spawner.IsOccupied(next)&&guard.ApplyKnockback(next))
                StartCoroutine(PlayGuardStep(guard));
            if(guard!=null&&markers.TryGetValue(guard,out PromotionMarker marker)&&marker!=null)
                marker.SetRowsLeft(RowsToPromotion(guard.GridPosition));
        }
    }

    // 지금 칸에서 아래로 몇 칸 더 가야 맨 아래 줄인지(0 = 이미 맨 아래 줄, 다음 전진 차례에 승격)
    private int RowsToPromotion(Vector3Int cell)
    {
        int rows=0;
        for(Vector3Int c=cell+Vector3Int.down;grid.IsWalkableCell(c);c+=Vector3Int.down)rows++;
        return rows;
    }

    private void Promote(MonsterMovement guard)
    {
        Vector3Int cell=guard.GridPosition;
        MonsterMovementPattern pattern=markers.TryGetValue(guard,out PromotionMarker marker)&&marker!=null
            ?marker.Promotion:PromotionPool[0];
        if(marker!=null)Destroy(marker.gameObject);
        markers.Remove(guard);guards.Remove(guard);
        guard.Defeated-=OnGuardDefeated;
        spawner.RemoveActor(guard);
        KnightQuakeEffect.PlayDust(grid,new[]{cell});
        promoted.RemoveAll(m=>m==null||m.IsDead);
        if(promoted.Count>=MaxPromoted)
        {
            // 상한 초과: 승격 대신 무너지며 둘레 1칸에 작은 피해
            KnightQuakeEffect.PlayCells(grid,Neighbours(cell),false);
            if(Mathf.Max(Mathf.Abs(Player.GridPosition.x-cell.x),Mathf.Abs(Player.GridPosition.y-cell.y))<=1)
                Player.TakeDamage(CrumbleDamage,PlayerDamageKind.Hazard,Player.GridPosition-cell);
            LastOutcome="근위병 붕괴(승격 상한)";
            return;
        }
        MonsterMovement promotedActor=spawner.SpawnPlaytestMonster(pattern,cell,square,new Color32(230,200,120,255),
            PromotedHealth(pattern),serial++,2,4,2);
        promotedActor.name="Promoted "+pattern;
        promoted.Add(promotedActor);
        CameraController.PlayImpactShake(Vector3.down);
        LastOutcome="근위병 승격 → "+PromotionName(pattern);
    }

    // 승격 직후 체력은 일반 적의 절반(올림): 나이트 4 → 2, 비숍 2 → 1, 폭탄병 2 → 1
    public static int PromotedHealth(MonsterMovementPattern pattern)=>pattern==MonsterMovementPattern.Knight?2:1;
    public static string PromotionName(MonsterMovementPattern pattern)=>pattern switch{
        MonsterMovementPattern.Knight=>"나이트",MonsterMovementPattern.Bishop=>"비숍",MonsterMovementPattern.Bomber=>"폭탄병",_=>pattern.ToString()};

    private static IEnumerable<Vector3Int> Neighbours(Vector3Int cell)
    {
        for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)yield return cell+new Vector3Int(x,y,0);
    }

    // 승격 전에 근위병을 쓰러뜨리면 주도권 +1(주도권 규칙이 있는 씬에서만).
    private void OnGuardDefeated(MonsterMovement guard,Vector3Int cell)
    {
        guard.Defeated-=OnGuardDefeated;
        if(markers.TryGetValue(guard,out PromotionMarker marker)&&marker!=null)Destroy(marker.gameObject);
        markers.Remove(guard);
        Player.Initiative?.Grant(1,"승격 저지");
        LastOutcome="승격 저지";
    }
    // 전진은 논리상 한 번의 칸 이동이다. 걷기 모션은 연출만 맡는다.
    private System.Collections.IEnumerator PlayGuardStep(MonsterMovement guard)
    {
        ActorMotion.SetMoving(guard,true);
        yield return new WaitForSeconds(GuardStepSeconds);
        if(guard!=null)ActorMotion.SetMoving(guard,false);
    }

    // 상태 HUD 아이콘: 여왕은 현재 패턴, 근위병은 전진·정면 공격·정지.
    private MonsterStatusHud.Intent? IntentOf(MonsterMovement actor)
    {
        if(actor==null||IsFinished)return null;
        if(actor==Queen)
        {
            if(Queen.IsActionBlocked)return MonsterStatusHud.Intent.Blocked;
            if(IsRecovery)return MonsterStatusHud.Intent.Vulnerable;
            return CurrentPattern switch {
                Pattern.Bombardment=>MonsterStatusHud.Intent.Fire,
                Pattern.Guards=>MonsterStatusHud.Intent.Summon,
                Pattern.Checkmate=>MonsterStatusHud.Intent.Checkmate,
                Pattern.Charge=>MonsterStatusHud.Intent.Attack,
                _=>MonsterStatusHud.Intent.Crossbow };
        }
        if(!guards.Contains(actor))return null;
        if(actor.IsActionBlocked)return MonsterStatusHud.Intent.Blocked;
        if(CurrentPattern==Pattern.Checkmate)return MonsterStatusHud.Intent.Wait;
        return actor.GridPosition+Vector3Int.down==Player.GridPosition
            ? MonsterStatusHud.Intent.Attack : MonsterStatusHud.Intent.Move;
    }

    private void StrikeOutside(HashSet<Vector3Int> safe, string label)
    {
        var area=new HashSet<Vector3Int>();
        foreach(Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
        {
            Vector3Int local=cell-Origin;
            if(grid.IsInsideMapCell(cell)&&local.x>=1&&local.x<=13&&local.y>=1&&local.y<=13&&!safe.Contains(cell))area.Add(cell);
        }
        Color32 color=label=="A"?new Color32(90,170,255,255):new Color32(255,92,84,255);
        float delay=QueenAttackFx.Checkmate(grid,area,color,Turn*7+label[0],Player.GridPosition);
        Strike(area,UltimateDamage,delay);
        if(Player.CurrentHealth>0&&!area.Contains(Player.GridPosition))LastOutcome=label+" 회피 성공";
    }
    private void Strike(HashSet<Vector3Int> area, int damage, float visualDelay = 0f)
    {
        // 시전 모션은 호출한 쪽이 패턴별 발동 클립으로 고른다(컨트롤러에 없으면 예전 공통 시전).
        if(QueenAnimator==null||!QueenAnimator.HasState(0,Animator.StringToHash(WindupState)))ActorMotion.Trigger(Queen,ActorMotion.Attack);
        bool hit=area.Contains(Player.GridPosition);
        if(hit)
        {
            float previousDelay=CombatFeedback.DamageVisualDelay;
            CombatFeedback.DamageVisualDelay=visualDelay;
            try{Player.TakeDamage(damage,PlayerDamageKind.Hazard,Player.GridPosition-Center);}
            finally{CombatFeedback.DamageVisualDelay=previousDelay;}
        }
        foreach(MonsterMovement guard in guards.ToArray())if(guard!=null&&!guard.IsDead&&area.Contains(guard.GridPosition))guard.TakeDamage(damage);
        foreach(MonsterMovement ally in promoted.ToArray())if(ally!=null&&!ally.IsDead&&area.Contains(ally.GridPosition))ally.TakeDamage(damage);
        LastOutcome=hit?$"{PatternName} 피격 −{damage}":"공격 회피";
    }
    private void OnDestroy()
    {
        if (MonsterStatusHud.IntentOverride == (Func<MonsterMovement, MonsterStatusHud.Intent?>)IntentOf)
            MonsterStatusHud.IntentOverride = null;
    }

    private void Finish(bool won)
    {
        IsFinished=true; Won=won; LastOutcome=won?"여왕 처치 · 클리어":"사망 · 같은 조건으로 재시작 가능";
        if (DisableInputOnFinish) Player.SetInputEnabled(false);
        Changed?.Invoke();
    }
}
