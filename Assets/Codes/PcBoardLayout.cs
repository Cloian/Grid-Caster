using UnityEngine;

// PC는 보드 위·아래 HUD 공간만 남기고 남은 화면을 전장에 사용한다.
public static class PcBoardLayout
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
    public const float BoardSide = 850f;
    public const float HeaderSpace = 78f;
    public const float ActionSpace = 152f;
    public const float BoardCenterY = (ActionSpace - HeaderSpace) * 0.5f;
    public const float PanelGap = 18f;
    public const float PanelWidth = 330f;
    public const float PanelHeight = 820f;

    // 외벽 포함 15칸, 타일 원본 32px. 화면 배율이 정수일 때만 픽셀 크기가 고르게 보인다.
    public const int BoardTiles = 15;
    public const int TilePixels = 32;

    public static float Scale(int width, int height)
        => Mathf.Max(0.01f, Mathf.Min(width / ReferenceResolution.x, height / ReferenceResolution.y));

    // 화면 픽셀 / 원본 픽셀. 2배 이상 들어가면 정수로 내림해 픽셀이 고르게 보이게 하고,
    // 그보다 작은 화면에서는 남는 공간에 맞춘다. 위·아래 외벽 한 줄씩은 헤더·행동 HUD 뒤에 겹쳐도 된다.
    public static float PixelScale(int width, int height)
    {
        float scale = Scale(width, height);
        float fitHeight = (height - (HeaderSpace + ActionSpace) * scale) / ((BoardTiles - 2) * TilePixels);
        float fitWidth = (width - 48f * scale) / (BoardTiles * TilePixels);
        float fit = Mathf.Max(0.05f, Mathf.Min(fitHeight, fitWidth));
        return fit >= 2f ? Mathf.Floor(fit) : fit;
    }

    public static Rect BoardPixelRect(int width, int height)
    {
        float scale = Scale(width, height);
        float pixelScale = PixelScale(width, height);
        float side = BoardTiles * TilePixels * pixelScale;
        float wall = TilePixels * pixelScale;
        // 헤더 + 보드(위·아래 벽 제외) + 행동 HUD 묶음을 세로 가운데에 둔다.
        float block = HeaderSpace * scale + side - 2f * wall + ActionSpace * scale;
        float top = height - Mathf.Max(0f, (height - block) * 0.5f) - HeaderSpace * scale + wall;
        // 맵 모서리를 정수 화면 픽셀에 맞춘다.
        return new Rect(Mathf.Round((width - side) * 0.5f), Mathf.Round(top - side), side, side);
    }
}
