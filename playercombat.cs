using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// Gerencia o combate do jogador (ataques, detecção de inimigos, dano)
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 2f;
    public float attackAngle = 90f;
    public LayerMask enemyLayerMask = 1 << 8; // Layer dos inimigos
    public Transform attackPoint; // Ponto de origem dos ataques
    
    [Header("Attack Types")]
    public AttackData primaryAttack;
    public AttackData secondaryAttack;
    
    [Header("Combat Effects")]
    public GameObject hitEffectPrefab;
    public GameObject criticalHitEffectPrefab;
    
    [Header("Audio")]
    public AudioClip attackSound;
    public AudioClip hitSound;
    public AudioClip criticalHitSound;
    
    // Componentes
    private PlayerStats playerStats;
    private PlayerAnimationController animationController;
    private PlayerEquipment playerEquipment;
    
    // Estado do combate
    private bool canAttack = true;
    private float lastAttackTime;
    private int currentComboCount = 0;
    private int maxComboCount = 3;
    
    // Lista de inimigos já atingidos no ataque atual (para evitar hit múltiplo)
    private List<GameObject> hitEnemiesThisAttack = new List<GameObject>();
    
    private void Awake()
    {
        // Obter componentes
        playerStats = GetComponent<PlayerStats>();
        animationController = GetComponent<PlayerAnimationController>();
        playerEquipment = GetComponent<PlayerEquipment>();
        
        // Configurar attackPoint se não estiver definido
        if (attackPoint == null)
        {
            GameObject attackPointObj = new GameObject("AttackPoint");
            attackPointObj.transform.SetParent(transform);
            attackPointObj.transform.localPosition = Vector3.forward;
            attackPoint = attackPointObj.transform;
        }
    }
    
    private void Start()
    {
        // Inscrever nos eventos de input
        SubscribeToInputEvents();
    }
    
    private void Update()
    {
        UpdateCombatState();
    }
    
    private void SubscribeToInputEvents()
    {
        if (InputManager.Instance != null)
        {
            InputManager.OnPrimaryAttackInput += TryPrimaryAttack;
            InputManager.OnSecondaryAttackInput += TrySecondaryAttack;
        }
    }
    
    #region Attack System
    
    private void TryPrimaryAttack()
    {
        if (CanPerformAttack())
        {
            PerformAttack(primaryAttack, 0);
        }
    }
    
    private void TrySecondaryAttack()
    {
        if (CanPerformAttack())
        {
            PerformAttack(secondaryAttack, 1);
        }
    }
    
    private bool CanPerformAttack()
    {
        // Verificações para permitir ataque
        return canAttack && 
               playerStats.IsAlive && 
               !animationController.IsPlayingSkill &&
               GameManager.Instance.IsGamePlaying;
    }
    
    private void PerformAttack(AttackData attackData, int attackType)
    {
        // Verificar se tem mana suficiente
        if (attackData.manaCost > 0 && !playerStats.HasEnoughMana(attackData.manaCost))
        {
            Debug.Log("Mana insuficiente para atacar!");
            return;
        }
        
        // Consumir mana
        if (attackData.manaCost > 0)
        {
            playerStats.UseMana(attackData.manaCost);
        }
        
        // Configurar estado de ataque
        canAttack = false;
        lastAttackTime = Time.time;
        hitEnemiesThisAttack.Clear();
        
        // Atualizar combo
        UpdateComboCount();
        
        // Tocar som de ataque
        if (attackSound != null)
        {
            AudioManager.Instance?.PlaySFX(attackSound);
        }
        
        // Trigger da animação (será executado via Animation Event)
        // O dano real é aplicado no método ExecuteAttack()
    }
    
    /// <summary>
    /// Método chamado pelo Animation Event para executar o dano
    /// </summary>
    public void ExecuteAttack()
    {
        // Determinar qual ataque usar baseado no estado atual
        AttackData currentAttack = GetCurrentAttackData();
        
        if (currentAttack == null) return;
        
        // Detectar inimigos na área de ataque
        List<GameObject> enemiesInRange = DetectEnemiesInAttackRange(currentAttack);
        
        // Aplicar dano aos inimigos
        foreach (GameObject enemy in enemiesInRange)
        {
            if (!hitEnemiesThisAttack.Contains(enemy))
            {
                DealDamageToEnemy(enemy, currentAttack);
                hitEnemiesThisAttack.Add(enemy);
            }
        }
    }
    
    private AttackData GetCurrentAttackData()
    {
        // Determinar qual ataque foi usado baseado na animação atual
        if (animationController.IsPlayingAnimation("PrimaryAttack"))
            return primaryAttack;
        else if (animationController.IsPlayingAnimation("SecondaryAttack"))
            return secondaryAttack;
        
        return primaryAttack; // Fallback
    }
    
    #endregion
    
    #region Enemy Detection
    
    private List<GameObject> DetectEnemiesInAttackRange(AttackData attackData)
    {
        List<GameObject> detectedEnemies = new List<GameObject>();
        
        // Obter todos os colliders na área de ataque
        Collider[] hitColliders = Physics.OverlapSphere(attackPoint.position, attackData.range, enemyLayerMask);
        
        foreach (Collider col in hitColliders)
        {
            // Verificar se está dentro do ângulo de ataque
            Vector3 directionToEnemy = (col.transform.position - transform.position).normalized;
            float angleToEnemy = Vector3.Angle(transform.forward, directionToEnemy);
            
            if (angleToEnemy <= attackData.angle / 2f)
            {
                detectedEnemies.Add(col.gameObject);
            }
        }
        
        return detectedEnemies;
    }
    
    #endregion
    
    #region Damage Calculation
    
    private void DealDamageToEnemy(GameObject enemy, AttackData attackData)
    {
        EnemyStats enemyStats = enemy.GetComponent<EnemyStats>();
        if (enemyStats == null) return;
        
        // Calcular dano base
        float baseDamage = CalculateBaseDamage(attackData);
        
        // Calcular dano crítico
        bool isCritical = CalculateCriticalHit();
        float finalDamage = isCritical ? baseDamage * (playerStats.FinalCriticalDamage / 100f) : baseDamage;
        
        // Aplicar dano
        enemyStats.TakeDamage(finalDamage, transform.position);
        
        // Efeitos visuais e sonoros
        SpawnHitEffect(enemy.transform.position, isCritical);
        PlayHitSound(isCritical);
        
        // Aplicar efeitos da arma
        ApplyWeaponEffects(enemy);
        
        // Evento de dano
        EventManager.TriggerDamageDealt(finalDamage, enemy.transform.position);
        
        Debug.Log($"Dano aplicado: {finalDamage} {(isCritical ? "(CRÍTICO)" : "")}");
    }
    
    private float CalculateBaseDamage(AttackData attackData)
    {
        float damage = playerStats.FinalDamage;
        
        // Aplicar multiplicador do ataque
        damage *= attackData.damageMultiplier;
        
        // Aplicar bônus de combo
        damage *= GetComboDamageMultiplier();
        
        // Aplicar bônus de atributos baseado no tipo de dano
        damage += GetAttributeDamageBonus(attackData.damageType);
        
        // Adicionar dano da arma equipada
        Equipment weapon = playerEquipment?.GetEquippedItem(EquipmentSlot.MainHand) as Equipment;
        if (weapon != null)
        {
            damage += weapon.damage;
        }
        
        return damage;
    }
    
    private bool CalculateCriticalHit()
    {
        float critChance = playerStats.FinalCriticalChance;
        return Random.Range(0f, 100f) <= critChance;
    }
    
    private float GetComboDamageMultiplier()
    {
        // Aumento de dano baseado no combo
        return 1f + (currentComboCount * 0.1f); // +10% por hit no combo
    }
    
    private float GetAttributeDamageBonus(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Physical:
                return playerStats.FinalStrength * 0.5f;
            case DamageType.Magic:
            case DamageType.Fire:
            case DamageType.Cold:
            case DamageType.Lightning:
                return playerStats.FinalIntelligence * 0.5f;
            default:
                return 0f;
        }
    }
    
    #endregion
    
    #region Combat Effects
    
    private void SpawnHitEffect(Vector3 position, bool isCritical)
    {
        GameObject effectPrefab = isCritical ? criticalHitEffectPrefab : hitEffectPrefab;
        
        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, position, Quaternion.identity);
            Destroy(effect, 2f); // Destruir após 2 segundos
        }
    }
    
    private void PlayHitSound(bool isCritical)
    {
        AudioClip soundToPlay = isCritical ? criticalHitSound : hitSound;
        
        if (soundToPlay != null)
        {
            AudioManager.Instance?.PlaySFX(soundToPlay);
        }
    }
    
    private void ApplyWeaponEffects(GameObject enemy)
    {
        Equipment weapon = playerEquipment?.GetEquippedItem(EquipmentSlot.MainHand) as Equipment;
        
        if (weapon != null && weapon.weaponEffects.Count > 0)
        {
            foreach (SkillEffect effect in weapon.weaponEffects)
            {
                if (effect != null)
                {
                    effect.ApplyEffect(enemy, gameObject);
                }
            }
        }
    }
    
    #endregion
    
    #region Combo System
    
    private void UpdateComboCount()
    {
        float timeSinceLastAttack = Time.time - lastAttackTime;
        
        if (timeSinceLastAttack < 2f) // 2 segundos para manter combo
        {
            currentComboCount = Mathf.Min(currentComboCount + 1, maxComboCount);
        }
        else
        {
            currentComboCount = 1; // Reset combo
        }
    }
    
    #endregion
    
    #region Combat State
    
    private void UpdateCombatState()
    {
        // Reset da capacidade de atacar baseado na velocidade de ataque
        if (!canAttack)
        {
            float attackCooldown = 1f / playerStats.FinalAttackSpeed;
            
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                canAttack = true;
            }
        }
        
        // Reset do combo se passou muito tempo
        if (Time.time - lastAttackTime > 3f)
        {
            currentComboCount = 0;
        }
    }
    
    #endregion
    
    #region Public Methods
    
    public void SetAttackEnabled(bool enabled)
    {
        canAttack = enabled;
    }
    
    public void ResetCombo()
    {
        currentComboCount = 0;
    }
    
    public bool IsInCombat()
    {
        return Time.time - lastAttackTime < 5f; // Considerado em combate por 5 segundos após último ataque
    }
    
    #endregion
    
    #region Properties
    
    public bool CanAttack => canAttack;
    public int CurrentCombo => currentComboCount;
    public float LastAttackTime => lastAttackTime;
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        
        // Desenhar range de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        
        // Desenhar ângulo de ataque
        Vector3 leftBoundary = Quaternion.AngleAxis(-attackAngle / 2f, Vector3.up) * transform.forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(attackAngle / 2f, Vector3.up) * transform.forward;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(attackPoint.position, leftBoundary * attackRange);
        Gizmos.DrawRay(attackPoint.position, rightBoundary * attackRange);
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        if (InputManager.Instance != null)
        {
            InputManager.OnPrimaryAttackInput -= TryPrimaryAttack;
            InputManager.OnSecondaryAttackInput -= TrySecondaryAttack;
        }
    }
}

/// <summary>
/// Dados de um ataque específico
/// </summary>
[System.Serializable]
public class AttackData
{
    public string attackName = "Attack";
    public float damageMultiplier = 1f;
    public float range = 2f;
    public float angle = 90f;
    public float manaCost = 0f;
    public DamageType damageType = DamageType.Physical;
    public float knockbackForce = 0f;
}