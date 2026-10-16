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
public static class ImpactPresentationValidation
{
    private const string Pending = "GridCaster.ImpactPresentationValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static ImpactPresentationValidation()
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

    [MenuItem("Tools/Playtest/Validate Impact Presentation")]
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
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(ImpactPresentationValidation))))
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
        string report = "IMPACT_PRESENTATION_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; gameplay-rule validation, not human fun assessment.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "impact-presentation-2026-10-05.txt"), report);
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
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        hud.SelectTrait(0); yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke(); yield return null;
        GameAudio.GetOrCreate().SetSfxVolume(0.6f);
        GameAudio.GetOrCreate().SetMusicVolume(0f);
        Time.captureDeltaTime = 1f / 240f;
        player.MoveCompleted += () => starts++;
        spawner.WorldTurnCompleted += () => completions++;
        Reset(Vector3Int.zero);
        var direct = Spawn(new Vector3Int(2,0,0));
        var survivingSide = Spawn(new Vector3Int(2,1,0));
        var lethalSide = Spawn(new Vector3Int(3,-1,0), 1);
        bool sideDied = false;
        lethalSide.GetComponent<CharacterHealth>().Died += _ => sideDied = true;
        Ready(); player.SelectTraitAttackMode();
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right), "actual F attack accepted");
        // 투사체는 공격 모션이 지팡이를 내지르는 시점(AttackWindupSeconds)에 생긴다.
        SpriteRenderer projectile = null;
        float launchDeadline = Time.time + player.AttackWindupSeconds + 0.1f;
        while (projectile == null && Time.time <= launchDeadline)
        {
            projectile = UnityEngine.Object.FindObjectsByType<SpriteRenderer>()
                .FirstOrDefault(r => r.gameObject.name.StartsWith("PlayerProjectile_"));
            if (projectile == null) yield return null;
        }
        Check(projectile != null && Hp(direct) == 20, "projectile launches after attack windup before any damage");
        Check(projectile != null && projectile.transform.localScale.x > 1f && projectile.transform.Find("EmpoweredShortTail") != null,
            "prepared damage boost has thicker magic-colored projectile and short tail");
        while (Hp(direct) == 20) yield return null;
        float impact = Time.time;
        Vector3Int impactCell = direct.GridPosition;
        int sigilCount = 1 + DamageBoostArea.Offsets(growth.DamageBoostAreaLevel)
            .Count(offset => grid.IsWalkableCell(impactCell + offset));
        var sigils = UnityEngine.Object.FindObjectsByType<DamageBoostSigilEffect>()
            .Select(effect => effect.GetComponent<SpriteRenderer>()).ToArray();
        Check(sigils.Length == sigilCount && sigils.Count(r => r.sprite != null) == 1,
            "boost blast stamps center sigil now and one waiting sigil per walkable area cell: " + sigils.Length + "/" + sigilCount);
        Check(Hp(direct) == 18 && Hp(survivingSide) == 19 && sideDied,
            "all damage resolves at center impact, independently of fragment animation");
        Check(!spawner.TryGetMonsterAtCell(new Vector3Int(3,-1,0), out _), "splash kill frees occupancy immediately");
        Check(CombatFeedback.ResidualFragmentCount == 2 && CombatFeedback.DeathEchoCount == 1,
            "living and lethal side targets both receive fragments, dead target retains independent echo");
        Check(!survivingSide.GetComponent<DamageFlash>().IsFlashing
            && CombatFeedback.VisualPosition(survivingSide) == survivingSide.transform.position,
            "side flash and recoil wait for fragment arrival");
        Capture("residual-magic-core");
        var fragment = GameObject.Find("ResidualMagicFragment");
        Vector3 frozen = fragment.transform.position;
        Time.timeScale = 0f;
        for (int i = 0; i < 6; i++) yield return null;
        Check(fragment.transform.position == frozen && CombatFeedback.ResidualFragmentCount == 2
            && !survivingSide.GetComponent<DamageFlash>().IsFlashing,
            "pause freezes fragment flight and queued hit presentation");
        Time.timeScale = 1f;
        while (Time.time - impact < 0.045f) yield return null;
        Check(fragment.transform.position != frozen && CombatFeedback.ResidualFragmentCount == 2,
            "fragments split into short curved paths after gathering");
        Capture("residual-magic-split");
        while (Time.time - impact < 0.085f) yield return null;
        Check(CombatFeedback.ResidualFragmentCount == 0 && survivingSide.GetComponent<DamageFlash>().IsFlashing,
            "within 80ms plus one frame, fragments disappear and neighbor hit flash begins");
        Check(sigils.All(r => r != null && r.sprite != null),
            "area sigils appear together with fragment arrival");
        Capture("residual-magic-arrival");
        while (player.IsActionInProgress) yield return null;
        Check(starts == 1 && completions == 1 && trait.ChargeProgress == 0,
            "presentation retains one world turn and never recharges the F attack");
        // 80ms 도착 시점 캡처 이후에는 실제 프레임 시간으로 나머지 행동을 검사한다.
        Time.captureDeltaTime = 0f;
        Reset(Vector3Int.zero);
        direct = Spawn(new Vector3Int(2,0,0),2);
        Spawn(new Vector3Int(2,1,0),1); Spawn(new Vector3Int(3,-1,0),1);
        Spawn(new Vector3Int(-4,4,0));
        yield return Fire(Vector3Int.right);
        Check(CombatFeedback.CurrentOutcome == "3체 처치", "direct and splash kills merge into one action outcome");
        yield return null;
        var label = (Text)typeof(GameHudController).GetField("actionModeText", Private).GetValue(hud);
        Check(label != null && label.text == "3체 처치" && label.rectTransform.rect.width > 0f,
            "existing visible HUD displays exactly one major result");
        Spawn(new Vector3Int(-3,3,0),1).TakeDamage(1);
        Check(CombatFeedback.CurrentOutcome == "3체 처치", "damage outside completed player attack cannot add kills");
        yield return Gap(0.7f); yield return null;
        Check(CombatFeedback.CurrentOutcome == "" && label.text != "3체 처치", "outcome expires and restores action guidance");
        Reset(Vector3Int.zero);
        Spawn(new Vector3Int(2,0,0)); Spawn(new Vector3Int(2,1,0));
        yield return Fire(Vector3Int.right, false);
        Check(CombatFeedback.ResidualFragmentCount == 0 && CombatFeedback.CurrentOutcome == "",
            "ordinary attack produces neither residual fragments nor outcome spam");
        Reset(Vector3Int.zero);
        var knight = spawner.SpawnPlaytestMonster(MonsterMovementPattern.Knight, new Vector3Int(3,2,0),
            player.GetComponent<SpriteRenderer>().sprite, Color.white, 20, 0, 2, 4, 2);
        bool landed = false;
        Vector3Int landing = new Vector3Int(1,1,0);
        knight.TakeTurn(() => landed = true, (_, cell) => cell == landing, cell => cell != landing);
        while (!landed) yield return null;
        knight.SkipTurns(99);
        Check(knight.GetComponent<ChessMonsterBehaviour>().IsKnightCounterVulnerable,
            "actual Knight landing opens a counter window");
        yield return Fire(landing, false);
        Check(Hp(knight) == 18 && CombatFeedback.CurrentOutcome == "반격 +1",
            "actual adjacent counter hit applies bonus and displays its cause");
        yield return Fire(landing, false);
        Check(CombatFeedback.CurrentOutcome == "", "later ordinary hit never reuses counter label");
        Reset(Vector3Int.zero, "knockback");
        direct = Spawn(new Vector3Int(2,0,0));
        var blocker = Spawn(new Vector3Int(3,0,0));
        yield return Fire(Vector3Int.right);
        Check(direct.GridPosition == new Vector3Int(2,0,0) && CombatFeedback.CurrentOutcome == "충돌",
            "actual occupied knockback destination displays collision once");
        Reset(Vector3Int.zero, "knockback");
        direct = Spawn(new Vector3Int(2,0,0));
        yield return Fire(Vector3Int.right);
        Check(direct.GridPosition == new Vector3Int(3,0,0) && CombatFeedback.CurrentOutcome == "",
            "unblocked displacement never displays collision");
        Reset(Vector3Int.zero);
        direct = Spawn(new Vector3Int(2,0,0), 2); Spawn(new Vector3Int(-4,4,0));
        direct.TakeDamage(1);
        float normalRay = UnityEngine.Object.FindObjectsByType<SpriteRenderer>().Where(r => r.name == "Ray")
            .Max(r => r.transform.localScale.x);
        yield return Gap(0.15f);
        direct.TakeDamage(1);
        Check(normalRay == 3f && UnityEngine.Object.FindObjectsByType<SpriteRenderer>()
            .Any(r => r.name == "Ray" && r.transform.localScale.x == 6f && r.color.g < 1f),
            "kill spark is larger and warm colored compared with ordinary white hit");
        Check(GameAudio.GetOrCreate().GetComponents<AudioSource>().Any(v => v.clip != null
            && v.clip.name.Contains("hit") && v.pitch == 0.84f), "actual kill uses lower pitched impact audio");
        Reset(Vector3Int.zero);
        direct = Spawn(new Vector3Int(2,0,0));
        CombatFeedback.EmitResidualFragment(grid.GetCellCenterWorld(Vector3Int.zero), direct);
        CombatFeedback.BeginPlayerAttack(true); CombatFeedback.NoteCounter(); CombatFeedback.CompletePlayerAttack();
        CombatFeedback.ClearAll();
        Check(CombatFeedback.ResidualFragmentCount == 0 && CombatFeedback.CurrentOutcome == "" && CombatFeedback.DamageVisualDelay == 0f,
            "restart clears particles, queued audio, result and damage presentation scope");
        yield return Gap(0.1f);
        float audioGap = Time.realtimeSinceStartup + 0.06f;
        while (Time.realtimeSinceStartup < audioGap) yield return null;
        var voices = UnityEngine.Object.FindAnyObjectByType<GameAudio>().GetComponents<AudioSource>();
        foreach (AudioSource voice in voices) voice.Stop();
        Check(GameAudio.PlayImpact(CombatSound.SoftHit,0.84f), "isolated pitch fixture plays impact");
        foreach (AudioSource voice in voices) voice.Stop();
        Check(GameAudio.Play(CombatSound.MagicLaunch) && voices.Any(v => v.clip != null
            && v.clip.name.Contains("laser") && v.pitch == 1f),
            "normal launch resets pitch after a heavier kill sound");

        // 새 문양은 CombatFeedback 인스턴스 유무와 일시정지에 관계없이 재시작 즉시 지운다.
        Reset(Vector3Int.zero);
        DamageBoostSigilEffect.Play(grid, Vector3Int.zero, 0);
        Check(DamageBoostSigilEffect.ActiveCount == 9, "lifecycle fixture creates nine sigils");
        Time.timeScale = 0f;
        spawner.ResetPlaytestEncounter();
        Check(DamageBoostSigilEffect.ActiveCount == 0
            && !UnityEngine.Object.FindObjectsByType<DamageBoostSigilEffect>().Any(),
            "paused restart immediately removes visible and delayed sigils");
        for (int i = 0; i < 3; i++) yield return null;
        Time.timeScale = 1f;
        yield return Gap(0.5f);
        Check(DamageBoostSigilEffect.ActiveCount == 0, "canceled sigils never resume after unpause");

        var projectiles = UnityEngine.Object.FindAnyObjectByType<ProjectileManager>();
        bool canceledAttackCompleted = false;
        Check(projectiles.SpawnPlayerProjectile(Vector3Int.zero, Vector3Int.right, 1, 0,
            null, () => canceledAttackCompleted = true), "cancellation fixture launches actual projectile");
        projectiles.ClearProjectiles();
        yield return null;
        Check(!canceledAttackCompleted && projectiles.ActiveProjectileCount == 0,
            "clearing an in-flight attack does not invoke attack completion");

        GameObject closingRoot = new GameObject("ClosingSceneFixture");
        Check(projectiles.SpawnPlayerProjectile(Vector3Int.zero, Vector3Int.right, 1, 0,
            null, () => canceledAttackCompleted = true), "scene-close fixture launches actual projectile");
        var closingProjectile = UnityEngine.Object.FindObjectsByType<TurnProjectile>()
            .Single(p => !p.IsFinished);
        closingProjectile.transform.SetParent(closingRoot.transform, true);
        closingRoot.SetActive(false);
        yield return null;
        Check(!canceledAttackCompleted && projectiles.ActiveProjectileCount == 0,
            "parent teardown cancels projectile without starting an enemy turn");
        UnityEngine.Object.Destroy(closingRoot);

        Reset(Vector3Int.zero);
        var canceledMonster = Spawn(new Vector3Int(3,3,0), kind: MonsterMovementPattern.Bishop);
        // Spawn은 다른 검사에서 적 턴을 막으므로 이번 픽스처만 봉쇄를 해제한다.
        typeof(MonsterMovement).GetField("skippedTurns", Private).SetValue(canceledMonster, 0);
        bool shortBishopCompleted = false;
        Vector3Int bishopDestination = new Vector3Int(4,4,0);
        float bishopStarted = Time.time;
        canceledMonster.TakeTurn(() => shortBishopCompleted = true, (_, __) => true,
            cell => cell != bishopDestination);
        Check(canceledMonster.GridPosition == bishopDestination && canceledMonster.IsMovingVisual,
            "short bishop move reserves its destination before visual travel");
        while (!shortBishopCompleted) yield return null;
        Check(Time.time - bishopStarted >= 0.24f
            && canceledMonster.transform.position == grid.GetCellCenterWorld(bishopDestination),
            "one-cell bishop diagonal visibly travels for at least 250ms within one frame tolerance");
        bool canceledMonsterCompleted = false;
        bishopDestination = new Vector3Int(5,5,0);
        canceledMonster.TakeTurn(() => canceledMonsterCompleted = true, (_, __) => true,
            cell => cell != bishopDestination);
        Check(canceledMonster.IsMovingVisual, "monster teardown fixture begins movement");
        canceledMonster.gameObject.SetActive(false);
        yield return null;
        Check(!canceledMonsterCompleted && !canceledMonster.IsMovingVisual,
            "monster teardown cancels pending movement without advancing world turn");
        Reset(Vector3Int.zero);
    }
}
