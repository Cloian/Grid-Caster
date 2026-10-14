using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

// 도트 작업 폴더의 lively 모션 시트를 플레이어·박쥐·뱀 애니메이션으로 덮어쓴다.
// 프레임 길이는 Aseprite 원본(Art/Sources/Lively)의 프레임별 ms 값을 그대로 쓴다.
public static class LivelyMotionSetupTool
{
    private const float PixelsPerUnit = 32f;
    // 기존 박쥐·뱀 Aseprite가 40 PPU(40px 프레임 = 1칸)라 같은 크기를 유지한다.
    private const float MonsterPixelsPerUnit = 40f;
    private const string BatPrefabPath = "Assets/Monsters/Prefabs/Bat.prefab";
    private const string SnakePrefabPath = "Assets/Monsters/Prefabs/Snake.prefab";
    private const float ClipFrameRate = 100f;
    private const string PlayerControllerPath = "Assets/Player/idle-run.controller";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ChessScenePath = "Assets/Scenes/ChessPlaytest.unity";
    private const string BatControllerPath = "Assets/Monsters/Controllers/Bat.controller";
    private const string SnakeControllerPath = "Assets/Monsters/Controllers/Snake.controller";
    private const string ArmadilloControllerPath = "Assets/Monsters/Controllers/ArmadilloBishop.controller";
    private const string FireTrailSheet = "Assets/Resources/Effects/fire_trail.png";

    private sealed class Motion
    {
        public string Sheet;
        public string Clip;
        public int Size;
        public bool Loop;
        public int[] DurationsMs;
        public float PpuOverride;
        public float Ppu => PpuOverride > 0f ? PpuOverride : Size == 50 ? PixelsPerUnit : MonsterPixelsPerUnit;
    }

