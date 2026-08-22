using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(DirectionalActionIndicator))]
public class Move : MonoBehaviour
{
    public event Action MoveCompleted;
    public event Action PlayerMoved;
    public event Action AttackHit;
    public event Action<PlayerActionSelectionMode> SelectionModeChanged;

    [Header("이동 설정")]
    [SerializeField] private float moveSpeed = 2.67f;

    [Header("전투 설정")]
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private int attackDamage = 1;

    [Header("이동 불가 피드백")]
    [SerializeField] private AudioClip blockedMoveSound;
    [SerializeField, Range(0f, 1f)] private float blockedMoveSoundVolume = 0.45f;

    [Header("행동 선택 표시")]
    [SerializeField] private DirectionalActionIndicator actionIndicator;
    [SerializeField] private Camera worldCamera;

    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private const int FeedbackSampleRate = 22050;
    private const float BlockedSoundDuration = 0.12f;

    private static readonly Vector2Int[] EightDirections =
    {
        Vector2Int.up,
        new Vector2Int(1, 1),
        Vector2Int.right,
        new Vector2Int(1, -1),
        Vector2Int.down,
        new Vector2Int(-1, -1),
        Vector2Int.left,
        new Vector2Int(-1, 1)
    };

    private Animator playerAnimator;
    private SpriteRenderer playerSprite;
    private CharacterHealth characterHealth;
    private AudioSource feedbackAudioSource;
    private AudioClip generatedBlockedMoveSound;
    private GridManager gridManager;
    private MonsterSpawner monsterSpawner;
    private ProjectileManager projectileManager;
    private PlayerTraitSystem playerTraitSystem;
    private readonly List<Vector3Int> selectableCells = new List<Vector3Int>(8);
    private readonly List<Vector3> selectableWorldPositions = new List<Vector3>(8);
    private readonly List<bool> emphasizedChoices = new List<bool>(8);
    private Vector3 targetPosition;
    private PlayerActionSelectionMode selectionMode;
    private bool moving;
    private bool waitingForMonsters;
    private bool inputEnabled = true;
    private Vector3Int activeAttackOriginCell;
    private Vector3Int activeAttackDirection;
    private int activeAttackDamage;
    private int activeAttackPenetrations;
    private int remainingAttackCasts;
    private bool activeAttackKnockback;
    private bool attackHitReported;

    public int CurrentHealth => characterHealth != null ? characterHealth.CurrentHealth : 0;
    public PlayerActionSelectionMode SelectionMode => selectionMode;

    private void Awake()
    {
        // Move가 붙어 있는 플레이어 자신의 컴포넌트만 사용한다.
        playerAnimator = GetComponent<Animator>();
        playerSprite = GetComponent<SpriteRenderer>();
        characterHealth = GetComponent<CharacterHealth>();
        feedbackAudioSource = GetComponent<AudioSource>();
        actionIndicator = GetComponent<DirectionalActionIndicator>();

        if (characterHealth == null)
        {
            characterHealth = gameObject.AddComponent<CharacterHealth>();
        }

        characterHealth.Initialize(maxHealth);
        characterHealth.Died += HandleDeath;

        ConfigureFeedbackAudio();

        targetPosition = transform.position;
        SetMoving(false);
    }

    private void Start()
    {
        ResolveReferences();
    }

