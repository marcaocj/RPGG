using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerencia as estatísticas e estado de saúde dos inimigos
/// </summary>
public class EnemyStats : MonoBehaviour
{
    [Header("Basic Stats")]
    public int enemyLevel = 1;
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public int damage = 15;
    public int armor = 5;
    public float movementSpeed = 3f;
    
    [Header("Combat Stats")]
    public float attackSpeed = 1f;
    public float criticalChance = 2f;
    public float criticalDamage = 150f;
    public float attackRange = 2f;
    
    [Header("Resistances")]
    public float fireResistance = 0f;
    public float coldResistance = 0f;
    public float lightningResistance = 0f;
    public float poisonResistance = 0f;
    public float physicalResistance = 0f;
    
    [Header("Experience and Loot")]
    public int experienceReward = 25;
    public int goldReward = 10;
    public List<LootDrop> lootTable = new List<LootDrop>();
    public float lootDropChance = 0.3f;
    
    [Header("Status")]
    public bool isAlive = true;
    public bool isInvulnerable = false;
    public float invulnerabilityDuration = 0f;
    
    [Header("Death Effects")]
    public GameObject deathEffect;
    public AudioClip deathSound;
    public AudioClip attackSound; // Som de ataque para o AI usar
    public float corpseLifetime = 10f;
    
    // Eventos
    public System.Action<float, float> OnHealthChanged; // current, max
    public System.Action OnDeath;
    public System.Action<float, Vector3> OnDamageTaken; // damage, position
    
    // Componentes relacionados
    private StatusEffectManager statusEffectManager;
    private EnemyController enemyController;
    private EnemyAI enemyAI;
    
    // Estado interno
    private float lastDamageTime;
    private GameObject lastAttacker;
    
    private void Awake()
    {
        // Obter componentes
        statusEffectManager = GetComponent<StatusEffectManager>();
        enemyController = GetComponent<EnemyController>();
        enemyAI = GetComponent<EnemyAI>();
        
        // Se não tem StatusEffectManager, adicionar
        if (statusEffectManager == null)
        {
            statusEffectManager = gameObject.AddComponent<StatusEffectManager>();
        }
    }
    
