using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 실제 SampleScene 입력으로 충돌 피해·방사형 밀기·봉쇄·충전·처치 연출을 검증한다.
[InitializeOnLoad]
public static class KnockbackCollisionValidation
{
    private const string Pending = "GridCaster.KnockbackCollisionValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static KnockbackCollisionValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); frame = -1; runtimeError = null;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            // 공격 준비 모션·투사체·충돌 연출이 늘어 150초로는 전체 픽스처가 끝나지 않는다.
            deadline = EditorApplication.timeSinceStartup + 300;
            Application.logMessageReceived += Error;
            routine = Flatten(Run()); EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Knockback Collision")]
    public static void RunBatch()
    {
        if (Application.isBatchMode)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("검사 전에 Play를 종료하고 씬을 저장하세요.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("원본 SampleScene을 열어 검사하세요.");
        CombatAudioSetupTool.Configure();
        foreach (string key in new[] { GameAudio.SfxVolumeKey, GameAudio.MusicVolumeKey })
        {
            SessionState.SetBool(Pending + key, PlayerPrefs.HasKey(key));
            SessionState.SetFloat(Pending + key, PlayerPrefs.GetFloat(key));
        }
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void Error(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(KnockbackCollisionValidation))))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("넉백 충돌 검사 시간 초과/중단");
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(true);
        }
        catch (Exception error) { checks.Add(error.ToString()); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= Error;
        Application.runInBackground = previousRunInBackground;
        Time.timeScale = 1f; Time.captureDeltaTime = 0f;
        foreach (string key in new[] { GameAudio.SfxVolumeKey, GameAudio.MusicVolumeKey })
        {
            if (SessionState.GetBool(Pending + key, false)) PlayerPrefs.SetFloat(key, SessionState.GetFloat(Pending + key, 0f));
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        // OnDisable 시 검사 중 볼륨을 다시 저장하지 않도록 원래 값을 복원한다.
        GameAudio audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
        if (audio != null)
        {
            audio.SetSfxVolume(PlayerPrefs.GetFloat(GameAudio.SfxVolumeKey, GameAudio.DefaultSfxVolume));
            audio.SetMusicVolume(PlayerPrefs.GetFloat(GameAudio.MusicVolumeKey, GameAudio.DefaultMusicVolume));
        }
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        string report = "KNOCKBACK_COLLISION_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; gameplay-rule validation, not human fun assessment.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "knockback-collision-2026-10-05.txt"), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }


    private static Move player;
    private static MonsterSpawner spawner;
    private static GridManager grid;
    private static PlayerTraitSystem trait;
    private static RunProgressionSystem growth;
    private static int starts, completions;
    private static readonly Vector3Int[] Directions =
    {
        Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down,
        new Vector3Int(1,1,0), new Vector3Int(-1,1,0),
        new Vector3Int(-1,-1,0), new Vector3Int(1,-1,0)
    };
    private static Dictionary<string, int> Stacks =>
        (Dictionary<string, int>)typeof(RunProgressionSystem).GetField("upgradeStacks", Private).GetValue(growth);
    private static List<RelicDefinition> Relics =>
        (List<RelicDefinition>)typeof(RunProgressionSystem).GetField("relics", Private).GetValue(growth);
    private static IEnumerator Flatten(IEnumerator source)
    {
        while (source.MoveNext())
        {
            if (source.Current is IEnumerator nested)
            {
                IEnumerator child = Flatten(nested);
                while (child.MoveNext()) yield return child.Current;
            }
            else yield return source.Current;
        }
    }
    private static void Reset(Vector3Int cell, string id = "damage_boost")
    {
        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(cell);
        // 넉백 규칙만 본다. 주도권·여왕의 시계 프로토타입 영향은 뺀다.
        player.GetComponent<InitiativeTracker>()?.SetEnabledForValidation(false);
        spawner.SetQueenClockForValidation(false);
        Stacks.Clear(); Relics.Clear();
        growth.SetMovementArtForPlaytest(PlayerMovementArt.None);
        typeof(PlayerTraitSystem).GetField("selectedTraitId", Private).SetValue(trait, id);
        trait.SetActivationBagSize(4); trait.ResetCharge();
        // 이전 공격이 저장한 유물 효과를 새 픽스처에 가져오지 않는다.
        typeof(RunProgressionSystem).GetField("storedAttackSources", Private).SetValue(growth, 0);
    }
    private static void Ready() { while (!trait.IsReady) trait.RegisterCompletedAttack(true, false); }
    private static MonsterMovement Spawn(Vector3Int cell, int hp = 20, MonsterMovementPattern kind = MonsterMovementPattern.CardinalFour)
    {
        var monster = spawner.SpawnPlaytestMonster(kind, cell, player.GetComponent<SpriteRenderer>().sprite,
            Color.white, hp, 0, 2, 4, 2);
        monster.SkipTurns(99);
        return monster;
    }
    private static int Hp(MonsterMovement monster) => monster.GetComponent<CharacterHealth>().CurrentHealth;
    private static IEnumerator Fire(Vector3Int direction, bool special = true)
    {
        int a = starts, b = completions;
        int chargeBefore = trait.ChargeProgress;
        if (special) { Ready(); Check(player.SelectTraitAttackMode(), "prepared special selectable"); }
        else player.SelectAttackMode();
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, direction), "actual attack accepted");
        Check(player.GetComponent<DirectionalActionIndicator>().ActiveAreaCount == 0,
            "confirmed attack clears knockback preview");
        while (player.IsActionInProgress)
        {
            if (player.IsChoosingEchoDirection)
                Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, direction), "reaim consumes no separate action");
            yield return null;
        }
        Check(starts == a + 1 && completions == b + 1, "one attack gives exactly one enemy and projectile turn");
        Check(trait.ChargeProgress == (special ? 0 : Mathf.Min(trait.ChargeRequired, chargeBefore + 1)),
            "collision never adds charge or recharges a special attack");
    }
    private static void Capture(string name)
    {
        Camera camera = Camera.main;
        RenderTexture old = camera.targetTexture, active = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24);
        var picture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            picture.ReadPixels(new Rect(0,0,1920,1080),0,0); picture.Apply();
            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification/captures");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir,name + ".png"), picture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = old; RenderTexture.active = active;
            UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(picture);
        }
    }
    private static IEnumerator Gap(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }
    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        trait = player.GetComponent<PlayerTraitSystem>(); growth = player.GetComponent<RunProgressionSystem>();
        UnityEngine.Object.FindAnyObjectByType<GameHudController>().SelectTrait(0); yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke(); yield return null;
        Time.captureDeltaTime = 1f / 240f;
        player.MoveCompleted += () => starts++;
        spawner.WorldTurnCompleted += () => completions++;
        var bounds=grid.GroundTilemap.cellBounds;
        int east=bounds.xMax-2;
        foreach (Vector3Int dir in Directions)
        {
            Vector3Int edge = new Vector3Int(dir.x>0?east:dir.x<0?bounds.xMin+1:0,
                dir.y>0?bounds.yMax-2:dir.y<0?bounds.yMin+1:0,0);
            yield return null; Reset(edge-dir*2,"knockback");
            var pushed = Spawn(edge);
            yield return Fire(dir);
            Check(pushed.GridPosition == edge && Hp(pushed)==18,"wall collision damages direct survivor in direction "+dir);
        }
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        var direct = Spawn(Vector3Int.zero); var behind = Spawn(Vector3Int.right);
        var unrelated = Spawn(new Vector3Int(0,2,0));
        yield return Fire(Vector3Int.right);
        Check(Hp(direct)==18 && Hp(behind)==19 && Hp(unrelated)==20,"enemy collision damages both participants only");
        Check(direct.GridPosition==Vector3Int.zero && behind.GridPosition==Vector3Int.right,"blocked enemies never overlap or retry movement");
        // Without the spread upgrade, neighbors do not move on an ordinary free push.
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.up);
        yield return Fire(Vector3Int.right);
        Check(direct.GridPosition==Vector3Int.right && behind.GridPosition==Vector3Int.up && Hp(behind)==20,"base knockback does not spread");
        // Primary resolves first. The radial receiver can move afterward without reopening primary movement.
        yield return null; Reset(Vector3Int.left,"knockback");
        Stacks["long_impact"]=1; direct=Spawn(Vector3Int.zero);
        var ringDirections=Directions.Where(dir=>dir!=Vector3Int.left).ToArray();
        var ring = ringDirections.Select(dir=>Spawn(dir)).ToArray();
        Ready(); player.SelectTraitAttackMode();
        Check(player.TryPreviewBoardPoint(Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(player.GridPosition+Vector3Int.right)),out _)
            && player.GetComponent<DirectionalActionIndicator>().ActiveAreaCount==7,"spread preview shows hit-centered cells and excludes player");
        yield return Fire(Vector3Int.right);
        Check(Hp(direct)==18 && direct.GridPosition==Vector3Int.zero,"spread retains original hit center and does not repush direct target");
        for(int i=0;i<ring.Length;i++)
            Check(ring[i].GridPosition==ringDirections[i]*2 && Hp(ring[i])==(ringDirections[i]==Vector3Int.right?19:20),"outward neighbor moves once without free splash damage "+ringDirections[i]);
        // If one receiver collides again during radial resolution, it cannot take collision damage twice.
        yield return null; Reset(new Vector3Int(east-3,0,0),"knockback");
        Stacks["long_impact"]=1; direct=Spawn(new Vector3Int(east-1,0,0)); behind=Spawn(new Vector3Int(east,0,0));
        yield return Fire(Vector3Int.right);
        Check(Hp(direct)==18 && Hp(behind)==19,"primary receiver and secondary wall collision share per-action damage cap");
        yield return Fire(Vector3Int.right);
        Check(Hp(direct)==16 && Hp(behind)==18,"collision cap resets on next accepted attack");
        // A multi-cell push can travel one legal cell and collide with a creature at the next cell.
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        Stacks["rapid_cycle"]=1; direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.right*2);
        yield return Fire(Vector3Int.right);
        Check(direct.GridPosition==Vector3Int.right && behind.GridPosition==Vector3Int.right*2
            && Hp(direct)==18 && Hp(behind)==19,"partial enemy collision applies pair damage at last legal position");
        // Mounted Rook immunity is different from a collision with a mounted Rook.
        foreach(bool nail in new[]{false,true})
        {
            yield return null; Reset(new Vector3Int(0,bounds.yMax-3,0),"knockback");
            if(nail) Relics.Add(RunProgressionCatalog.Relic("iron_nail"));
            var rook=Spawn(new Vector3Int(0,bounds.yMax-1,0),20,MonsterMovementPattern.Rook);
            yield return Fire(Vector3Int.up);
            Check(Hp(rook)==(nail?18:19) && rook.GridPosition==new Vector3Int(0,bounds.yMax-1,0),
                "immovable Rook does not take fictitious basic collision damage; nail="+nail);
        }
        yield return null; Reset(new Vector3Int(0,bounds.yMax-4,0),"knockback");
        direct=Spawn(new Vector3Int(0,bounds.yMax-2,0));
        behind=Spawn(new Vector3Int(0,bounds.yMax-1,0),20,MonsterMovementPattern.Rook);
        yield return Fire(Vector3Int.up);
        Check(Hp(direct)==18 && Hp(behind)==19,"mounted Rook receives actual creature collision damage before wall classification");
        // Actual multi-cell push takes a legal step before the wall. Rule damage precedes presentation.
        yield return null; Reset(new Vector3Int(east-3,0,0),"knockback");
        Stacks["rapid_cycle"]=1; direct=Spawn(new Vector3Int(east-1,0,0));
        Ready(); player.SelectTraitAttackMode(); int worldBefore=completions;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack,Vector3Int.right),"partial push actual attack accepted");
        while(Hp(direct)==20) yield return null;
        Check(direct.GridPosition==new Vector3Int(east,0,0) && Hp(direct)==18,"partial push lands at last legal cell and applies collision damage immediately");
        Check(CombatFeedback.HasPendingDisplacement && completions==worldBefore,"enemy turn waits for displacement presentation");
        Capture("knockback-partial-wall-start-2026-10-05");
        while(player.IsActionInProgress) yield return null;
        Check(completions==worldBefore+1,"partial collision creates one world turn");
        // A lethal collision releases occupancy immediately; independent corpse visual still travels to the wall.
        yield return null; Reset(new Vector3Int(east-3,0,0),"knockback");
        Stacks["rapid_cycle"]=1; direct=Spawn(new Vector3Int(east-1,0,0),2); bool killed=false;
        direct.GetComponent<CharacterHealth>().Died+=_=>killed=true;
        Ready(); player.SelectTraitAttackMode(); worldBefore=completions;
        player.TryPerformAction(PlayerActionSelectionMode.Attack,Vector3Int.right);
        while(!killed) yield return null;
        Check(!spawner.TryGetMonsterAtCell(new Vector3Int(east,0,0),out _),"lethal collision immediately frees landing cell");
        Check(CombatFeedback.HasPendingDisplacement && completions==worldBefore,"lethal collision corpse keeps displacement recovery pending");
        yield return Gap(0.035f); Capture("knockback-lethal-wall-travel-2026-10-05");
        var echo=UnityEngine.Object.FindObjectsByType<SpriteRenderer>().FirstOrDefault(item=>item.gameObject.name.Contains("DeathEcho"));
        Check(echo!=null && echo.transform.position.x>grid.GetCellCenterWorld(new Vector3Int(east-1,0,0)).x+0.2f,"delayed corpse travels instead of freezing at original cell");
        while(player.IsActionInProgress) yield return null;
        Check(completions==worldBefore+1,"lethal partial collision creates one world turn");
        // Direct death still emits radial movement. Side kills do not count as direct kills.
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        Stacks["long_impact"]=1; direct=Spawn(Vector3Int.zero,1); behind=Spawn(Vector3Int.up);
        yield return Fire(Vector3Int.right);
        Check(behind.GridPosition==Vector3Int.up*2 && Hp(behind)==20,"radial push survives direct target death");
        // Player blocks a radial neighbor without receiving or causing collision damage.
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        Stacks["long_impact"]=1; direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.left);
        // Left neighbor intercepts attack; test the raw push collision classification without a fake hit.
        Check(!spawner.TryKnockbackMonster(behind,Vector3Int.left,1,out _,out KnockbackBlockKind block)
            && block==KnockbackBlockKind.Player && Hp(behind)==20,"player obstruction is distinct from damaging collision");
        // Damage bonuses are additive within one collision, never separate repeated hits.
        foreach(string relicId in new[]{"iron_nail","domino_crest"})
        {
            yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
            Relics.Add(RunProgressionCatalog.Relics.First(item=>item.Id==relicId));
            direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.right);
            yield return Fire(Vector3Int.right);
            Check(Hp(direct)==17 && Hp(behind)==(relicId=="domino_crest"?18:19),"collision combines basic damage with "+relicId);
        }
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        Stacks["trait_mastery"]=1; direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.right);
        yield return Fire(Vector3Int.right);
        Check(Hp(direct)==17 && Hp(behind)==18,"mastery adds direct and receiver damage exactly once");
        // Secondary wall deaths cannot grant king's follow-up move.
        yield return null; Reset(new Vector3Int(east-3,0,0),"knockback");
        Stacks["long_impact"]=1; Relics.Add(RunProgressionCatalog.Relics.First(item=>item.Id=="kings_turn"));
        direct=Spawn(new Vector3Int(east-1,0,0)); behind=Spawn(new Vector3Int(east,1,0),1);
        yield return Fire(Vector3Int.right);
        Check(!player.IsChoosingFreeMove && Hp(direct)==19,"secondary wall kill gives no direct-kill follow-up move");
        // Control upgrade remains limited to primary survivor and its first actual receiver.
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        Stacks["long_impact"]=1; Stacks["impact_recovery"]=2;
        direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.right); unrelated=Spawn(Vector3Int.up);
        foreach(var monster in new[]{direct,behind,unrelated}) typeof(MonsterMovement).GetField("skippedTurns",Private).SetValue(monster,0);
        Ready(); player.SelectTraitAttackMode(); player.TryPerformAction(PlayerActionSelectionMode.Attack,Vector3Int.right);
        while(Hp(direct)==20) yield return null;
        Check(direct.IsActionBlocked && behind.IsActionBlocked && !unrelated.IsActionBlocked,"spread never extends direct-target blockade to unrelated neighbors");
        Check((int)typeof(MonsterMovement).GetField("skippedTurns",Private).GetValue(direct)==2
            && (int)typeof(MonsterMovement).GetField("skippedTurns",Private).GetValue(behind)==1,"blockade maintains two primary actions and one receiver action");
        while(player.IsActionInProgress) yield return null;
        // Ordinary attacks retain damage/charge even with the spread upgrade held.
        yield return null; Reset(new Vector3Int(-2,0,0),"knockback");
        Stacks["long_impact"]=1; direct=Spawn(Vector3Int.zero); behind=Spawn(Vector3Int.up);
        yield return Fire(Vector3Int.right,false);
        Check(Hp(direct)==19 && direct.GridPosition==Vector3Int.zero && Hp(behind)==20 && behind.GridPosition==Vector3Int.up,"ordinary attack cannot emit knockback or radial spread");
        yield return Gap(0.4f);
        Check(!CombatFeedback.HasPendingDisplacement,"all displacement presentation clears after action");
    }
}
