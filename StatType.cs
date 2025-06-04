/// <summary>
/// Tipos de estatísticas que podem ser modificadas
/// </summary>
public enum StatType
{
    // Atributos primários
    Strength,
    Dexterity,
    Intelligence,
    Vitality,
    
    // Stats de combate
    Damage,
    Armor,
    CriticalChance,
    CriticalDamage,
    AttackSpeed,
    
    // Stats de movimento
    MovementSpeed,
    
    // Stats de vida e mana
    MaxHealth,
    MaxMana,
    HealthRegeneration,
    ManaRegeneration,
    
    // Resistências
    FireResistance,
    ColdResistance,
    LightningResistance,
    PoisonResistance,
    PhysicalResistance,
    
    // Stats especiais
    ExperienceGain,
    GoldFind,
    MagicFind,
    SkillCooldownReduction
}