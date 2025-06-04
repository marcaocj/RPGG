using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Classe para equipamentos (armas, armaduras) que herda de Item
/// </summary>
[CreateAssetMenu(fileName = "New Equipment", menuName = "Game/Items/Equipment")]
public class Equipment : Item
{
    [Header("Equipment Properties")]
    public EquipmentType equipmentType;
    public EquipmentSlot equipmentSlot;
    public int levelRequirement = 1;
    
    [Header("Base Stats")]
    public int damage = 0;
    public int armor = 0;
    public int health = 0;
    public int mana = 0;
    
    [Header("Attribute Bonuses")]
    public int strength = 0;
    public int dexterity = 0;
    public int intelligence = 0;
    public int vitality = 0;
    
    [Header("Special Properties")]
    public float criticalChance = 0f;
    public float criticalDamage = 0f;
    public float attackSpeed = 0f;
    public float movementSpeed = 0f;
    
    [Header("Resistances")]
    public float fireResistance = 0f;
    public float coldResistance = 0f;
    public float lightningResistance = 0f;
    public float poisonResistance = 0f;
    
    [Header("Weapon Specific")]
    public float weaponRange = 1f;
    public DamageType damageType = DamageType.Physical;
    public List<SkillEffect> weaponEffects = new List<SkillEffect>();
    
    [Header("Visual Equipment")]
    public GameObject equipmentModel; // Modelo que aparece no personagem quando equipado
    public RuntimeAnimatorController weaponAnimatorController; // Para armas
    
    public override string GetTooltipText()
    {
        string tooltip = base.GetTooltipText();
        
        // Adicionar requisitos
        if (levelRequirement > 1)
        {
            tooltip += $"\n\n<color=red>Nível Requerido: {levelRequirement}</color>";
        }
        
        // Adicionar stats
        tooltip += "\n\n<color=cyan><b>Atributos:</b></color>";
        
        if (damage > 0)
            tooltip += $"\n+{damage} Dano";
        if (armor > 0)
            tooltip += $"\n+{armor} Armadura";
        if (health > 0)
            tooltip += $"\n+{health} Vida";
        if (mana > 0)
            tooltip += $"\n+{mana} Mana";
            
        // Atributos principais
        if (strength > 0)
            tooltip += $"\n+{strength} Força";
        if (dexterity > 0)
            tooltip += $"\n+{dexterity} Destreza";
        if (intelligence > 0)
            tooltip += $"\n+{intelligence} Inteligência";
        if (vitality > 0)
            tooltip += $"\n+{vitality} Vitalidade";
            
        // Propriedades especiais
        if (criticalChance > 0)
            tooltip += $"\n+{criticalChance:F1}% Chance Crítica";
        if (criticalDamage > 0)
            tooltip += $"\n+{criticalDamage:F1}% Dano Crítico";
        if (attackSpeed > 0)
            tooltip += $"\n+{attackSpeed:F1}% Velocidade de Ataque";
        if (movementSpeed > 0)
            tooltip += $"\n+{movementSpeed:F1}% Velocidade de Movimento";
            
        // Resistências
        bool hasResistances = fireResistance > 0 || coldResistance > 0 || 
                            lightningResistance > 0 || poisonResistance > 0;
        if (hasResistances)
        {
            tooltip += "\n\n<color=orange><b>Resistências:</b></color>";
            if (fireResistance > 0)
                tooltip += $"\n+{fireResistance:F1}% Fogo";
            if (coldResistance > 0)
                tooltip += $"\n+{coldResistance:F1}% Gelo";
            if (lightningResistance > 0)
                tooltip += $"\n+{lightningResistance:F1}% Raio";
            if (poisonResistance > 0)
                tooltip += $"\n+{poisonResistance:F1}% Veneno";
        }
        
        // Efeitos especiais da arma
        if (weaponEffects.Count > 0)
        {
            tooltip += "\n\n<color=magenta><b>Efeitos Especiais:</b></color>";
            foreach (var effect in weaponEffects)
            {
                if (effect != null)
                {
                    tooltip += $"\n{effect.effectName}";
                }
            }
        }
        
        return tooltip;
    }
    
    /// <summary>
    /// Verifica se o equipamento pode ser usado pelo jogador
    /// </summary>
    public bool CanEquip(PlayerStats playerStats)
    {
        if (playerStats == null) return false;
        
        // Verificar nível
        if (playerStats.level < levelRequirement)
        {
            return false;
        }
        
        // Aqui podem ser adicionadas outras verificações
        // como classe do personagem, stats mínimos, etc.
        
        return true;
    }
    
