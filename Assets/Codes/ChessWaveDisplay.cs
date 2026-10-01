using UnityEngine;

// 이동 목적지는 숨기되 종류·체력·방향과 다음 도약/봉쇄 상태는 공개한다.
public sealed class ChessWaveDisplay : MonoBehaviour
{
    private MonsterSpawner spawner;
    private Move player;
    private GUIStyle labelStyle;

    public static Color ColorFor(MonsterMovementPattern pattern)
    {
        if (pattern == MonsterMovementPattern.Knight) return new Color(0.2f, 0.85f, 1f);
        if (pattern == MonsterMovementPattern.Bishop) return new Color(1f, 0.75f, 0.15f);
        return new Color(1f, 0.3f, 0.7f);
    }

    public void Initialize(MonsterSpawner owner, Move target)
    {
        spawner = owner;
        player = target;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (spawner == null || player == null || camera == null) return;
        Rect viewport = camera.pixelRect;
        // 카메라가 비추지 않는 UI 영역에는 월드 좌표 기반 이름표를 그리지 않는다.
        GUI.BeginGroup(new Rect(viewport.xMin, Screen.height - viewport.yMax,
            viewport.width, viewport.height));
        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Normal };
        foreach (MonsterMovement monster in spawner.ActiveMonsters)
        {
            if (monster == null || monster.IsDead
                || !monster.TryGetComponent(out ChessMonsterBehaviour behaviour)) continue;
            Vector3 point = camera.WorldToScreenPoint(monster.transform.position);
            if (point.z <= 0) continue;
            Vector3 delta = player.transform.position - monster.transform.position;
            int x = delta.x > 0.1f ? 1 : delta.x < -0.1f ? -1 : 0;
            int y = delta.y > 0.1f ? 1 : delta.y < -0.1f ? -1 : 0;
            string arrow = x == 0 ? (y >= 0 ? "↑" : "↓") : y == 0 ? (x > 0 ? "→" : "←")
                : x > 0 ? (y > 0 ? "↗" : "↘") : (y > 0 ? "↖" : "↙");
            string label = monster.MovementPattern == MonsterMovementPattern.Knight ? "N"
                : monster.MovementPattern == MonsterMovementPattern.Bishop ? "B" : "R";
            Color previous = GUI.color;
            GUI.color = ColorFor(monster.MovementPattern);
            string intent = monster.MovementPattern == MonsterMovementPattern.Knight
                ? (behaviour.WillKnightJumpNextTurn ? " 도약" : " 준비") : string.Empty;
            if (monster.MovementPattern == MonsterMovementPattern.Rook)
            {
                arrow = "↓";
                intent = behaviour.IsRookCharged ? " 발사 예고"
                    : behaviour.IsRookRecovering ? " 재정비" : " 장전 대기";
            }
            if (monster.IsActionBlocked) intent = " 봉쇄";
            GUI.Label(new Rect(point.x - viewport.xMin - 28, viewport.yMax - point.y - 46, 120, 28),
                $"{label} {arrow} {monster.GetComponent<CharacterHealth>().CurrentHealth}{intent}", labelStyle);
            GUI.color = previous;
        }
        GUI.EndGroup();
    }
}
