using System;
using System.Collections.Generic;

[Serializable]
public sealed class WaveTemplate
{
    public WaveTemplate(int waveNumber, string displayName, bool specialWave, int difficultyTier,
        params MonsterMovementPattern[] monsters)
    {
        WaveNumber = waveNumber;
        DisplayName = displayName;
        IsSpecialWave = specialWave;
        DifficultyTier = difficultyTier;
        Monsters = monsters ?? Array.Empty<MonsterMovementPattern>();
    }

    public int WaveNumber { get; }
    public string DisplayName { get; }
    public bool IsSpecialWave { get; }
    public int DifficultyTier { get; }
    public bool IsRelicWave => WaveNumber >= 4 && WaveNumber <= 16 && WaveNumber % 4 == 0;
    public bool IsMidBossWave => WaveNumber == 10;
    public IReadOnlyList<MonsterMovementPattern> Monsters { get; }
}

public static class WaveTemplateCatalog
{
    public const int FinalWave = 20;

    private static readonly WaveTemplate[] Templates =
    {
        Build(1, "추적의 시작", false, (MonsterMovementPattern.CardinalFour, 3)),
        // 첫 유물 웨이브 전에는 수량과 새 기믹이 동시에 급증하지 않게 한다.
        Build(2, "엇갈린 추격", false, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 1)),
        Build(3, "박쥐 무리", false, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 2)),
        Build(4, "나이트 첫 등장", true, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 1)),
        Build(5, "도약 추격", false, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 2)),
        Build(6, "엇갈린 도약", false, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 2)),
        Build(7, "기사단", true, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 3)),
        Build(8, "비숍 첫 등장", true, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 2),
            (MonsterMovementPattern.Bishop, 2)),
        Build(9, "불길과 도약", false, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 2),
            (MonsterMovementPattern.Bishop, 2)),
        // 중간전은 두 기믹의 비중으로 압박하고, 직후에는 재정비할 전투를 둔다.
        Build(10, "중간보스 · 도약 화염진", true, (MonsterMovementPattern.CardinalFour, 1),
            (MonsterMovementPattern.EightDirection, 1), (MonsterMovementPattern.Knight, 3),
            (MonsterMovementPattern.Bishop, 3)),
        Build(11, "잔불 추격", false, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 3), (MonsterMovementPattern.Knight, 1),
            (MonsterMovementPattern.Bishop, 1)),
        Build(12, "룩 첫 등장", true, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 2),
            (MonsterMovementPattern.Bishop, 2), (MonsterMovementPattern.Rook, 2)),
        Build(13, "외곽 압박", false, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 3), (MonsterMovementPattern.Knight, 2),
            (MonsterMovementPattern.Bishop, 2), (MonsterMovementPattern.Rook, 2)),
        Build(14, "화염 포위", true, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 2),
            (MonsterMovementPattern.Bishop, 4), (MonsterMovementPattern.Rook, 2)),
        Build(15, "교차 사격", true, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 3),
            (MonsterMovementPattern.Bishop, 2), (MonsterMovementPattern.Rook, 3)),
        Build(16, "완전 혼합", true, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 3), (MonsterMovementPattern.Knight, 3),
            (MonsterMovementPattern.Bishop, 3), (MonsterMovementPattern.Rook, 2)),
        Build(17, "도약 공세", false, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 5),
            (MonsterMovementPattern.Bishop, 3), (MonsterMovementPattern.Rook, 2)),
        Build(18, "불길 공세", false, (MonsterMovementPattern.CardinalFour, 2),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 4),
            (MonsterMovementPattern.Bishop, 5), (MonsterMovementPattern.Rook, 2)),
        Build(19, "왕의 수비진", true, (MonsterMovementPattern.CardinalFour, 3),
            (MonsterMovementPattern.EightDirection, 2), (MonsterMovementPattern.Knight, 3),
            (MonsterMovementPattern.Bishop, 4), (MonsterMovementPattern.Rook, 4)),
        Build(20, "모든 기믹의 종착점", true, (MonsterMovementPattern.CardinalFour, 4),
            (MonsterMovementPattern.EightDirection, 3), (MonsterMovementPattern.Knight, 4),
            (MonsterMovementPattern.Bishop, 4), (MonsterMovementPattern.Rook, 3))
    };

    private static WaveTemplate Build(int waveNumber, string displayName, bool specialWave,
        params (MonsterMovementPattern Pattern, int Count)[] groups)
    {
        List<MonsterMovementPattern> monsters = new List<MonsterMovementPattern>();
        foreach ((MonsterMovementPattern pattern, int count) in groups)
            for (int i = 0; i < count; i++) monsters.Add(pattern);
        return new WaveTemplate(waveNumber, displayName, specialWave,
            GetDifficultyTier(waveNumber), monsters.ToArray());
    }

    // 유물을 얻는 4·8·12·16웨이브마다 다음 난이도 단계가 시작된다.
    private static int GetDifficultyTier(int waveNumber)
    {
        int safeWave = Math.Max(1, waveNumber);
        if (safeWave < 4) return 1;
        if (safeWave < 8) return 2;
        if (safeWave < 12) return 3;
        if (safeWave < 16) return 4;
        return 5;
    }

    public static WaveTemplate Get(int waveNumber)
    {
        int index = Math.Max(1, Math.Min(Templates.Length, waveNumber)) - 1;
        return Templates[index];
    }
}
