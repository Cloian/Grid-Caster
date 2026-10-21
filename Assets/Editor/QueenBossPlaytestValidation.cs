using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[InitializeOnLoad]
public static class QueenBossPlaytestValidation
{
    private const string Pending="GridCaster.QueenValidation";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly List<string> checks=new List<string>();
    private static IEnumerator routine;
    private static int frame;
    private static double deadline;
    private static string runtimeError;
    private static bool previousBackground;
    static QueenBossPlaytestValidation()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
            SessionState.SetBool(Pending,false);checks.Clear();runtimeError=null;frame=-1;
            previousBackground=Application.runInBackground;Application.runInBackground=true;
            deadline=EditorApplication.timeSinceStartup+300;Application.logMessageReceived+=OnError;
            routine=Run();EditorApplication.update+=Tick;
        };
    }
    [MenuItem("Tools/Playtest/Validate Queen Boss Playtest")]
    public static void Begin()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Play를 종료하세요.");
        QueenBossPlaytestSetupTool.Open();SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
    }
    private static void OnError(string message,string stack,LogType type)
    { if(type==LogType.Exception||type==LogType.Error||type==LogType.Assert)runtimeError=message+"\n"+stack; }
    private static void Tick()
    {
        try
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("보스 검사 시간 초과 / 중단");
            if(runtimeError!=null)throw new InvalidOperationException(runtimeError);
            if(frame==Time.frameCount)return;frame=Time.frameCount;
            if(routine.MoveNext())return;Finish(true);
        }
        catch(Exception e){checks.Add(e.ToString());Finish(false);}
    }
    private static void Finish(bool passed)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=OnError;Application.runInBackground=previousBackground;
        string report="QUEEN_PLAYTEST_"+(passed?"PASS":"FAIL")+"\n"+DateTime.Now.ToString("O")+" Unity "+Application.unityVersion
            +"\nActual player actions + UI raycasts; controlled pattern/death fixtures. Not a human balance assessment.\n"+string.Join("\n",checks);
        Directory.CreateDirectory("docs/verification");File.WriteAllText("docs/verification/queen-boss-playtest-2026-10-12.txt",report);
        if(passed)Debug.Log(report);else Debug.LogError(report);EditorApplication.isPlaying=false;
    }
    private static void Check(bool value,string message)
    { if(!value)throw new InvalidOperationException(message);checks.Add("PASS "+message); }
    private static void ForceNext(QueenBossBehaviour boss,int index,bool ultimate=false)
    {
        typeof(QueenBossBehaviour).GetField("normalIndex",Private).SetValue(boss,index);
        typeof(QueenBossBehaviour).GetField("phaseTwoNormals",Private).SetValue(boss,ultimate?3:0);
        typeof(QueenBossBehaviour).GetMethod("PrepareNext",Private).Invoke(boss,null);
    }
    private static IEnumerator Settle(Move player)
    {
        yield return null;
        double until=EditorApplication.timeSinceStartup+10;
        while(player.IsActionInProgress)
        { if(EditorApplication.timeSinceStartup>until)throw new TimeoutException("플레이어 행동 완료 안 됨");yield return null; }
        yield return null;
    }
    private static IEnumerator Action(QueenBossPlaytest test,PlayerActionSelectionMode mode,Vector3Int direction)
    {
        int before=test.Encounter.Turn;Check(test.Player.TryPerformAction(mode,direction),"actual action accepted "+mode+" "+direction);
        IEnumerator settle=Settle(test.Player);while(settle.MoveNext())yield return settle.Current;
        Check(test.Encounter.Turn==before+1,"one completed action advances boss exactly once");
    }
    private static IEnumerator Wait(QueenBossPlaytest test)
    {
        int before=test.Encounter.Turn;Check(test.Player.TryWaitForPlaytest(),"actual wait accepted");
        IEnumerator settle=Settle(test.Player);while(settle.MoveNext())yield return settle.Current;
        Check(test.Encounter.Turn==before+1,"wait consumes one action");
    }
    private static void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();RectTransform rect=button.GetComponent<RectTransform>();
        var pointer=new PointerEventData(EventSystem.current){position=rect.TransformPoint(rect.rect.center),button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        Check(hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button,"HUD GraphicRaycaster reaches "+button.name);
        button.OnPointerClick(pointer);
    }
    private static IEnumerator Run()
    {
        yield return null;yield return null;yield return null;
        QueenBossPlaytest test=UnityEngine.Object.FindAnyObjectByType<QueenBossPlaytest>();
        Move player=test.Player;GridManager grid=test.GetComponent<GridManager>();MonsterSpawner spawner=test.GetComponent<MonsterSpawner>();
        QueenBossPlaytestUi ui=UnityEngine.Object.FindAnyObjectByType<QueenBossPlaytestUi>();
        Check(!test.HasStarted&&!player.CanAct&&spawner.ActiveMonsters.Count==0&&spawner.StageFlow==null,"separate scene starts in setup, no wave / reward loop");
        Check(grid.GroundTilemap.cellBounds.size==new Vector3Int(15,15,1)&&grid.IsWalkableCell(new Vector3Int(13,13,0))&&!grid.IsWalkableCell(new Vector3Int(14,13,0)),"13×13 interior with boundary");
        Check(UnityEngine.Object.FindObjectsByType<EventSystem>().Length==1&&ui.GetComponent<GraphicRaycaster>()!=null,"one input system and HUD raycaster");
        Click(ui.GetComponentsInChildren<Button>(true).First(b=>b.name=="StartEncounter"));
        Check(test.HasStarted&&player.CanAct,"start button begins actual boss combat");
        for(int y=10;y<=12;y++)for(int x=6;x<=8;x++)Check(spawner.TryGetMonsterAtCell(new Vector3Int(x,y,0),out MonsterMovement q)&&q==test.Encounter.Queen,"boss occupies body tile "+x+","+y);
        int frozen=test.Encounter.Turn;Vector3Int queenCell=test.Encounter.Queen.GridPosition;
        for(int i=0;i<12;i++)yield return null;
        player.SelectMoveMode();player.CancelSelection();
        Check(test.Encounter.Turn==frozen&&test.Encounter.Queen.GridPosition==queenCell,"no-input frames and selection/cancel do not advance rules");

        foreach(string traitId in new[]{"double_cast","pierce","damage_boost","knockback"})
        foreach(PlayerMovementArt art in new[]{PlayerMovementArt.Knight,PlayerMovementArt.Bishop,PlayerMovementArt.Rook})
        foreach(int level in new[]{1,2,3})
        {
            Check(test.StartEncounter(traitId,art,level,false,false),"restart configuration "+traitId+" / "+art+" / "+level);
            yield return null;
            Check(player.GetComponent<PlayerTraitSystem>().IsReady&&player.GetComponent<RunProgressionSystem>().MovementArtLevel==level,"preset charge and movement stage reset");
            player.SelectTraitAttackMode();int hp=test.Encounter.Queen.GetComponent<CharacterHealth>().CurrentHealth;
            IEnumerator action=Action(test,PlayerActionSelectionMode.Attack,Vector3Int.up);while(action.MoveNext())yield return action.Current;
            Check(test.Encounter.Queen.GetComponent<CharacterHealth>().CurrentHealth<hp,"real trait attack damages queen "+traitId);
        }
        Check(test.StartEncounter("pierce",PlayerMovementArt.Rook,3,true,false),"strong preset starts");yield return null;
        Check(player.CurrentHealth==24&&player.GetComponent<RunProgressionSystem>().Stack("trait_mastery")==1,"strong preset uses fixed health and capped upgrades");
        MonsterMovement behind=spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,new Vector3Int(7,13,0),test.Encounter.Queen.GetComponent<SpriteRenderer>().sprite,Color.white,10,99,2,4,2);
        player.SelectTraitAttackMode();IEnumerator pierce=Action(test,PlayerActionSelectionMode.Attack,Vector3Int.up);while(pierce.MoveNext())yield return pierce.Current;
        Check(test.Encounter.Queen.GetComponent<CharacterHealth>().CurrentHealth==57&&behind.GetComponent<CharacterHealth>().CurrentHealth<10,"pierce hits 3×3 queen once and continues to distinct target");

        Check(test.StartEncounter("damage_boost",PlayerMovementArt.Rook,3,false,false),"area edge fixture starts");yield return null;
        MonsterMovement near=spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,new Vector3Int(6,9,0),test.Encounter.Queen.GetComponent<SpriteRenderer>().sprite,Color.white,10,99,2,4,2);
        player.SelectTraitAttackMode();IEnumerator area=Action(test,PlayerActionSelectionMode.Attack,Vector3Int.up);while(area.MoveNext())yield return area.Current;
        Check(test.Encounter.Queen.GetComponent<CharacterHealth>().CurrentHealth==56&&near.GetComponent<CharacterHealth>().CurrentHealth==9,"area originates at actual body impact; primary queen not splashed again");
        Check(test.StartEncounter("damage_boost",PlayerMovementArt.Rook,3,false,false),"secondary footprint fixture starts");yield return null;
        player.ResetPlaytest(new Vector3Int(5,3,0));
        spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,new Vector3Int(5,9,0),test.Encounter.Queen.GetComponent<SpriteRenderer>().sprite,Color.white,10,99,2,4,2);
        player.SelectTraitAttackMode();IEnumerator secondary=Action(test,PlayerActionSelectionMode.Attack,Vector3Int.up);while(secondary.MoveNext())yield return secondary.Current;
        Check(test.Encounter.Queen.GetComponent<CharacterHealth>().CurrentHealth==59,"secondary area tests entire boss footprint, once per actor");

        foreach(PlayerMovementArt art in new[]{PlayerMovementArt.None,PlayerMovementArt.Knight,PlayerMovementArt.Bishop,PlayerMovementArt.Rook})
        foreach(int level in new[]{1,3})
        {
            Check(test.StartEncounter("pierce",art,level,false,true),"phase-two mobility fixture "+art+level);yield return null;
            ForceNext(test.Encounter,2); // summon guards first, then freeze that occupancy during checkmate
            ForceNext(test.Encounter,0,true);
            Check(test.Encounter.CurrentPattern==QueenBossBehaviour.Pattern.Checkmate,"reachable ultimate is selected for "+art+level);
            QueenSafeZonePlanner.Layout layout=test.Encounter.SafeZones;
            Check(layout.A.Count==8&&layout.B.Count==8&&!layout.A.Overlaps(layout.B),"A / B each have eight disjoint cells");
            var guardPositions=test.Encounter.Guards.Where(g=>g!=null).ToDictionary(g=>g,g=>g.GridPosition);
            Vector3Int[] plan=Plan(player,grid,layout);
            Check(plan!=null,"actual movement rules admit two actions to A then one to B");
            int hp=player.CurrentHealth;
            foreach(Vector3Int destination in plan)
            {
                IEnumerator step=destination==player.GridPosition?Wait(test):Action(test,PlayerActionSelectionMode.Move,destination-player.GridPosition);
                while(step.MoveNext())yield return step.Current;
            }
            Check(player.CurrentHealth==hp&&test.Encounter.IsRecovery,"actual A→B movement avoids both strikes and opens two recovery actions");
            Check(guardPositions.All(pair=>pair.Key==null||pair.Key.GridPosition==pair.Value),"guards stay fixed during ultimate");
            HashSet<Vector3Int> oldB=new HashSet<Vector3Int>(layout.B);
            ForceNext(test.Encounter,0,true);
            Check(test.Encounter.CurrentPattern==QueenBossBehaviour.Pattern.Checkmate&&!test.Encounter.SafeZones.A.Overlaps(oldB),"next A excludes previous B, no camping");
        }
        Check(test.StartEncounter("pierce",PlayerMovementArt.Bishop,3,false,true),"fortress fixture starts");yield return null;
        ForceNext(test.Encounter,3);
        Check(test.Encounter.Walls.Count==2&&test.Encounter.Walls.All(c=>!grid.IsWalkableCell(c)),"temporary walls share actual walkability");
        Check(!player.CanReachCellFrom(player.GridPosition,new Vector3Int(9,5,0)),"bishop cannot pass through the temporary wall");
        int noTurn=test.Encounter.Turn;
        Check(!player.TryPerformAction(PlayerActionSelectionMode.Move,new Vector3Int(2,2,0))&&test.Encounter.Turn==noTurn,"blocked selection cannot consume an action");
        player.CancelSelection();
        IEnumerator wallWait=Wait(test);while(wallWait.MoveNext())yield return wallWait.Current;
        wallWait=Wait(test);while(wallWait.MoveNext())yield return wallWait.Current;
        wallWait=Wait(test);while(wallWait.MoveNext())yield return wallWait.Current;
        Check(test.Encounter.Walls.Count==0&&grid.IsWalkableCell(new Vector3Int(8,4,0)),"wall removed after recovery");

        Check(test.StartEncounter("pierce",PlayerMovementArt.Rook,3,false,true),"large-damage fixture starts");yield return null;
        ForceNext(test.Encounter,0,true);
        player.ResetPlaytest(Floor(grid).First(c=>!spawner.IsOccupied(c)&&!test.Encounter.SafeZones.A.Contains(c)));
        // Direct rule fixture: stay outside A with full health; no HP-threshold execution.
        test.Encounter.Tick();test.Encounter.Tick();
        Check(player.CurrentHealth==10&&!test.Encounter.IsFinished,"checkmate deals fixed 8, full-health failure is not instant death");
        // 근위병 승격: 소환 때 승격할 말이 정해지고, 맨 아래 줄에 닿으면 그 말(체력 절반)로 바뀐다. 동시 2마리 상한.
        Check(test.StartEncounter("pierce",PlayerMovementArt.Rook,3,false,false),"promotion fixture starts");yield return null;
        player.ResetPlaytest(new Vector3Int(7,1,0));
        ForceNext(test.Encounter,2);
        Check(test.Encounter.Guards.Count==3&&test.Encounter.Guards.All(g=>test.Encounter.PromotionMarkers.TryGetValue(g,out PromotionMarker m)&&m!=null
            &&QueenBossBehaviour.PromotionPool.Contains(m.Promotion)),"every summoned guard shows its promotion piece (knight/bishop/bomber, no rook/queen)");
        var expected=test.Encounter.Guards.ToDictionary(g=>g.GridPosition.x,g=>test.Encounter.PromotionMarkers[g].Promotion);
        player.ResetPlaytest(new Vector3Int(12,8,0));
        MethodInfo advance=typeof(QueenBossBehaviour).GetMethod("AdvanceGuards",Private);
        for(int i=0;i<4;i++){advance.Invoke(test.Encounter,null);yield return null;}
        Check(test.Encounter.Guards.Count==0&&test.Encounter.Promoted.Count==QueenBossBehaviour.MaxPromoted,"guards reaching the last row promote, third one crumbles at the cap");
        Check(test.Encounter.Promoted.All(m=>expected.TryGetValue(m.GridPosition.x,out MonsterMovementPattern p)&&p==m.MovementPattern
            &&m.GetComponent<CharacterHealth>().CurrentHealth==QueenBossBehaviour.PromotedHealth(m.MovementPattern)&&m.GridPosition.y==1),
            "promoted piece matches the telegraphed icon, at half health, on the last row");
        Check(test.Encounter.Promoted.All(m=>m.GetComponent<SpriteRenderer>().sprite!=test.Encounter.Queen.GetComponent<SpriteRenderer>().sprite),"promoted pieces use their own art");
        IEnumerator allyTurn=Wait(test);while(allyTurn.MoveNext())yield return allyTurn.Current;
        allyTurn=Wait(test);while(allyTurn.MoveNext())yield return allyTurn.Current;
        Check(test.Encounter.Promoted.All(m=>m!=null),"promoted pieces act inside the boss turn after real player actions");
        Check(test.StartEncounter("pierce",PlayerMovementArt.Rook,3,false,false),"promotion denial fixture starts");yield return null;
        ForceNext(test.Encounter,2);
        MonsterMovement denied=test.Encounter.Guards[0];int markersBefore=test.Encounter.PromotionMarkers.Count;
        denied.TakeDamage(99);yield return null;
        Check(test.Encounter.PromotionMarkers.Count==markersBefore-1&&!test.Encounter.PromotionMarkers.ContainsKey(denied),"killing a guard before the last row cancels its promotion");
        // 여왕 이동: 첫 예고는 시작 자리, 이후 패턴마다 위쪽 영역 안에서 퀸처럼 한 방향으로 미끄러진다.
        Check(test.StartEncounter("pierce",PlayerMovementArt.Rook,3,false,true),"queen movement fixture starts");yield return null;
        Check(test.Encounter.Center==QueenBossBehaviour.QueenCenter,"first telegraph starts at the home square");
        player.ResetPlaytest(new Vector3Int(3,3,0));
        for(int i=0;i<3;i++){IEnumerator step=Wait(test);while(step.MoveNext())yield return step.Current;}
        Vector3Int moved=test.Encounter.Center,delta=moved-QueenBossBehaviour.QueenCenter;
        Check(moved.y>=8&&(delta.x==0||delta.y==0||Mathf.Abs(delta.x)==Mathf.Abs(delta.y))&&!test.Encounter.ContainsQueenCell(player.GridPosition),
            "next pattern glides like a queen inside the top area without overlapping the player");
        Check(spawner.TryGetMonsterAtCell(moved,out MonsterMovement movedQueen)&&movedQueen==test.Encounter.Queen,"boss footprint follows the moved queen");
        // 돌진(2페이즈): 줄을 예고하고, 줄 위의 플레이어 앞에서 멈추며 피해 5.
        ForceNext(test.Encounter,4);
        Check(test.Encounter.CurrentPattern==QueenBossBehaviour.Pattern.Charge&&test.Encounter.Danger.Count>0,"charge telegraphs its swept line");
        int beforeCharge=player.CurrentHealth;bool onLine=test.Encounter.Danger.Contains(player.GridPosition);
        IEnumerator chargeWait=Wait(test);while(chargeWait.MoveNext())yield return chargeWait.Current;
        chargeWait=Wait(test);while(chargeWait.MoveNext())yield return chargeWait.Current;
        Check(!test.Encounter.ContainsQueenCell(player.GridPosition)&&player.CurrentHealth==(onLine?beforeCharge-5:beforeCharge),
            "charge stops in front of the player and deals 5 only on the telegraphed line");
        Check(test.StartEncounter("pierce",PlayerMovementArt.Rook,3,false,false),"victory fixture starts");yield return null;
        test.Encounter.Queen.TakeDamage(999);IEnumerator win=Wait(test);while(win.MoveNext())yield return win.Current;
        Check(test.Encounter.IsFinished&&test.Encounter.Won&&!player.CanAct&&player.CurrentHealth==18,"kill before enemy phase ends fight without retaliation");
        test.TogglePause();test.TogglePause();Check(!player.CanAct,"closing pause cannot reopen a finished fight");
        Check(test.RestartEncounter(),"restart after victory");yield return null;
        player.GetComponent<CharacterHealth>().TakeDamage(17);
        IEnumerator death=Wait(test);while(death.MoveNext())yield return death.Current;
        death=Wait(test);while(death.MoveNext())yield return death.Current;
        Check(test.Encounter.IsFinished&&!test.Encounter.Won&&!player.CanAct,"death closes combat input");
        Check(test.RestartEncounter()&&player.CurrentHealth==18&&test.Encounter.Turn==0,"restart clears death, guards, charge and pattern state");yield return null;
        test.TogglePause();Check(Time.timeScale==0&&!player.CanAct,"ESC pause freezes presentation and combat input");
        yield return null; // Pause panel activation is presentation in LateUpdate, even while timeScale is zero.
        Click(ui.GetComponentsInChildren<Button>(true).First(b=>b.name=="Resume"));yield return null;
        Check(Time.timeScale==1&&player.CanAct,"pause resume restores unfinished combat only");
        Check(ui.GetComponentsInChildren<TMP_Text>(true).All(t=>t.font!=null&&!t.enableAutoSizing),"fixed-size Galmuri TMP font assigned throughout HUD");
        checks.Add("36 trait/movement/level attack configurations; 8 actual A→B paths; controlled damage/wall/promotion/end fixtures.");
    }
    private static Vector3Int[] Plan(Move player,GridManager grid,QueenSafeZonePlanner.Layout layout)
    {
        Vector3Int origin=player.GridPosition;
        var floor=Floor(grid).ToArray();
        foreach(Vector3Int first in floor.Where(c=>c==origin||player.CanReachCellFrom(origin,c)))
        foreach(Vector3Int a in layout.A.Where(c=>c==first||player.CanReachCellFrom(first,c)))
        foreach(Vector3Int b in layout.B.Where(c=>player.CanReachCellFrom(a,c)))return new[]{first,a,b};
        return null;
    }
    private static IEnumerable<Vector3Int> Floor(GridManager grid)
    {
        foreach(Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
            if(grid.IsWalkableCell(cell))yield return cell;
    }
}
