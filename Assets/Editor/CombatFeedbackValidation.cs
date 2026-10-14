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

// 실제 SampleScene 재생/판정/레이캐스트 검사. 음색 선호도와 사람의 청취 평가는 별도다.
[InitializeOnLoad]
public static class CombatFeedbackValidation
{
    private const string Pending = "GridCaster.CombatFeedbackValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static CombatFeedbackValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            checks.Clear(); frame = -1; runtimeError = null;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            deadline = EditorApplication.timeSinceStartup + 90;
            Application.logMessageReceived += Error;
            routine = Flatten(Run()); EditorApplication.update += Tick;
        };
    }

    [MenuItem("Tools/Playtest/Validate Combat Feedback")]
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
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(CombatFeedbackValidation))))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("전투 오디오 검사 시간 초과/중단");
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
        string report = "COMBAT_FEEDBACK_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; not human listening or normal stage completion.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "combat-feedback-2026-10-04.txt"), report);
        if (passed) Debug.Log(report); else Debug.LogError(report);
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
    }

    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException(description);
        checks.Add("PASS " + description);
    }
    private static int Count(GameAudio audio, CombatSound kind) =>
        ((int[])typeof(GameAudio).GetField("playedCounts", Private).GetValue(audio))[(int)kind];
    private static IEnumerator QuietGap()
    {
        float until = Time.realtimeSinceStartup + 0.6f;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

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

    private static MonsterMovement Spawn(MonsterSpawner spawner, Move player, Vector3Int cell, int hp = 20)
        => spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour, cell,
            player.GetComponent<SpriteRenderer>().sprite, Color.white, hp, 0, 1, 4, 2);

    private static IEnumerator Gap(float seconds)
    {
        float until = Time.time + seconds;
        while (Time.time < until) yield return null;
    }

    private static void Charge(PlayerTraitSystem trait, string id)
    {
        // 특성 선택 UI 재진입 없이 준비된 공격의 런타임 픽스처를 구성한다.
        typeof(PlayerTraitSystem).GetField("selectedTraitId", Private).SetValue(trait, id);
        trait.SetActivationBagSize(3);
        trait.ResetCharge();
        while (!trait.IsReady) trait.RegisterCompletedAttack(true, false);
    }

    private static void CaptureBoard(Camera camera, string name)
    {
        // 실제 URP 카메라 렌더. HUD는 Screen Space Overlay이므로 보드 캡처에는 포함하지 않는다.
        RenderTexture previous = camera.targetTexture;
        RenderTexture active = RenderTexture.active;
        var target = new RenderTexture(1920, 1080, 24);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification/captures");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous;
            RenderTexture.active = active;
            UnityEngine.Object.Destroy(target);
            UnityEngine.Object.Destroy(texture);
        }
    }

    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        var player = UnityEngine.Object.FindAnyObjectByType<Move>();
        var spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        var grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        var trait = player.GetComponent<PlayerTraitSystem>();
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        hud.SelectTrait(0); yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke(); yield return null;
        Time.captureDeltaTime = 1f / 120f;
        var camera = Camera.main;
        int starts = 0, completions = 0;
        float worldAt = -1;
        player.MoveCompleted += () => { starts++; worldAt = Time.time; };
        spawner.WorldTurnCompleted += () => completions++;

        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(Vector3Int.zero);
        var target = Spawn(spawner, player, new Vector3Int(2, 0, 0));
        Vector3 root = target.transform.position;
        Vector3Int cell = target.GridPosition;
        target.TakeDamage(0, Vector3Int.right);
        Check(CombatFeedback.SparkCount == 0 && CombatFeedback.DeathEchoCount == 0,
            "zero damage creates no impact feedback");
        target.TakeDamage(1, Vector3Int.right);
        Check(target.GridPosition == cell && target.transform.position == root
            && CombatFeedback.VisualPosition(target).x > root.x && !target.GetComponent<SpriteRenderer>().enabled,
            "surviving hit recoils immediately without changing root or occupied cell");
        Check(CombatFeedback.SparkCount == 1, "actual HP loss creates one spark");
        yield return null;
        CaptureBoard(camera, "impact-feedback-recoil");
        yield return Gap(0.15f);
        Check(target.GetComponent<SpriteRenderer>().enabled && CombatFeedback.VisualPosition(target) == root
            && CombatFeedback.SparkCount == 0, "reaction returns original sprite and removes spark");

        Vector3 baseCamera = camera.transform.position;
        float unitsPerPixel = camera.orthographicSize * 2f / camera.pixelHeight;
        CameraController.PlayImpactShake(Vector3.right);
        float maximumPixels = 0;
        for (int i = 0; i < 12; i++)
        {
            yield return null;
            Vector3 delta = (camera.transform.position - baseCamera) / unitsPerPixel;
            maximumPixels = Mathf.Max(maximumPixels, Mathf.Abs(delta.x), Mathf.Abs(delta.y));
            Check(Mathf.Abs(delta.x) <= 2.01f && Mathf.Abs(delta.y) <= 2.01f,
                "camera reaction stays inside two display pixels: frame " + i);
        }
        Check(maximumPixels > 0 && camera.transform.position == baseCamera,
            "camera visibly reacts then returns exactly to whole-board center");

        Spawn(spawner, player, new Vector3Int(0, 4, 0)); // 처치 연출 검사 중 웨이브 보상 화면이 열리지 않게 한다.
        target.TakeDamage(1000, Vector3Int.right);
        Check(!spawner.TryGetMonsterAtCell(cell, out _) && CombatFeedback.DeathEchoCount == 1,
            "lethal damage releases occupancy immediately while an independent echo survives");
        var echo = GameObject.Find("CombatDeathEcho");
        Check(echo != null && echo.GetComponent<CharacterHealth>() == null
            && echo.GetComponent<MonsterMovement>() == null && echo.GetComponent<Collider2D>() == null,
            "death echo has no HP, actor, or collision rules");
        yield return null;
        CaptureBoard(camera, "impact-feedback-lethal");
        // 즉시 정지해 연출도 멈추는지 확인한다.
        Time.timeScale = 0;
        Vector3 frozen = echo.transform.position;
        for (int i = 0; i < 8; i++) yield return null;
        Check(echo != null && echo.transform.position == frozen, "pause freezes death echo");
        Time.timeScale = 1;
        // 사망 모션이 있는 몬스터는 모션 길이만큼 잔상이 남는다.
        float echoLifetime = CombatFeedback.DeathEchoSeconds;
        if (echo.TryGetComponent(out Animator echoAnimator))
            echoLifetime = Mathf.Max(echoLifetime, ActorMotion.DeathSeconds(echoAnimator.runtimeAnimatorController));
        yield return Gap(echoLifetime + 0.06f);
        Check(CombatFeedback.DeathEchoCount == 0, "death echo expires independently after its brief visual lifetime");

        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(Vector3Int.zero);
        target = Spawn(spawner, player, new Vector3Int(2, 0, 0));
        root = target.transform.position;
        target.TakeDamage(1, Vector3Int.right);
        Check(spawner.TryKnockbackMonster(target, Vector3Int.right), "legal knockback accepted");
        Check(target.GridPosition == new Vector3Int(3, 0, 0)
            && target.transform.position == grid.GetCellCenterWorld(target.GridPosition)
            && CombatFeedback.VisualPosition(target).x < target.transform.position.x - 0.8f,
            "knockback updates occupancy immediately and presents movement from previous visual location");
        Vector3 destination = target.transform.position;
        var blocker = Spawn(spawner, player, new Vector3Int(4, 0, 0));
        Check(!spawner.TryKnockbackMonster(target, Vector3Int.right) && target.transform.position == destination,
            "blocked knockback preserves existing collision rules");
        yield return Gap(0.15f);
        Check(!CombatFeedback.HasPendingDisplacement && CombatFeedback.VisualPosition(target) == destination,
            "knockback presentation settles at the actual occupied cell");

        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(Vector3Int.zero);
        trait.SetActivationBagSize(5);
        trait.ResetCharge();
        target = Spawn(spawner, player, new Vector3Int(2, 0, 0));
        int beforeStarts = starts, beforeCompletions = completions;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right), "normal attack accepted");
        while (target.GetComponent<CharacterHealth>().CurrentHealth == 20) yield return null;
        float hitAt = Time.time;
        Check(starts == beforeStarts && !player.CanAct, "hit result holds before enemy turn and blocks repeated input");
        Time.timeScale = 0;
        for (int i = 0; i < 8; i++) yield return null;
        Check(starts == beforeStarts, "pause during recovery cannot advance an enemy turn");
        Time.timeScale = 1;
        while (!player.CanAct) yield return null;
        Check(starts == beforeStarts + 1 && completions == beforeCompletions + 1
            && worldAt - hitAt >= 0.07f && trait.ChargeProgress == 1,
            "normal hit holds about 80ms, then exactly one world turn and one charge; measured ms=" + ((worldAt-hitAt)*1000f));

        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(new Vector3Int(-6, -6, 0));
        target = Spawn(spawner, player, new Vector3Int(5, 5, 0));
        float launchAt = Time.time;
        beforeStarts = starts;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, new Vector3Int(1, 1, 0)), "far diagonal attack accepted");
        yield return Gap(0.04f);
        Time.timeScale = 0;
        int hp = target.GetComponent<CharacterHealth>().CurrentHealth;
        for (int i = 0; i < 8; i++) yield return null;
        Check(target.GetComponent<CharacterHealth>().CurrentHealth == hp && starts == beforeStarts,
            "paused capped flight preserves damage and enemy-turn timing");
        Time.timeScale = 1;
        while (target.GetComponent<CharacterHealth>().CurrentHealth == 20) yield return null;
        // 공격 모션의 시전 프레임까지 기다린 뒤 투사체가 나가므로 그 시간을 뺀 비행 시간을 잰다.
        Check(Time.time - launchAt - player.AttackWindupSeconds <= 0.27f, "far diagonal flight capped at 250ms plus frame quantization; ms=" + ((Time.time-launchAt-player.AttackWindupSeconds)*1000f));
        while (!player.CanAct) yield return null;
        Check(starts == beforeStarts + 1, "capped flight still produces exactly one enemy turn");

        spawner.ResetPlaytestEncounter();
        player.ResetPlaytest(new Vector3Int(-6, 0, 0));
        target = Spawn(spawner, player, new Vector3Int(-3, 0, 0));
        var second = Spawn(spawner, player, new Vector3Int(5, 0, 0));
        Charge(trait, "pierce");
        launchAt = Time.time; beforeStarts = starts;
        Check(player.SelectTraitAttackMode() && player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right),
            "prepared penetrating attack accepted");
        while (second.GetComponent<CharacterHealth>().CurrentHealth == 20) yield return null;
        Check(target.GetComponent<CharacterHealth>().CurrentHealth < 20 && Time.time-launchAt-player.AttackWindupSeconds <= 0.27f,
            "both penetrating targets take damage within one shared 250ms cast cap; ms=" + ((Time.time-launchAt-player.AttackWindupSeconds)*1000f));
        while (!player.CanAct) yield return null;
        Check(starts == beforeStarts + 1 && trait.ChargeProgress == 0,
            "penetrating segments create one world turn and do not recharge special attack");

        foreach (string id in new[] { "double_cast", "knockback" })
        {
            spawner.ResetPlaytestEncounter();
            player.ResetPlaytest(Vector3Int.zero);
            target = Spawn(spawner, player, new Vector3Int(2, 0, 0));
            Charge(trait, id); beforeStarts = starts;
            Check(player.SelectTraitAttackMode() && player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right),
                "prepared " + id + " attack accepted");
            while (player.IsActionInProgress)
            {
                if (player.IsChoosingEchoDirection)
                    Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right),
                        "additional cast direction confirmed without an extra world turn");
                yield return null;
            }
            Check(starts == beforeStarts + 1 && trait.ChargeProgress == 0 && !CombatFeedback.HasPendingDisplacement,
                id + " finishes all casts/displacement before one enemy turn");
        }

        // 실제 발사 음성 볼륨을 확인한다. 사용자 설정은 runner가 원래 값으로 복원한다.
        var audio = GameAudio.GetOrCreate();
        foreach (AudioSource source in audio.GetComponents<AudioSource>()) source.Stop();
        yield return QuietGap();
        GameAudio.Play(CombatSound.MagicLaunch);
        AudioSource launch = audio.GetComponents<AudioSource>().First(source => source.isPlaying);
        float initialVolume = launch.volume;
        Check(GameAudio.Play(CombatSound.SoftHit) && Mathf.Abs(launch.volume-initialVolume*0.25f) < 0.0001f,
            "actual impact ducks active launch tail to 25 percent");
        GameAudio.Play(CombatSound.MagicLaunch);
        Check(audio.GetComponents<AudioSource>().Any(source => source.isPlaying && source.clip.name.Contains("laser")
            && Mathf.Abs(source.volume-initialVolume) < 0.0001f), "new cast restores full launch gain");
        Time.captureDeltaTime = 0f;
        yield return Gap(0.3f);
        Check(CombatFeedback.SparkCount == 0 && CombatFeedback.DeathEchoCount == 0,
            "temporary hit effects clean up after combat");
    }
}
