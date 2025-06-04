using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerencia todas as estatísticas do jogador (vida, mana, atributos, etc.)
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")]
    public int level = 1;
    public int experience = 0;
    public int experienceToNextLevel = 100;
    
    [Header("Health and Mana")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float maxMana = 50f;
    public float currentMana = 50f;
    public float healthRegenRate = 1f; // HP por segundo
    public float manaRegenRate = 2f; // MP por segundo
    
    [Header("Primary Attributes")]
    public int strength = 10;      // Aumenta dano físico e HP
    public int dexterity = 10;     // Aumenta velocidade de ataque e crítico
    public int intelligence = 10;  // Aumenta dano mágico e MP
    public int vitality = 10;      // Aumenta HP e regeneração
    
    [Header("Combat Stats")]
    public int damage = 10;
    public int armor = 0;
    public float criticalChance = 5f; // Porcentagem
    public float criticalDamage = 150f; // Porcentagem
    public float attackSpeed = 1f; // Multiplicador
    public float movementSpeed = 5f;
    
    [Header("Resistances")]
    public float fireResistance = 0f;
    public float coldResistance = 0f;
    public float lightningResistance = 0f;
    public float poisonResistance = 0f;
    
    [Header("Level Up")]
    public int availableStatPoints = 0;
    public int statPointsPerLevel = 5;
    
    // Bônus de equipamentos (separados para facilitar cálculos)
    private Dictionary<string, float> equipmentBonuses = new Dictionary<string, float>();
    
    // Buffs temporários
    private List<StatModifier> activeModifiers = new List<StatModifier>();
    
    // Propriedades calculadas (stats finais)
    public int FinalStrength => strength + GetEquipmentBonus("strength") + GetModifierBonus("strength");
    public int FinalDexterity => dexterity + GetEquipmentBonus("dexterity") + GetModifierBonus("dexterity");
    public int FinalIntelligence => intelligence + GetEquipmentBonus("intelligence") + GetModifierBonus("intelligence");
    public int FinalVitality => vitality + GetEquipmentBonus("vitality") + GetModifierBonus("vitality");
    
    public float FinalMaxHealth => CalculateMaxHealth();
    public float FinalMaxMana => CalculateMaxMana();
    public int FinalDamage => damage + GetEquipmentBonus("damage") + GetModifierBonus("damage");
    public int FinalArmor => armor + GetEquipmentBonus("armor") + GetModifierBonus("armor");
    public float FinalCriticalChance => criticalChance + GetEquipmentBonus("criticalChance") + GetModifierBonus("criticalChance");
    public float FinalCriticalDamage => criticalDamage + GetEquipmentBonus("criticalDamage") + GetModifierBonus("criticalDamage");
    public float FinalAttackSpeed => attackSpeed + (GetEquipmentBonus("attackSpeed") + GetModifierBonus("attackSpeed")) / 100f;
    public float FinalMovementSpeed => movementSpeed + (GetEquipmentBonus("movementSpeed") + GetModifierBonus("movementSpeed")) / 100f;
    
    // Eventos
    public System.Action OnStatsChanged;
    public System.Action OnLevelUp;
    
    private void Start()
    {
        InitializeStats();
        InvokeRepeating(nameof(RegenerateHealthAndMana), 1f, 1f);
    }
    
    private void Update()
    {
        UpdateModifiers();
    }
    
    private void InitializeStats()
    {
        // Calcular stats baseados no nível inicial
        RecalculateStats();
        
        // Setar vida e mana para o máximo
        currentHealth = FinalMaxHealth;
        currentMana = FinalMaxMana;
        
        // Disparar eventos iniciais
        EventManager.TriggerPlayerHealthChanged(currentHealth, FinalMaxHealth);
        EventManager.TriggerPlayerManaChanged(currentMana, FinalMaxMana);
    }
    
    #region Health and Mana Management
    
    public void TakeDamage(float damageAmount)
    {
        if (damageAmount <= 0) return;
        
        // Aplicar redução de armor
        float finalDamage = CalculateDamageReduction(damageAmount);
        
        currentHealth = Mathf.Max(0, currentHealth - finalDamage);
        
        // Trigger eventos
        EventManager.TriggerPlayerHealthChanged(currentHealth, FinalMaxHealth);
        EventManager.TriggerDamageDealt(finalDamage, transform.position);
        
        // Verificar morte
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void Heal(float healAmount)
    {
        if (healAmount <= 0) return;
        
        currentHealth = Mathf.Min(FinalMaxHealth, currentHealth + healAmount);
        EventManager.TriggerPlayerHealthChanged(currentHealth, FinalMaxHealth);
    }
    
    public void UseMana(float manaAmount)
    {
        if (manaAmount <= 0) return;
        
        currentMana = Mathf.Max(0, currentMana - manaAmount);
        EventManager.TriggerPlayerManaChanged(currentMana, FinalMaxMana);
    }
    
    public void RestoreMana(float manaAmount)
    {
        if (manaAmount <= 0) return;
        
        currentMana = Mathf.Min(FinalMaxMana, currentMana + manaAmount);
        EventManager.TriggerPlayerManaChanged(currentMana, FinalMaxMana);
    }
    
    public bool HasEnoughMana(float requiredMana)
    {
        return currentMana >= requiredMana;
    }
    
    private void RegenerateHealthAndMana()
    {
        // Regeneração de vida
        if (currentHealth < FinalMaxHealth)
        {
            float healthRegen = healthRegenRate + (FinalVitality * 0.1f);
            Heal(healthRegen);
        }
        
        // Regeneração de mana
        if (currentMana < FinalMaxMana)
        {
            float manaRegen = manaRegenRate + (FinalIntelligence * 0.1f);
            RestoreMana(manaRegen);
        }
    }
    
    private void Die()
    {
        Debug.Log("Player morreu!");
        EventManager.TriggerPlayerDeath();
    }
    
    #endregion
    
    #region Experience and Leveling
    
    public void GainExperience(int xpAmount)
    {
        if (xpAmount <= 0) return;
        
        experience += xpAmount;
        EventManager.TriggerPlayerExperienceGained(xpAmount);
        
        // Verificar level up
        while (experience >= experienceToNextLevel)
        {
            LevelUp();
        }
    }
    
    private void LevelUp()
    {
        experience -= experienceToNextLevel;
        level++;
        
        // Calcular XP para próximo nível
        experienceToNextLevel = CalculateExperienceForNextLevel();
        
        // Ganhar pontos de atributo
        availableStatPoints += statPointsPerLevel;
        
        // Recalcular stats
        RecalculateStats();
        
        // Curar completamente ao subir de nível
        currentHealth = FinalMaxHealth;
        currentMana = FinalMaxMana;
        
        // Disparar eventos
        EventManager.TriggerPlayerLevelUp(level);
        EventManager.TriggerPlayerHealthChanged(currentHealth, FinalMaxHealth);
        EventManager.TriggerPlayerManaChanged(currentMana, FinalMaxMana);
        
        OnLevelUp?.Invoke();
        
        Debug.Log($"Level Up! Novo nível: {level}");
    }
    
    private int CalculateExperienceForNextLevel()
    {
        // Fórmula exponencial para XP necessário
        return Mathf.RoundToInt(100 * Mathf.Pow(1.5f, level - 1));
    }
    
    #endregion
    
    #region Stat Point Distribution
    
    public bool AddStatPoint(string statName, int points = 1)
    {
        if (availableStatPoints < points) return false;
        
        switch (statName.ToLower())
        {
            case "strength":
                strength += points;
                break;
            case "dexterity":
                dexterity += points;
                break;
            case "intelligence":
                intelligence += points;
                break;
            case "vitality":
                vitality += points;
                break;
            default:
                return false;
        }
        
        availableStatPoints -= points;
        RecalculateStats();
        OnStatsChanged?.Invoke();
        
        return true;
    }
    
    #endregion
    
    #region Equipment Bonuses
    
    public void AddEquipmentBonus(string statName, float bonus)
    {
        if (!equipmentBonuses.ContainsKey(statName))
        {
            equipmentBonuses[statName] = 0f;
        }
        
        equipmentBonuses[statName] += bonus;
        RecalculateStats();
        OnStatsChanged?.Invoke();
    }
    
    public void RemoveEquipmentBonus(string statName, float bonus)
    {
        AddEquipmentBonus(statName, -bonus);
    }
    
    public int GetEquipmentBonus(string statName)
    {
        return equipmentBonuses.ContainsKey(statName) ? Mathf.RoundToInt(equipmentBonuses[statName]) : 0;
    }
    
    #endregion
    
    #region Temporary Modifiers (Buffs/Debuffs)
    
    public void AddModifier(StatModifier modifier)
    {
        activeModifiers.Add(modifier);
        RecalculateStats();
        OnStatsChanged?.Invoke();
    }
    
    public void RemoveModifier(StatModifier modifier)
    {
        activeModifiers.Remove(modifier);
        RecalculateStats();
        OnStatsChanged?.Invoke();
    }
    
    public void RemoveModifiersBySource(string source)
    {
        activeModifiers.RemoveAll(m => m.source == source);
        RecalculateStats();
        OnStatsChanged?.Invoke();
    }
    
    private void UpdateModifiers()
    {
        // Remover modifiers expirados
        for (int i = activeModifiers.Count - 1; i >= 0; i--)
        {
            if (activeModifiers[i].HasExpired())
            {
                activeModifiers.RemoveAt(i);
                RecalculateStats();
                OnStatsChanged?.Invoke();
            }
        }
    }
    
    private int GetModifierBonus(string statName)
    {
        float totalBonus = 0f;
        
        foreach (var modifier in activeModifiers)
        {
            if (modifier.statName == statName)
            {
                totalBonus += modifier.GetCurrentValue();
            }
        }
        
        return Mathf.RoundToInt(totalBonus);
    }
    
    #endregion
    
    #region Calculations
    
    private void RecalculateStats()
    {
        // Recalcular vida e mana máximas
        float newMaxHealth = FinalMaxHealth;
        float newMaxMana = FinalMaxMana;
        
        // Ajustar vida e mana atuais proporcionalmente
        if (maxHealth > 0)
        {
            float healthRatio = currentHealth / maxHealth;
            currentHealth = newMaxHealth * healthRatio;
        }
        
        if (maxMana > 0)
        {
            float manaRatio = currentMana / maxMana;
            currentMana = newMaxMana * manaRatio;
        }
        
        // Atualizar eventos
        EventManager.TriggerPlayerHealthChanged(currentHealth, newMaxHealth);
        EventManager.TriggerPlayerManaChanged(currentMana, newMaxMana);
    }
    
    private float CalculateMaxHealth()
    {
        float baseHealth = maxHealth;
        float vitalityBonus = FinalVitality * 5f; // 5 HP por ponto de vitalidade
        float equipmentBonus = GetEquipmentBonus("maxHealth");
        float modifierBonus = GetModifierBonus("maxHealth");
        
        return baseHealth + vitalityBonus + equipmentBonus + modifierBonus;
    }
    
    private float CalculateMaxMana()
    {
        float baseMana = maxMana;
        float intelligenceBonus = FinalIntelligence * 3f; // 3 MP por ponto de inteligência
        float equipmentBonus = GetEquipmentBonus("maxMana");
        float modifierBonus = GetModifierBonus("maxMana");
        
        return baseMana + intelligenceBonus + equipmentBonus + modifierBonus;
    }
    
    private float CalculateDamageReduction(float incomingDamage)
    {
        // Fórmula de redução baseada em armor
        float damageReduction = FinalArmor / (FinalArmor + 100f);
        return incomingDamage * (1f - damageReduction);
    }
    
    #endregion
    
    #region Public Properties
    
    public float HealthPercentage => FinalMaxHealth > 0 ? currentHealth / FinalMaxHealth : 0f;
    public float ManaPercentage => FinalMaxMana > 0 ? currentMana / FinalMaxMana : 0f;
    public bool IsAlive => currentHealth > 0;
    public bool IsAtFullHealth => currentHealth >= FinalMaxHealth;
    public bool IsAtFullMana => currentMana >= FinalMaxMana;
    
    #endregion
}

/// <summary>
/// Classe para modificadores temporários de stats (buffs/debuffs)
/// </summary>
[System.Serializable]
public class StatModifier
{
    public string statName;
    public float value;
    public float duration;
    public string source;
    public ModifierType type;
    
    private float startTime;
    
    public StatModifier(string stat, float val, float dur, string src, ModifierType modType = ModifierType.Flat)
    {
        statName = stat;
        value = val;
        duration = dur;
        source = src;
        type = modType;
        startTime = Time.time;
    }
    
    public bool HasExpired()
    {
        return duration > 0 && Time.time - startTime >= duration;
    }
    
    public float GetCurrentValue()
    {
        return value; // Para percentage modifiers, seria value * baseValue / 100
    }
}

public enum ModifierType
{
    Flat,        // +10 damage
    Percentage   // +10% damage
}