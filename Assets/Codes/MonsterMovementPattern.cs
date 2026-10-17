public enum MonsterMovementPattern
{
    CardinalFour,
    EightDirection,
    Knight,
    Bishop,
    Rook,
    // 원거리 폭탄병: 체스 적과 같은 행동 경로(ChessMonsterBehaviour)를 쓴다. 끝에 추가해 기존 직렬화 값을 유지한다.
    Bomber
}
