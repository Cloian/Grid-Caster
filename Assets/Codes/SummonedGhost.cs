using UnityEngine;

// 여왕의 시계 초과로 소환된 증원 표시: 반투명한 푸른 유령처럼 보이고 투명도가 천천히 일렁인다.
// 원래 웨이브 적을 모두 잡으면 소환이 풀려 사라진다는 걸 그림으로 알린다. 판정에는 관여하지 않는다.
[DisallowMultipleComponent]
public sealed class SummonedGhost : MonoBehaviour
{
    public static readonly Color GhostColor = new Color(0.5f, 0.78f, 1f, 0.58f);
    private const float PulseSpeed = 3f;
    private const float PulseAmount = 0.14f;

    private SpriteRenderer body;
    private DamageFlash flash;

    private void Start()
    {
        body = GetComponent<SpriteRenderer>();
        flash = GetComponent<DamageFlash>();
        if (flash != null) flash.SetBaseColor(body, GhostColor);
        else if (body != null) body.color = GhostColor;
    }

    private void LateUpdate()
    {
        if (body == null || (flash != null && flash.IsFlashing)) return;
        Color color = GhostColor;
        color.a = GhostColor.a + Mathf.Sin(Time.time * PulseSpeed) * PulseAmount;
        body.color = color;
    }
}
