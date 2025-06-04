using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Classe base para todas as skills/habilidades do jogo
/// </summary>
[CreateAssetMenu(fileName = "New Skill", menuName = "Game/Skills/Basic Skill")]
public class Skill : ScriptableObject
{
    [Header("Basic Info")]
    public int skillID;
    public string skillName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
    
    [Header("Skill Properties")]
    public SkillType skillType;
    public TargetType targetType;
    public DamageType damageType = DamageType.Magic;
    
    [Header("Requirements")]
    public int levelRequirement = 1;
    public int skillPointCost = 1;
    public List<Skill> prerequisiteSkills = new List<Skill>();
    
    [Header("Costs")]
    public float manaCost = 10f;
    public float healthCost = 0f;
    public float staminaCost = 0f;
    
    [Header("Cooldown and Cast")]
    public float cooldownTime = 5f;
    public float castTime = 0f; // 0 = instantâneo
    public float channelTime = 0f; // Tempo de canalização
    public bool canCastWhileMoving = true;
    public bool interruptible = true;
    
    [Header("Range and Area")]
    public float skillRange = 5f;
    public float areaOfEffect = 0f; // 0 = single target
    public LayerMask targetLayers = -1;
    
    [Header("Damage and Effects")]
    public float baseDamage = 25f;
    public float damageScaling = 1f; // Scaling com atributos
    public List<SkillEffect> skillEffects = new List<SkillEffect>();
    
    [Header("Visual and Audio")]
    public GameObject castEffectPrefab;
    public GameObject projectilePrefab;
    public GameObject impactEffectPrefab;
    public AudioClip castSound;
    public AudioClip impactSound;
    
    [Header("Animation")]
    public string animationTrigger = "Skill";
    public int animationID = 1;
    public float animationSpeed = 1f;
    
