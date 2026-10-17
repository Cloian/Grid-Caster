using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class QueenBossPlaytest : MonoBehaviour
{
    [Header("독립 보스 실험 · 정식 런과 무관")]
    [SerializeField] private Sprite grayboxSprite;
    [Tooltip("여왕·근위병 그림과 모션. 비우면 단색 그레이박스로 시험한다.")]
    [SerializeField] private QueenBossBehaviour.Visuals visuals;
    [SerializeField, Min(1)] private int bossHealth = 60;
    [SerializeField] private int layoutSeed = 173821;
    private static readonly Vector3Int PlayerStart = new Vector3Int(7,3,0);
    private MonsterSpawner spawner;
    private GridManager grid;
    private ProjectileManager projectiles;
    private bool initialized;
    public Move Player { get; private set; }
    public QueenBossBehaviour Encounter { get; private set; }
    public bool HasStarted { get; private set; }
    public bool Paused { get; private set; }
    public string TraitId { get; private set; } = "double_cast";
    public PlayerMovementArt MovementArt { get; private set; } = PlayerMovementArt.Rook;
    public int MovementLevel { get; private set; } = 3;
    public bool StrongBuild { get; private set; }
    public bool StartPhaseTwo { get; private set; }
    public event Action Changed;

    public void Initialize(MonsterSpawner spawner, Move player, GridManager grid, ProjectileManager projectiles)
    {
        this.spawner=spawner;Player=player;this.grid=grid;this.projectiles=projectiles;
        Encounter=GetComponent<QueenBossBehaviour>();
        player.GetComponent<SpriteRenderer>().sortingOrder=10;
        player.SetInputEnabled(false);initialized=true;Changed?.Invoke();
    }

    public bool StartEncounter(string traitId, PlayerMovementArt movementArt, int level, bool strong, bool phaseTwo)
    {
        if(!initialized||grayboxSprite==null||(!Paused&&HasStarted&&Player.IsActionInProgress&&!Encounter.IsFinished))return false;
        Time.timeScale=1f;Paused=false;spawner.ResetPlaytestEncounter();grid.SetTemporaryObstacles(null);
        TraitId=traitId;MovementArt=movementArt;MovementLevel=Mathf.Clamp(level,1,3);StrongBuild=strong;StartPhaseTwo=phaseTwo;
        Player.ConfigurePlaytestCombat(strong?24:18,strong?3:2,PlayerStart);
        PlayerTraitSystem trait=Player.GetComponent<PlayerTraitSystem>();
        trait.SelectTrait(traitId,traitId=="double_cast"?5:traitId=="pierce"?3:4);
        RunProgressionSystem growth=Player.GetComponent<RunProgressionSystem>();
        growth.SetMovementArtForPlaytest(movementArt,MovementLevel);
        string[] specific=traitId switch {
            "double_cast"=>new[]{"quick_cast","echo_warhead"},
            "pierce"=>new[]{"thin_reload","long_needle","pierce_recovery"},
            "damage_boost"=>new[]{"dense_mana","overcharge","afterglow_recovery"},
            _=>new[]{"compressed_impact","long_impact","impact_recovery"} };
        var upgrades=new List<string>(specific);
        if(strong){upgrades.AddRange(specific);upgrades.Add("trait_mastery");}
        growth.SetUpgradesForPlaytest(upgrades);
        for(int i=0;i<trait.ChargeRequired;i++)trait.RegisterCompletedAttack(true,false);
        Encounter.Initialize(spawner,grid,Player,grayboxSprite,bossHealth,layoutSeed,phaseTwo,visuals);
        HasStarted=true;Player.SetInputEnabled(true);Changed?.Invoke();return true;
    }
    public bool RestartEncounter()=>StartEncounter(TraitId,MovementArt,MovementLevel,StrongBuild,StartPhaseTwo);

    public void TakeWorldTurn(Action completed)
    {
        if(!HasStarted){completed();return;}
        Encounter.Tick();
        // 남아 있는 특수 탄도 기존 투사체 턴을 사용한다. 기본 공격은 이 호출 전에 이미 종료했다.
        if(Encounter.IsFinished){completed();Changed?.Invoke();return;}
        projectiles.TakeTurn(()=>{completed();Changed?.Invoke();});
    }
    public bool TryGetQueenAtCell(Vector3Int cell,out MonsterMovement queen)
    {
        queen=Encounter!=null&&Encounter.ContainsQueenCell(cell)?Encounter.Queen:null;return queen!=null;
    }
    public bool IsQueen(MonsterMovement actor)=>Encounter!=null&&actor!=null&&actor==Encounter.Queen;

    public void TogglePause()
    {
        if(!HasStarted)return;
        Paused=!Paused;Time.timeScale=Paused?0f:1f;
        Player.SetInputEnabled(!Paused&&!Encounter.IsFinished);Changed?.Invoke();
    }
    private void Update()
    {
        if(!initialized||Keyboard.current==null)return;
        if(Keyboard.current.escapeKey.wasPressedThisFrame)TogglePause();
        if(!HasStarted||Paused||Encounter.IsFinished)return;
        if(Keyboard.current.spaceKey.wasPressedThisFrame)Player.TryWaitForPlaytest();
    }
    private void OnDestroy(){if(Paused)Time.timeScale=1f;}
}
