using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// 실제 행동 중의 Animator와 메뉴 표시를 검사한다. 판정만 통과하고 모션이 빠지는 회귀를 잡는다.
[InitializeOnLoad]
public static class MotionUiRegressionValidation
{
    private const string Pending = "GridCaster.MotionUiRegression";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<string> checks = new List<string>();
    private static IEnumerator routine;
    private static int frame;
    private static double deadline;
    private static string runtimeError;
    private static bool previousRunInBackground;
    private static Vector2 previousMousePosition;

    static MotionUiRegressionValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); frame = -1; runtimeError = null;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            previousMousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            deadline = EditorApplication.timeSinceStartup + 90;
            Application.logMessageReceived += OnError;
            routine = Run();
            EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Motion and UI Regression")]
    public static void RunBatch()
    {
        if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Exit Play and save the scene before validation.");
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnError(string message, string stack, LogType type)
    {
        if ((type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(MotionUiRegressionValidation))))
            runtimeError = message + stack;
    }

    private static void Tick()
    {
        try
        {
            if (runtimeError != null) throw new InvalidOperationException(runtimeError);
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("Motion/UI validation interrupted or timed out.");
            if (frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (!routine.MoveNext()) Finish(true);
        }
        catch (Exception exception) { checks.Add(exception.ToString()); Finish(false); }
    }

    private static void Finish(bool passed)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnError;
        Application.runInBackground = previousRunInBackground;
        Time.timeScale = 1f;
        if (Mouse.current != null)
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = previousMousePosition });
        string report = "MOTION_UI_REGRESSION_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled SampleScene Play fixtures; not a human playtest.\n" + string.Join("\n", checks);
        string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "motion-ui-regression-"
            + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        routine = null;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }

    private static IEnumerator Run()
    {
        yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        var player = UnityEngine.Object.FindAnyObjectByType<Move>();
        var spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        var grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        var ui = hud.GetComponent<RunProgressionUiController>();
        var run = player.GetComponent<RunProgressionSystem>();
        var animator = player.GetComponent<Animator>();
        hud.SelectTrait(0);
        yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        yield return null;
        Vector3Int origin = player.GridPosition;
        int playerTurns = 0, worldTurns = 0;
        player.MoveCompleted += () => playerTurns++;
        spawner.WorldTurnCompleted += () => worldTurns++;

        foreach (PlayerMovementArt art in new[] { PlayerMovementArt.None, PlayerMovementArt.Knight,
            PlayerMovementArt.Bishop, PlayerMovementArt.Rook })
        {
            spawner.ResetPlaytestEncounter();
            player.ConfigurePlaytestCombat(100, 1, origin);
            run.SetUpgradesForPlaytest(Array.Empty<string>());
            run.SetMovementArtForPlaytest(art);
            spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,
                origin + new Vector3Int(4, 4, 0), player.GetComponent<SpriteRenderer>().sprite,
                Color.white, 100, 0, 1, 4, 2);
            yield return null;
            player.TakeDamage(1);
            animator.Update(0f);
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Hit"), "fixture begins in Hit: " + art);
            Vector3Int step = art == PlayerMovementArt.Knight ? new Vector3Int(2, 1, 0)
                : art == PlayerMovementArt.Bishop ? new Vector3Int(2, 2, 0)
                : art == PlayerMovementArt.Rook ? new Vector3Int(2, 0, 0) : Vector3Int.right;
            int starts = playerTurns, ends = worldTurns;
            Check(player.TryPerformAction(PlayerActionSelectionMode.Move, step), "movement accepted after damage: " + art);
            Check(animator.GetBool(ActorMotion.IsMoving), "IsMoving set: " + art);
            yield return null;
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("player_walk"), "walk replaces remaining Hit: " + art);
            Check(Vector3.Distance(player.transform.position, grid.GetCellCenterWorld(origin + step)) > 0.01f,
                "movement has visible travel before arrival: " + art);
            while (animator.GetBool(ActorMotion.IsMoving))
            {
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("player_walk"), "walk throughout travel: " + art);
                yield return null;
            }
            while (!player.CanAct) yield return null;
            Check(player.GridPosition == origin + step && playerTurns == starts + 1 && worldTurns == ends + 1,
                "one landing and one world turn: " + art);
        }

        // 새 모션을 사용하는 세 컨트롤러 모두 피격/공격 뒤 이동 전환을 지원한다.
        foreach (string path in new[] { "Assets/Player/idle-run.controller", "Assets/Monsters/Controllers/Bat.controller",
            "Assets/Monsters/Controllers/Snake.controller" })
        {
            var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path);
            var machine = controller.layers[0].stateMachine;
            var move = machine.states.Select(x => x.state).First(x => x.name == "Move" || x.name == "player_walk");
            foreach (string action in new[] { "Hit", "Attack" })
                Check(machine.states.First(x => x.state.name == action).state.transitions.Any(x =>
                    x.destinationState == move && !x.hasExitTime && x.duration == 0f
                    && x.conditions.Any(c => c.parameter == "IsMoving"
                        && c.mode == UnityEditor.Animations.AnimatorConditionMode.If)),
                    "action can resume moving: " + controller.name + "/" + action);
        }

        Check(Mouse.current != null, "mouse available for actual board hover");
        var altars = (List<RelicOffer>)typeof(RunProgressionSystem).GetField("activeAltars", Private).GetValue(run);
        Vector3Int altarCell = player.GridPosition + Vector3Int.left;
        altars.Add(new RelicOffer(RunProgressionCatalog.Relics[0], altarCell, false));
        Vector2 point = Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(altarCell));
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point });
        yield return null; yield return null;
        RectTransform tooltip = ui.GetComponentsInChildren<RectTransform>(true).First(x => x.name == "HoverTooltip");
        Check(tooltip.gameObject.activeInHierarchy, "altar hover visible during battle");
        hud.TogglePauseMenu();
        yield return null;
        Check(hud.IsPauseMenuOpen && !tooltip.gameObject.activeInHierarchy && !player.CanAct,
            "pause hides existing tooltip and blocks actions");
        InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = point });
        yield return null; yield return null;
        Check(!tooltip.gameObject.activeInHierarchy, "hover cannot reopen tooltip over pause");
        hud.TogglePauseMenu();
        yield return null; yield return null;
        Check(tooltip.gameObject.activeInHierarchy, "altar hover resumes after pause");
        int turnsAtDeath = worldTurns;
        var enemy = spawner.ActiveMonsters.First();
        Vector3 enemyPosition = enemy.transform.position;
        SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
        Color baseColor = player.GetComponent<DamageFlash>().BaseColor(renderer);
        player.TakeDamage(999);
        yield return null;
        Check(hud.IsGameOverOverlayVisible && Time.timeScale == 0f && !tooltip.gameObject.activeInHierarchy,
            "result panel freezes combat and hides tooltip");
        AnimationClip death = animator.runtimeAnimatorController.animationClips
            .First(x => x.name.EndsWith("death", StringComparison.OrdinalIgnoreCase));
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(death)
            .First(x => x.propertyName == "m_Sprite");
        Sprite grave = AnimationUtility.GetObjectReferenceCurve(death, binding).Last().value as Sprite;
        // Editor 지연·상태 전환 프레임을 클립 재생 시간으로 오인하지 않는다.
        // 실제 마지막 프레임을 기다리되 재생이 멈춘 경우에는 제한 시간 안에 실패한다.
        double until = EditorApplication.timeSinceStartup + death.length + 2f;
        while ((renderer.sprite != grave || !animator.GetCurrentAnimatorStateInfo(0).IsName(ActorMotion.DeathState)
            || animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            && EditorApplication.timeSinceStartup < until) yield return null;
        Check(renderer.sprite == grave && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
            "death reaches final grave sprite while timeScale is zero");
        Check(renderer.color == baseColor && !player.GetComponent<DamageFlash>().IsFlashing,
            "grave retains original color instead of frozen damage flash");
        Check(worldTurns == turnsAtDeath && enemy.transform.position == enemyPosition && !player.CanAct,
            "death visual does not advance combat or reopen input");
        hud.ToggleGameOverReview();
        yield return null; yield return null;
        Check(!hud.IsGameOverOverlayVisible && tooltip.gameObject.activeInHierarchy && !player.CanAct,
            "board review may show altar details without reopening combat");
        player.ResetPlaytest(origin);
        Check(animator.updateMode == AnimatorUpdateMode.Normal && !player.GetComponent<PlayerDeathMarker>().ShowsGrave,
            "reset restores normal animator clock and clears death marker");
        hud.RestartGame();
        yield return null; yield return null;
        hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        Check(hud.IsTraitSelectionOpen && !hud.IsRunEnded && Time.timeScale == 1f
            && player.GetComponent<Animator>().updateMode == AnimatorUpdateMode.Normal,
            "new run starts with normal animation clock and fresh selection");
    }
}