    /// <summary>
    /// Verifica se a skill pode ser usada
    /// </summary>
    public virtual bool CanUse(GameObject caster)
    {
        if (caster == null) return false;
        
        PlayerStats playerStats = caster.GetComponent<PlayerStats>();
        if (playerStats == null) return false;
        
        // Verificar se está vivo
        if (!playerStats.IsAlive) return false;
        
        // Verificar nível
        if (playerStats.level < levelRequirement) return false;
        
        // Verificar recursos
        if (!HasSufficientResources(playerStats)) return false;
        
        // Verificar cooldown
        PlayerSkillManager skillManager = caster.GetComponent<PlayerSkillManager>();
        if (skillManager != null && !skillManager.IsSkillReady(skillID)) return false;
        
        // Verificar status effects
        StatusEffectManager statusManager = caster.GetComponent<StatusEffectManager>();
        if (statusManager != null)
        {
            if (statusManager.IsSilenced() || statusManager.IsStunned()) return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Usa a skill
    /// </summary>
    public virtual bool UseSkill(GameObject caster, Vector3 targetPosition, GameObject targetObject = null)
    {
        if (!CanUse(caster)) return false;
        
        PlayerStats playerStats = caster.GetComponent<PlayerStats>();
        PlayerSkillManager skillManager = caster.GetComponent<PlayerSkillManager>();
        
        // Consumir recursos
        ConsumeResources(playerStats);
        
        // Iniciar cooldown
        if (skillManager != null)
        {
            skillManager.StartSkillCooldown(skillID, cooldownTime);
        }
        
        // Executar skill
        if (castTime > 0f)
        {
            // Skill com tempo de cast
            StartCasting(caster, targetPosition, targetObject);
        }
        else
        {
            // Skill instantânea
            ExecuteSkill(caster, targetPosition, targetObject);
        }
        
        // Disparar evento
        EventManager.TriggerSkillUsed(skillName);
        
        Debug.Log($"Skill usada: {skillName}");
        return true;
    }
    
    /// <summary>
    /// Inicia o processo de cast da skill
    /// </summary>
    protected virtual void StartCasting(GameObject caster, Vector3 targetPosition, GameObject targetObject)
    {
        PlayerAnimationController animController = caster.GetComponent<PlayerAnimationController>();
        if (animController != null)
        {
            animController.TriggerSkillAnimation(animationID, animationSpeed);
        }
        
        // Efeito visual de cast
        if (castEffectPrefab != null)
        {
            GameObject castEffect = Instantiate(castEffectPrefab, caster.transform.position, caster.transform.rotation);
            Destroy(castEffect, castTime + 1f);
        }
        
        // Som de cast
        if (castSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(castSound, caster.transform.position);
        }
        
        // Criar timer para executar a skill após o cast time
        TimeManager.Instance?.CreateTimer(castTime, () => {
            ExecuteSkill(caster, targetPosition, targetObject);
        });
    }
    
    /// <summary>
    /// Executa o efeito principal da skill
    /// </summary>
    protected virtual void ExecuteSkill(GameObject caster, Vector3 targetPosition, GameObject targetObject)
    {
        switch (skillType)
        {
            case SkillType.Projectile:
                FireProjectile(caster, targetPosition);
                break;
                
            case SkillType.Instant:
                ApplyInstantEffect(caster, targetPosition, targetObject);
                break;
                
            case SkillType.AreaOfEffect:
                ApplyAreaEffect(caster, targetPosition);
                break;
                
            case SkillType.Buff:
                ApplyBuffEffect(caster, targetObject ?? caster);
                break;
                
            case SkillType.Heal:
                ApplyHealEffect(caster, targetObject ?? caster);
                break;
                
            case SkillType.Channel:
                StartChanneling(caster, targetPosition, targetObject);
                break;
        }
    }
    
    #region Skill Execution Methods
    
    protected virtual void FireProjectile(GameObject caster, Vector3 targetPosition)
    {
        if (projectilePrefab == null) return;
        
        Vector3 spawnPosition = caster.transform.position + caster.transform.forward * 1f + Vector3.up * 1f;
        Vector3 direction = (targetPosition - spawnPosition).normalized;
        
        GameObject projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        // Configurar projétil
        SkillProjectile projectileScript = projectile.GetComponent<SkillProjectile>();
        if (projectileScript != null)
        {
            projectileScript.Initialize(this, caster, targetPosition);
        }
        else
        {
            // Fallback: movimento simples
            Rigidbody rb = projectile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * 20f; // Velocidade padrão
            }
            
            // Destruir após tempo
            Destroy(projectile, 5f);
        }
    }
    
    protected virtual void ApplyInstantEffect(GameObject caster, Vector3 targetPosition, GameObject targetObject)
    {
        List<GameObject> targets = GetTargetsInRange(caster, targetPosition);
        
        foreach (GameObject target in targets)
        {
            ApplySkillEffectsToTarget(caster, target);
            
            // Efeito de impacto
            SpawnImpactEffect(target.transform.position);
        }
    }
    
    protected virtual void ApplyAreaEffect(GameObject caster, Vector3 targetPosition)
    {
        List<GameObject> targets = GetTargetsInArea(targetPosition, areaOfEffect);
        
        foreach (GameObject target in targets)
        {
            ApplySkillEffectsToTarget(caster, target);
        }
        
        // Efeito visual da área
        SpawnImpactEffect(targetPosition);
    }
    
    protected virtual void ApplyBuffEffect(GameObject caster, GameObject target)
    {
        foreach (SkillEffect effect in skillEffects)
        {
            if (effect != null && effect.beneficial)
            {
                effect.ApplyEffect(target, caster);
            }
        }
        
        SpawnImpactEffect(target.transform.position);
    }
    
    protected virtual void ApplyHealEffect(GameObject caster, GameObject target)
    {
        float healAmount = CalculateHealAmount(caster);
        
        PlayerStats targetStats = target.GetComponent<PlayerStats>();
        if (targetStats != null)
        {
            targetStats.Heal(healAmount);
        }
        
        // Aplicar outros efeitos
        foreach (SkillEffect effect in skillEffects)
        {
            if (effect != null)
            {
                effect.ApplyEffect(target, caster);
            }
        }
        
        SpawnImpactEffect(target.transform.position);
    }
    
    protected virtual void StartChanneling(GameObject caster, Vector3 targetPosition, GameObject targetObject)
    {
        // Implementar canalização se necessário
        Debug.Log($"Canalizando skill: {skillName} por {channelTime} segundos");
        
        // Criar timer para ticks durante canalização
        float tickInterval = 1f; // Tick a cada segundo
        int tickCount = Mathf.FloorToInt(channelTime / tickInterval);
        
        for (int i = 1; i <= tickCount; i++)
        {
            float delay = i * tickInterval;
            TimeManager.Instance?.CreateTimer(delay, () => {
                ExecuteChannelTick(caster, targetPosition, targetObject);
            });
        }
    }
    
    protected virtual void ExecuteChannelTick(GameObject caster, Vector3 targetPosition, GameObject targetObject)
    {
        // Aplicar efeito por tick de canalização
        ApplyInstantEffect(caster, targetPosition, targetObject);
    }
    
    #endregion
    
    #region Target Detection
    
    protected virtual List<GameObject> GetTargetsInRange(GameObject caster, Vector3 targetPosition)
    {
        List<GameObject> targets = new List<GameObject>();
        
        if (targetType == TargetType.Self)
        {
            targets.Add(caster);
            return targets;
        }
        
        Collider[] colliders = Physics.OverlapSphere(targetPosition, skillRange, targetLayers);
        
        foreach (Collider col in colliders)
        {
            if (IsValidTarget(caster, col.gameObject))
            {
                targets.Add(col.gameObject);
                
                if (targetType == TargetType.Single)
                    break; // Apenas um alvo para skills single target
            }
        }
        
        return targets;
    }
    
    protected virtual List<GameObject> GetTargetsInArea(Vector3 center, float radius)
    {
        List<GameObject> targets = new List<GameObject>();
        
        Collider[] colliders = Physics.OverlapSphere(center, radius, targetLayers);
        
        foreach (Collider col in colliders)
        {
            targets.Add(col.gameObject);
        }
        
        return targets;
    }
    
    protected virtual bool IsValidTarget(GameObject caster, GameObject target)
    {
        if (target == null) return false;
        
        switch (targetType)
        {
            case TargetType.Self:
                return target == caster;
                
            case TargetType.Enemy:
                return target.CompareTag("Enemy");
                
            case TargetType.Ally:
                return target.CompareTag("Player") || target.CompareTag("Ally");
                
            case TargetType.Single:
            case TargetType.Multiple:
                return target.CompareTag("Enemy") || target.CompareTag("Player") || target.CompareTag("Ally");
                
            default:
                return true;
        }
    }
    
    #endregion
    
    #region Effect Application
    
    protected virtual void ApplySkillEffectsToTarget(GameObject caster, GameObject target)
    {
        // Aplicar dano se houver
        if (baseDamage > 0)
        {
            float damage = CalculateDamage(caster);
            ApplyDamageToTarget(target, damage, caster);
        }
        
        // Aplicar efeitos
        foreach (SkillEffect effect in skillEffects)
        {
            if (effect != null)
            {
                effect.ApplyEffect(target, caster);
            }
        }
    }
    
    protected virtual void ApplyDamageToTarget(GameObject target, float damage, GameObject caster)
    {
        if (target.CompareTag("Enemy"))
        {
            EnemyStats enemyStats = target.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(damage, caster.transform.position);
            }
        }
        else if (target.CompareTag("Player"))
        {
            PlayerStats playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(damage);
            }
        }
        
