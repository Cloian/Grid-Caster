using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 보스 규칙은 플레이어 행동 완료 뒤에만 Tick으로 진행한다. 렌더링은 별도 UI가 맡는다.
public sealed class QueenBossBehaviour : MonoBehaviour
{
    public enum Pattern { RoyalLines, Bombardment, Guards, Fortress, Checkmate }
    public static readonly Vector3Int QueenCenter = new Vector3Int(7,11,0);
    public const int FootprintRadius = 1;
    public event Action Changed;
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
        Pattern.Guards => "근위대 전진", Pattern.Fortress => "성벽 봉쇄 · 사격", _ => "체크메이트" };
    public HashSet<Vector3Int> Danger { get; } = new HashSet<Vector3Int>();
    public HashSet<Vector3Int> Walls { get; } = new HashSet<Vector3Int>();
    public QueenSafeZonePlanner.Layout SafeZones { get; private set; }
    public IReadOnlyList<MonsterMovement> Guards => guards;
    public bool IsRecovery => PatternStep >= (CurrentPattern == Pattern.Checkmate ? 3 : 2);
    public int RecoveryActionsLeft => CurrentPattern == Pattern.Checkmate ? 5 - PatternStep : 3 - PatternStep;
    public string TimingText => IsFinished ? (Won ? "여왕 처치" : "플레이어 사망")
        : IsRecovery ? $"반격 기회 · {RecoveryActionsLeft}행동"
        : CurrentPattern == Pattern.Checkmate ? $"A까지 {Mathf.Max(0,2-PatternStep)}행동 / B까지 {3-PatternStep}행동"
        : CurrentPattern == Pattern.Guards ? $"정면 전진·공격 · {2-PatternStep}행동"
        : $"발동까지 {2-PatternStep}행동";

    private readonly List<MonsterMovement> guards = new List<MonsterMovement>();
    private readonly HashSet<Vector3Int> previousB = new HashSet<Vector3Int>();
    private MonsterSpawner spawner;
    private GridManager grid;
    private Sprite square;
    private QueenSafeZonePlanner safePlanner;
    private int maximumHealth, serial, normalIndex, phaseTwoNormals, lineVariant;
    private const int LineDamage = 3, BombardDamage = 4, UltimateDamage = 8, GuardHealth = 4;
    private static readonly Pattern[] NormalCycle = { Pattern.RoyalLines, Pattern.Bombardment, Pattern.Guards, Pattern.Fortress };
    private static readonly Vector3Int[] Orthogonal = { Vector3Int.up,Vector3Int.right,Vector3Int.down,Vector3Int.left };
    private static readonly Vector3Int[] Diagonal = { new Vector3Int(1,1,0),new Vector3Int(1,-1,0),new Vector3Int(-1,1,0),new Vector3Int(-1,-1,0) };

    public void Initialize(MonsterSpawner spawner, GridManager grid, Move player, Sprite square, int health, int seed, bool phaseTwo)
    {
        this.spawner = spawner; this.grid = grid; Player = player; this.square = square; maximumHealth = health;
        Turn = normalIndex = phaseTwoNormals = lineVariant = CheckmateCount = 0; serial = 0;
        IsFinished = Won = false; guards.Clear(); previousB.Clear(); Danger.Clear(); Walls.Clear(); grid.SetTemporaryObstacles(null);
        Queen = null;
        Queen = Spawn(QueenCenter, maximumHealth, new Color32(80,55,110,255), "Queen · 3×3");
        Queen.transform.localScale = new Vector3(3f,3f,1f);
        if (phaseTwo) Queen.GetComponent<CharacterHealth>().TakeDamage(maximumHealth / 2);
        // 초기 좌표와 3×3 점유가 확정된 뒤 후보를 만든다.
        safePlanner = new QueenSafeZonePlanner(grid, spawner, Player, Queen, seed);
        PrepareNext();
    }

    private MonsterMovement Spawn(Vector3Int cell, int health, Color color, string label)
    {
        MonsterMovement actor = spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,
            cell, square, color, health, serial++, 2, 4, 2);
        actor.name = label;
        return actor;
    }

    public bool ContainsQueenCell(Vector3Int cell) => Queen != null && Queen.gameObject.activeInHierarchy && !Queen.IsDead
        && Mathf.Abs(cell.x-QueenCenter.x) <= FootprintRadius && Mathf.Abs(cell.y-QueenCenter.y) <= FootprintRadius;

    private void PrepareNext()
    {
        grid.SetTemporaryObstacles(null); Walls.Clear(); Danger.Clear(); SafeZones = null; PatternStep = 0;
        bool ultimate = Phase == 2 && phaseTwoNormals >= 3;
        if (ultimate && safePlanner.TryChoose(Player.GridPosition, previousB, out QueenSafeZonePlanner.Layout layout))
        {
            CurrentPattern = Pattern.Checkmate; SafeZones = layout; phaseTwoNormals = 0; CheckmateCount++;
        }
        else
        {
            // 현재 점유로 해법을 만들 수 없다면 궁극기를 강행하지 않고 일반 패턴 후 다시 검사한다.
            do { CurrentPattern = NormalCycle[normalIndex++ % NormalCycle.Length]; }
                while (Phase == 1 && CurrentPattern == Pattern.Fortress);
            if (Phase == 2) phaseTwoNormals++;
            if (CurrentPattern == Pattern.Guards) SummonGuards();
            else if (CurrentPattern == Pattern.Bombardment) BuildBombardment();
            else
            {
                if (CurrentPattern == Pattern.Fortress) BuildWalls();
                BuildLines();
            }
        }
        LastOutcome = "새 예고 · " + PatternName; Changed?.Invoke();
    }

    private void BuildBombardment()
    {
        AddBombardment(Player.GridPosition);
        if (Phase == 2) AddBombardment(new Vector3Int(14-Player.GridPosition.x,Player.GridPosition.y+2,0));
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
            Vector3Int origin = QueenCenter + (direction.x == 0 ? new Vector3Int(lane,0,0) : new Vector3Int(0,lane,0));
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
            Vector3Int cell = new Vector3Int(Mathf.Clamp(Player.GridPosition.x+offset,1,13),Mathf.Min(9,Player.GridPosition.y+3),0);
            if(cell==Player.GridPosition||!grid.IsWalkableCell(cell)||spawner.IsOccupied(cell))continue;
            MonsterMovement guard=Spawn(cell,GuardHealth,new Color32(115,135,151,255),"Guard ↓");
            guard.transform.localScale = Vector3.one*.75f; guards.Add(guard);
        }
    }

    public void Tick()
    {
        if (IsFinished) return;
        Turn++;
        if (Queen == null || Queen.IsDead) { Finish(true); return; }
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        LastOutcome = "준비";
        bool blocked = Queen.ConsumeBlockedTurn();
        if (!IsRecovery && CurrentPattern != Pattern.Checkmate) AdvanceGuards();
        if (Player.CurrentHealth <= 0) { Finish(false); return; }
        if (blocked) { LastOutcome = "여왕 봉쇄 · 발동 지연"; Changed?.Invoke(); return; }
        if (!IsRecovery)
        {
            if (CurrentPattern == Pattern.Checkmate)
            {
                if (PatternStep == 1) StrikeOutside(SafeZones.A,"A");
                else if (PatternStep == 2) { StrikeOutside(SafeZones.B,"B"); previousB.Clear(); previousB.UnionWith(SafeZones.B); }
            }
            else if (PatternStep == 1 && CurrentPattern != Pattern.Guards)
                Strike(Danger,CurrentPattern == Pattern.Bombardment ? BombardDamage : LineDamage);
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
            if(next==Player.GridPosition) {Player.TakeDamage(1,PlayerDamageKind.Normal,Vector3Int.down);LastOutcome="근위병 정면 공격 −1";}
            else if(grid.IsWalkableCell(next)&&!spawner.IsOccupied(next))guard.ApplyKnockback(next);
        }
    }
    private void StrikeOutside(HashSet<Vector3Int> safe, string label)
    {
        var area=new HashSet<Vector3Int>();
        foreach(Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
            if(grid.IsInsideMapCell(cell)&&cell.x>=1&&cell.x<=13&&cell.y>=1&&cell.y<=13&&!safe.Contains(cell))area.Add(cell);
        Strike(area,UltimateDamage);
        if(Player.CurrentHealth>0&&!area.Contains(Player.GridPosition))LastOutcome=label+" 회피 성공";
    }
    private void Strike(HashSet<Vector3Int> area, int damage)
    {
        bool hit=area.Contains(Player.GridPosition);
        if(hit)Player.TakeDamage(damage,PlayerDamageKind.Hazard,Player.GridPosition-QueenCenter);
        foreach(MonsterMovement guard in guards.ToArray())if(guard!=null&&!guard.IsDead&&area.Contains(guard.GridPosition))guard.TakeDamage(damage);
        LastOutcome=hit?$"{PatternName} 피격 −{damage}":"공격 회피";
    }
    private void Finish(bool won)
    {
        IsFinished=true; Won=won; LastOutcome=won?"여왕 처치 · 클리어":"사망 · 같은 조건으로 재시작 가능";
        Player.SetInputEnabled(false); Changed?.Invoke();
    }
}