    private void Update()
    {
        if (!inputEnabled || waitingForMonsters)
        {
            CancelSelection();
            return;
        }

        if (moving)
        {
            MoveOneTile();
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
        {
            ToggleSelectionMode(PlayerActionSelectionMode.Attack);
            return;
        }

        if (keyboard != null && keyboard.sKey.wasPressedThisFrame)
        {
            ToggleSelectionMode(PlayerActionSelectionMode.Move);
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            CancelSelection();
            return;
        }

        if (selectionMode != PlayerActionSelectionMode.None
            && mouse != null
            && mouse.leftButton.wasPressedThisFrame)
        {
            TryConfirmSelectedCell(mouse.position.ReadValue());
        }
    }

    public void SelectAttackMode()
    {
        SetSelectionMode(PlayerActionSelectionMode.Attack);
    }

    public void SelectMoveMode()
    {
        SetSelectionMode(PlayerActionSelectionMode.Move);
    }

    public void CancelSelection()
    {
        if (selectionMode == PlayerActionSelectionMode.None)
            return;

        selectionMode = PlayerActionSelectionMode.None;
        selectableCells.Clear();
        selectableWorldPositions.Clear();
        emphasizedChoices.Clear();
        actionIndicator?.ClearChoices();
        SelectionModeChanged?.Invoke(selectionMode);
    }

    private void ToggleSelectionMode(PlayerActionSelectionMode requestedMode)
    {
        if (selectionMode == requestedMode)
        {
            CancelSelection();
            return;
        }

        SetSelectionMode(requestedMode);
    }

    private void SetSelectionMode(PlayerActionSelectionMode requestedMode)
    {
        ResolveReferences();

        if (!inputEnabled || waitingForMonsters || moving)
            return;

        if (gridManager == null || !gridManager.IsReady
            || monsterSpawner == null || projectileManager == null
            || actionIndicator == null)
        {
            Debug.LogError(
                "행동 선택에 GridManager, MonsterSpawner, ProjectileManager와 방향 표시기가 필요합니다.",
                this
            );
            return;
        }

        selectionMode = requestedMode;
        BuildSelectableCells();
        actionIndicator.ShowChoices(
            transform.position,
            selectableWorldPositions,
            selectionMode,
            emphasizedChoices
        );
        SelectionModeChanged?.Invoke(selectionMode);
    }

    private void BuildSelectableCells()
    {
        selectableCells.Clear();
        selectableWorldPositions.Clear();
        emphasizedChoices.Clear();

        Vector3Int currentCell = gridManager.WorldToCell(transform.position);

        foreach (Vector2Int direction in EightDirections)
        {
            Vector3Int destinationCell = currentCell
                + new Vector3Int(direction.x, direction.y, 0);

            if (selectionMode == PlayerActionSelectionMode.Move)
            {
                if (!gridManager.IsWalkableCell(destinationCell)
                    || monsterSpawner.TryGetMonsterAtCell(destinationCell, out _))
                {
                    continue;
                }
            }
            else if (!gridManager.IsInsideMapCell(destinationCell))
            {
                continue;
            }

            bool hasAttackTarget = selectionMode == PlayerActionSelectionMode.Attack
                && monsterSpawner.TryGetMonsterAtCell(destinationCell, out _);

            selectableCells.Add(destinationCell);
            selectableWorldPositions.Add(gridManager.GetCellCenterWorld(destinationCell));
            emphasizedChoices.Add(hasAttackTarget);
        }
    }

    private void TryConfirmSelectedCell(Vector2 pointerScreenPosition)
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        ResolveReferences();

        if (worldCamera == null || gridManager == null || !gridManager.IsReady)
            return;

        Vector3 pointerWorldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(pointerScreenPosition.x, pointerScreenPosition.y, 0f)
        );
        Vector3Int selectedCell = gridManager.WorldToCell(pointerWorldPosition);

        if (!selectableCells.Contains(selectedCell))
            return;