        // Evento de dano
        EventManager.TriggerDamageDealt(damage, target.transform.position);
    }
    
    #endregion
    
    #region Calculations
    
    protected virtual float CalculateDamage(GameObject caster)
    {
        float damage = baseDamage;
        
        PlayerStats casterStats = caster.GetComponent<PlayerStats>();
        if (casterStats != null)
        {
            // Scaling baseado no tipo de dano
            switch (damageType)
            {
                case DamageType.Physical:
                    damage += casterStats.FinalStrength * damageScaling;
                    break;
                case DamageType.Magic:
                case DamageType.Fire:
                case DamageType.Cold:
                case DamageType.Lightning:
                case DamageType.Poison:
                    damage += casterStats.FinalIntelligence * damageScaling;
                    break;
            }
            
            // Aplicar bônus de dano geral
            damage += casterStats.FinalDamage * 0.1f;
        }
        
        return damage;
    }
    
    protected virtual float CalculateHealAmount(GameObject caster)
    {
        float heal = baseDamage; // Usar baseDamage como base de cura
        
        PlayerStats casterStats = caster.GetComponent<PlayerStats>();
        if (casterStats != null)
        {
            // Scaling com inteligência para cura
            heal += casterStats.FinalIntelligence * damageScaling;
        }
        
        return heal;
    }
    
    #endregion
    
    #region Resource Management
    
    protected virtual bool HasSufficientResources(PlayerStats playerStats)
    {
        if (manaCost > 0 && !playerStats.HasEnoughMana(manaCost))
            return false;
        
        if (healthCost > 0 && playerStats.currentHealth <= healthCost)
            return false;
        
        // Stamina seria implementado se existisse
        
        return true;
    }
    
    protected virtual void ConsumeResources(PlayerStats playerStats)
    {
        if (manaCost > 0)
        {
            playerStats.UseMana(manaCost);
        }
        
        if (healthCost > 0)
        {
            playerStats.TakeDamage(healthCost);
        }
    }
    
    #endregion
    
    #region Visual Effects
    
    protected virtual void SpawnImpactEffect(Vector3 position)
    {
        if (impactEffectPrefab != null)
        {
            GameObject effect = Instantiate(impactEffectPrefab, position, Quaternion.identity);
            Destroy(effect, 3f);
        }
        
        if (impactSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(impactSound, position);
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Obtém texto do tooltip da skill
    /// </summary>
    public virtual string GetTooltipText()
    {
        string tooltip = $"<color=cyan><b>{skillName}</b></color>\n\n";
        tooltip += description + "\n\n";
        
        // Custos
        if (manaCost > 0)
            tooltip += $"<color=blue>Custo de Mana: {manaCost}</color>\n";
        if (healthCost > 0)
            tooltip += $"<color=red>Custo de Vida: {healthCost}</color>\n";
        
        // Cooldown
        if (cooldownTime > 0)
            tooltip += $"<color=yellow>Cooldown: {cooldownTime}s</color>\n";
        
        // Dano
        if (baseDamage > 0)
            tooltip += $"<color=orange>Dano Base: {baseDamage}</color>\n";
        
        // Range
        if (skillRange > 0)
            tooltip += $"Alcance: {skillRange}m\n";
        
        return tooltip;
    }
    
    /// <summary>
    /// Cria cópia da skill
    /// </summary>
    public virtual Skill CreateCopy()
    {
        return Instantiate(this);
    }
    
    #endregion
}

/// <summary>
/// Tipos de skill
/// </summary>
public enum SkillType
{
    Instant,        // Efeito instantâneo
    Projectile,     // Projétil
    AreaOfEffect,   // Área de efeito
    Buff,           // Melhoria
    Heal,           // Cura
    Channel,        // Canalização
    Toggle          // Liga/desliga
}

/// <summary>
/// Tipos de alvo
/// </summary>
public enum TargetType
{
    Self,       // Próprio caster
    Single,     // Alvo único
    Multiple,   // Múltiplos alvos
    Enemy,      // Apenas inimigos
    Ally,       // Apenas aliados
    Ground      // Posição no chão
}