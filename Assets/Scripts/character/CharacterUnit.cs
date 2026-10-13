using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

public class CharacterUnit : MonoBehaviourPun
{
    private static readonly List<CharacterUnit> ActiveUnits = new List<CharacterUnit>();

    [Header("Character Data")]
    public string className;
    public string skinName;

    public int maxHP;
    public int currentHP;
    public int slotCount;
    // Visual hit recoil must not become camera tracking motion.
    public Vector3 PresentationOffset { get; private set; }
    public void AddPresentationOffset(Vector3 delta) => PresentationOffset += delta;

    [Header("Heavy Charge State")]
    public bool isChargingHeavy = false;
    public int heavyReleaseTurn = -1;

    [Header("Animator Override")]
    [SerializeField] private Animator animatorOverride;

    private DirectionalHealthBarUI healthBarUI;
    private Animator cachedAnimator;
    private Collider2D[] cachedBodyColliders;
    private bool initialIdleSynchronized;

    // Spawn delivery is local, but the opening Idle belongs to the shared music timeline.
    // Only seek once: registration can happen from both GameManager and TurnPlanningManager.
    public bool SynchronizeInitialIdle(int elapsedMilliseconds)
    {
        if (initialIdleSynchronized || elapsedMilliseconds < 0)
            return false;

        Animator animator = GetAnimator();
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            return false;

        if (!animator.isInitialized)
            animator.Update(0f);

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0) || !(state.IsName("Idle") || state.IsName("idle")))
        {
            // A late registration must never cancel an action that has already started.
            initialIdleSynchronized = true;
            return false;
        }

        float playbackSpeed = animator.speed * state.speed * state.speedMultiplier;
        if (state.length <= 0f || playbackSpeed <= 0f)
            return false;

        double cycles = elapsedMilliseconds / 1000.0 * playbackSpeed / state.length;
        float phase = (float)(cycles - System.Math.Floor(cycles));
        animator.Play(state.fullPathHash, 0, phase);
        animator.Update(0f);
        initialIdleSynchronized = true;
        return true;
    }

    private void Awake()
    {
        cachedAnimator = GetAnimator();
        CacheBodyColliders();
    }

    private void OnEnable()
    {
        RegisterNonPushingBodyCollision();
    }

    private void OnDisable()
    {
        ActiveUnits.Remove(this);
    }

    private void OnDestroy()
    {
        ActiveUnits.Remove(this);
    }

    private void Start()
    {
        RegisterNonPushingBodyCollision();
        RegisterToTurnPlanningManager();
    }

    public void Init(string className, string skinName, int maxHP, int slotCount)
    {
        this.className = className;
        this.skinName = skinName;
        this.maxHP = maxHP;
        this.currentHP = maxHP;
        this.slotCount = slotCount;

        BindHPBarByOwner();
        UpdateHPBar();

        Debug.Log($"Initialized character: {className}, Skin={skinName}, HP={maxHP}, Slots={slotCount}");
    }   

    private void RegisterToTurnPlanningManager()
    {
        TurnPlanningManager manager = FindFirstObjectByType<TurnPlanningManager>();
        if (manager != null)
        {
            manager.RegisterCharacter(this);
        }
        else
        {
            Debug.LogWarning("CharacterUnit: TurnPlanningManager not found; cannot auto-register character.");
        }
    }

    private void RegisterNonPushingBodyCollision()
    {
        PruneInactiveUnits();

        if (!ActiveUnits.Contains(this))
            ActiveUnits.Add(this);

        for (int i = 0; i < ActiveUnits.Count; i++)
        {
            CharacterUnit other = ActiveUnits[i];
            if (other == null || other == this || !other.isActiveAndEnabled)
                continue;

            IgnoreBodyCollisionWith(other);
        }
    }

    private void IgnoreBodyCollisionWith(CharacterUnit other)
    {
        Collider2D[] myColliders = GetBodyColliders();
        Collider2D[] otherColliders = other.GetBodyColliders();

        for (int i = 0; i < myColliders.Length; i++)
        {
            Collider2D myCollider = myColliders[i];
            if (myCollider == null)
                continue;

            for (int j = 0; j < otherColliders.Length; j++)
            {
                Collider2D otherCollider = otherColliders[j];
                if (otherCollider == null || otherCollider == myCollider)
                    continue;

                Physics2D.IgnoreCollision(myCollider, otherCollider, true);
            }
        }
    }

    private Collider2D[] GetBodyColliders()
    {
        if (cachedBodyColliders == null || cachedBodyColliders.Length == 0)
            CacheBodyColliders();

        return cachedBodyColliders;
    }

    private void CacheBodyColliders()
    {
        Collider2D rootCollider = GetComponent<Collider2D>();
        cachedBodyColliders = rootCollider != null
            ? new[] { rootCollider }
            : GetComponentsInChildren<Collider2D>(true);
    }

    private static void PruneInactiveUnits()
    {
        for (int i = ActiveUnits.Count - 1; i >= 0; i--)
        {
            CharacterUnit unit = ActiveUnits[i];
            if (unit == null || !unit.isActiveAndEnabled)
                ActiveUnits.RemoveAt(i);
        }
    }

    public Animator GetAnimator()
    {
        if (animatorOverride != null)
            return animatorOverride;

        if (cachedAnimator == null)
            cachedAnimator = GetComponentInChildren<Animator>(true);

        return cachedAnimator;
    }

    public bool IsMine()
    {
        return photonView != null && photonView.IsMine;
    }

    public bool IsOwnedByMaster()
    {
        return photonView != null && photonView.Owner != null && photonView.Owner.IsMasterClient;
    }

    private void BindHPBarByOwner()
    {
        if (BattleUIManager.Instance == null)
        {
            Debug.LogWarning("BattleUIManager.Instance is null; cannot bind health bar.");
            return;
        }

        if (photonView == null || photonView.Owner == null)
        {
            Debug.LogWarning("PhotonView or Owner is null; cannot bind health bar.");
            return;
        }

        bool ownerIsMaster = photonView.Owner.IsMasterClient;
        healthBarUI = BattleUIManager.Instance.GetBarByOwner(ownerIsMaster);
    }

    public void TakeDamage(int damage)
    {
        if (damage > 0)
        {
            GetComponent<CharacterShaderFeedback>()?.FlashHit();
            if (currentHP > 0 && CharacterPoseHitStop.FramesForDamage(damage) > 0)
            {
                foreach (CharacterUnit unit in ActiveUnits)
                {
                    if (unit == null || !unit.isActiveAndEnabled || unit.gameObject.scene != gameObject.scene) continue;
                    var hold = unit.GetComponent<CharacterPoseHitStop>();
                    if (hold == null) hold = unit.gameObject.AddComponent<CharacterPoseHitStop>();
                    hold.HoldForDamage(damage);
                }
            }
        }
        currentHP -= damage;
        if (currentHP < 0)
            currentHP = 0;

        UpdateHPBar();
    }

    public void Heal(int value)
    {
        currentHP += value;
        if (currentHP > maxHP)
            currentHP = maxHP;

        UpdateHPBar();
    }

    private void UpdateHPBar()
    {
        if (healthBarUI != null)
        {
            healthBarUI.SetHP(currentHP, maxHP);
        }
    }

    public void BeginHeavyCharge(int currentTurn, int chargeTurns)
    {
        isChargingHeavy = true;
        heavyReleaseTurn = currentTurn + Mathf.Max(1, chargeTurns);
    }

    public bool ShouldReleaseHeavy(int currentTurn)
    {
        return isChargingHeavy && currentTurn >= heavyReleaseTurn;
    }

    public void ClearHeavyCharge()
    {
        isChargingHeavy = false;
        heavyReleaseTurn = -1;
    }
}
