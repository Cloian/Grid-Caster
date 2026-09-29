using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Aseprite;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArmadilloBishopSetupTool
{
    private const string SourcePath = "Assets/Monsters/Armadillo/armadillo.aseprite";
    private const string SheetPath = "Assets/Monsters/Armadillo/armadillo_sheet.png";
    private const string ClipPath =
        "Assets/Monsters/Animations/Armadillo/ArmadilloIdle.anim";
    private const string ControllerPath =
        "Assets/Monsters/Controllers/ArmadilloBishop.controller";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ChessScenePath = "Assets/Scenes/ChessPlaytest.unity";
    private const int FrameWidth = 70;
    private const int FrameHeight = 70;
    private const int FrameCount = 4;
    private const float FramesPerSecond = 5f;

    [MenuItem("Tools/Monster/Apply Armadillo Bishop Visual")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Play를 종료한 뒤 비숍 외형을 적용하세요.");

        ConfigureAsepriteSource();
        ConfigureSpriteSheet();
        Sprite[] frames = LoadFrames();
        AnimationClip clip = CreateOrUpdateClip(frames);
        AnimatorController controller = CreateOrUpdateController(clip);
        ApplyToScene(SampleScenePath, frames[0], controller);
        ApplyToScene(ChessScenePath, frames[0], controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate(frames, clip, controller);
    }

    private static void ConfigureAsepriteSource()
    {
        AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
        AsepriteImporter importer = AssetImporter.GetAtPath(SourcePath) as AsepriteImporter;
        if (importer == null)
            throw new InvalidOperationException("아르마딜로 Aseprite 원본을 불러올 수 없습니다.");

        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.spritePixelsPerUnit = 32f;
        importer.spriteMeshType = SpriteMeshType.FullRect;
        importer.pivotAlignment = SpriteAlignment.Center;
        importer.includeHiddenLayers = false;
        importer.generateAnimationClips = false;
        importer.generateModelPrefab = false;
        importer.generatePhysicsShape = false;
        importer.SaveAndReimport();
    }

    private static void ConfigureSpriteSheet()
    {
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("아르마딜로 PNG 시트를 불러올 수 없습니다.");

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();
        ISpriteEditorDataProvider dataProvider =
            factories.GetSpriteEditorDataProviderFromObject(importer);
        if (dataProvider == null)
            throw new InvalidOperationException("Sprite 데이터 공급자를 만들 수 없습니다.");

        dataProvider.InitSpriteEditorDataProvider();
        ISpriteFrameEditCapability editCapability =
            dataProvider.GetDataProvider<ISpriteFrameEditCapability>();
        if (editCapability == null)
            throw new InvalidOperationException("Sprite 편집 기능을 지원하지 않아 작업을 중단했습니다.");

        EditCapability capability = editCapability.GetEditCapability();
        if (!capability.HasCapability(EEditCapability.CreateAndDeleteSprite)
            || !capability.HasCapability(EEditCapability.EditSpriteName)
            || !capability.HasCapability(EEditCapability.EditSpriteRect)
            || !capability.HasCapability(EEditCapability.EditPivot))
            throw new InvalidOperationException("필요한 Sprite 편집 기능을 지원하지 않아 작업을 중단했습니다.");

        SpriteRect[] previousRects = dataProvider.GetSpriteRects();
        SpriteRect[] spriteRects = new SpriteRect[FrameCount];
        for (int index = 0; index < FrameCount; index++)
        {
            SpriteRect previous = previousRects.FirstOrDefault(rect =>
                string.Equals(rect.name, $"armadillo_{index}", StringComparison.Ordinal));
            spriteRects[index] = new SpriteRect
            {
                name = $"armadillo_{index}",
                rect = new Rect(index * FrameWidth, 0f, FrameWidth, FrameHeight),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
                spriteID = previous != null ? previous.spriteID : GUID.Generate()
            };
        }

        dataProvider.SetSpriteRects(spriteRects);
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadFrames()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();
        if (frames.Length != FrameCount)
            throw new InvalidOperationException(
                $"아르마딜로 프레임은 {FrameCount}장이어야 하지만 {frames.Length}장입니다."
            );
        return frames;
    }

    private static AnimationClip CreateOrUpdateClip(Sprite[] frames)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, ClipPath);
        }

        clip.name = "ArmadilloIdle";
        clip.frameRate = FramesPerSecond;
        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        ObjectReferenceKeyframe[] keyframes = frames.Select((frame, index) =>
            new ObjectReferenceKeyframe
            {
                time = index / FramesPerSecond,
                value = frame
            }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateOrUpdateController(AnimationClip clip)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState idleState = stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Idle");
        if (idleState == null)
            idleState = stateMachine.AddState("Idle");
        idleState.motion = clip;
        stateMachine.defaultState = idleState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ApplyToScene(
        string scenePath,
        Sprite firstFrame,
        RuntimeAnimatorController controller)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;
        if (openedForSetup)
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

        try
        {
            MonsterSpawner spawner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonsterSpawner>(true))
                .FirstOrDefault();
            if (spawner == null)
                throw new InvalidOperationException($"MonsterSpawner를 찾을 수 없습니다: {scenePath}");

            SerializedObject serializedSpawner = new SerializedObject(spawner);
            serializedSpawner.FindProperty("bishopMonsterSprite").objectReferenceValue = firstFrame;
            serializedSpawner.FindProperty("bishopMonsterAnimatorController").objectReferenceValue =
                controller;
            serializedSpawner.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
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

    private static void Validate(
        Sprite[] frames,
        AnimationClip clip,
        RuntimeAnimatorController controller)
    {
        TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        AsepriteImporter sourceImporter =
            AssetImporter.GetAtPath(SourcePath) as AsepriteImporter;
        if (sourceImporter == null
            || !Mathf.Approximately(sourceImporter.spritePixelsPerUnit, 32f)
            || sourceImporter.generateAnimationClips
            || sourceImporter.generateModelPrefab
            || importer == null
            || importer.spriteImportMode != SpriteImportMode.Multiple
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 32f)
            || importer.filterMode != FilterMode.Point
            || frames.Any(frame => frame.rect.width != FrameWidth
                || frame.rect.height != FrameHeight)
            || !Mathf.Approximately(clip.frameRate, FramesPerSecond)
            || controller == null)
        {
            throw new InvalidOperationException("아르마딜로 비숍 에셋 검증에 실패했습니다.");
        }

        Debug.Log(
            $"ARMADILLO_BISHOP_SETUP_PASS frames={frames.Length} fps={clip.frameRate} "
            + "ppu=32 filter=Point scenes=2"
        );
    }
}
