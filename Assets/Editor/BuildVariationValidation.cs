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
public static class BuildVariationValidation
{
    private const string Pending = "GridCaster.BuildVariationValidation";
    private static readonly List<string> checks = new List<string>();
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator routine;
    private static double deadline;
    private static int frame;
    private static bool previousRunInBackground;
    private static string runtimeError;

    static BuildVariationValidation()
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

    [MenuItem("Tools/Playtest/Validate Build and Wave Variation")]
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
            && (stack.Contains("Assets/Codes/") || stack.Contains(nameof(BuildVariationValidation))))
            runtimeError = message + "\n" + stack;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new TimeoutException("빌드 변주 검사 시간 초과/중단");
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
        string report = "BUILD_VARIATION_" + (passed ? "PASS" : "FAIL") + "\n"
            + "Controlled actual SampleScene fixtures; not human listening or normal stage completion.\n"
            + "Unity " + Application.unityVersion + "; " + DateTime.Now.ToString("O") + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(directory, "build-variation-2026-10-04.txt"), report);
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

    private static void Invoke(RunProgressionSystem run, string method, int value)
        => typeof(RunProgressionSystem).GetMethod(method, Private).Invoke(run, new object[] { value });

    private static string Composition(WaveTemplate wave)
        => string.Join(",", Enum.GetValues(typeof(MonsterMovementPattern)).Cast<MonsterMovementPattern>()
            .Select(pattern => pattern + ":" + wave.Monsters.Count(item => item == pattern)));

    private static IEnumerator Run()
    {
        yield return null; yield return null; yield return null;
        PlaySessionRecorder.MarkControlledValidation();
        var player = UnityEngine.Object.FindAnyObjectByType<Move>();
        var run = player.GetComponent<RunProgressionSystem>();
        var trait = player.GetComponent<PlayerTraitSystem>();
        var hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        hud.SelectTrait(0); yield return null;
        Check(run.CurrentUpgradeOptions.Count == 3 && run.CurrentUpgradeOptions.All(run.CanSelectUpgrade),
            "actual startup UI receives three legal weighted choices");
        GameObject.Find("UpgradeChoice_1").GetComponent<Button>().onClick.Invoke(); yield return null;
        Check(player.CanAct && !run.IsStarterUpgradeSelection, "actual startup choice unlocks combat");
        var stacks = (Dictionary<string,int>)typeof(RunProgressionSystem).GetField("upgradeStacks", Private).GetValue(run);
        UnityEngine.Random.State saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(20261004);
            foreach (string id in new[] { "double_cast", "pierce", "damage_boost", "knockback" })
            {
                stacks.Clear();
                typeof(PlayerTraitSystem).GetField("selectedTraitId", Private).SetValue(trait, id);
                run.SetMovementArtForPlaytest(PlayerMovementArt.None);
                var counts = new Dictionary<string,int>();
                var offers = new HashSet<string>();
                int noTrait = 0;
                const int samples = 3000;
                for (int i = 0; i < samples; i++)
                {
                    Invoke(run, "BuildUpgradeOptions", 2);
                    var choices = run.CurrentUpgradeOptions;
                    if (choices.Count != 3 || choices.Select(item=>item.Id).Distinct().Count()!=3
                        || choices.Any(item=>!run.CanSelectUpgrade(item) || item.IsAdvanced))
                        throw new InvalidOperationException("invalid normal weighted offer: " + id);
                    if (choices.All(item=>!item.IsTraitUpgrade)) noTrait++;
                    offers.Add(string.Join(",",choices.Select(item=>item.Id).OrderBy(item=>item)));
                    foreach (var item in choices) counts[item.Id] = counts.TryGetValue(item.Id,out int count) ? count+1 : 1;
                }
                Check(noTrait > 0 && offers.Count > 10,
                    id + ": non-trait-only offers remain possible; distinct sets="+offers.Count+"; no-trait="+noTrait+"/"+samples);
                foreach (string art in new[] { "movement_knight", "movement_bishop", "movement_rook" })
                    Check(counts.TryGetValue(art,out int value) && value>0, id + ": other movement choice reachable: " + art);
                double traitAverage = RunProgressionCatalog.Upgrades.Where(item=>item.RequiredTrait==id)
                    .Average(item=>counts[item.Id]);
                double survivalAverage = new[] {"life_tempering","healing_breath"}.Average(key=>counts[key]);
                Check(traitAverage > survivalAverage*1.5, id + ": trait cards appear more often per eligible card than neutral survival cards");
                checks.Add("SAMPLE "+id+" " + string.Join("; ",counts.OrderBy(pair=>pair.Key)
                    .Select(pair=>pair.Key+"="+pair.Value+"/"+samples+" offers")));
            }
            stacks.Clear();
            typeof(PlayerTraitSystem).GetField("selectedTraitId", Private).SetValue(trait,"double_cast");
            run.SetMovementArtForPlaytest(PlayerMovementArt.Knight,1);
            stacks["echo_warhead"] = 1;
            int owned=0, otherTrait=0, landing=0, healing=0;
            for (int i=0;i<3000;i++)
            {
                Invoke(run,"BuildUpgradeOptions",6);
                foreach(var item in run.CurrentUpgradeOptions)
                {
                    if(item.Id=="echo_warhead") owned++;
                    if(item.Id=="quick_cast") otherTrait++;
                    if(item.Id=="movement_breath") landing++;
                    if(item.Id=="healing_breath") healing++;
                }
            }
            Check(owned>otherTrait && landing>healing,
                "owned upgrade and current movement synergy increase draw frequency: owned="+owned+", trait="+otherTrait+", landing="+landing+", healing="+healing);
            stacks.Clear();
            foreach(int level in new[]{0,1,3})
            {
                run.SetMovementArtForPlaytest(level==0?PlayerMovementArt.None:PlayerMovementArt.Knight,level);
                for(int tier=1;tier<=4;tier++)
                {
                    var distinct = new HashSet<string>();
                    bool sawCommon=false;
                    for(int sample=0;sample<300;sample++)
                    {
                        Invoke(run,"BuildAdvancedUpgradeOptions",tier);
                        var choices=run.CurrentUpgradeOptions;
                        if(choices.Count!=3 || choices.Any(item=>!run.CanSelectUpgrade(item)
                            || item.IsAdvanced && item.RewardTier>tier)
                            || choices.Select(item=>RunProgressionDescriptions.ChoiceSummary(item,run)).Distinct().Count()!=3
                            || !choices.Any(item=>item.IsAdvanced))
                            throw new InvalidOperationException("invalid advanced offer tier="+tier+" level="+level);
                        distinct.Add(string.Join(",",choices.Select(item=>item.Id).OrderBy(item=>item)));
                        sawCommon |= choices.Any(item=>!item.IsAdvanced);
                    }
                    Check(distinct.Count>1 && sawCommon,"advanced tier="+tier+" movement="+level+": varied legal choices, at least one advanced, common alternatives, no equivalent duplicate");
                }
            }
            foreach(var item in RunProgressionCatalog.AllUpgrades) stacks[item.Id]=item.MaxStacks;
            Invoke(run,"BuildAdvancedUpgradeOptions",4);
            Check(run.CurrentUpgradeOptions.Count==0,"exhausted pools terminate without max-stack or inactive-trait offers");

            for(int wave=1;wave<=20;wave++)
            {
                WaveTemplate baseline=WaveTemplateCatalog.Get(wave);
                var variants=new HashSet<string>();
                for(int sample=0;sample<300;sample++)
                {
                    WaveTemplate rolled=WaveTemplateCatalog.Roll(wave);
                    if(rolled.WaveNumber!=wave || rolled.Monsters.Count!=baseline.Monsters.Count
                        || rolled.DifficultyTier!=baseline.DifficultyTier || rolled.IsSpecialWave!=baseline.IsSpecialWave
                        || !rolled.Monsters.Distinct().OrderBy(item=>item).SequenceEqual(baseline.Monsters.Distinct().OrderBy(item=>item)))
                        throw new InvalidOperationException("invalid wave variant="+wave);
                    foreach(var pattern in baseline.Monsters.Distinct())
                        if(Math.Abs(rolled.Monsters.Count(item=>item==pattern)-baseline.Monsters.Count(item=>item==pattern))>1)
                            throw new InvalidOperationException("wave composition exceeds one-unit swap budget");
                    variants.Add(Composition(rolled));
                }
                // 박쥐·뱀을 폰 하나로 합친 뒤 한 종류만 나오는 웨이브(1·2웨이브)는 바꿀 상대가 없어 변주가 하나다.
                bool singleType=baseline.Monsters.Distinct().Count()==1;
                Check(wave==1 || wave==10 || wave==20 || singleType ? variants.Count==1 : variants.Count>1,
                    "wave="+wave+": preserved count/unlocks/tier/special role; sampled compositions="+variants.Count);
                if(wave==10 || wave==20)
                    Check(variants.Single()==Composition(baseline),"wave="+wave+": boss composition unchanged");
            }
            var spawner=UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
            var layouts=new HashSet<string>();
            stacks.Clear();
            for(int sample=0;sample<25;sample++)
            {
                spawner.ResetPlaytestEncounter();
                typeof(MonsterSpawner).GetField("waveActive",Private).SetValue(spawner,false);
                player.ResetPlaytest(Vector3Int.zero);
                Check(spawner.StartWave(3),"actual varied wave starts: fixture="+sample);
                layouts.Add(string.Join(";",spawner.ActiveMonsters.Where(item=>item!=null&&!item.IsDead)
                    .Select(item=>item.MovementPattern+":"+item.GridPosition).OrderBy(item=>item)));
                var actors=spawner.ActiveMonsters.Where(item=>item!=null&&!item.IsDead).ToArray();
                if(actors.Length!=4 || actors.Select(item=>item.GridPosition).Distinct().Count()!=4
                    || actors.Any(item=>item.GridPosition==player.GridPosition))
                    throw new InvalidOperationException("actual variant spawn overlap/count failed");
            }
            Check(layouts.Count>1,"actual spawner varies composition/placement without overlap; layouts="+layouts.Count+"/25");
        }
        finally { UnityEngine.Random.state=saved; }
    }
}
