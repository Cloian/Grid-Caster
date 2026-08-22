using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Aseprite;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectileSetupTool
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ProjectileSourcePath = "Assets/Projectiles/Attack/attack.aseprite";
    private const string ProjectileClipPath =
        "Assets/Projectiles/Animations/PlayerProjectileAttack.anim";
    private const string ProjectileControllerPath =
        "Assets/Projectiles/Controllers/PlayerProjectile.controller";
    private const float ProjectileFrameRate = 10f;

    [MenuItem("Tools/Combat/턴 투사체 애니메이션 설정 적용")]
    public static void ApplySetup()
    {
        ConfigureProjectileSource();
        Sprite[] projectileFrames = LoadProjectileFrames();
        AnimationClip projectileClip = CreateOrUpdateProjectileClip(projectileFrames);
        AnimatorController projectileController =
            CreateOrUpdateProjectileController(projectileClip);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedForSetup = !scene.IsValid() || !scene.isLoaded;

        if (openedForSetup)
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        try
        {
            MonsterSpawner spawner = FindComponentInScene<MonsterSpawner>(scene);

            if (spawner == null)
                throw new InvalidOperationException("SampleScene에서 MonsterSpawner를 찾을 수 없습니다.");

            ProjectileManager projectileManager = spawner.GetComponent<ProjectileManager>();

            if (projectileManager == null)
            {
                projectileManager = spawner.gameObject.AddComponent<ProjectileManager>();
            }

            SerializedObject serializedManager = new SerializedObject(projectileManager);
            serializedManager.FindProperty("moveSpeed").floatValue = 16.8f;
            serializedManager.FindProperty("projectileColor").colorValue =
                Color.white;
            serializedManager.FindProperty("sortingOrder").intValue = 15;
            serializedManager.FindProperty("projectileSprite").objectReferenceValue =
                projectileFrames[0];
            serializedManager.FindProperty("projectileAnimatorController").objectReferenceValue =
                projectileController;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(projectileManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (spawner.GetComponent<ProjectileManager>() == null)
                throw new InvalidOperationException("ProjectileManager가 저장되지 않았습니다.");

            SerializedObject validationManager = new SerializedObject(projectileManager);

            if (validationManager.FindProperty("projectileSprite").objectReferenceValue == null
                || validationManager.FindProperty("projectileAnimatorController")
                    .objectReferenceValue == null)
            {
                throw new InvalidOperationException("투사체 애니메이션 참조가 저장되지 않았습니다.");
            }

            Debug.Log("PROJECTILE_SETUP_VALIDATION_PASS");
        }
        finally
        {
            if (openedForSetup)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("PROJECTILE_SETUP_COMPLETE");
    }

    private static void ConfigureProjectileSource()
    {
        AssetDatabase.ImportAsset(
            ProjectileSourcePath,
            ImportAssetOptions.ForceSynchronousImport
        );

        if (AssetImporter.GetAtPath(ProjectileSourcePath) is not AsepriteImporter importer)
            throw new InvalidOperationException(
                $"Aseprite 투사체 에셋을 불러올 수 없습니다: {ProjectileSourcePath}"
            );

        bool changed = importer.importMode != FileImportModes.AnimatedSprite
            || importer.layerImportMode != LayerImportModes.MergeFrame
            || !Mathf.Approximately(importer.spritePixelsPerUnit, 32f)
            || importer.spriteMeshType != SpriteMeshType.FullRect
            || importer.pivotAlignment != SpriteAlignment.Center
            || importer.includeHiddenLayers
            || importer.filterMode != FilterMode.Point
            || importer.mipmapEnabled
            || importer.wrapMode != TextureWrapMode.Clamp
            || importer.generateAnimationClips
            || importer.generateModelPrefab
            || importer.generatePhysicsShape;

        if (!changed)
            return;

        importer.importMode = FileImportModes.AnimatedSprite;
        importer.layerImportMode = LayerImportModes.MergeFrame;
        importer.spritePixelsPerUnit = 32f;
        importer.spriteMeshType = SpriteMeshType.FullRect;
        importer.pivotAlignment = SpriteAlignment.Center;
        importer.includeHiddenLayers = false;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.generateAnimationClips = false;
        importer.generateModelPrefab = false;
        importer.generatePhysicsShape = false;
        importer.SaveAndReimport();
    }

    private static Sprite[] LoadProjectileFrames()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(ProjectileSourcePath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        if (frames.Length != 3)
            throw new InvalidOperationException(
                $"attack.aseprite에서 3프레임을 예상했지만 {frames.Length}개를 찾았습니다."
            );

        return frames;
    }

    private static AnimationClip CreateOrUpdateProjectileClip(Sprite[] frames)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ProjectileClipPath);

        if (clip == null)
        {
            clip = new AnimationClip { name = "PlayerProjectileAttack" };
            AssetDatabase.CreateAsset(clip, ProjectileClipPath);
        }

        clip.frameRate = ProjectileFrameRate;
        clip.wrapMode = WrapMode.Loop;

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[frames.Length];

        for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
        {
            keyframes[frameIndex] = new ObjectReferenceKeyframe
            {
                time = frameIndex / ProjectileFrameRate,
                value = frames[frameIndex]
            };
        }

        EditorCurveBinding spriteBinding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, spriteBinding, keyframes);

        AnimationClipSettings clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
        clipSettings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static AnimatorController CreateOrUpdateProjectileController(AnimationClip clip)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ProjectileControllerPath);

        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(
                ProjectileControllerPath
            );
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState attackState = stateMachine.states
            .Select(childState => childState.state)
            .FirstOrDefault(state => state.name == "AttackLoop");

        if (attackState == null)
            attackState = stateMachine.AddState("AttackLoop");

        attackState.motion = clip;
        attackState.speed = 1f;
        stateMachine.defaultState = attackState;
        EditorUtility.SetDirty(attackState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            T component = rootObject.GetComponentInChildren<T>(true);

            if (component != null)
                return component;
        }

        return null;
    }
}
