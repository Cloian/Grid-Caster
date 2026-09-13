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
    private GameObject graveObject;

    private void Awake()
    {
        characterHealth = GetComponent<CharacterHealth>();
        playerSprite = GetComponent<SpriteRenderer>();
        playerAnimator = GetComponent<Animator>();
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
        if (graveObject != null)
        {
            Destroy(graveObject);
            graveObject = null;
        }
        if (playerSprite != null)
            playerSprite.enabled = true;
        if (playerAnimator != null)
            playerAnimator.enabled = true;
    }
}
