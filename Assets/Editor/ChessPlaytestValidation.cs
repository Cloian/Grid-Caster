using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 실제 Play 모드에서 이동/공격 진입점을 실행한다. 테스트 씬의 저장된 값은 수정하지 않는다.
[InitializeOnLoad]
public static class ChessPlaytestValidation
{
    private const string PendingKey = "GridCaster.ChessValidation";
    private static IEnumerator routine;
    private static double deadline;
    private static readonly List<string> checks = new List<string>();
    private static readonly Vector3Int[] Directions =
        { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down };

    static ChessPlaytestValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                checks.Clear();
                deadline = EditorApplication.timeSinceStartup + 120;
                routine = Run();
                EditorApplication.update += Tick;
            }
        };

    }

    [MenuItem("Tools/Playtest/Validate Chess Playtest")]
    public static void Validate()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning("검증은 Play를 종료한 뒤 실행하세요.");
            return;
        }
        ChessPlaytestSetupTool.OpenPlaytest();
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    public static void ValidateBatch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ChessPlaytestSetupTool.ScenePath);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
                throw new InvalidOperationException("Play 검증이 중단되었거나 제한 시간을 넘었습니다.");
            if (routine.MoveNext()) return;
            Complete("PASS");
        }
        catch (Exception exception)
        {
            checks.Add(exception.ToString());
            Complete("FAIL");
        }
    }

    private static void Complete(string status)
    {
        EditorApplication.update -= Tick;
        string report = "CHESS_PLAYTEST_VALIDATION_" + status + "\n" + string.Join("\n", checks);
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "GridCasterChessValidation.txt"), report);
        if (status == "PASS") Debug.Log(report); else Debug.LogError(report);
        routine = null;
        EditorApplication.isPlaying = false;
        if (Application.isBatchMode) EditorApplication.Exit(status == "PASS" ? 0 : 1);
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks.Add("OK " + message);
    }

    private static IEnumerator Run()
    {
        yield return null;
        ChessPlaytest test = UnityEngine.Object.FindAnyObjectByType<ChessPlaytest>();
        while (test != null && !test.IsReady) yield return null;
        Check(test != null, "테스트 씬 초기화");
        Move player = UnityEngine.Object.FindAnyObjectByType<Move>();
        GridManager grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        ProjectileManager projectiles = UnityEngine.Object.FindAnyObjectByType<ProjectileManager>();
        Check(grid.GroundTilemap.cellBounds.size == new Vector3Int(12, 12, 1), "외벽 포함 12×12");
        Check(test.Encounter.Count == 3 && player.GridPosition == new Vector3Int(5, 5, 0), "고정 배치 3종 및 플레이어");
        Check(GameObject.Find("ThreatTile") == null, "독립 테스트에서 다음 이동 예고 타일 없음");
        Check(!grid.IsWalkableCell(Vector3Int.zero) && grid.IsWalkableCell(new Vector3Int(1, 1, 0)), "경계 벽 충돌");
        Check(!player.TryPerformAction(PlayerActionSelectionMode.Move, Vector3Int.right), "시작 전 입력 차단");
        GameObject batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Monsters/Prefabs/Bat.prefab");
        Check(batPrefab != null && batPrefab.GetComponent<MonsterMovement>().MovementPattern
            == MonsterMovementPattern.EightDirection, "실제 박쥐 프리팹은 8방향 이동 설정");
        MonsterSpawner spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        BishopFireTrail fire = spawner.FireTrail;
        MonsterMovement knight = test.Encounter[0];
        MonsterMovement bishop = test.Encounter[1];
        MonsterMovement fixtureRook = test.Encounter[2];
        bool done = false;

        // 일반 추적형도 실제 행동/예약/연출 완료까지 실행해 막힘과 최단 경로를 검증한다.
        GameObject snakeObject = new GameObject("PathfindingValidationSnake");
        MonsterMovement snake = snakeObject.AddComponent<MonsterMovement>();
        snake.Initialize(player.transform, grid, 1234);
        snake.ConfigurePlaytest(MonsterMovementPattern.CardinalFour, 3, 1234);
        Check(snake.SpawnOrder == 1234, "일반 적 생성 순서 보존");
        player.ResetPlaytest(new Vector3Int(7, 5, 0));
        snake.ApplyKnockback(new Vector3Int(2, 5, 0));
        bool RingBlocked(Vector3Int cell) => Mathf.Abs(cell.x - 7) + Mathf.Abs(cell.y - 5) == 1;
        for (int i = 0; i < 4; i++)
        {
            done = false;
            snake.TakeTurn(() => done = true, (_, destination) => !RingBlocked(destination), RingBlocked);
            while (!done) yield return null;
        }
        Check(snake.GridPosition == new Vector3Int(5, 5, 0), "포위된 목표에도 접근 후 배회 없이 대기");
        snake.ApplyKnockback(new Vector3Int(2, 5, 0));
        bool WallBlocked(Vector3Int cell) => cell.x == 3 && cell.y >= 2 && cell.y <= 8;
        int pathSteps = 0;
        while (Mathf.Abs(snake.GridPosition.x - 7) + Mathf.Abs(snake.GridPosition.y - 5) > 1)
        {
            Vector3Int origin = snake.GridPosition;
            done = false;
            snake.TakeTurn(() => done = true, (_, destination) => !WallBlocked(destination), WallBlocked);
            while (!done) yield return null;
            Vector3Int delta = snake.GridPosition - origin;
            Check(Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1, "뱀은 우회 중에도 상하좌우 한 칸");
            Check(++pathSteps <= 12, "장벽 우회 중 왕복 또는 정지 없음");
        }
        Check(pathSteps == 12, "장벽을 돌아 공격 인접 칸까지 최단 12걸음");
        snake.ApplyKnockback(new Vector3Int(3, 3, 0));
        player.ResetPlaytest(new Vector3Int(5, 5, 0));
        Vector3Int defeatedFrontCell = new Vector3Int(4, 3, 0);
        done = false;
        snake.TakeTurn(
            () => done = true,
            (_, destination) => true,
            _ => false,
            cell => cell == defeatedFrontCell
        );
        Check(snake.GridPosition == defeatedFrontCell,
            "뱀은 같은 최단 경로라면 앞 적이 죽어 비운 칸으로 전진");
        while (!done) yield return null;
        ValidateTransitReservations(spawner, bishop, snake);
        snake.ConfigurePlaytest(MonsterMovementPattern.EightDirection, 3, 1234);
        snake.ApplyKnockback(new Vector3Int(2, 5, 0));
        player.ResetPlaytest(new Vector3Int(4, 7, 0));
        Vector3Int cornerWall = new Vector3Int(3, 5, 0);
        UnityEngine.Tilemaps.TileBase savedTile = grid.GroundTilemap.GetTile(cornerWall);
        try
        {
            grid.GroundTilemap.SetTile(cornerWall, null);
            done = false;
            snake.TakeTurn(() => done = true, (_, __) => true, _ => false);
            Check(snake.GridPosition != new Vector3Int(3, 6, 0), "박쥐는 막힌 지형 모서리를 대각선으로 통과하지 않음");
            while (!done) yield return null;
        }
        finally { grid.GroundTilemap.SetTile(cornerWall, savedTile); }
        snake.ApplyKnockback(new Vector3Int(4, 2, 0));
        player.ResetPlaytest(new Vector3Int(4, 7, 0));
        done = false;
        IsolatedTurn(snake, test, () => done = true);
        Check(snake.GridPosition == new Vector3Int(4, 3, 0),
            "박쥐는 같은 열 목표로 직선 한 칸 이동");
        while (!done) yield return null;
        snake.ApplyKnockback(new Vector3Int(2, 4, 0));
        player.ResetPlaytest(new Vector3Int(7, 4, 0));
        done = false;
        IsolatedTurn(snake, test, () => done = true);
        Check(snake.GridPosition == new Vector3Int(3, 4, 0),
            "박쥐는 같은 행 목표로 직선 한 칸 이동");
        while (!done) yield return null;
        snake.ApplyKnockback(new Vector3Int(2, 4, 0));
        player.ResetPlaytest(new Vector3Int(7, 9, 0));
        done = false;
        IsolatedTurn(snake, test, () => done = true);
        Check(snake.GridPosition == new Vector3Int(3, 5, 0),
            "박쥐는 대각선 목표로 대각선 한 칸 이동");
        while (!done) yield return null;
        UnityEngine.Object.Destroy(snakeObject);
        yield return null;

        PlaceFixture(test, player, new Vector3Int(6, 4, 0), new Vector3Int(3, 3, 0));
        done = false;
        IsolatedTurn(knight, test, () => done = true);
        Check(player.CurrentHealth == 10, "나이트 착지 전 피해 없음");
        Check(knight.GridPosition == new Vector3Int(5, 4, 0), "나이트 빈 L자 착지 칸 선택");
        while (!done) yield return null;
        Check(player.CurrentHealth == 9, "착지 십자 피해 1회");
        PlaceFixture(test, player, new Vector3Int(6, 5, 0), new Vector3Int(3, 3, 0));
        done = false;
        IsolatedTurn(knight, test, () => done = true, cell => cell != new Vector3Int(5, 4, 0));
        while (!done) yield return null;
        Check(player.CurrentHealth == 10, "착지 대각선은 십자 피해 없음");
        done = false;
        IsolatedTurn(knight, test, () => done = true, _ => true);
        Check(done && player.CurrentHealth == 10, "착지 불가이면 범위공격 없음");

        PlaceFixture(test, player, new Vector3Int(5, 5, 0), new Vector3Int(10, 9, 0));
        bishop.ApplyKnockback(new Vector3Int(2, 2, 0));
        fire.Clear();
        HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
        for (int turn = 0; turn < 8; turn++)
        {
            Vector3Int origin = bishop.GridPosition;
            fire.AdvanceTurn();
            done = false;
            IsolatedTurn(bishop, test, () => done = true);
            while (!done) yield return null;
            Vector3Int delta = bishop.GridPosition - origin;
            Check(Mathf.Abs(delta.x) == Mathf.Abs(delta.y) && delta.x != 0 && Mathf.Abs(delta.x) <= 4,
                "비숍은 매 턴 최대 4칸 대각선 횡단");
            Vector3Int step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
            for (Vector3Int cell = origin; cell != bishop.GridPosition + step; cell += step)
                Check(fire.HasFire(cell), "비숍 출발점/경로/도착점에 불길");
            Check(new[]
            {
                new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
                new Vector3Int(-1, 1, 0), new Vector3Int(-1, -1, 0)
            }.Any(direction => fire.HasFire(player.GridPosition + direction)),
                "비숍이 플레이어 주위 대각선에 차단 불길 생성");
            visited.Add(bishop.GridPosition);
        }
        Check(visited.Count >= 5 && player.CurrentHealth == 10, "비숍 주변 순회 및 몸통 직접 피해 없음");
        fire.Clear();
        Vector3Int fireCell = new Vector3Int(4, 4, 0);
        fire.AddFire(fireCell);
        fire.AddFire(fireCell);
        Check(fire.Count == 1 && fire.RemainingTurns(fireCell) == 3, "겹친 불길은 수명 갱신만");
        double idleUntil = EditorApplication.timeSinceStartup + 0.2;
        while (EditorApplication.timeSinceStartup < idleUntil) yield return null;
        Check(fire.RemainingTurns(fireCell) == 3, "입력 대기 중 불길 수명 정지");
        fire.AdvanceTurn(); fire.AdvanceTurn();
        Check(fire.HasFire(fireCell), "불길은 3번째 다음 행동까지 유지");
        fire.AdvanceTurn();
        Check(!fire.HasFire(fireCell), "불길 3턴 만료");

        PlaceFixture(test, player, new Vector3Int(7, 5, 0), new Vector3Int(8, 8, 0));
        fixtureRook.ApplyKnockback(new Vector3Int(5, 11, 0));
        projectiles.ClearProjectiles();
        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true);
        while (!done) yield return null;
        ChessMonsterBehaviour fixtureRookBehaviour = fixtureRook.GetComponent<ChessMonsterBehaviour>();
        Check(grid.IsTopBoundaryWallCell(fixtureRook.GridPosition)
            && fixtureRook.GridPosition.x == 7
            && fixtureRookBehaviour.IsRookCharged
            && fixtureRookBehaviour.RookTargetColumn == 7
            && EnemyBullets().Length == 0,
            "룩은 상단 벽에서 플레이어 열로 이동해 한 행동 장전");
        Check(!spawner.TryKnockbackMonster(fixtureRook, Vector3Int.down),
            "성벽 쇠뇌 룩은 지상 넉백 면역");

        player.ResetPlaytest(new Vector3Int(6, 5, 0));
        int healthBeforeDodgingShot = player.CurrentHealth;
        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true);
        while (!done) yield return null;
        Check(player.CurrentHealth == healthBeforeDodgingShot
            && !fixtureRookBehaviour.IsRookCharged
            && EnemyBullets().Length == 0,
            "룩은 장전 열을 즉시 사격하며 이동한 플레이어를 재조준하지 않음");

        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true);
        while (!done) yield return null;
        Check(fixtureRookBehaviour.IsRookCharged
            && fixtureRookBehaviour.RookTargetColumn == player.GridPosition.x,
            "룩은 다음 장전에서 새 플레이어 열을 고정");
        int healthBeforeHit = player.CurrentHealth;
        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true);
        while (!done) yield return null;
        Check(player.CurrentHealth == healthBeforeHit - 2
            && fixtureRookBehaviour.PatternExecutions >= 2
            && EnemyBullets().Length == 0,
            "룩 발사는 보드 잔류 탄 없이 예고 열에 즉시 피해 2");

        player.ResetPlaytest(new Vector3Int(fixtureRook.GridPosition.x, 5, 0));
        CharacterHealth rookHealth = fixtureRook.GetComponent<CharacterHealth>();
        int rookHealthBeforeShot = rookHealth.CurrentHealth;
        bool wallShotCompleted = false;
        Check(projectiles.SpawnPlayerProjectile(player.GridPosition, Vector3Int.up, 1, 0,
                null, () => wallShotCompleted = true),
            "상단 성벽 룩을 향한 플레이어 투사체 생성");
        while (!wallShotCompleted) yield return null;
        Check(rookHealth.CurrentHealth == rookHealthBeforeShot - 1,
            "플레이어 기본공격은 벽 충돌 전에 성벽 룩을 타격");
        // 이후 검사는 불길·이동만 측정하므로 새 즉시 사격이 결과에 섞이지 않게 룩 픽스처를 정리한다.
        fixtureRook.TakeDamage(rookHealth.CurrentHealth);
        yield return null;

        PlaceFixture(test, player, new Vector3Int(4, 4, 0), new Vector3Int(10, 9, 0));
        projectiles.ClearProjectiles();
        fire.Clear();
        fire.AddFire(new Vector3Int(5, 4, 0));
        int healthOnEntry = -1;
        Action entered = () => healthOnEntry = player.CurrentHealth;
        player.PlayerMoved += entered;
        player.ProcessInputFrame(false, true, true, false,
            Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(new Vector3Int(5, 4, 0))));
        while (!player.CanAct) yield return null;
        player.PlayerMoved -= entered;
        Check(healthOnEntry == 9, "실제 클릭 이동으로 불길 진입 피해 1회");
        int healthBeforeAttack = player.CurrentHealth;
        fire.AddFire(player.GridPosition);
        player.ProcessInputFrame(true, false, true, false,
            Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(player.GridPosition + Vector3Int.left)));
        while (!player.CanAct) yield return null;
        Check(player.CurrentHealth == healthBeforeAttack, "불길 위에서 공격만 하면 진입 피해 반복 없음");

        PlaceFixture(test, player, new Vector3Int(5, 4, 0), new Vector3Int(10, 9, 0));
        fire.Clear();
        projectiles.ClearProjectiles();
        UltimateGauge playtestGauge = player.gameObject.AddComponent<UltimateGauge>();
        player.GetComponent<RunProgressionSystem>()
            .SetMovementArtForPlaytest(PlayerMovementArt.Rook);
        playtestGauge.AddGauge(playtestGauge.MaxGauge);
        Vector3Int chessTeleportTarget = new Vector3Int(8, 4, 0);
        Check(player.TryPerformAction(PlayerActionSelectionMode.MovementArt,
            chessTeleportTarget - player.GridPosition), "체스 3종 앞에서 룩 이동술");
        while (!player.CanAct) yield return null;
        Check(player.GridPosition == chessTeleportTarget, "체스 테스트 룩 이동술 착지 좌표");
        foreach (MonsterMovement chessMonster in test.Encounter)
        {
            if (chessMonster == null || chessMonster.IsDead) continue;
            int horizontal = chessTeleportTarget.x - chessMonster.GridPosition.x;
            if (horizontal == 0) continue;
            bool spriteFacesRight = new SerializedObject(chessMonster).FindProperty("spriteFacesRight").boolValue;
            bool expectedFlip = horizontal < 0 ? spriteFacesRight : !spriteFacesRight;
            Check(chessMonster.GetComponent<SpriteRenderer>().flipX == expectedFlip,
                "나이트·비숍·룩의 새 위치 방향 바라보기");
        }

        // 독립 패턴 검증 후 원래 고정 배치를 다시 로드해 세션/초기화를 확인한다.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            ChessPlaytestSetupTool.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null; yield return null;
        test = UnityEngine.Object.FindAnyObjectByType<ChessPlaytest>();
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        spawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        test.StartRound();
        player.GetComponent<CharacterHealth>().Initialize(1000);
        Check(player.TryPerformAction(PlayerActionSelectionMode.Move, new Vector3Int(1, -1, 0)), "대각선 이동 요청");
        while (!player.CanAct) yield return null;
        Check(player.GridPosition == new Vector3Int(6, 4, 0), "대각선 한 칸 이동");
        while (test.IsRunning)
        {
            int turn = test.TurnCount;
            Vector3Int direction = Directions.First(d => !RayHasMonster(player.GridPosition, d, test, grid));
            Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, direction), "혼합 패턴 턴 진행");
            while (test.IsRunning && test.TurnCount == turn) yield return null;
            CheckOccupancy(test, player, grid);
        }
        Check(test.TurnCount == 20 && !player.CanAct, "테스트 20턴 종료");
        test.StartSecondRound();
        yield return null;
        Check(test.IsReady && player.CurrentHealth == 10 && spawner.FireTrail.Count == 0,
            "2회차 초기화 시 체력/불길 복원");
        test.StartRound();
        player.TakeDamage(10000);
        // 사망 처리와 화면 복기는 기존 구성 그대로이며 보드가 더 진행되지 않는다.
        yield return null;
        Check(!player.CanAct && GameObject.Find("PlayerGrave") != null, "사망 입력 차단과 묘비");

        // 공통 이동/투사체 코드가 원래 씬에서도 동작하는지 최소 회귀 검증한다.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        yield return null;
        player = UnityEngine.Object.FindAnyObjectByType<Move>();
        grid = UnityEngine.Object.FindAnyObjectByType<GridManager>();
        MonsterSpawner normalSpawner = UnityEngine.Object.FindAnyObjectByType<MonsterSpawner>();
        GameHudController hud = UnityEngine.Object.FindAnyObjectByType<GameHudController>();
        Check(player != null && grid != null && normalSpawner != null && hud != null,
            "SampleScene 기존 컴포넌트 로드");
        CameraController boardCameraController = UnityEngine.Object.FindAnyObjectByType<CameraController>();
        Camera boardCamera = Camera.main;
        Check(boardCameraController != null && boardCamera != null
            && boardCamera.rect == new Rect(0f, 0f, 1f, 1f),
            "보드 카메라가 전체 화면 사용");
        Check(typeof(CameraController).GetMethod("HandleZoom",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null,
            "마우스 스크롤 확대/축소 기능 제거");
        BoundsInt boardBounds = grid.GroundTilemap.cellBounds;
        Vector3 boardWorldMin = grid.GroundTilemap.CellToWorld(boardBounds.min);
        Vector3 boardWorldMax = grid.GroundTilemap.CellToWorld(boardBounds.max);
        Vector3 expectedBoardCenter = (boardWorldMin + boardWorldMax) * 0.5f;
        Check(Vector2.Distance(boardCamera.transform.position, expectedBoardCenter) < 0.01f,
            "카메라가 전체 맵 중앙에 고정");
        Vector3 minViewport = boardCamera.WorldToViewportPoint(boardWorldMin);
        Vector3 maxViewport = boardCamera.WorldToViewportPoint(boardWorldMax);
        Check(minViewport.x >= 0f && minViewport.y >= 0f
            && maxViewport.x <= 1f && maxViewport.y <= 1f,
            "시작 화면에 외벽을 포함한 타일맵 전체 표시");
        Check(GameObject.Find("MinimapHud") == null
            && GameObject.Find("CameraControlHud") == null,
            "미니맵과 플레이어 고정 UI 제거");
        hud.SelectTrait(0);
        Check(player.CanAct && normalSpawner.CurrentWave == 1, "SampleScene 특성 선택과 첫 웨이브");
        Check(normalSpawner.StageFlow != null
            && normalSpawner.StageFlow.State == StageFlowState.Combat
            && normalSpawner.StageFlow.FinalWave == WaveTemplateCatalog.FinalWave,
            "20웨이브 스테이지 흐름과 전투 상태");
        int[] expectedWaveCounts =
        {
            4, 5, 6, 7, 8, 9, 10, 11, 12, 14,
            13, 15, 16, 17, 18, 20, 21, 22, 23, 25
        };
        int[] relicWaves = { 4, 8, 12, 16 };
        for (int wave = 1; wave <= expectedWaveCounts.Length; wave++)
        {
            WaveTemplate template = WaveTemplateCatalog.Get(wave);
            int expectedTier = wave < 4 ? 1 : wave < 8 ? 2 : wave < 12 ? 3 : wave < 16 ? 4 : 5;
            Check(template.Monsters.Count == expectedWaveCounts[wave - 1],
                $"웨이브 {wave} 단계형 총 마릿수");
            Check(template.DifficultyTier == expectedTier,
                $"웨이브 {wave} 유물 기준 난이도 단계");
            Check(template.IsRelicWave == relicWaves.Contains(wave),
                $"웨이브 {wave} 유물 웨이브 표시");
            if (relicWaves.Contains(wave))
            {
                WaveTemplate previous = WaveTemplateCatalog.Get(wave - 1);
                Check(template.DifficultyTier == previous.DifficultyTier + 1
                    && template.Monsters.Count > previous.Monsters.Count,
                    $"웨이브 {wave} 유물 획득과 동시에 난이도 상승");
            }

            bool hasKnight = template.Monsters.Contains(MonsterMovementPattern.Knight);
            bool hasBishop = template.Monsters.Contains(MonsterMovementPattern.Bishop);
            bool hasRook = template.Monsters.Contains(MonsterMovementPattern.Rook);
            Check(hasKnight == (wave >= 4), $"웨이브 {wave} 나이트 순차 해금");
            Check(hasBishop == (wave >= 8), $"웨이브 {wave} 비숍 순차 해금");
            Check(hasRook == (wave >= 12), $"웨이브 {wave} 룩 순차 해금");
        }
        Check(WaveTemplateCatalog.Get(10).IsMidBossWave
            && !WaveTemplateCatalog.Get(9).IsMidBossWave
            && !WaveTemplateCatalog.Get(11).IsMidBossWave,
            "10웨이브 중간보스 조합 표시");
        int[] standardRewardWaves = { 2, 6, 10, 14, 18 };
        for (int wave = 1; wave < WaveTemplateCatalog.FinalWave; wave++)
            Check(RunProgressionCatalog.IsStandardUpgradeWave(wave)
                == standardRewardWaves.Contains(wave), $"웨이브 {wave} 일반 강화 보상 간격");
        RunProgressionSystem progression = player.GetComponent<RunProgressionSystem>();
        Check(progression != null && RunProgressionCatalog.Upgrades.Count == 16
            && RunProgressionCatalog.AdvancedUpgrades.Count == 12
            && RunProgressionCatalog.Relics.Count == 21,
            "일반 강화 16종·상급 강화 12종·유물 21종 카탈로그");
        for (int tier = 1; tier <= 4; tier++)
            Check(RunProgressionCatalog.AdvancedUpgrades.Count(item => item.RewardTier == tier) == 3,
                $"유물 {tier}단계 상급 강화 3종");
        RunProgressionUiController progressionUi = hud.GetComponent<RunProgressionUiController>();
        GameObject buildHud = progressionUi != null
            ? progressionUi.transform.Find("BuildHudPanel")?.gameObject : null;
        GameObject relicHud = progressionUi != null
            ? progressionUi.transform.Find("RelicHudPanel")?.gameObject : null;
        Check(buildHud != null && relicHud != null
            && progressionUi.transform.Find("UpgradeSelectionPanel") != null,
            "좌측 강화 빌드와 우측 유물/선택 UI 생성");
        Vector3[] buildCorners = new Vector3[4];
        Vector3[] relicCorners = new Vector3[4];
        buildHud.GetComponent<RectTransform>().GetWorldCorners(buildCorners);
        relicHud.GetComponent<RectTransform>().GetWorldCorners(relicCorners);
        float boardScreenMinX = boardCamera.WorldToScreenPoint(boardWorldMin).x;
        float boardScreenMaxX = boardCamera.WorldToScreenPoint(boardWorldMax).x;
        Check(buildCorners[2].x <= boardScreenMinX + 1f
            && relicCorners[0].x >= boardScreenMaxX - 1f,
            "좌우 성장 HUD가 보드 화면을 가리지 않음");
        RectTransform waveHud = GameObject.Find("WaveHud")?.GetComponent<RectTransform>();
        RectTransform timeHud = GameObject.Find("TimeHud")?.GetComponent<RectTransform>();
        RectTransform healthHud = GameObject.Find("HealthHud")?.GetComponent<RectTransform>();
        RectTransform ultimateHud = GameObject.Find("UltimateFrame")?.GetComponent<RectTransform>();
        RectTransform traitHud = GameObject.Find("TraitHud")?.GetComponent<RectTransform>();
        RectTransform attackHud = GameObject.Find("AttackActionFrame")?.GetComponent<RectTransform>();
        RectTransform moveHud = GameObject.Find("MoveActionFrame")?.GetComponent<RectTransform>();
        Text ultimateShortcut = GameObject.Find("UltimateShortcutKey")?.GetComponent<Text>();
        Check(waveHud != null && timeHud != null
            && Mathf.Approximately(waveHud.anchoredPosition.x, 266f)
            && Mathf.Approximately(timeHud.anchoredPosition.x, 424f)
            && Mathf.Approximately(waveHud.anchoredPosition.y, timeHud.anchoredPosition.y),
            "웨이브와 시간이 해상도에 맞춰 보드 중앙 상단 묶음으로 정렬");
        Check(ultimateHud != null && healthHud != null && traitHud != null
            && attackHud != null && moveHud != null
            && Mathf.Approximately(ultimateHud.anchoredPosition.x, 70f)
            && Mathf.Approximately(healthHud.anchoredPosition.x, 174f)
            && Mathf.Approximately(traitHud.anchoredPosition.x, 502f)
            && Mathf.Approximately(attackHud.anchoredPosition.x, 602f)
            && Mathf.Approximately(moveHud.anchoredPosition.x, 702f)
            && Mathf.Approximately(ultimateHud.anchoredPosition.y, healthHud.anchoredPosition.y)
            && Mathf.Approximately(healthHud.anchoredPosition.y, traitHud.anchoredPosition.y)
            && Mathf.Approximately(traitHud.anchoredPosition.y, attackHud.anchoredPosition.y)
            && Mathf.Approximately(attackHud.anchoredPosition.y, moveHud.anchoredPosition.y),
            "하단 전투 HUD가 해상도에 맞춰 보드 중앙 아래에 밀착 정렬");
        Check(ultimateShortcut != null && ultimateShortcut.text == "F",
            "이동술 카드에 F 단축키 배지 표시");
        Vector3[] topHudCorners = new Vector3[4];
        Vector3[] bottomHudCorners = new Vector3[4];
        waveHud.GetWorldCorners(topHudCorners);
        moveHud.GetWorldCorners(bottomHudCorners);
        float boardScreenMinY = boardCamera.WorldToScreenPoint(boardWorldMin).y;
        float boardScreenMaxY = boardCamera.WorldToScreenPoint(boardWorldMax).y;
        float referenceScale = Screen.height / 1080f;
        Check(topHudCorners[0].y >= boardScreenMaxY
            && topHudCorners[0].y - boardScreenMaxY <= 32f * referenceScale,
            "상단 HUD가 윗벽 바로 위 안전 간격에 위치");
        Check(bottomHudCorners[1].y <= boardScreenMinY
            && boardScreenMinY - bottomHudCorners[1].y <= 32f * referenceScale,
            "하단 HUD가 아랫벽 바로 아래 안전 간격에 위치");
        Check(normalSpawner.ActiveMonsters.All(monster =>
        {
            Vector3 viewport = boardCamera.WorldToViewportPoint(monster.transform.position);
            return viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
        }), "첫 웨이브 몬스터가 시작 화면 안에 모두 표시");

        Vector3Int[] previewDirections =
        {
            Vector3Int.right,
            new Vector3Int(1, 1, 0),
            Vector3Int.up,
            new Vector3Int(-1, 1, 0),
            Vector3Int.left,
            new Vector3Int(-1, -1, 0),
            Vector3Int.down,
            new Vector3Int(1, -1, 0)
        };
        Vector3Int previewDirection = previewDirections.First(direction =>
        {
            for (Vector3Int cell = player.GridPosition + direction;
                grid.IsWalkableCell(cell); cell += direction)
                if (normalSpawner.TryGetMonsterAtCell(cell, out _)) return false;
            return true;
        });
        player.SelectAttackMode();
        const System.Reflection.BindingFlags privateInstance =
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(Move).GetMethod("ShowAttackPathPreview", privateInstance).Invoke(
            player, new object[] { player.GridPosition + previewDirection });
        DirectionalActionIndicator indicator = player.GetComponent<DirectionalActionIndicator>();
        int expectedPreviewCells = 0;
        for (Vector3Int cell = player.GridPosition + previewDirection;
            grid.IsWalkableCell(cell); cell += previewDirection) expectedPreviewCells++;
        Check(indicator.ActivePathCount == expectedPreviewCells,
            "공격 노란 경로는 맵 끝 외벽 직전까지만 표시");
        Check(player.transform.Cast<Transform>()
            .Where(child => child.name.StartsWith("AttackPath_") && child.gameObject.activeSelf)
            .All(child => grid.IsWalkableCell(grid.WorldToCell(child.position))),
            "공격 경로 표시가 외곽 벽 타일을 점유하지 않음");
        player.CancelSelection();
        Vector3Int oldPlayerCell = player.GridPosition;
        Vector3Int moveDirection = Directions.First(d => grid.IsWalkableCell(oldPlayerCell + d)
            && !normalSpawner.TryGetMonsterAtCell(oldPlayerCell + d, out _));
        player.SelectAttackMode();
        typeof(Move).GetField("choosingEchoDirection", privateInstance)
            .SetValue(player, true);
        Vector3Int echoOrigin = player.GridPosition;
        Check(!player.TryPerformAction(PlayerActionSelectionMode.Move, moveDirection)
            && player.GridPosition == echoOrigin
            && player.SelectionMode == PlayerActionSelectionMode.Attack,
            "회전 거울 방향 선택 중 이동 행동 완전 차단");
        typeof(Move).GetField("choosingEchoDirection", privateInstance)
            .SetValue(player, false);
        player.CancelSelection();
        int completedTurns = 0;
        normalSpawner.WorldTurnCompleted += () => completedTurns++;
        Vector3 cameraPositionBeforeMove = boardCamera.transform.position;
        Check(player.TryPerformAction(PlayerActionSelectionMode.Move, moveDirection), "SampleScene 이동 요청");
        while (!player.CanAct) yield return null;
        Check(player.GridPosition == oldPlayerCell + moveDirection && completedTurns == 1,
            "SampleScene 한 칸 이동 및 적 한 턴");
        Check(Vector3.Distance(boardCamera.transform.position, cameraPositionBeforeMove) < 0.01f,
            "플레이어 이동 후에도 카메라가 맵 중앙에 고정");
        Vector3Int fireClearDirection = previewDirections.First(direction =>
            grid.IsWalkableCell(player.GridPosition + direction)
            && grid.IsWalkableCell(player.GridPosition + direction * 2));
        Vector3Int fireClearCell = player.GridPosition + fireClearDirection;
        Vector3Int secondFireClearCell = player.GridPosition + fireClearDirection * 2;
        normalSpawner.FireTrail.AddFire(fireClearCell);
        normalSpawner.FireTrail.AddFire(secondFireClearCell);
        Check(normalSpawner.FireTrail.HasFire(fireClearCell)
            && normalSpawner.FireTrail.HasFire(secondFireClearCell),
            "공격 경로의 연속 불길 테스트 준비");
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, fireClearDirection),
            "SampleScene 기본공격 요청");
        while (!player.CanAct) yield return null;
        Check(completedTurns == 2 && UnityEngine.Object.FindAnyObjectByType<ProjectileManager>().ActiveProjectileCount == 0,
            "SampleScene 기본공격 완료 후 적 한 턴 및 탄 제거");
        Check(!normalSpawner.FireTrail.HasFire(fireClearCell)
            && !normalSpawner.FireTrail.HasFire(secondFireClearCell),
            "기본공격이 지나간 모든 바닥 칸의 비숍 불길 제거");
        UltimateGauge gauge = player.GetComponent<UltimateGauge>();
        progression.SetMovementArtForPlaytest(PlayerMovementArt.None);
        Check(gauge != null && !gauge.IsReady
            && !player.TryPerformAction(PlayerActionSelectionMode.MovementArt, new Vector3Int(2, 1, 0))
            && completedTurns == 2, "이동술 미보유 상태는 턴을 쓰지 않음");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, true);
        Check(player.SelectionMode != PlayerActionSelectionMode.MovementArt && completedTurns == 2,
            "이동술 미보유 상태의 F 키는 선택을 열지 않음");
        gauge.AddGauge(gauge.MaxGauge);
        Button teleportButton = GameObject.Find("UltimateFrame")?.GetComponent<Button>();
        Check(teleportButton != null, "기존 F 아이콘 버튼 연결");
        teleportButton.onClick.Invoke();
        Check(player.SelectionMode != PlayerActionSelectionMode.MovementArt,
            "게이지가 가득 차도 이동술 미보유면 F 버튼 비활성");

        progression.SetMovementArtForPlaytest(PlayerMovementArt.Knight);
        teleportButton.onClick.Invoke();
        Check(player.SelectionMode == PlayerActionSelectionMode.MovementArt,
            "나이트 이동술 보유 시 F 아이콘으로 착지 타일 선택");
        Vector3Int[] knightOffsets =
        {
            new Vector3Int(1, 2, 0), new Vector3Int(2, 1, 0),
            new Vector3Int(2, -1, 0), new Vector3Int(1, -2, 0),
            new Vector3Int(-1, -2, 0), new Vector3Int(-2, -1, 0),
            new Vector3Int(-2, 1, 0), new Vector3Int(-1, 2, 0)
        };
        int knightChoiceCount = knightOffsets.Count(offset =>
            grid.IsWalkableCell(player.GridPosition + offset)
            && !normalSpawner.TryGetMonsterAtCell(player.GridPosition + offset, out _));
        Check(player.GetComponent<DirectionalActionIndicator>().ActiveChoiceCount == knightChoiceCount,
            "나이트 이동술은 L자 빈 타일만 후보로 표시");
        Transform firstChoice = player.transform.Find("ActionArrow_1");
        SpriteRenderer teleportTile = firstChoice != null
            ? firstChoice.Find("Arrow")?.GetComponent<SpriteRenderer>() : null;
        Check(teleportTile != null && teleportTile.sprite != null
            && teleportTile.sprite.name == "RuntimeMovementArtTileSprite"
            && !firstChoice.GetComponent<SpriteRenderer>().enabled
            && teleportTile.color == new Color32(255, 205, 91, 235),
            "이동술 후보는 노란색 타일 테두리로 표시");
        MonsterMovement occupiedTarget = normalSpawner.ActiveMonsters.First(m => m != null && !m.IsDead);
        Check(!player.TryUseMovementArtAtCell(occupiedTarget.GridPosition) && gauge.IsReady
            && completedTurns == 2, "적 점유 타일은 게이지와 턴을 소모하지 않음");
        Check(!player.TryUseMovementArtAtCell(player.GridPosition + Vector3Int.right * 2)
            && gauge.IsReady && completedTurns == 2,
            "나이트는 직선 이동을 할 수 없고 게이지도 소모하지 않음");
        Vector3Int teleportTarget = knightOffsets.Select(offset => player.GridPosition + offset)
            .First(cell => grid.IsWalkableCell(cell)
                && !normalSpawner.TryGetMonsterAtCell(cell, out _));
        Dictionary<MonsterMovement, Vector3Int> beforeTeleport = normalSpawner.ActiveMonsters
            .Where(m => m != null && !m.IsDead).ToDictionary(m => m, m => m.GridPosition);
        Check(player.TryUseMovementArtAtCell(teleportTarget), "나이트 L자 이동술 실행");
        Check(player.GridPosition == teleportTarget && grid.WorldToCell(player.transform.position) == teleportTarget,
            "애니메이션 없이 논리 좌표와 화면 위치 동시 이동");
        while (!player.CanAct) yield return null;
        Check(completedTurns == 3 && gauge.CurrentGauge == 0,
            "이동술 한 번에 적 한 턴 진행 및 게이지 전량 소모");
        Check(beforeTeleport.Any(pair => pair.Key != null && !pair.Key.IsDead
            && Mathf.Abs(pair.Key.GridPosition.x - teleportTarget.x)
                + Mathf.Abs(pair.Key.GridPosition.y - teleportTarget.y)
                < Mathf.Abs(pair.Value.x - teleportTarget.x)
                + Mathf.Abs(pair.Value.y - teleportTarget.y)),
            "몬스터가 이동술 착지점 기준으로 즉시 경로 재계산");
        foreach (MonsterMovement monster in normalSpawner.ActiveMonsters.Where(m => m != null && !m.IsDead))
        {
            int horizontal = teleportTarget.x - monster.GridPosition.x;
            if (horizontal == 0) continue;
            bool spriteFacesRight = new SerializedObject(monster).FindProperty("spriteFacesRight").boolValue;
            bool expectedFlip = horizontal < 0 ? spriteFacesRight : !spriteFacesRight;
            Check(monster.GetComponent<SpriteRenderer>().flipX == expectedFlip,
                "이동술 새 위치에 맞춰 몬스터 시선 갱신");
        }

        progression.SetMovementArtForPlaytest(PlayerMovementArt.Bishop);
        gauge.AddGauge(gauge.MaxGauge);
        Vector3Int nearbyTeleport = new[]
            {
                new Vector3Int(1, 1, 0), new Vector3Int(1, -1, 0),
                new Vector3Int(-1, -1, 0), new Vector3Int(-1, 1, 0)
            }.Select(direction => teleportTarget + direction)
            .First(cell => grid.IsWalkableCell(cell)
                && !normalSpawner.TryGetMonsterAtCell(cell, out _));
        normalSpawner.FireTrail.AddFire(nearbyTeleport);
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, true);
        Check(player.SelectionMode == PlayerActionSelectionMode.MovementArt,
            "F 키로 비숍 이동술 타일 선택");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, true);
        Check(player.SelectionMode == PlayerActionSelectionMode.None && gauge.IsReady,
            "F 키를 다시 누르면 게이지 소모 없이 선택 취소");
        player.ProcessInputFrame(false, false, false, false, Vector2.zero, true);
        Check(player.TryConfirmSelectedCell(Camera.main.WorldToScreenPoint(grid.GetCellCenterWorld(nearbyTeleport))),
            "게임 화면의 타일 클릭으로 비숍 이동술 확정");
        while (!player.CanAct) yield return null;
        Check(player.GridPosition == nearbyTeleport && completedTurns == 4
            && !normalSpawner.FireTrail.HasFire(nearbyTeleport),
            "비숍 이동술은 대각선 착지와 경로 불길 제거 후 적 한 턴 처리");
        if (normalSpawner.ChessWavesEnabled)
        {
            player.GetComponent<CharacterHealth>().Initialize(1000);
            for (int wave = 1; wave <= 13; wave++)
            {
                while (normalSpawner.CurrentWave < wave) yield return null;
                MonsterMovement[] enemies = normalSpawner.ActiveMonsters.Where(m => m != null && !m.IsDead).ToArray();
                WaveTemplate template = WaveTemplateCatalog.Get(wave);
                Check(enemies.Length == template.Monsters.Count, $"웨이브 {wave} 고정 조합 총 마릿수");
                Check(enemies.Select(m => m.SpawnOrder).Distinct().Count() == enemies.Length,
                    $"웨이브 {wave} 일반/체스 적의 고유 예약 순서");
                foreach (MonsterMovementPattern pattern in Enum.GetValues(typeof(MonsterMovementPattern)))
                    Check(enemies.Count(m => m.MovementPattern == pattern)
                        == template.Monsters.Count(item => item == pattern),
                        $"웨이브 {wave} {pattern} 조합");
                if (wave == 4 || wave == 8 || wave == 12)
                    Check(progression.ActiveAltars.Count == 2, $"웨이브 {wave} 안전/위험 유물 제단");
                if (wave == 4)
                {
                    RelicOffer teleportOffer = progression.ActiveAltars[0];
                    int relicCount = progression.Relics.Count;
                    progression.TryHandleAltarAtCell(teleportOffer.Cell, null);
                    Check(progression.Relics.Count == relicCount + 1
                        && progression.ActiveAltars.Count == 0,
                        "유물 제단 획득 즉시 반대 제단 제거");
                }
                else if (wave == 8)
                {
                    RelicOffer directOffer = progression.ActiveAltars[0];
                    int relicCount = progression.Relics.Count;
                    progression.TryHandleAltarAtCell(directOffer.Cell, null);
                    Check(progression.Relics.Count == relicCount + 1
                        && progression.ActiveAltars.Count == 0,
                        "두 번째 유물 획득을 상급 강화 조건으로 기록");
                }
                Check(GameObject.Find("ChessThreatTile") == null,
                    $"웨이브 {wave}에서 체스 적의 다음 이동 칸을 예고하지 않음");
                Check(UnityEngine.Object.FindAnyObjectByType<ChessPlaytest>() == null, "SampleScene에 2회차/20턴 제한 없음");
                if (wave == 12)
                {
                    MonsterMovement rook = enemies.First(m => m.MovementPattern == MonsterMovementPattern.Rook);
                    ChessMonsterBehaviour rookBehaviour = rook.GetComponent<ChessMonsterBehaviour>();
                    BoundsInt bounds = grid.GroundTilemap.cellBounds;
                    Vector3Int rookCell = rook.GridPosition;
                    Check(rookBehaviour.IsRookWallCell(rookCell)
                        && grid.IsTopBoundaryWallCell(rookCell), "룩 상단 성벽 스폰");
                    Check(!normalSpawner.TryKnockbackMonster(rook, Vector3Int.down),
                        "성벽 룩 지상 넉백 불가");
                    Vector3Int safeCell = Enumerable.Range(bounds.yMin + 1, bounds.size.y - 2)
                        .Select(y => new Vector3Int(bounds.xMin + 1, y, 0))
                        .First(cell => grid.IsWalkableCell(cell) && !normalSpawner.TryGetMonsterAtCell(cell, out _));
                    player.ResetPlaytest(safeCell);
                    player.GetComponent<CharacterHealth>().Initialize(1000);
                    for (int action = 0; action < 4; action++)
                    {
                        int previousTurns = completedTurns;
                        Vector3Int direction = Vector3Int.left;
                        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, direction), "혼합 웨이브 기본공격");
                        while (!player.CanAct) yield return null;
                        Check(completedTurns == previousTurns + 1, "혼합 웨이브에서 한 행동에 적/탄 한 턴");
                    }
                    Check(rookBehaviour.PatternExecutions > 0
                        && EnemyBullets().Length == 0,
                        "SampleScene 룩 장전 후 즉시 사격 실행");
                }

                if (wave == 1)
                {
                    player.TakeDamage(250);
                    Check(player.CurrentHealth == 750, "웨이브 회복 검증용 체력 감소");
                }
                else if (wave == 2)
                {
                    player.GetComponent<CharacterHealth>().Initialize(1000);
                    player.TakeDamage(25);
                    Check(player.CurrentHealth == 975, "최대 체력 상한 회복 검증용 체력 감소");
                }

                // 웨이브 연결 검증을 위해서만 남은 적을 처치한다.
                foreach (MonsterMovement enemy in enemies) if (enemy != null && !enemy.IsDead) enemy.TakeDamage(10000);
                if (wave < 13)
                {
                    bool relicReward = (wave == 4 || wave == 8);
                    bool expectsSelection = RunProgressionCatalog.IsStandardUpgradeWave(wave)
                        || relicReward;
                    if (expectsSelection)
                    {
                        while (!normalSpawner.StageFlow.IsWaitingForUpgrade) yield return null;
                        if (wave == 2)
                            Check(player.CurrentHealth == 1000, "웨이브 회복은 최대 체력을 넘지 않음");
                        Check(progression.CurrentUpgradeOptions.Count == 3,
                            $"웨이브 {wave} 강화 3개 제시");
                        Check(progression.CurrentUpgradeOptions.All(item => item.IsAdvanced == relicReward),
                            $"웨이브 {wave} 일반/상급 강화 종류 분리");
                        if (relicReward)
                            Check(progression.CurrentUpgradeOptions.All(item => item.RewardTier == wave / 4),
                                $"웨이브 {wave} 유물 단계에 맞는 상급 강화");
                        GameObject upgradePanel = GameObject.Find("UpgradeSelectionPanel");
                        Check(upgradePanel != null && upgradePanel.activeInHierarchy,
                            $"웨이브 {wave} 중앙 강화 선택 패널 표시");
                        RectTransform upgradeRect = upgradePanel.GetComponent<RectTransform>();
                        Check(upgradeRect.anchorMin == Vector2.zero
                            && upgradeRect.anchorMax == Vector2.one,
                            $"웨이브 {wave} 강화 선택이 시작 특성처럼 중앙 전체 화면 사용");
                        GameObject firstUpgradeChoice = GameObject.Find("UpgradeChoice_1");
                        Check(firstUpgradeChoice != null, $"웨이브 {wave} 첫 강화 카드 생성");
                        string selectedUpgradeId = progression.CurrentUpgradeOptions[0].Id;
                        int previousUpgradeStack = progression.Stack(selectedUpgradeId);
                        firstUpgradeChoice.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                        Check(progression.Stack(selectedUpgradeId) == previousUpgradeStack + 1,
                            $"웨이브 {wave} 선택한 강화 적용");
                    }
                    while (normalSpawner.CurrentWave == wave) yield return null;
                    if (!expectsSelection)
                    {
                        Check(progression.CurrentUpgradeOptions.Count == 0,
                            $"웨이브 {wave} 영구 강화 없이 자동 진행");
                    }
                    if (wave == 1)
                        Check(player.CurrentHealth == 850, "웨이브 클리어 시 최대 체력의 10% 회복");
                    Check(EnemyBullets().Length == 0 && normalSpawner.FireTrail.Count == 0, "다음 웨이브에 이전 적 탄/불길 없음");
                }
            }
        }
    }

    private static void ValidateTransitReservations(MonsterSpawner spawner, MonsterMovement bishop, MonsterMovement snake)
    {
        // 예약만 독립 검증한 뒤 즉시 비운다. 다음 실제 턴도 점유를 다시 구축한다.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic;
        var occupied = (HashSet<Vector3Int>)typeof(MonsterSpawner).GetField("blockedMonsterCells", flags).GetValue(spawner);
        var transit = (HashSet<Vector3Int>)typeof(MonsterSpawner).GetField("reservedTransitCells", flags).GetValue(spawner);
        var reserve = typeof(MonsterSpawner).GetMethod("TryReserveMonsterCell", flags);
        bool Reserve(MonsterMovement actor, Vector3Int origin, Vector3Int destination)
            => (bool)reserve.Invoke(spawner, new object[] { actor, origin, destination });
        occupied.Clear();
        transit.Clear();
        try
        {
            occupied.Add(new Vector3Int(2, 2, 0));
            occupied.Add(new Vector3Int(2, 3, 0));
            Check(Reserve(bishop, new Vector3Int(2, 2, 0), new Vector3Int(4, 4, 0)), "비숍 대각선 경로 예약");
            Check(!Reserve(snake, new Vector3Int(2, 3, 0), new Vector3Int(3, 3, 0)), "뒤 적은 비숍 통과 칸 예약 불가");
            Check(Reserve(snake, new Vector3Int(2, 3, 0), new Vector3Int(2, 2, 0)), "앞 적이 비운 출발 칸은 같은 턴 사용 가능");
        }
        finally
        {
            occupied.Clear();
            transit.Clear();
        }
    }

    private static void PlaceFixture(ChessPlaytest test, Move player, Vector3Int playerCell, Vector3Int knightCell)
    {
        player.ResetPlaytest(playerCell);
        test.Encounter[0].ApplyKnockback(knightCell);
        test.Encounter[1].ApplyKnockback(new Vector3Int(9, 9, 0));
        if (test.Encounter[2] != null && !test.Encounter[2].IsDead)
            test.Encounter[2].ApplyKnockback(new Vector3Int(10, 11, 0));
    }

    private static void IsolatedTurn(MonsterMovement actor, ChessPlaytest test, Action completed,
        Func<Vector3Int, bool> extraBlocked = null)
    {
        bool Blocked(Vector3Int cell) => (extraBlocked != null && extraBlocked(cell))
            || test.Encounter.Any(m => m != actor && m != null && !m.IsDead && m.GridPosition == cell);
        actor.TakeTurn(completed, (origin, destination) => !Blocked(destination), Blocked);
    }

    private static TurnProjectile[] EnemyBullets()
    {
        return UnityEngine.Object.FindObjectsByType<TurnProjectile>()
            .Where(p => p.IsEnemyProjectile && !p.IsFinished).ToArray();
    }

    private static string Snapshot(ChessPlaytest test, Move player)
    {
        return player.GridPosition + ":" + player.CurrentHealth + ":" + test.TurnCount + ":"
            + string.Join(";", test.Encounter.Where(m => m != null).Select(m => m.name + m.GridPosition))
            + ":" + string.Join(";", EnemyBullets().Select(p => p.CurrentCell.ToString()).OrderBy(p => p));
    }

    private static void CheckOccupancy(ChessPlaytest test, Move player, GridManager grid)
    {
        HashSet<Vector3Int> cells = new HashSet<Vector3Int> { player.GridPosition };
        foreach (MonsterMovement monster in test.Encounter)
            if (monster != null && !monster.IsDead)
                if (!cells.Add(monster.GridPosition)
                    || (!grid.IsWalkableCell(monster.GridPosition)
                        && !(monster.MovementPattern == MonsterMovementPattern.Rook
                            && grid.IsTopBoundaryWallCell(monster.GridPosition))))
                    throw new InvalidOperationException("몬스터 점유 중복 또는 벽 침범");
    }

    private static bool RayHasMonster(Vector3Int cell, Vector3Int direction, ChessPlaytest test, GridManager grid)
    {
        for (cell += direction; grid.IsWalkableCell(cell); cell += direction)
            if (test.Encounter.Any(m => m != null && !m.IsDead && m.GridPosition == cell)) return true;
        return false;
    }
}
