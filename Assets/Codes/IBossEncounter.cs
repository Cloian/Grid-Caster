using System.Collections.Generic;

// 본 게임 보스 웨이브(10웨이브 기사단장, 20웨이브 여왕)를 MonsterSpawner가 같은 방식으로 돌리기 위한 약속.
// 보스 턴은 플레이어 행동이 끝날 때마다 Tick 한 번, 몸이 여러 칸을 차지해도 피해는 몸 하나로 받는다.
public interface IBossEncounter
{
    MonsterMovement Body { get; }
    bool IsFinished { get; }
    bool Won { get; }
    IEnumerable<UnityEngine.Vector3Int> BodyCells { get; }
    bool ContainsBodyCell(UnityEngine.Vector3Int cell);
    void Tick();
}
