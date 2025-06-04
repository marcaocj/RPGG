using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Representa um efeito que pode ser aplicado por skills ou itens
/// </summary>
[CreateAssetMenu(fileName = "New Skill Effect", menuName = "Game/Skills/Effect")]
public class SkillEffect : ScriptableObject
{
    [Header("Basic Info")]
    public string effectName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;
    
    [Header("Effect Properties")]
    public EffectType effectType;
    public EffectTarget targetType;
    public float duration = 5f;
    public float value = 10f;
    public float interval = 1f; // Para efeitos periódicos
    public bool isStackable = false;
    public int maxStacks = 1;
    
    [Header("Visual Effects")]
    public GameObject effectPrefab;
    public Color effectColor = Color.white;
    public ParticleSystem particleEffect;
    
    [Header("Audio")]
    public AudioClip applySound;
    public AudioClip tickSound; // Som para efeitos periódicos
    public AudioClip removeSound;
    
    [Header("Damage Over Time")]
    public DamageType damageType = DamageType.Magic;
    public bool canCrit = false;
    
    [Header("Movement Effects")]
    public float movementSpeedModifier = 0f; // % de mudança na velocidade
    public bool rootsTarget = false; // Impede movimento
    
    [Header("Combat Effects")]
    public float damageModifier = 0f;
    public float defenseModifier = 0f;
    public float attackSpeedModifier = 0f;
    
    [Header("Stat Modifiers")]
    public List<StatModifierData> statModifiers = new List<StatModifierData>();
    
    [Header("Special Properties")]
    public bool dispellable = true;
    public bool beneficial = true; // True para buffs, false para debuffs
    public int priority = 0; // Para ordenação de efeitos
    
    /// <summary>
    /// Aplica o efeito a um alvo
    /// </summary>
    public void ApplyEffect(GameObject target, GameObject caster = null)
    {
        if (target == null) return;
        
        StatusEffectManager statusManager = target.GetComponent<StatusEffectManager>();
        if (statusManager == null)
        {
            statusManager = target.AddComponent<StatusEffectManager>();
        }
        
        // Criar instância do efeito
        ActiveEffect activeEffect = new ActiveEffect(this, caster);
        
        // Aplicar o efeito
        statusManager.AddEffect(activeEffect);
        
        // Efeitos visuais e sonoros
        PlayApplyEffect(target);
        
        Debug.Log($"Efeito aplicado: {effectName} em {target.name}");
    }
    
    /// <summary>
    /// Remove o efeito de um alvo
    /// </summary>
    public void RemoveEffect(GameObject target)
    {
        StatusEffectManager statusManager = target.GetComponent<StatusEffectManager>();
        if (statusManager != null)
        {
            statusManager.RemoveEffect(this);
        }
    }
    
    /// <summary>
    /// Executa o tick do efeito (para DoT, HoT, etc.)
    /// </summary>
    public void ExecuteTick(ActiveEffect activeEffect)
    {
        GameObject target = activeEffect.target;
        GameObject caster = activeEffect.caster;
        
        if (target == null) return;
        
        switch (effectType)
        {
            case EffectType.DamageOverTime:
                ApplyDamageOverTime(target, caster);
                break;
                
            case EffectType.HealOverTime:
                ApplyHealOverTime(target);
                break;
                
            case EffectType.ManaRegeneration:
                ApplyManaRegeneration(target);
                break;
                
            case EffectType.Custom:
                ExecuteCustomEffect(target, caster);
                break;
        }
        
        // Som do tick
        if (tickSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(tickSound, target.transform.position);
        }
    }
    
    #region Effect Application Methods
    