    private void Start()
    {
        // Configurar vida inicial
        currentHealth = maxHealth;
        
        // Escalar stats baseado no nível
        ScaleStatsWithLevel();
        
        // Disparar evento inicial de saúde
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    private void Update()
    {
        // Atualizar invulnerabilidade
        if (isInvulnerable && invulnerabilityDuration > 0f)
        {
            invulnerabilityDuration -= Time.deltaTime;
            if (invulnerabilityDuration <= 0f)
            {
                isInvulnerable = false;
            }
        }
    }
    
    #region Health Management
    
    /// <summary>
    /// Aplica dano ao inimigo
    /// </summary>
    public void TakeDamage(float damageAmount, Vector3 damageSource, DamageType damageType = DamageType.Physical)
    {
        if (!isAlive || isInvulnerable || damageAmount <= 0) return;
        
        // Calcular resistência
        float resistance = GetResistance(damageType);
        float finalDamage = damageAmount * (1f - resistance / 100f);
        
        // Aplicar redução de armor para dano físico
        if (damageType == DamageType.Physical)
        {
            finalDamage = CalculatePhysicalDamageReduction(finalDamage);
        }
        
        // Aplicar dano
        currentHealth = Mathf.Max(0f, currentHealth - finalDamage);
        lastDamageTime = Time.time;
        
        // Definir último atacante
        GameObject attacker = FindAttackerFromPosition(damageSource);
        if (attacker != null)
        {
            lastAttacker = attacker;
        }
        
        // Disparar eventos
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(finalDamage, transform.position);
        EventManager.TriggerDamageDealt(finalDamage, transform.position);
        
        // Alertar IA sobre o dano
        if (enemyAI != null)
        {
            enemyAI.OnTakeDamage(finalDamage, damageSource);
        }
        
        // Verificar morte
        if (currentHealth <= 0f)
        {
            Die();
        }
        
        Debug.Log($"{gameObject.name} tomou {finalDamage:F1} de dano. Vida restante: {currentHealth:F1}");
    }
    
    /// <summary>
    /// Aplica dano simples (sem tipo específico)
    /// </summary>
    public void TakeDamage(float damageAmount, Vector3 damageSource)
    {
        TakeDamage(damageAmount, damageSource, DamageType.Physical);
    }
    
    /// <summary>
    /// Cura o inimigo
    /// </summary>
    public void Heal(float healAmount)
    {
        if (!isAlive || healAmount <= 0) return;
        
        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        Debug.Log($"{gameObject.name} foi curado em {healAmount:F1}. Vida atual: {currentHealth:F1}");
    }
    
    /// <summary>
    /// Define invulnerabilidade temporária
    /// </summary>
    public void SetInvulnerable(float duration)
    {
        isInvulnerable = true;
        invulnerabilityDuration = duration;
    }
    
    #endregion
    
    #region Death System
    
    private void Die()
    {
        if (!isAlive) return;
        
        isAlive = false;
        currentHealth = 0f;
        
        // Disparar eventos
        OnDeath?.Invoke();
        EventManager.TriggerEnemyDeath(gameObject);
        
        // Dar experiência e loot ao jogador
        GiveRewards();
        
        // Efeitos de morte
        PlayDeathEffects();
        
        // Desativar componentes
        DisableComponents();
        
        // Agendar destruição
        Destroy(gameObject, corpseLifetime);
        
        Debug.Log($"{gameObject.name} morreu!");
    }
    
    private void GiveRewards()
    {
        if (lastAttacker != null && lastAttacker.CompareTag("Player"))
        {
            // Dar experiência
            PlayerStats playerStats = lastAttacker.GetComponent<PlayerStats>();
            if (playerStats != null && experienceReward > 0)
            {
                playerStats.GainExperience(experienceReward);
            }
            
            // Dar ouro
            PlayerInventory playerInventory = lastAttacker.GetComponent<PlayerInventory>();
            if (playerInventory != null && goldReward > 0)
            {
                playerInventory.AddGold(goldReward);
            }
        }
        
        // Dropar loot
        DropLoot();
    }
    
    private void DropLoot()
    {
        foreach (LootDrop lootDrop in lootTable)
        {
            if (Random.Range(0f, 1f) <= lootDrop.dropChance)
            {
                Vector3 dropPosition = transform.position + Random.insideUnitSphere * 2f;
                dropPosition.y = transform.position.y; // Manter no chão
                
                // Instanciar item no mundo
                if (lootDrop.item != null)
                {
                    GameObject lootObject = Instantiate(lootDrop.item.worldModel, dropPosition, Quaternion.identity);
                    
                    // Adicionar componente LootItem se não tiver
                    LootItem lootComponent = lootObject.GetComponent<LootItem>();
                    if (lootComponent == null)
                    {
                        lootComponent = lootObject.AddComponent<LootItem>();
                    }
                    
                    lootComponent.Initialize(lootDrop.item, lootDrop.quantity);
                }
            }
        }
    }
    
    private void PlayDeathEffects()
    {
        // Efeito visual
        if (deathEffect != null)
        {
            GameObject effect = Instantiate(deathEffect, transform.position, transform.rotation);
            Destroy(effect, 5f);
        }
        
        // Som de morte
        if (deathSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(deathSound, transform.position);
        }
    }
    
    private void DisableComponents()
    {
        // Desativar controlador e IA
        if (enemyController != null)
            enemyController.enabled = false;
        
        if (enemyAI != null)
            enemyAI.enabled = false;
        
        // Desativar colliders de combate
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (!col.isTrigger) // Manter triggers para loot
                col.enabled = false;
        }
        
        // Parar animações se houver
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Death");
        }
    }
    
    #endregion
    
    #region Damage Calculations
    
    private float CalculatePhysicalDamageReduction(float damage)
    {
        // Fórmula de redução de dano baseada em armor
        float reduction = armor / (armor + 100f);
        return damage * (1f - reduction);
    }
    
    private float GetResistance(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Fire: return fireResistance;
            case DamageType.Cold: return coldResistance;
            case DamageType.Lightning: return lightningResistance;
            case DamageType.Poison: return poisonResistance;
            case DamageType.Physical: return physicalResistance;
            default: return 0f;
        }
    }
    
    private GameObject FindAttackerFromPosition(Vector3 damageSource)
    {
        // Tentar encontrar o atacante baseado na posição
        GameObject player = GameManager.Instance?.CurrentPlayer;
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(damageSource, player.transform.position);
            if (distanceToPlayer < 10f) // Assumir que veio do player se estiver próximo
            {
                return player;
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region Stat Scaling
    
    private void ScaleStatsWithLevel()
    {
        if (enemyLevel <= 1) return;
        
        float levelMultiplier = 1f + (enemyLevel - 1) * 0.2f; // +20% por nível
        
        // Escalar stats principais
        maxHealth *= levelMultiplier;
        currentHealth = maxHealth;
        damage = Mathf.RoundToInt(damage * levelMultiplier);
        armor = Mathf.RoundToInt(armor * levelMultiplier);
        
        // Escalar recompensas
        experienceReward = Mathf.RoundToInt(experienceReward * levelMultiplier);
        goldReward = Mathf.RoundToInt(goldReward * levelMultiplier);
    }
    
    #endregion
    
    #region Status Queries
    
    public bool IsAlive => isAlive;
    public bool IsInvulnerable => isInvulnerable;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsAtFullHealth => currentHealth >= maxHealth;
    public bool IsLowHealth => HealthPercentage <= 0.25f;
    public float TimeSinceLastDamage => Time.time - lastDamageTime;
    public GameObject LastAttacker => lastAttacker;
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Força a morte do inimigo
    /// </summary>
    public void ForceKill()
    {
        currentHealth = 0f;
        Die();
    }
    
    /// <summary>
    /// Reseta as estatísticas para valores iniciais
    /// </summary>
    public void ResetStats()
    {
        currentHealth = maxHealth;
        isAlive = true;
        isInvulnerable = false;
        invulnerabilityDuration = 0f;
        lastAttacker = null;
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    /// <summary>
    /// Modifica stat temporariamente
    /// </summary>
    public void ModifyStat(string statName, float modifier, float duration)
    {
        // Implementar modificação temporária de stats se necessário
        // Similar ao sistema do PlayerStats
        switch (statName.ToLower())
        {
            case "damage":
                // Aplicar modificador temporário de dano
                break;
            case "movementspeed":
                // Aplicar modificador temporário de velocidade
                if (enemyController != null)
                {
                    // Notificar o controller sobre mudança de velocidade
                }
                break;
            case "attackspeed":
                // Aplicar modificador temporário de velocidade de ataque
                break;
        }
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmosSelected()
    {
        // Desenhar range de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Indicador de saúde
        if (isAlive)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, HealthPercentage);
            Vector3 healthBarPos = transform.position + Vector3.up * 2f;
            Gizmos.DrawLine(healthBarPos, healthBarPos + Vector3.right * (HealthPercentage * 2f));
        }
        
        // Desenhar nível do inimigo
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.2f);
    }
    
    #endregion
}

/// <summary>
/// Configuração de loot drop
/// </summary>
[System.Serializable]
public class LootDrop
{
    public Item item;
    public int quantity = 1;
    [Range(0f, 1f)]
    public float dropChance = 0.1f;
}