    /// <summary>
    /// Aplica os bônus do equipamento aos stats do jogador
    /// </summary>
    public void ApplyBonuses(PlayerStats playerStats)
    {
        if (playerStats == null) return;
        
        playerStats.AddEquipmentBonus("damage", damage);
        playerStats.AddEquipmentBonus("armor", armor);
        playerStats.AddEquipmentBonus("maxHealth", health);
        playerStats.AddEquipmentBonus("maxMana", mana);
        playerStats.AddEquipmentBonus("strength", strength);
        playerStats.AddEquipmentBonus("dexterity", dexterity);
        playerStats.AddEquipmentBonus("intelligence", intelligence);
        playerStats.AddEquipmentBonus("vitality", vitality);
        playerStats.AddEquipmentBonus("criticalChance", criticalChance);
        playerStats.AddEquipmentBonus("criticalDamage", criticalDamage);
        playerStats.AddEquipmentBonus("attackSpeed", attackSpeed);
        playerStats.AddEquipmentBonus("movementSpeed", movementSpeed);
        playerStats.AddEquipmentBonus("fireResistance", fireResistance);
        playerStats.AddEquipmentBonus("coldResistance", coldResistance);
        playerStats.AddEquipmentBonus("lightningResistance", lightningResistance);
        playerStats.AddEquipmentBonus("poisonResistance", poisonResistance);
    }
    
    /// <summary>
    /// Remove os bônus do equipamento dos stats do jogador
    /// </summary>
    public void RemoveBonuses(PlayerStats playerStats)
    {
        if (playerStats == null) return;
        
        playerStats.AddEquipmentBonus("damage", -damage);
        playerStats.AddEquipmentBonus("armor", -armor);
        playerStats.AddEquipmentBonus("maxHealth", -health);
        playerStats.AddEquipmentBonus("maxMana", -mana);
        playerStats.AddEquipmentBonus("strength", -strength);
        playerStats.AddEquipmentBonus("dexterity", -dexterity);
        playerStats.AddEquipmentBonus("intelligence", -intelligence);
        playerStats.AddEquipmentBonus("vitality", -vitality);
        playerStats.AddEquipmentBonus("criticalChance", -criticalChance);
        playerStats.AddEquipmentBonus("criticalDamage", -criticalDamage);
        playerStats.AddEquipmentBonus("attackSpeed", -attackSpeed);
        playerStats.AddEquipmentBonus("movementSpeed", -movementSpeed);
        playerStats.AddEquipmentBonus("fireResistance", -fireResistance);
        playerStats.AddEquipmentBonus("coldResistance", -coldResistance);
        playerStats.AddEquipmentBonus("lightningResistance", -lightningResistance);
        playerStats.AddEquipmentBonus("poisonResistance", -poisonResistance);
    }
    
    /// <summary>
    /// Calcula o DPS (Damage Per Second) da arma
    /// </summary>
    public float GetDPS()
    {
        if (equipmentType != EquipmentType.Weapon) return 0f;
        
        float baseAttackSpeed = 1f + (attackSpeed / 100f);
        return damage * baseAttackSpeed;
    }
    
    /// <summary>
    /// Gera um equipamento aleatório baseado no nível e raridade
    /// </summary>
    public static Equipment GenerateRandomEquipment(int level, ItemRarity rarity, EquipmentType type)
    {
        // Esta função seria implementada para gerar loot procedural
        // Por enquanto é apenas um placeholder
        Debug.Log($"Gerando equipamento: Nível {level}, {rarity}, {type}");
        return null;
    }
    
    public override bool UseItem(GameObject user)
    {
        // Equipamentos são "usados" ao serem equipados
        PlayerEquipment playerEquipment = user.GetComponent<PlayerEquipment>();
        if (playerEquipment != null)
        {
            return playerEquipment.EquipItem(this);
        }
        
        return false;
    }
}

/// <summary>
/// Tipos de equipamento
/// </summary>
public enum EquipmentType
{
    Weapon,
    Armor,
    Accessory
}

/// <summary>
/// Slots de equipamento
/// </summary>
public enum EquipmentSlot
{
    // Armas
    MainHand,
    OffHand,
    TwoHanded,
    
    // Armadura
    Helmet,
    Chest,
    Legs,
    Boots,
    Gloves,
    
    // Acessórios
    Ring1,
    Ring2,
    Necklace,
    Belt
}

/// <summary>
/// Tipos de dano
/// </summary>
public enum DamageType
{
    Physical,
    Fire,
    Cold,
    Lightning,
    Poison,
    Magic
}