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

// 실제 SampleScene에서 직격·주변 피해·예고·충전·성장과 처치 경계를 검증한다.
[InitializeOnLoad]
public static class DamageBoostSplashValidation
{
    private const string Pending = "GridCaster.DamageBoostSplashValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static DamageBoostSplashValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); frame = -1; runtimeError = null;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 150;
            Application.logMessageReceived += Error;
            routine = Flatten(Run()); EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Damage Boost Splash")]
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
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(DamageBoostSplashValidation))))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("데미지 강화 범위 공격 검사 시간 초과/중단");
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
        string report = "DAMAGE_BOOST_SPLASH_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; gameplay-rule validation, not human fun assessment.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "damage-boost-range-2026-10-05.txt"), report);
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
            "confirmed attack clears splash preview");
        while (player.IsActionInProgress)
        {
            if (player.IsChoosingEchoDirection)
                Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, direction), "reaim consumes no separate action");
            yield return null;
        }
        Check(starts == a + 1 && completions == b + 1, "one attack gives exactly one enemy and projectile turn");
        Check(trait.ChargeProgress == (special ? 0 : Mathf.Min(trait.ChargeRequired, chargeBefore + 1)),
            "splash never adds charge or recharges a special attack");
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
    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        trait = player.GetComponent<PlayerTraitSystem>(); growth = player.GetComponent<RunProgressionSystem>();
        Check(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.GetType().FullName.Contains("Universal"),
            "current scene uses URP; no rendering package change");
        UnityEngine.Object.FindAnyObjectByType<GameHudController>().SelectTrait(0); yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke(); yield return null;
        Time.captureDeltaTime = 1f/120f;
        player.MoveCompleted += () => starts++;
        spawner.WorldTurnCompleted += () => completions++;
        Vector3Int center = new Vector3Int(2,0,0);
        foreach (Vector3Int offset in Directions)
        {
            Vector3Int direction = offset == Vector3Int.left ? Vector3Int.up : Vector3Int.right;
            Reset(center - direction * 2);
            MonsterMovement direct = Spawn(center), near = Spawn(center + offset), far = Spawn(center + offset*2);
            int hp = player.CurrentHealth;
            yield return Fire(direction);
            Check(Hp(direct) == 18 && Hp(near) == 19 && Hp(far) == 20 && player.CurrentHealth == hp,
                "surviving impact: direct two, splash one at " + offset + ", radius two untouched and player unharmed");
        }
        Reset(Vector3Int.zero);
        MonsterMovement main = Spawn(center), side = Spawn(center + Vector3Int.up);
        yield return Fire(Vector3Int.right, false);
        Check(Hp(main) == 19 && Hp(side) == 20, "ordinary A attack has no splash");
        Ready();
        yield return Fire(Vector3Int.right, false);
        Check(Hp(main) == 18 && Hp(side) == 20 && trait.IsReady,
            "ordinary A attack while F is prepared retains charge and does not explode");
        int a0 = starts, b0 = completions;
        yield return Fire(Vector3Int.left);
        Check(Hp(main) == 18 && Hp(side) == 20 && trait.ChargeProgress == 0,
            "missed prepared F attack is consumed with no wall explosion");

        Reset(Vector3Int.zero);
        main = Spawn(center,1); side = Spawn(center+Vector3Int.up,1);
        MonsterMovement beyond = Spawn(center+Vector3Int.up*2);
        bool mainDead = false, sideDead = false;
        main.GetComponent<CharacterHealth>().Died += _ => mainDead = true;
        side.GetComponent<CharacterHealth>().Died += _ => sideDead = true;
        yield return Fire(Vector3Int.right);
        Check(mainDead && sideDead && Hp(beyond) == 20
            && !spawner.TryGetMonsterAtCell(center,out _) && !spawner.TryGetMonsterAtCell(center+Vector3Int.up,out _),
            "lethal impact still splashes, splash kill frees occupancy and never chains");

        Reset(Vector3Int.zero);
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="blood_knot"));
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="kings_turn"));
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="excess_reservoir"));
        player.TakeDamage(3);
        int beforeHp = player.CurrentHealth;
        main = Spawn(center); side = Spawn(center+Vector3Int.up,1); Spawn(new Vector3Int(-5,5,0));
        yield return Fire(Vector3Int.right);
        Check(player.CurrentHealth == beforeHp && !player.IsChoosingFreeMove,
            "splash-only kill triggers neither direct-kill heal nor king free move");
        growth.BeginAttack(default);
        Check(growth.ModifyAttackDamage(1,default,0)==1, "splash-only kill creates no excess-reservoir storage");

        Reset(Vector3Int.zero);
        main = Spawn(center); side = Spawn(center+Vector3Int.up);
        Ready(); player.SelectTraitAttackMode();
        var indicator = player.GetComponent<DirectionalActionIndicator>();
        Vector2 point = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(Vector3Int.right));
        Check(player.TryPreviewBoardPoint(point,out _) && indicator.ActiveAreaCount==8,
            "F preview includes eight surrounding cells before action commitment");
        int pendingCharge = trait.ChargeProgress;
        Capture("damage-boost-splash-preview-2026-10-05");
        player.CancelSelection();
        Check(indicator.ActiveAreaCount==0 && trait.ChargeProgress==pendingCharge,
            "cancel clears area preview without spending prepared attack");
        player.SelectAttackMode(); player.TryPreviewBoardPoint(point,out _);
        Check(indicator.ActiveAreaCount==0, "ordinary A preview has no blast footprint");
        player.SelectTraitAttackMode();
        Vector2 missPoint = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(Vector3Int.left));
        player.TryPreviewBoardPoint(missPoint,out _);
        Check(indicator.ActiveAreaCount==0, "miss preview has no blast footprint");

        Reset(Vector3Int.zero);
        main = Spawn(Vector3Int.right);
        Ready(); player.SelectTraitAttackMode(); player.TryPreviewBoardPoint(point,out _);
        Check(indicator.ActiveAreaCount==7, "adjacent blast preview excludes player cell");
        beforeHp = player.CurrentHealth;
        yield return Fire(Vector3Int.right);
        Check(player.CurrentHealth==beforeHp && Hp(main)==18,
            "adjacent blast excludes player and never double-hits its direct target");

        Vector3Int topGround = Vector3Int.zero;
        foreach (Vector3Int cell in grid.GroundTilemap.cellBounds.allPositionsWithin)
            if (cell.x==0 && grid.IsWalkableCell(cell) && cell.y>topGround.y) topGround=cell;
        Vector3Int wall = topGround + Vector3Int.up;
        Check(grid.IsTopBoundaryWallCell(wall), "wall-mounted fixture uses actual top boundary");
        Reset(wall+Vector3Int.down*2);
        main = Spawn(wall,20,MonsterMovementPattern.Rook);
        side = Spawn(wall+Vector3Int.right,20,MonsterMovementPattern.Rook);
        Ready(); player.SelectTraitAttackMode();
        Vector2 wallAim = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(player.GridPosition+Vector3Int.up));
        player.TryPreviewBoardPoint(wallAim,out _);
        var area = (List<Vector3>)typeof(Move).GetField("attackAreaWorldPositions",Private).GetValue(player);
        Check(area.Contains(grid.GetCellCenterWorld(wall+Vector3Int.right)),
            "splash preview includes a neighboring wall-mounted Rook");
        yield return Fire(Vector3Int.up);
        Check(Hp(main)==18 && Hp(side)==19, "wall impact splashes adjacent Rook with the same preview footprint");

        // 실제 입력으로 7×7 배치를 타격해 구멍·범위 밖·플레이어 제외와 조준 일치를 검사한다.
        for (int level = 0; level <= 2; level++)
        {
            Reset(new Vector3Int(-1,0,0));
            Stacks["afterglow_recovery"] = level;
            Vector3Int blastCenter = Vector3Int.zero;
            main = Spawn(blastCenter);
            var field = new Dictionary<Vector3Int,MonsterMovement>();
            for (int x = -3; x <= 3; x++)
                for (int y = -3; y <= 3; y++)
                {
                    var cell = new Vector3Int(x,y,0);
                    if (cell != blastCenter && cell != player.GridPosition) field[cell] = Spawn(cell);
                }
            Ready(); player.SelectTraitAttackMode();
            Vector2 aim = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(blastCenter));
            Check(player.TryPreviewBoardPoint(aim, out _), "grown F preview accepted at level " + level);
            var predicted = ((List<Vector3>)typeof(Move).GetField("attackAreaWorldPositions",Private).GetValue(player))
                .Select(point => grid.WorldToCell(point)).ToHashSet();
            var expected = field.Keys.Where(cell =>
                Mathf.Max(Mathf.Abs(cell.x),Mathf.Abs(cell.y)) == 1
                || (level >= 1 && Mathf.Abs(cell.x)+Mathf.Abs(cell.y)==2 && (cell.x==0 || cell.y==0))
                || (level >= 2 && Mathf.Abs(cell.x)==2 && Mathf.Abs(cell.y)==2)).ToHashSet();
            Check(predicted.SetEquals(expected) && predicted.Count == 7 + level*4,
                "preview exactly matches grown shape, excluding player and center: " + level);
            Check(field.Keys.All(cell => DamageBoostArea.Contains(cell,level)==expected.Contains(cell)),
                "integer shape agrees with preview and leaves outer knight-shaped gaps: " + level);
            Capture("damage-boost-grown-range-" + (8+level*4) + "-2026-10-05");
            int playerHp = player.CurrentHealth;
            yield return Fire(Vector3Int.right);
            Check(Hp(main)==18 && player.CurrentHealth==playerHp && field.All(pair =>
                Hp(pair.Value)==(expected.Contains(pair.Key) ? 19 : 20)),
                "actual area damage hits precisely 8/12/16 footprint, gaps and radius three remain untouched: " + level);
            yield return Fire(Vector3Int.right,false);
            Check(field.All(pair => Hp(pair.Value)==(expected.Contains(pair.Key) ? 19 : 20)),
                "grown range never leaks into ordinary A attack: " + level);
        }
        Reset(Vector3Int.zero);
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="blast_core"));
        main=Spawn(center); side=Spawn(center+Vector3Int.up,1); Spawn(new Vector3Int(-4,4,0));
        var anotherSide=Spawn(center+new Vector3Int(1,1,0));
        yield return Fire(Vector3Int.right);
        Check(Hp(main)==18 && Hp(anotherSide)==19,
            "side-only kill never activates blast core or recursively changes remaining fragments");

        foreach (int amplification in new[]{0,1,2})
            foreach (int mastery in new[]{0,1})
                foreach (int core in new[]{0,1})
                    foreach (int deepening in new[]{0,1})
                    {
                        Reset(Vector3Int.zero);
                        Stacks["afterglow_recovery"]=amplification; Stacks["trait_mastery"]=mastery;
                        Stacks["rapid_cycle"]=deepening;
                        if(core==1) Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="blast_core"));
                        growth.BeginAttack(new PlayerAttackTraitRoll(false,false,true,false));
                        Check(growth.DamageBoostAreaLevel==amplification
                            && DamageBoostArea.Offsets(amplification).Count==8+amplification*4
                            && growth.DamageBoostSplashDamage==1+mastery
                            && growth.DamageBoostSplashDamageFor(false)==1+mastery
                            && growth.DamageBoostSplashDamageFor(true)==1+mastery+core
                            && growth.ModifyAttackDamage(1,new PlayerAttackTraitRoll(false,false,true,false),0)==2+deepening,
                            "growth separates range, power and conditional core: " + amplification+"/"+mastery+"/"+core+"/"+deepening);
                    }
        Reset(Vector3Int.zero);
        Stacks["afterglow_recovery"]=2; Stacks["overcharge"]=2; Stacks["rapid_cycle"]=1; Stacks["trait_mastery"]=1;
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="blast_core"));
        main = Spawn(center); side = Spawn(center+Vector3Int.up);
        yield return Fire(Vector3Int.right);
        Check(Hp(main)==15 && Hp(side)==18, "grown surviving impact has direct five and splash two; core cannot trigger");
        Reset(Vector3Int.zero);
        Stacks["afterglow_recovery"]=2; Stacks["overcharge"]=2; Stacks["rapid_cycle"]=1; Stacks["trait_mastery"]=1;
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="blast_core"));
        main = Spawn(center,5); side = Spawn(center+Vector3Int.up);
        var outerSide = Spawn(center+new Vector3Int(2,2,0));
        bool killedCenter = false; main.GetComponent<CharacterHealth>().Died += _ => killedCenter = true;
        yield return Fire(Vector3Int.right);
        Check(killedCenter && Hp(side)==17 && Hp(outerSide)==17,
            "direct center kill boosts both inner and added outer fragments to three");

        foreach (string id in new[]{"double_cast","pierce","knockback"})
        {
            Reset(Vector3Int.zero,id); main = Spawn(center); side = Spawn(center+Vector3Int.up);
            yield return Fire(Vector3Int.right);
            Check(Hp(side)==20, id+" special retains no free splash");
        }
        Reset(Vector3Int.zero);
        main = Spawn(center); side = Spawn(center+Vector3Int.up);
        var boost = new PlayerAttackTraitRoll(false,false,true,false);
        growth.BeginAttack(boost); main.TakeDamage(2);
        growth.NotifyAttackHit(main,boost,0,0); growth.NotifyAttackHit(main,boost,1,0);
        Check(Hp(main)==18 && Hp(side)==19,
            "repeated direct notifications in same action cannot create another blast");
        Relics.Add(RunProgressionCatalog.Relics.First(x=>x.Id=="glass_heart"));
        growth.BeginAttack(default); main.GetComponent<CharacterHealth>().Initialize(1);
        main.TakeDamage(4); growth.NotifyAttackHit(main,default,0,0);
        Check(Hp(side)==17, "glass heart retains its independent capped surplus transfer on normal attacks");
        Check(RunProgressionDescriptions.TraitEffect("damage_boost").Contains("주변 8칸")
            && RunProgressionCatalog.Upgrade("afterglow_recovery").Name=="폭발 증폭"
            && RunProgressionCatalog.Relics.First(x=>x.Id=="blast_core").Name=="폭발 핵",
            "trait and renamed growth explain the applied splash rule");
    }
}
