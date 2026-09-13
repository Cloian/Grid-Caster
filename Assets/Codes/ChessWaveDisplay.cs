using System.Collections.Generic;
using UnityEngine;

// 전투 판정을 바꾸지 않고 테스트와 같은 위협 칸/체력/바라보는 방향만 표시한다.
public sealed class ChessWaveDisplay : MonoBehaviour
{
    private MonsterSpawner spawner;
    private GridManager grid;
    private Move player;
    private Sprite outline;
    private Texture2D texture;
    private readonly List<SpriteRenderer> markers = new List<SpriteRenderer>();

    public static Color ColorFor(MonsterMovementPattern pattern)
    {
        if (pattern == MonsterMovementPattern.Knight) return new Color(0.2f, 0.85f, 1f);
        if (pattern == MonsterMovementPattern.Bishop) return new Color(1f, 0.75f, 0.15f);
        return new Color(1f, 0.3f, 0.7f);
    }

    public void Initialize(MonsterSpawner owner, GridManager map, Move target)
    {
        spawner = owner;
        grid = map;
        player = target;
        texture = new Texture2D(32, 32) { filterMode = FilterMode.Point };
        Color[] pixels = new Color[1024];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = x < 2 || x > 29 || y < 2 || y > 29 ? Color.white : Color.clear;
        texture.SetPixels(pixels);
        texture.Apply();
        outline = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
        spawner.WorldTurnCompleted += Refresh;
        spawner.WaveStarted += OnWaveChanged;
        spawner.WaveCleared += OnWaveChanged;
    }

    private void OnWaveChanged(int wave) => Refresh();

    private void Refresh()
    {
        int index = 0;
        foreach (MonsterMovement monster in spawner.ActiveMonsters)
        {
            if (monster == null || monster.IsDead || !monster.TryGetComponent(out ChessMonsterBehaviour behaviour)) continue;
            foreach (Vector3Int cell in behaviour.GetThreatCells(spawner.IsOccupied))
            {
                if (index == markers.Count)
                {
                    GameObject tile = new GameObject("ChessThreatTile");
                    tile.transform.SetParent(transform, false);
                    SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
                    renderer.sprite = outline;
                    renderer.sortingOrder = 4;
                    markers.Add(renderer);
                }
                SpriteRenderer marker = markers[index++];
                marker.gameObject.SetActive(true);
                marker.transform.position = grid.GetCellCenterWorld(cell);
                marker.transform.localScale = Vector3.one * (0.96f - ((int)monster.MovementPattern - (int)MonsterMovementPattern.Knight) * 0.1f);
                marker.color = ColorFor(monster.MovementPattern);
            }
        }
        for (; index < markers.Count; index++) markers[index].gameObject.SetActive(false);
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (spawner == null || player == null || camera == null) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        foreach (MonsterMovement monster in spawner.ActiveMonsters)
        {
            if (monster == null || monster.IsDead || !monster.TryGetComponent(out ChessMonsterBehaviour _)) continue;
            Vector3 point = camera.WorldToScreenPoint(monster.transform.position);
            if (point.z < 0) continue;
            Vector3 delta = player.transform.position - monster.transform.position;
            int x = delta.x > 0.1f ? 1 : delta.x < -0.1f ? -1 : 0;
            int y = delta.y > 0.1f ? 1 : delta.y < -0.1f ? -1 : 0;
            string arrow = x == 0 ? (y >= 0 ? "↑" : "↓") : y == 0 ? (x > 0 ? "→" : "←")
                : x > 0 ? (y > 0 ? "↗" : "↘") : (y > 0 ? "↖" : "↙");
            string label = monster.MovementPattern == MonsterMovementPattern.Knight ? "N"
                : monster.MovementPattern == MonsterMovementPattern.Bishop ? "B" : "R";
            Color previous = GUI.color;
            GUI.color = ColorFor(monster.MovementPattern);
            GUI.Label(new Rect(point.x - 28, Screen.height - point.y - 46, 100, 28),
                $"{label} {arrow} {monster.GetComponent<CharacterHealth>().CurrentHealth}", style);
            GUI.color = previous;
        }
    }

    private void OnDestroy()
    {
        if (spawner != null)
        {
            spawner.WorldTurnCompleted -= Refresh;
            spawner.WaveStarted -= OnWaveChanged;
            spawner.WaveCleared -= OnWaveChanged;
        }
        if (outline != null) Destroy(outline);
        if (texture != null) Destroy(texture);
    }
}