        if (selectionMode == PlayerActionSelectionMode.Move)
        {
            TryBeginMove(selectedCell);
        }
        else if (selectionMode == PlayerActionSelectionMode.Attack)
        {
            ExecuteAttack(selectedCell);
        }
    }

    private void TryBeginMove(Vector3Int destinationCell)
    {
        if (!gridManager.IsWalkableCell(destinationCell)
            || monsterSpawner.TryGetMonsterAtCell(destinationCell, out _))
        {
            PlayBlockedMoveFeedback();
            BuildSelectableCells();
            actionIndicator.ShowChoices(
                transform.position,
                selectableWorldPositions,
                selectionMode,
                emphasizedChoices
            );
            return;
        }

        Vector3Int currentCell = gridManager.WorldToCell(transform.position);
        Vector3Int difference = destinationCell - currentCell;
        UpdateFacing(new Vector2Int(difference.x, difference.y));

        targetPosition = gridManager.GetCellCenterWorld(destinationCell);
        targetPosition.z = transform.position.z;

        CancelSelection();
        SetMoving(true);
    }

    private void ExecuteAttack(Vector3Int attackCell)
    {
        Vector3Int currentCell = gridManager.WorldToCell(transform.position);
        Vector3Int difference = attackCell - currentCell;
        UpdateFacing(new Vector2Int(difference.x, difference.y));

        PlayerAttackTraitRoll traitRoll = playerTraitSystem != null
            ? playerTraitSystem.RollBasicAttack()
            : default;

        activeAttackOriginCell = currentCell;
        activeAttackDirection = difference;
        activeAttackDamage = attackDamage + (traitRoll.DamageBoost ? 1 : 0);
        activeAttackPenetrations = traitRoll.Pierce ? 1 : 0;
        remainingAttackCasts = traitRoll.DoubleCast ? 2 : 1;
        activeAttackKnockback = traitRoll.Knockback;
        attackHitReported = false;

        CancelSelection();
        // 플레이어 투사체의 전체 경로 비행이 끝날 때까지 추가 입력과 적 턴을 막는다.
        waitingForMonsters = true;

        LaunchNextAttackCast();
    }

    private void LaunchNextAttackCast()
    {
        if (remainingAttackCasts <= 0)
        {
            BeginMonsterTurn();
            return;
        }

        remainingAttackCasts--;

        bool projectileCreated = projectileManager.SpawnPlayerProjectile(
            activeAttackOriginCell,
            activeAttackDirection,
            activeAttackDamage,
            activeAttackPenetrations,
            HandleProjectileHit,
            HandleAttackCastCompleted
        );

        if (!projectileCreated)
        {
            Debug.LogError("플레이어 투사체를 생성하지 못했습니다.", this);
            remainingAttackCasts = 0;
            waitingForMonsters = false;
        }
    }

    private void HandleAttackCastCompleted()
    {
        if (remainingAttackCasts > 0)
        {
            LaunchNextAttackCast();
            return;
        }

        BeginMonsterTurn();
    }

    private void HandleProjectileHit(MonsterMovement targetMonster)
    {
        // 더블 캐스트나 관통으로 여러 번 맞혀도 게이지는 플레이어 행동당 한 번만 획득한다.
        if (!attackHitReported)
        {
            attackHitReported = true;
            AttackHit?.Invoke();
        }

        if (activeAttackKnockback && targetMonster != null && !targetMonster.IsDead)
        {
            monsterSpawner.TryKnockbackMonster(targetMonster, activeAttackDirection);
        }
    }

    private void UpdateFacing(Vector2Int direction)
    {
        if (direction.x < 0)
        {
            playerSprite.flipX = true;
        }
        else if (direction.x > 0)
        {
            playerSprite.flipX = false;
        }
    }

    private void ConfigureFeedbackAudio()
    {
        if (feedbackAudioSource == null)
        {
            feedbackAudioSource = gameObject.AddComponent<AudioSource>();
        }

        feedbackAudioSource.playOnAwake = false;
        feedbackAudioSource.loop = false;
        feedbackAudioSource.spatialBlend = 0f;

        if (blockedMoveSound == null)
        {
            generatedBlockedMoveSound = CreateBlockedMoveSound();
        }
    }

    private void PlayBlockedMoveFeedback()
    {
        AudioClip sound = blockedMoveSound != null
            ? blockedMoveSound
            : generatedBlockedMoveSound;

        if (feedbackAudioSource != null && sound != null)
        {
            feedbackAudioSource.PlayOneShot(sound, blockedMoveSoundVolume);
        }
    }

    private AudioClip CreateBlockedMoveSound()
    {
        int sampleCount = Mathf.CeilToInt(FeedbackSampleRate * BlockedSoundDuration);
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = (float)i / sampleCount;
            float frequency = Mathf.Lerp(190f, 90f, progress);
            float envelope = (1f - progress) * (1f - progress);

            phase += 2f * Mathf.PI * frequency / FeedbackSampleRate;
            samples[i] = (
                Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.35f
            ) * envelope * 0.3f;
        }

        AudioClip clip = AudioClip.Create(
            "GeneratedBlockedMove",
            sampleCount,
            1,
            FeedbackSampleRate,
            false
        );

        clip.hideFlags = HideFlags.DontSave;
        clip.SetData(samples, 0);
        return clip;
    }

    private void MoveOneTile()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            moveSpeed * Time.deltaTime
        );

        if (transform.position == targetPosition)
        {
            transform.position = targetPosition;
            SetMoving(false);
            PlayerMoved?.Invoke();
            BeginMonsterTurn();
        }
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;

        if (!inputEnabled)
        {
            CancelSelection();
        }
    }

    private void BeginMonsterTurn()
    {
        if (MoveCompleted == null)
        {
            waitingForMonsters = false;
            return;
        }

        waitingForMonsters = true;
        MoveCompleted.Invoke();
    }

    public void TakeDamage(int damage)
    {
        characterHealth.TakeDamage(damage);
    }

    public void CompleteMonsterTurn()
    {
        waitingForMonsters = false;
    }

    private void SetMoving(bool value)
    {
        moving = value;
        playerAnimator.SetBool(IsMoving, value);

        // IsMoving 변경을 즉시 평가해 이동 첫 프레임부터 상태를 전환한다.
        playerAnimator.Update(0f);
    }

    private void ResolveReferences()
    {
        if (gridManager == null)
        {
            gridManager = FindAnyObjectByType<GridManager>();
        }

        if (monsterSpawner == null)
        {
            monsterSpawner = FindAnyObjectByType<MonsterSpawner>();
        }

        if (projectileManager == null)
        {
            projectileManager = FindAnyObjectByType<ProjectileManager>();
        }

        if (playerTraitSystem == null)
        {
            playerTraitSystem = GetComponent<PlayerTraitSystem>();
        }

        if (actionIndicator == null)
        {
            actionIndicator = GetComponent<DirectionalActionIndicator>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;

            if (worldCamera == null)
            {
                worldCamera = FindAnyObjectByType<Camera>();
            }
        }
    }

    private void HandleDeath(CharacterHealth defeatedCharacter)
    {
        waitingForMonsters = true;
        CancelSelection();
        SetMoving(false);
        enabled = false;
        Debug.Log("플레이어가 사망했습니다. 이동 입력을 중지합니다.", this);
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        maxHealth = Mathf.Max(1, maxHealth);
        attackDamage = Mathf.Max(1, attackDamage);
        blockedMoveSoundVolume = Mathf.Clamp01(blockedMoveSoundVolume);
    }

    private void OnDestroy()
    {
        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDeath;
        }

        if (generatedBlockedMoveSound != null)
        {
            Destroy(generatedBlockedMoveSound);
        }
    }
}
