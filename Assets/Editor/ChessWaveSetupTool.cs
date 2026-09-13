using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ChessWaveSetupTool
{
    [MenuItem("Tools/Playtest/Apply Chess Waves to SampleScene")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play를 종료한 뒤 적용하세요.");
        Scene previous = SceneManager.GetActiveScene();
        Scene sample = SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.unity");
        bool openedSample = !sample.IsValid() || !sample.isLoaded;
        if (openedSample) sample = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Additive);
        Scene test = SceneManager.GetSceneByPath(ChessPlaytestSetupTool.ScenePath);
        bool openedTest = !test.IsValid() || !test.isLoaded;
        if (openedTest) test = EditorSceneManager.OpenScene(ChessPlaytestSetupTool.ScenePath, OpenSceneMode.Additive);
        try
        {
            MonsterSpawner spawner = Find<MonsterSpawner>(sample);
            Move player = Find<Move>(sample);
            ChessPlaytest settings = Find<ChessPlaytest>(test);
            if (!spawner || !player || !settings) throw new InvalidOperationException("씬의 스포너/플레이어/테스트 설정이 필요합니다.");
            SerializedObject source = new SerializedObject(settings);
            SerializedObject target = new SerializedObject(spawner);
            target.FindProperty("enableChessWaves").boolValue = true;
            target.FindProperty("chessMonsterSprite").objectReferenceValue = source.FindProperty("monsterSprite").objectReferenceValue;
            target.FindProperty("chessMonsterHealth").intValue = source.FindProperty("monsterHealth").intValue;
            foreach (string field in new[] { "knightActionInterval", "bishopMoveDistance", "bishopOrbitDistance", "bishopFireTurns", "bishopFireDamage" })
                target.FindProperty(field).intValue = source.FindProperty(field).intValue;
            target.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject input = new SerializedObject(player);
            input.FindProperty("cardinalOnly").boolValue = false;
            input.ApplyModifiedPropertiesWithoutUndo();
            // 새 패턴 필드를 테스트 씬에도 저장해 사용하지 않는 이전 회복/사격 간격을 제거한다.
            EditorSceneManager.MarkSceneDirty(test);
            if (!EditorSceneManager.SaveScene(test)) throw new InvalidOperationException("테스트 씬 저장 실패");
            EditorSceneManager.MarkSceneDirty(sample);
            if (!EditorSceneManager.SaveScene(sample)) throw new InvalidOperationException("SampleScene 저장 실패");
            Debug.Log("CHESS_WAVES_SETUP_PASS");
        }
        finally
        {
            if (openedTest) EditorSceneManager.CloseScene(test, true);
            if (openedSample) EditorSceneManager.CloseScene(sample, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    public static void ApplyAndValidateBatch()
    {
        Apply();
        ChessPlaytestValidation.ValidateBatch();
    }

    private static T Find<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).FirstOrDefault();
}