    private static readonly Motion[] Player =
    {
        new Motion { Sheet = "Assets/Player/Lively/wizard_idle.png", Clip = "Assets/Player/player_idle.anim",
            Size = 50, Loop = true, DurationsMs = new[] { 380, 140, 110, 280, 110, 340, 100, 220, 110, 200, 220, 110 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_walk.png", Clip = "Assets/Player/player_walk.anim",
            Size = 50, Loop = true, DurationsMs = new[] { 110, 130, 120, 150, 130, 110 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_attack.png", Clip = "Assets/Player/player_attack.anim",
            Size = 50, DurationsMs = new[] { 70, 90, 60, 100, 110, 170 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_hit.png", Clip = "Assets/Player/player_hit.anim",
            Size = 50, DurationsMs = new[] { 50, 80, 110, 60, 60, 90, 140 } },
        new Motion { Sheet = "Assets/Player/Lively/wizard_death.png", Clip = "Assets/Player/player_death.anim",
            Size = 50, DurationsMs = new[] { 60, 90, 120, 110, 90, 130, 120, 60, 50, 80, 90, 90, 800 } },
    };

    private static readonly Motion[] Bat =
    {
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_idle.png", Clip = "Assets/Monsters/Animations/Bat/BatIdle.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 90, 50, 60, 70, 90, 90, 50, 60, 70, 90 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_move.png", Clip = "Assets/Monsters/Animations/Bat/BatMove.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 70, 40, 50, 50, 60, 70, 40, 50, 50, 60 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_attack.png", Clip = "Assets/Monsters/Animations/Bat/BatAttack.anim",
            Size = 40, DurationsMs = new[] { 130, 150, 50, 110, 90, 130 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_hit.png", Clip = "Assets/Monsters/Animations/Bat/BatHit.anim",
            Size = 40, DurationsMs = new[] { 50, 80, 100, 60, 60, 90, 130 } },
        new Motion { Sheet = "Assets/Monsters/Bat/Lively/bat_death.png", Clip = "Assets/Monsters/Animations/Bat/BatDeath.anim",
            Size = 40, DurationsMs = new[] { 60, 90, 80, 60, 50, 160, 70, 70, 70, 70, 300 } },
    };

    private static readonly Motion[] Snake =
    {
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_idle.png", Clip = "Assets/Monsters/Animations/Snake/SnakeIdle.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 240, 90, 120, 140, 160, 60, 80, 60, 140, 150, 110, 260, 100, 180, 200 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_move.png", Clip = "Assets/Monsters/Animations/Snake/SnakeMove.anim",
            Size = 40, Loop = true, DurationsMs = new[] { 120, 90, 130, 120, 90, 130 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_attack.png", Clip = "Assets/Monsters/Animations/Snake/SnakeAttack.anim",
            Size = 40, DurationsMs = new[] { 140, 160, 50, 110, 100, 180 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_hit.png", Clip = "Assets/Monsters/Animations/Snake/SnakeHit.anim",
            Size = 40, DurationsMs = new[] { 50, 80, 110, 60, 60, 90, 140 } },
        new Motion { Sheet = "Assets/Monsters/Snake/Lively/snake_death.png", Clip = "Assets/Monsters/Animations/Snake/SnakeDeath.anim",
            Size = 40, DurationsMs = new[] { 60, 90, 120, 110, 180, 70, 70, 70, 70, 300 } },
    };

    // 비숍 아르마딜로: 64px 프레임, 32 PPU(스포너가 타일 안에 맞춰 축소). 이동은 굴러가는 공.
    private static readonly Motion[] Armadillo =
    {
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_idle.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloIdle.anim",
            Size = 64, PpuOverride = 32f, Loop = true, DurationsMs = new[] { 200, 200, 200, 200 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_move.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloMove.anim",
            Size = 64, PpuOverride = 32f, Loop = true, DurationsMs = new[] { 60, 60, 60, 60, 60, 60 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_attack.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloAttack.anim",
            Size = 64, PpuOverride = 32f, DurationsMs = new[] { 80, 100, 120, 140 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_hit.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloHit.anim",
            Size = 64, PpuOverride = 32f, DurationsMs = new[] { 50, 80, 110, 140 } },
        new Motion { Sheet = "Assets/Monsters/Armadillo/Lively/armadillo_death.png", Clip = "Assets/Monsters/Animations/Armadillo/ArmadilloDeath.anim",
            Size = 64, PpuOverride = 32f, DurationsMs = new[] { 80, 90, 90, 110, 140, 100, 100, 160 } },
    };

    [MenuItem("Tools/Art/Apply Lively Motions")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 모션을 적용하세요.");

        EnsureFolder("Assets/Monsters/Animations", "Bat");
        EnsureFolder("Assets/Monsters/Animations", "Snake");

        AnimationClip[] playerClips = Player.Select(Build).ToArray();
        AnimationClip[] batClips = Bat.Select(Build).ToArray();
        AnimationClip[] snakeClips = Snake.Select(Build).ToArray();
        AnimationClip[] armadilloClips = Armadillo.Select(Build).ToArray();
        ConfigureSheet(new Motion { Sheet = FireTrailSheet, Size = 32, PpuOverride = 32f });

        AnimatorController player = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerControllerPath);
        if (player == null)
            throw new InvalidOperationException("플레이어 Animator Controller를 찾을 수 없습니다.");
        ConfigureController(player, playerClips, "player_idle", "player_walk");
        AnimatorController bat = LoadOrCreate(BatControllerPath);
        ConfigureController(bat, batClips, "Idle", "Move");
        AnimatorController snake = LoadOrCreate(SnakeControllerPath);
        ConfigureController(snake, snakeClips, "Idle", "Move");
        AnimatorController armadillo = LoadOrCreate(ArmadilloControllerPath);
        ConfigureController(armadillo, armadilloClips, "Idle", "Move");

        Sprite batIdle = LoadFrames(Bat[0])[0];
        Sprite playerIdle = LoadFrames(Player[0])[0];
        ApplyToScene(SampleScenePath, batIdle, bat, playerIdle);
        ApplyToScene(ChessScenePath, batIdle, bat, playerIdle);
        // 일반 웨이브(4방향·8방향)는 기존 Bat/Snake 프리팹으로 생성되므로 외형만 교체한다.
        ApplyToPrefab(BatPrefabPath, batIdle, bat);
        ApplyToPrefab(SnakePrefabPath, LoadFrames(Snake[0])[0], snake);
        AssetDatabase.SaveAssets();
        Validate(playerClips.Concat(batClips).Concat(snakeClips).Concat(armadilloClips).ToArray(), player, bat, snake, armadillo);
    }

    // Temp/LivelyMotionSetup.pending 이 있으면 다음 스크립트 로드 때 한 번만 자동 적용한다.
    private const string PendingMarker = "Temp/LivelyMotionSetup.pending";

    [InitializeOnLoadMethod]
    private static void RunPendingOnce()
    {
        if (System.IO.File.Exists(PendingMarker)) EditorApplication.update += TryRunPending;
    }

    private static void TryRunPending()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
            || EditorApplication.isUpdating) return;
        EditorApplication.update -= TryRunPending;
        System.IO.File.Delete(PendingMarker);
        try { Apply(); }
        catch (Exception exception) { Debug.LogException(exception); }
    }

    public static void RunBatch()
    {
        try
        {
            Apply();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static AnimationClip Build(Motion motion)
    {
        ConfigureSheet(motion);
        Sprite[] frames = LoadFrames(motion);
        if (frames.Length != motion.DurationsMs.Length)
            throw new InvalidOperationException(
                $"{motion.Sheet} 프레임 {frames.Length}장과 길이 {motion.DurationsMs.Length}개가 다릅니다.");

        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(motion.Clip);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, motion.Clip);
        }

        clip.frameRate = ClipFrameRate;
        var keys = new List<ObjectReferenceKeyframe>();
        int elapsed = 0;
        for (int i = 0; i < frames.Length; i++)
        {
            keys.Add(new ObjectReferenceKeyframe { time = elapsed / 1000f, value = frames[i] });
            elapsed += motion.DurationsMs[i];
        }
        // 마지막 프레임도 자기 길이만큼 유지되도록 끝 시각에 같은 Sprite를 한 번 더 둔다.
        keys.Add(new ObjectReferenceKeyframe { time = elapsed / 1000f, value = frames[frames.Length - 1] });

        EditorCurveBinding binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = motion.Loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void ConfigureSheet(Motion motion)
    {
        AssetDatabase.ImportAsset(motion.Sheet, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(motion.Sheet) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException($"PNG 시트를 불러올 수 없습니다: {motion.Sheet}");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = motion.Ppu;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        TextureImporterSettings textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(textureSettings);
        importer.SaveAndReimport();

        var factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider = factories.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            throw new InvalidOperationException("Sprite 데이터 공급자를 만들 수 없습니다.");
        dataProvider.InitSpriteEditorDataProvider();

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(motion.Sheet);
        int count = texture.width / motion.Size;
        string prefix = System.IO.Path.GetFileNameWithoutExtension(motion.Sheet);
        SpriteRect[] previous = dataProvider.GetSpriteRects();
        var rects = new SpriteRect[count];
        for (int i = 0; i < count; i++)
        {
            string name = $"{prefix}_{i:00}";
            SpriteRect old = previous.FirstOrDefault(rect => rect.name == name);
            // 프레임 중심을 기준점으로 둬야 기존 시트(50px 프레임의 가운데 40px)와 발 위치가 같다.
            rects[i] = new SpriteRect
            {
                name = name,
                rect = new Rect(i * motion.Size, 0f, motion.Size, motion.Size),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = old != null ? old.spriteID : GUID.Generate()
            };
        }
        dataProvider.SetSpriteRects(rects);
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadFrames(Motion motion) => AssetDatabase.LoadAllAssetsAtPath(motion.Sheet)
        .OfType<Sprite>()
        .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
        .ToArray();

    private static AnimatorController LoadOrCreate(string path)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        return controller != null ? controller : AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    // clips 순서: idle, move, attack, hit, death
    private static void ConfigureController(
        AnimatorController controller, AnimationClip[] clips, string idleName, string moveName)
    {
        AddParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Die", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = State(machine, idleName, clips[0], new Vector3(300, 0));
        AnimatorState move = State(machine, moveName, clips[1], new Vector3(300, -100));
        AnimatorState attack = State(machine, "Attack", clips[2], new Vector3(560, -160));
        AnimatorState hit = State(machine, "Hit", clips[3], new Vector3(560, -60));
        AnimatorState death = State(machine, ActorMotion.DeathState, clips[4], new Vector3(560, 60));
        machine.defaultState = idle;

        foreach (AnimatorState state in new[] { idle, move, attack, hit, death })
            foreach (AnimatorStateTransition transition in state.transitions.ToArray())
                state.RemoveTransition(transition);
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions.ToArray())
            machine.RemoveAnyStateTransition(transition);

        Immediate(idle.AddTransition(move)).AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
        Immediate(move.AddTransition(idle)).AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");
        foreach (AnimatorState action in new[] { attack, hit })
        {
            // 다음 이동 행동은 이전 행동의 남은 모션에 가로막히지 않는다.
            Immediate(action.AddTransition(move)).AddCondition(AnimatorConditionMode.If, 0, "IsMoving");
            AnimatorStateTransition back = Immediate(action.AddTransition(idle));
            back.hasExitTime = true;
            back.exitTime = 1f;
        }

        AnimatorStateTransition toDeath = Immediate(machine.AddAnyStateTransition(death));
        toDeath.canTransitionToSelf = false;
        toDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");
        Immediate(machine.AddAnyStateTransition(hit)).AddCondition(AnimatorConditionMode.If, 0, "Hit");
        Immediate(machine.AddAnyStateTransition(attack)).AddCondition(AnimatorConditionMode.If, 0, "Attack");
        EditorUtility.SetDirty(controller);
    }

    private static AnimatorStateTransition Immediate(AnimatorStateTransition transition)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0f;
        transition.exitTime = 0f;
        return transition;
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(s => s.name == name);
        if (state == null) state = machine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = true;
        return state;
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (controller.parameters.All(p => p.name != name)) controller.AddParameter(name, type);
    }

    private static void ApplyToScene(string scenePath, Sprite batIdle, RuntimeAnimatorController bat, Sprite playerIdle)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (MonsterSpawner spawner in roots.SelectMany(r => r.GetComponentsInChildren<MonsterSpawner>(true)))
            {
                var serialized = new SerializedObject(spawner);
                serialized.FindProperty("chessMonsterSprite").objectReferenceValue = batIdle;
                serialized.FindProperty("chessMonsterAnimatorController").objectReferenceValue = bat;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            foreach (ChessPlaytest playtest in roots.SelectMany(r => r.GetComponentsInChildren<ChessPlaytest>(true)))
            {
                var serialized = new SerializedObject(playtest);
                serialized.FindProperty("monsterSprite").objectReferenceValue = batIdle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            // 에디터 정지 화면에서도 새 대기 첫 프레임이 보이게 한다.
            foreach (Move player in roots.SelectMany(r => r.GetComponentsInChildren<Move>(true)))
            {
                SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
                if (renderer == null) continue;
                var serialized = new SerializedObject(renderer);
                serialized.FindProperty("m_Sprite").objectReferenceValue = playerIdle;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"씬을 저장하지 못했습니다: {scenePath}");
        }
        finally
        {
            if (openedForSetup)
                EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }
    }

    private static void ApplyToPrefab(string path, Sprite idle, RuntimeAnimatorController controller)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            Animator animator = root.GetComponentInChildren<Animator>(true);
            if (renderer == null || animator == null)
                throw new InvalidOperationException($"프리팹에 SpriteRenderer/Animator가 없습니다: {path}");
            renderer.sprite = idle;
            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    private static void Validate(AnimationClip[] clips, params AnimatorController[] controllers)
    {
        foreach (Motion motion in Player.Concat(Bat).Concat(Snake).Concat(Armadillo))
        {
            TextureImporter importer = AssetImporter.GetAtPath(motion.Sheet) as TextureImporter;
            Sprite[] frames = LoadFrames(motion);
            if (importer == null || !Mathf.Approximately(importer.spritePixelsPerUnit, motion.Ppu)
                || importer.filterMode != FilterMode.Point
                || frames.Length != motion.DurationsMs.Length
                || frames.Any(f => f.rect.width != motion.Size || f.rect.height != motion.Size))
                throw new InvalidOperationException($"시트 검증 실패: {motion.Sheet}");
        }
        foreach (AnimatorController controller in controllers)
        {
            string[] names = controller.parameters.Select(p => p.name).ToArray();
            if (!new[] { "IsMoving", "Attack", "Hit", "Die" }.All(names.Contains)
                || controller.animationClips.Length < 5)
                throw new InvalidOperationException($"컨트롤러 검증 실패: {controller.name}");
        }
        Debug.Log($"LIVELY_MOTION_SETUP_PASS clips={clips.Length} controllers={controllers.Length} "
            + string.Join(" ", clips.Select(c => $"{c.name}={c.length:0.00}s")));
    }
}
