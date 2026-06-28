using Photon.Pun;
using UnityEngine;

public class CharacterUnit : MonoBehaviourPun
{
    [Header("角色資料")]
    public string className;
    public string skinName;

    public int maxHP;
    public int currentHP;
    public int slotCount;

    [Header("蓄力狀態")]
    public bool isChargingHeavy = false;
    public int heavyReleaseTurn = -1;

    [Header("可選：手動指定 Animator，不指定就自動抓")]
    [SerializeField] private Animator animatorOverride;

    private DirectionalHealthBarUI healthBarUI;
    private Animator cachedAnimator;

    private void Awake()
    {
        cachedAnimator = GetAnimator();
    }

    private void Start()
    {
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

        Debug.Log($"初始化角色: {className}, Skin={skinName}, HP={maxHP}, Slots={slotCount}");
    }   

    private void RegisterToTurnPlanningManager()
    {
        TurnPlanningManager manager = FindObjectOfType<TurnPlanningManager>();
        if (manager != null)
        {
            manager.RegisterCharacter(this);
        }
        else
        {
            Debug.LogWarning("CharacterUnit: 找不到 TurnPlanningManager，無法自動註冊角色");
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
            Debug.LogWarning("BattleUIManager.Instance 為空，無法綁定血條");
            return;
        }

        if (photonView == null || photonView.Owner == null)
        {
            Debug.LogWarning("PhotonView 或 Owner 為空，無法綁定血條");
            return;
        }

        bool ownerIsMaster = photonView.Owner.IsMasterClient;
        healthBarUI = BattleUIManager.Instance.GetBarByOwner(ownerIsMaster);
    }

    public void TakeDamage(int damage)
    {
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