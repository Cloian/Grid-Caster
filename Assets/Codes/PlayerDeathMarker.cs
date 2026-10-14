using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerDeathMarker : MonoBehaviour
{
    [Header("사망 표시")]
    [SerializeField] private Sprite graveSprite;
    [SerializeField] private bool hidePlayerVisual = true;
    [SerializeField] private int sortingOrderOffset = 1;

    private CharacterHealth characterHealth;
    private SpriteRenderer playerSprite;
    private Animator playerAnimator;
    private AnimatorUpdateMode aliveAnimatorUpdateMode;
    private GameObject graveObject;
    private bool deathAnimationShowsGrave;

    // 별도 묘비 오브젝트 또는 묘비로 끝나는 사망 모션이 표시 중인지.
    public bool ShowsGrave => graveObject != null || deathAnimationShowsGrave;

    private void Awake()
    {
        characterHealth = GetComponent<CharacterHealth>();
        playerSprite = GetComponent<SpriteRenderer>();
        playerAnimator = GetComponent<Animator>();
        if (playerAnimator != null) aliveAnimatorUpdateMode = playerAnimator.updateMode;
    }

    private void OnEnable()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (characterHealth != null)
        {
            characterHealth.Died += HandlePlayerDied;
        }
    }

    private void OnDisable()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandlePlayerDied;
        }
    }

    private void HandlePlayerDied(CharacterHealth defeatedCharacter)
    {
        if (graveObject != null)
            return;

        // 사망 모션이 묘비가 솟아오르는 장면까지 재생하고 마지막 프레임을 유지한다.
        if (ActorMotion.HasDeath(this))
        {
            // 결과 창은 전투 시간을 멈춘다. 사망 표현만 비배율 시간으로 끝까지 재생한다.
            playerAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            GetComponent<DamageFlash>()?.Stop();
            deathAnimationShowsGrave = true;
            return;
        }

        if (graveSprite == null)
        {
            Debug.LogError("플레이어 묘비 Sprite가 연결되지 않았습니다.", this);
            return;
        }

        if (hidePlayerVisual)
        {
            if (playerSprite != null)
            {
                playerSprite.enabled = false;
            }

            if (playerAnimator != null)
            {
                playerAnimator.enabled = false;
            }
        }

        graveObject = new GameObject("PlayerGrave");
        SceneManager.MoveGameObjectToScene(graveObject, gameObject.scene);
        graveObject.transform.position = transform.position;

        SpriteRenderer graveRenderer = graveObject.AddComponent<SpriteRenderer>();
        graveRenderer.sprite = graveSprite;

        if (playerSprite != null)
        {
            graveRenderer.sortingLayerID = playerSprite.sortingLayerID;
            graveRenderer.sortingOrder = playerSprite.sortingOrder + sortingOrderOffset;
            graveRenderer.sharedMaterial = playerSprite.sharedMaterial;
        }

        Debug.Log("플레이어 사망 위치에 묘비를 표시했습니다.", graveObject);
    }

    private void OnValidate()
    {
        sortingOrderOffset = Mathf.Max(0, sortingOrderOffset);
    }

    public void ResetMarker()
    {
        deathAnimationShowsGrave = false;
        if (graveObject != null)
        {
            Destroy(graveObject);
            graveObject = null;
        }
        if (playerSprite != null)
            playerSprite.enabled = true;
        if (playerAnimator != null)
        {
            playerAnimator.updateMode = aliveAnimatorUpdateMode;
            playerAnimator.enabled = true;
        }
    }
}
