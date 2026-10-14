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
public static class CombatAudioValidation
{
    private const string Pending = "GridCaster.CombatAudioValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static CombatAudioValidation()
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

    [MenuItem("Tools/Playtest/Validate Combat Audio and Volume %#&v")]
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
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(CombatAudioValidation))))
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
        string report = "COMBAT_AUDIO_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; not human listening or normal stage completion.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "combat-audio-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"), report);
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

    private static void PointerDown(Slider slider, float proportion)
    {
        Canvas.ForceUpdateCanvases();
        RectTransform rect = slider.GetComponent<RectTransform>();
        var pointer = new PointerEventData(EventSystem.current) {
            position = rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin + 12, rect.rect.xMax - 12, proportion), rect.rect.center.y, 0)),
            button = PointerEventData.InputButton.Left
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Slider>() == slider,
            "actual GraphicRaycaster reaches " + slider.name + "; top hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
        slider.OnPointerDown(pointer);
        Check(Mathf.Abs(slider.value - proportion * 100f) <= 1, "slider pointer adjusts percentage: " + slider.name);
    }

    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        GameAudio audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
        GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
        MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        Check(audio != null && audio.GetComponents<AudioSource>().Length == 4,
            "one scene audio controller, three general voices + reserved hurt voice");
        foreach (AudioClip clip in (AudioClip[])typeof(GameAudio).GetField("clips", Private).GetValue(audio))
        {
            AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            Check(clip != null && clip.frequency == 48000 && clip.channels == 1
                && clip.loadType == AudioClipLoadType.DecompressOnLoad
                && importer.defaultSampleSettings.compressionFormat == AudioCompressionFormat.PCM,
                "selected WAV imported without lossy encoding: " + clip.name);
        }
        Check(UnityEngine.Object.FindObjectsByType<AudioListener>().Count(item => item.enabled) == 1,
            "one active AudioListener");
        hud.TogglePauseMenu();
        Check(hud.IsPauseMenuOpen && Time.timeScale == 0 && !player.CanAct, "ESC menu pauses combat");
        yield return null; yield return null;
        Slider sfx = hud.GetComponentsInChildren<Slider>(true).First(item => item.name == "SfxVolumeSlider");
        Slider music = hud.GetComponentsInChildren<Slider>(true).First(item => item.name == "MusicVolumeSlider");
        PointerDown(sfx, 0.25f); PointerDown(music, 0.4f);
        Check(Mathf.Approximately(audio.SfxVolume, 0.25f) && Mathf.Approximately(audio.MusicVolume, 0.4f)
            && Mathf.Approximately(GameObject.Find("BGM").GetComponent<AudioSource>().volume, 0.4f),
            "independent SFX/music controls update live sources while paused");
        sfx.transform.parent.Find("Increase").GetComponent<Button>().onClick.Invoke();
        Check(sfx.value == 30, "plus control adjusts five percent");
        sfx.Select();
        sfx.OnMove(new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Right, moveVector = Vector2.right });
        Check(sfx.value == 31, "focused slider arrow key adjusts one percent");
        audio.SetSfxVolume(0.6f); sfx.SetValueWithoutNotify(60);
        float[] before = { player.GridPosition.x, player.GridPosition.y };
        yield return QuietGap();
        Check(player.GridPosition.x == before[0] && player.GridPosition.y == before[1], "volume changes never advance board");
        foreach (Text label in sfx.transform.parent.GetComponentsInChildren<Text>())
            Check(label.preferredHeight <= label.rectTransform.rect.height + 1f, "volume label fits: " + label.name);
        if (!Application.isBatchMode)
        {
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "docs/verification/captures");
            Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "combat-audio-esc.png"));
            yield return null; yield return null;
        }
        hud.ContinueGame();
        Check(!hud.IsPauseMenuOpen && Time.timeScale == 1 && !player.CanAct, "closing pause keeps startup choice locked");
        hud.SelectTrait(0); yield return null;
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke(); yield return null;
        Check(player.CanAct, "initial trait/upgrade still unlocks normal combat");
        spawner.ResetPlaytestEncounter(); yield return null;
        player.ResetPlaytest(Vector3Int.zero);
        MonsterMovement soft = spawner.SpawnPlaytestMonster(MonsterMovementPattern.CardinalFour,
            new Vector3Int(2, 0, 0), player.GetComponent<SpriteRenderer>().sprite, Color.white, 20, 0, 1, 4, 2);
        int launchBefore = Count(audio, CombatSound.MagicLaunch), hitBefore = Count(audio, CombatSound.SoftHit);
        int worldTurns = 0; spawner.WorldTurnCompleted += () => worldTurns++;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.right), "real player attack accepted");
        while (!player.CanAct) yield return null;
        Check(Count(audio, CombatSound.MagicLaunch) == launchBefore + 1 && Count(audio, CombatSound.SoftHit) == hitBefore + 1
            && soft.GetComponent<CharacterHealth>().CurrentHealth < 20 && worldTurns == 1,
            "actual launch + impact, with exactly one world turn after full-path projectile");
        yield return QuietGap();
        hitBefore = Count(audio, CombatSound.SoftHit);
        soft.TakeDamage(0);
        Check(Count(audio, CombatSound.SoftHit) == hitBefore, "zero damage has no hit sound");
        soft.TakeDamage(1000); soft.TakeDamage(1);
        Check(Count(audio, CombatSound.SoftHit) == hitBefore + 1, "lethal impact sounds once; dead target stays silent");
        yield return QuietGap();
        spawner.ResetPlaytestEncounter(); yield return null;
        MonsterMovement knight = spawner.SpawnPlaytestMonster(MonsterMovementPattern.Knight,
            new Vector3Int(-3, -3, 0), player.GetComponent<SpriteRenderer>().sprite, Color.white, 20, 1, 1, 4, 2);
        hitBefore = Count(audio, CombatSound.HardHit); knight.TakeDamage(1);
        Check(Count(audio, CombatSound.HardHit) == hitBefore + 1, "armored target uses selected hard hit");
        yield return QuietGap();
        bool landed = false;
        int landBefore = Count(audio, CombatSound.KnightLand);
        knight.TakeTurn(() => landed = true, (_, __) => true, _ => false);
        Check(Count(audio, CombatSound.KnightLand) == landBefore, "knight landing sound waits for actual landing");
        while (!landed) yield return null;
        Check(Count(audio, CombatSound.KnightLand) == landBefore + 1, "knight plays once at landing");
        yield return QuietGap();
        MonsterMovement bishop = spawner.SpawnPlaytestMonster(MonsterMovementPattern.Bishop,
            new Vector3Int(4, 4, 0), player.GetComponent<SpriteRenderer>().sprite, Color.white, 20, 2, 1, 4, 2);
        int fireBefore = Count(audio, CombatSound.FireTrail); bool moved = false;
        bishop.TakeTurn(() => moved = true, (_, __) => true, _ => false);
        while (!moved) yield return null;
        Check(Count(audio, CombatSound.FireTrail) == fireBefore + 1, "bishop fire sounds once per action, never per fire tile");
        yield return QuietGap();
        int hurtBefore = Count(audio, CombatSound.PlayerHurt), healthBefore = player.CurrentHealth;
        player.TakeDamage(0); Check(Count(audio, CombatSound.PlayerHurt) == hurtBefore, "zero player damage silent");
        player.TakeDamage(1);
        Check(player.CurrentHealth < healthBefore && Count(audio, CombatSound.PlayerHurt) == hurtBefore + 1,
            "actual HP loss uses selected player hurt");
        yield return QuietGap();
        fireBefore = Count(audio, CombatSound.FireTrail);
        for (int i = 0; i < 20; i++) GameAudio.Play(CombatSound.FireTrail);
        Check(Count(audio, CombatSound.FireTrail) == fireBefore + 1, "twenty simultaneous fire events produce one voice");
        hurtBefore = Count(audio, CombatSound.PlayerHurt);
        GameAudio.Play(CombatSound.MagicLaunch); GameAudio.Play(CombatSound.HardHit); GameAudio.Play(CombatSound.PlayerHurt);
        Check(Count(audio, CombatSound.PlayerHurt) == hurtBefore + 1, "player hurt remains audible with all general voices busy");
        hud.TogglePauseMenu(); sfx.value = 0; music.value = 0;
        Check(audio.GetComponents<AudioSource>().All(source => source.volume == 0)
            && GameObject.Find("BGM").GetComponent<AudioSource>().volume == 0
            && !GameAudio.Play(CombatSound.MagicLaunch), "zero percent mutes active and future SFX/music");
        sfx.value = 35; music.value = 20; hud.ContinueGame();
        hud.RestartGame(); yield return null; yield return null; yield return null;
        audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>(); hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        Check(Mathf.Approximately(audio.SfxVolume, 0.35f) && Mathf.Approximately(audio.MusicVolume, 0.2f)
            && hud.IsTraitSelectionOpen && !player.CanAct, "scene restart preserves audio prefs and resets run");
        player.TakeDamage(10000); hud.ToggleGameOverReview(); hud.TogglePauseMenu(); hud.ContinueGame();
        Check(hud.IsGameOver && !player.CanAct && !hud.IsPauseMenuOpen,
            "death review/pause callbacks cannot resume ended combat");
    }
}
