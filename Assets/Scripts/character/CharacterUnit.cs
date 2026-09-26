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

    [Header("Heavy Charge State")]
    public bool isChargingHeavy = false;
    public int heavyReleaseTurn = -1;

    [Header("Animator Override")]
    [SerializeField] private Animator animatorOverride;

    private DirectionalHealthBarUI healthBarUI;
    private Animator cachedAnimator;
    private Collider2D[] cachedBodyColliders;

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
            GetComponent<CharacterShaderFeedback>()?.FlashHit();
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