    private void ApplyDamageOverTime(GameObject target, GameObject caster)
    {
        // Calcular dano baseado no valor e no caster
        float damage = CalculateDamage(caster);
        
        // Aplicar dano
        if (target.CompareTag("Player"))
        {
            PlayerStats playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(damage);
            }
        }
        else if (target.CompareTag("Enemy"))
        {
            EnemyStats enemyStats = target.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage(damage, caster?.transform.position ?? Vector3.zero);
            }
        }
        
        // Efeito visual de dano
        EventManager.TriggerDamageDealt(damage, target.transform.position);
    }
    
    private void ApplyHealOverTime(GameObject target)
    {
        PlayerStats playerStats = target.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.Heal(value);
        }
    }
    
    private void ApplyManaRegeneration(GameObject target)
    {
        PlayerStats playerStats = target.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.RestoreMana(value);
        }
    }
    
    private void ExecuteCustomEffect(GameObject target, GameObject caster)
    {
        // Implementar efeitos customizados aqui
        // Pode ser sobrescrito em classes derivadas
    }
    
    #endregion
    
    #region Damage Calculation
    
    private float CalculateDamage(GameObject caster)
    {
        float damage = value;
        
        if (caster != null)
        {
            PlayerStats casterStats = caster.GetComponent<PlayerStats>();
            if (casterStats != null)
            {
                // Adicionar bônus baseado nos atributos do caster
                switch (damageType)
                {
                    case DamageType.Physical:
                        damage += casterStats.FinalStrength * 0.1f;
                        break;
                    case DamageType.Magic:
                    case DamageType.Fire:
                    case DamageType.Cold:
                    case DamageType.Lightning:
                    case DamageType.Poison:
                        damage += casterStats.FinalIntelligence * 0.1f;
                        break;
                }
                
                // Verificar crítico se permitido
                if (canCrit)
                {
                    bool isCrit = Random.Range(0f, 100f) <= casterStats.FinalCriticalChance;
                    if (isCrit)
                    {
                        damage *= casterStats.FinalCriticalDamage / 100f;
                    }
                }
            }
        }
        
        return damage;
    }
    
    #endregion
    
    #region Visual and Audio Effects
    
    private void PlayApplyEffect(GameObject target)
    {
        // Instanciar efeito visual
        if (effectPrefab != null)
        {
            GameObject effect = Instantiate(effectPrefab, target.transform.position, Quaternion.identity);
            effect.transform.SetParent(target.transform);
            
            // Auto-destruir após a duração
            Destroy(effect, duration);
        }
        
        // Ativar sistema de partículas
        if (particleEffect != null)
        {
            ParticleSystem particles = Instantiate(particleEffect, target.transform.position, Quaternion.identity);
            particles.transform.SetParent(target.transform);
            
            // Configurar duração
            var main = particles.main;
            main.duration = duration;
            main.startColor = effectColor;
            
            particles.Play();
            Destroy(particles.gameObject, duration + 2f);
        }
        
        // Som de aplicação
        if (applySound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(applySound, target.transform.position);
        }
    }
    
    public void PlayRemoveEffect(GameObject target)
    {
        if (removeSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(removeSound, target.transform.position);
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Verifica se este efeito pode ser aplicado junto com outro
    /// </summary>
    public bool CanStackWith(SkillEffect other)
    {
        if (other == null) return false;
        
        // Efeitos diferentes sempre podem coexistir
        if (other.effectName != effectName) return true;
        
        // Efeitos iguais só podem coexistir se forem stackable
        return isStackable;
    }
    
    /// <summary>
    /// Obtém a cor do efeito baseada no tipo
    /// </summary>
    public Color GetEffectTypeColor()
    {
        if (beneficial)
        {
            switch (effectType)
            {
                case EffectType.Heal:
                case EffectType.HealOverTime: return Color.green;
                case EffectType.Buff: return Color.blue;
                case EffectType.ManaRegeneration: return Color.cyan;
                default: return Color.white;
            }
        }
        else
        {
            switch (effectType)
            {
                case EffectType.DamageOverTime: return Color.red;
                case EffectType.Debuff: return Color.magenta;
                case EffectType.Root: return Color.yellow;
                case EffectType.Slow: return Color.gray;
                default: return Color.red;
            }
        }
    }
    
    /// <summary>
    /// Cria uma cópia do efeito
    /// </summary>
    public SkillEffect CreateCopy()
    {
        return Instantiate(this);
    }
    
    #endregion
}

/// <summary>
/// Instância ativa de um efeito
/// </summary>
[System.Serializable]
public class ActiveEffect
{
    public SkillEffect effectData;
    public GameObject target;
    public GameObject caster;
    public float remainingDuration;
    public float lastTickTime;
    public int stackCount;
    public System.DateTime startTime;
    
    public ActiveEffect(SkillEffect effect, GameObject casterObj)
    {
        effectData = effect;
        caster = casterObj;
        remainingDuration = effect.duration;
        lastTickTime = Time.time;
        stackCount = 1;
        startTime = System.DateTime.Now;
    }
    
    public bool HasExpired()
    {
        return remainingDuration <= 0f;
    }
    
    public bool ShouldTick()
    {
        return Time.time - lastTickTime >= effectData.interval;
    }
    
    public void ExecuteTick()
    {
        effectData.ExecuteTick(this);
        lastTickTime = Time.time;
    }
    
    public void AddStack()
    {
        if (effectData.isStackable && stackCount < effectData.maxStacks)
        {
            stackCount++;
            // Reset duration quando adiciona stack
            remainingDuration = effectData.duration;
        }
    }
    
    public float GetValue()
    {
        return effectData.value * stackCount;
    }
}

/// <summary>
/// Dados de modificador de stat
/// </summary>
[System.Serializable]
public class StatModifierData
{
    public string statName;
    public float value;
    public ModifierType modifierType;
}

/// <summary>
/// Tipos de efeito
/// </summary>
public enum EffectType
{
    Instant,            // Efeito instantâneo
    DamageOverTime,     // Dano ao longo do tempo
    HealOverTime,       // Cura ao longo do tempo
    Buff,               // Melhoria de stats
    Debuff,             // Redução de stats
    Root,               // Impede movimento
    Slow,               // Reduz velocidade
    Stun,               // Impede ações
    Silence,            // Impede uso de skills
    ManaRegeneration,   // Regeneração de mana
    Custom              // Efeito customizado
}

/// <summary>
/// Alvos do efeito
/// </summary>
public enum EffectTarget
{
    Self,       // Próprio caster
    Enemy,      // Inimigos
    Ally,       // Aliados
    Anyone      // Qualquer alvo
}