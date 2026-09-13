using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        Check(!grid.IsWalkableCell(Vector3Int.zero) && grid.IsWalkableCell(new Vector3Int(1, 1, 0)), "경계 벽 충돌");
        Check(!player.TryPerformAction(PlayerActionSelectionMode.Move, Vector3Int.right), "시작 전 입력 차단");
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
            Check(Mathf.Abs(delta.x) == Mathf.Abs(delta.y) && delta.x != 0 && Mathf.Abs(delta.x) <= 2,
                "비숍은 매 턴 최대 2칸 대각선 이동");
            Vector3Int step = new Vector3Int(Math.Sign(delta.x), Math.Sign(delta.y), 0);
            for (Vector3Int cell = origin; cell != bishop.GridPosition + step; cell += step)
                Check(fire.HasFire(cell), "비숍 출발점/경로/도착점에 불길");
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
        fixtureRook.ApplyKnockback(new Vector3Int(2, 10, 0));
        projectiles.ClearProjectiles();
        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true);
        while (!done) yield return null;
        Check(fixtureRook.GridPosition == new Vector3Int(3, 10, 0) && EnemyBullets().Length == 1,
            "룩은 플레이어 방향으로 이동하면서 사격");
        done = false;
        projectiles.TakeTurn(() => done = true);
        while (!done) yield return null;
        Check(EnemyBullets().Single().CurrentCell == new Vector3Int(3, 9, 0), "룩 탄은 한 턴에 한 칸");
        player.ResetPlaytest(new Vector3Int(2, 5, 0));
        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true);
        while (!done) yield return null;
        Check(fixtureRook.GridPosition == new Vector3Int(2, 10, 0) && EnemyBullets().Length == 2,
            "룩은 플레이어 이동에 따라 추적 방향 변경");
        done = false;
        IsolatedTurn(fixtureRook, test, () => done = true, _ => true);
        Check(done && EnemyBullets().Length == 3, "룩은 이동이 막혀도 사격");

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
        hud.SelectTrait(0);
        Check(player.CanAct && normalSpawner.CurrentWave == 1, "SampleScene 특성 선택과 첫 웨이브");
        int completedTurns = 0;
        normalSpawner.WorldTurnCompleted += () => completedTurns++;
        Vector3Int oldPlayerCell = player.GridPosition;
        Vector3Int moveDirection = Directions.First(d => grid.IsWalkableCell(oldPlayerCell + d)
            && !normalSpawner.TryGetMonsterAtCell(oldPlayerCell + d, out _));
        Check(player.TryPerformAction(PlayerActionSelectionMode.Move, moveDirection), "SampleScene 이동 요청");
        while (!player.CanAct) yield return null;
        Check(player.GridPosition == oldPlayerCell + moveDirection && completedTurns == 1,
            "SampleScene 한 칸 이동 및 적 한 턴");
        Check(player.TryPerformAction(PlayerActionSelectionMode.Attack, Vector3Int.up), "SampleScene 기본공격 요청");
        while (!player.CanAct) yield return null;
        Check(completedTurns == 2 && UnityEngine.Object.FindAnyObjectByType<ProjectileManager>().ActiveProjectileCount == 0,
            "SampleScene 기본공격 완료 후 적 한 턴 및 탄 제거");
        if (normalSpawner.ChessWavesEnabled)
        {
            player.GetComponent<CharacterHealth>().Initialize(1000);
            for (int wave = 1; wave <= 6; wave++)
            {
                while (normalSpawner.CurrentWave < wave) yield return null;
                MonsterMovement[] enemies = normalSpawner.ActiveMonsters.Where(m => m != null && !m.IsDead).ToArray();
                Check(enemies.Length == 4 + wave - 1, $"웨이브 {wave} 총 마릿수 유지");
                Check(enemies.Select(m => m.SpawnOrder).Distinct().Count() == enemies.Length,
                    $"웨이브 {wave} 일반/체스 적의 고유 예약 순서");
                Check(enemies.Count(m => m.MovementPattern == MonsterMovementPattern.Knight) == (wave >= 3 ? 1 : 0), $"웨이브 {wave} 나이트 해금");
                Check(enemies.Count(m => m.MovementPattern == MonsterMovementPattern.Bishop) == (wave >= 5 ? 1 : 0), $"웨이브 {wave} 비숍 해금");
                Check(enemies.Count(m => m.MovementPattern == MonsterMovementPattern.Rook) == (wave >= 5 ? 1 : 0), $"웨이브 {wave} 룩 해금");
                Check(UnityEngine.Object.FindAnyObjectByType<ChessPlaytest>() == null, "SampleScene에 2회차/20턴 제한 없음");
                if (wave == 5)
                {
                    MonsterMovement rook = enemies.Single(m => m.MovementPattern == MonsterMovementPattern.Rook);
                    ChessMonsterBehaviour rookBehaviour = rook.GetComponent<ChessMonsterBehaviour>();
                    BoundsInt bounds = grid.GroundTilemap.cellBounds;
                    Vector3Int rookCell = rook.GridPosition;
                    Check(rookBehaviour.IsRookBoundaryCell(rookCell), "룩 테두리 스폰");
                    Vector3Int inward = rookCell.y == bounds.yMax - 2 ? Vector3Int.down
                        : rookCell.y == bounds.yMin + 1 ? Vector3Int.up
                        : rookCell.x == bounds.xMin + 1 ? Vector3Int.right : Vector3Int.left;
                    if (!rookBehaviour.IsRookBoundaryCell(rookCell + inward))
                        Check(!normalSpawner.TryKnockbackMonster(rook, inward), "룩을 테두리 밖으로 넉백하지 않음");
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
                    Check(rookBehaviour.PatternExecutions > 0, "SampleScene 룩 사격 실행");
                }
                // 웨이브 연결 검증을 위해서만 남은 적을 처치한다.
                foreach (MonsterMovement enemy in enemies) if (enemy != null && !enemy.IsDead) enemy.TakeDamage(10000);
                if (wave < 6)
                {
                    while (normalSpawner.CurrentWave == wave) yield return null;
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
        test.Encounter[2].ApplyKnockback(new Vector3Int(10, 10, 0));
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
                if (!cells.Add(monster.GridPosition) || !grid.IsWalkableCell(monster.GridPosition))
                    throw new InvalidOperationException("몬스터 점유 중복 또는 벽 침범");
    }

    private static bool RayHasMonster(Vector3Int cell, Vector3Int direction, ChessPlaytest test, GridManager grid)
    {
        for (cell += direction; grid.IsWalkableCell(cell); cell += direction)
            if (test.Encounter.Any(m => m != null && !m.IsDead && m.GridPosition == cell)) return true;
        return false;
    }
}
