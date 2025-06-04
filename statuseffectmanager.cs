using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Tipos de efeitos de status
/// </summary>
public enum StatusEffectType
{
    Buff,           // Efeito positivo
    Debuff,         // Efeito negativo
    DOT,            // Damage Over Time
    HOT,            // Heal Over Time
    StatModifier,   // Modificador de atributo
    MovementModifier, // Modificador de movimento
    Custom          // Efeito customizado
}

/// <summary>
/// Como o efeito se comporta quando aplicado novamente
/// </summary>
public enum StackBehavior
{
    Replace,        // Substitui o efeito anterior
    Stack,          // Empilha com efeitos existentes
    RefreshDuration, // Renova a duração
    Ignore          // Ignora se já existe
}

/// <summary>
/// Representa um efeito de status ativo
/// </summary>
[System.Serializable]
public class ActiveStatusEffect
{
    [Header("Effect Data")]
    public SkillEffect sourceEffect;
    public GameObject caster;
    public GameObject target;
    
    [Header("Timing")]
    public float duration;
    public float remainingTime;
    public float tickInterval;
    public float timeSinceLastTick;
    
    [Header("Stacking")]
    public int stackCount = 1;
    public int maxStacks;
    
    [Header("State")]
    public bool isPersistent = false;
    public bool isActive = true;
    
    // Dados calculados
    private Dictionary<StatType, float> appliedModifiers = new Dictionary<StatType, float>();
    
    public ActiveStatusEffect(SkillEffect effect, GameObject effectCaster, GameObject effectTarget)
    {
        sourceEffect = effect;
        caster = effectCaster;
        target = effectTarget;
        duration = effect.duration;
        remainingTime = effect.duration;
        tickInterval = effect.tickInterval;
        maxStacks = effect.maxStacks;
        isPersistent = effect.isPersistent;
        timeSinceLastTick = 0f;
    }
    
    /// <summary>
    /// Verifica se o efeito expirou
    /// </summary>
    public bool HasExpired => !isPersistent && remainingTime <= 0f;
    
    /// <summary>
    /// Verifica se é hora de fazer tick
    /// </summary>
    public bool ShouldTick => tickInterval > 0f && timeSinceLastTick >= tickInterval;
    
    /// <summary>
    /// Atualiza o efeito
    /// </summary>
    public void Update(float deltaTime)
    {
        if (!isActive) return;
        
        // Atualizar tempo restante
        if (!isPersistent)
        {
            remainingTime -= deltaTime;
        }
        
        // Atualizar tick timer
        timeSinceLastTick += deltaTime;
    }
    
    /// <summary>
    /// Executa o tick do efeito
    /// </summary>
    public void ExecuteTick()
    {
        if (!isActive || !ShouldTick) return;
        
        timeSinceLastTick = 0f;
        
        if (sourceEffect != null)
        {
            sourceEffect.ExecuteTick(target, caster, stackCount);
        }
    }
    
    /// <summary>
    /// Adiciona stacks ao efeito
    /// </summary>
    public bool AddStack(int amount = 1)
    {
        int oldStacks = stackCount;
        stackCount = Mathf.Clamp(stackCount + amount, 1, maxStacks);
        
        return stackCount != oldStacks;
    }
    
    /// <summary>
    /// Remove stacks do efeito
    /// </summary>
    public bool RemoveStack(int amount = 1)
    {
        int oldStacks = stackCount;
        stackCount = Mathf.Max(1, stackCount - amount);
        
        return stackCount != oldStacks;
    }
    
    /// <summary>
    /// Renova a duração do efeito
    /// </summary>
    public void RefreshDuration()
    {
        remainingTime = duration;
    }
    
    /// <summary>
    /// Para o efeito
    /// </summary>
    public void Stop()
    {
        isActive = false;
        remainingTime = 0f;
    }
}

/// <summary>
/// Gerencia todos os efeitos de status de um objeto
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    [Header("Settings")]
    public bool enableDebugLogs = false;
    public int maxEffectsPerType = 10;
    
    [Header("Immunity")]
    public List<StatusEffectType> immuneToTypes = new List<StatusEffectType>();
    public List<string> immuneToEffectNames = new List<string>();
    
    // Lista de efeitos ativos
    private List<ActiveStatusEffect> activeEffects = new List<ActiveStatusEffect>();
    
    // Cache de modificadores aplicados
    private Dictionary<StatType, float> cachedModifiers = new Dictionary<StatType, float>();
    private bool modifiersCacheInvalid = true;
    
    // Componentes
    private PlayerStats playerStats;
    private EnemyStats enemyStats;
    
    // Eventos
    public System.Action<ActiveStatusEffect> OnEffectApplied;
    public System.Action<ActiveStatusEffect> OnEffectRemoved;
    public System.Action<ActiveStatusEffect> OnEffectExpired;
    public System.Action OnEffectsChanged;
    
    private void Awake()
    {
        // Obter componentes de stats
        playerStats = GetComponent<PlayerStats>();
        enemyStats = GetComponent<EnemyStats>();
    }
    
    private void Update()
    {
        UpdateEffects();
    }
    
    #region Effect Management
    
    /// <summary>
    /// Aplica um efeito de status
    /// </summary>
    public bool ApplyEffect(SkillEffect effect, GameObject caster)
    {
        if (effect == null) return false;
        
        // Verificar imunidade
        if (IsImmuneToEffect(effect))
        {
            if (enableDebugLogs)
                Debug.Log($"{name} é imune ao efeito: {effect.effectName}");
            return false;
        }
        
        // Verificar se já existe um efeito similar
        ActiveStatusEffect existingEffect = FindEffect(effect.effectName, caster);
        
        if (existingEffect != null)
        {
            return HandleExistingEffect(existingEffect, effect, caster);
        }
        else
        {
            return CreateNewEffect(effect, caster);
        }
    }
    
    /// <summary>
    /// Remove um efeito específico
    /// </summary>
    public bool RemoveEffect(string effectName, GameObject caster = null)
    {
        ActiveStatusEffect effect = FindEffect(effectName, caster);
        
        if (effect != null)
        {
            RemoveEffect(effect);
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Remove um efeito ativo
    /// </summary>
    public void RemoveEffect(ActiveStatusEffect effect)
    {
        if (effect == null || !activeEffects.Contains(effect)) return;
        
        // Remover modificadores aplicados por este efeito
        UnapplyEffectModifiers(effect);
        
        // Remover da lista
        activeEffects.Remove(effect);
        
        // Invalidar cache
        InvalidateModifiersCache();
        
        // Disparar eventos
        OnEffectRemoved?.Invoke(effect);
        OnEffectsChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"Efeito removido: {effect.sourceEffect.effectName}");
    }
    
    /// <summary>
    /// Remove todos os efeitos de um tipo
    /// </summary>
    public int RemoveEffectsByType(StatusEffectType effectType)
    {
        List<ActiveStatusEffect> effectsToRemove = activeEffects
            .Where(e => e.sourceEffect.effectType == effectType)
            .ToList();
        
        foreach (ActiveStatusEffect effect in effectsToRemove)
        {
            RemoveEffect(effect);
        }
        
        return effectsToRemove.Count;
    }
    
    /// <summary>
    /// Remove todos os debuffs
    /// </summary>
    public int RemoveAllDebuffs()
    {
        List<ActiveStatusEffect> debuffs = activeEffects
            .Where(e => e.sourceEffect.effectType == StatusEffectType.Debuff || 
                       e.sourceEffect.effectType == StatusEffectType.DOT)
            .ToList();
        
        foreach (ActiveStatusEffect effect in debuffs)
        {
            RemoveEffect(effect);
        }
        
        return debuffs.Count;
    }
    
    /// <summary>
    /// Remove todos os efeitos
    /// </summary>
    public void RemoveAllEffects()
    {
        List<ActiveStatusEffect> allEffects = new List<ActiveStatusEffect>(activeEffects);
        
        foreach (ActiveStatusEffect effect in allEffects)
        {
            RemoveEffect(effect);
        }
    }
    
    #endregion
    
    #region Effect Processing
    
    private void UpdateEffects()
    {
        List<ActiveStatusEffect> effectsToRemove = new List<ActiveStatusEffect>();
        
        foreach (ActiveStatusEffect effect in activeEffects)
        {
            // Atualizar efeito
            effect.Update(Time.deltaTime);
            
            // Executar tick se necessário
            if (effect.ShouldTick)
            {
                effect.ExecuteTick();
            }
            
            // Verificar se expirou
            if (effect.HasExpired)
            {
                effectsToRemove.Add(effect);
                OnEffectExpired?.Invoke(effect);
            }
        }
        
        // Remover efeitos expirados
        foreach (ActiveStatusEffect effect in effectsToRemove)
        {
            RemoveEffect(effect);
        }
    }
    
    private bool HandleExistingEffect(ActiveStatusEffect existingEffect, SkillEffect newEffect, GameObject caster)
    {
        switch (newEffect.stackBehavior)
        {
            case StackBehavior.Replace:
                RemoveEffect(existingEffect);
                return CreateNewEffect(newEffect, caster);
                
            case StackBehavior.Stack:
                if (existingEffect.AddStack(1))
                {
                    UpdateEffectModifiers(existingEffect);
                    if (enableDebugLogs)
                        Debug.Log($"Efeito empilhado: {newEffect.effectName} (Stack: {existingEffect.stackCount})");
                    return true;
                }
                return false;
                
            case StackBehavior.RefreshDuration:
                existingEffect.RefreshDuration();
                if (enableDebugLogs)
                    Debug.Log($"Duração renovada: {newEffect.effectName}");
                return true;
                
            case StackBehavior.Ignore:
                if (enableDebugLogs)
                    Debug.Log($"Efeito ignorado (já existe): {newEffect.effectName}");
                return false;
                
            default:
                return false;
        }
    }
    
    private bool CreateNewEffect(SkillEffect effect, GameObject caster)
    {
        // Verificar limite de efeitos
        int currentCount = activeEffects.Count(e => e.sourceEffect.effectType == effect.effectType);
        if (currentCount >= maxEffectsPerType)
        {
            // Remover o efeito mais antigo do mesmo tipo
            ActiveStatusEffect oldestEffect = activeEffects
                .Where(e => e.sourceEffect.effectType == effect.effectType)
                .OrderBy(e => e.remainingTime)
                .FirstOrDefault();
            
            if (oldestEffect != null)
            {
                RemoveEffect(oldestEffect);
            }
        }
        
        // Criar novo efeito
        ActiveStatusEffect newEffect = new ActiveStatusEffect(effect, caster, gameObject);
        activeEffects.Add(newEffect);
        
        // Aplicar modificadores iniciais
        ApplyEffectModifiers(newEffect);
        
        // Executar efeito inicial se necessário
        if (effect.applyOnStart)
        {
            effect.ExecuteEffect(gameObject, caster, newEffect.stackCount);
        }
        
        // Invalidar cache
        InvalidateModifiersCache();
        
        // Disparar eventos
        OnEffectApplied?.Invoke(newEffect);
        OnEffectsChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"Novo efeito aplicado: {effect.effectName}");
        
        return true;
    }
    
    #endregion
    
    #region Modifiers Management
    
    private void ApplyEffectModifiers(ActiveStatusEffect effect)
    {
        if (effect.sourceEffect.statModifiers == null) return;
        
        foreach (var modifier in effect.sourceEffect.statModifiers)
        {
            ApplyStatModifier(modifier.statType, modifier.value * effect.stackCount, effect);
        }
    }
    
    private void UnapplyEffectModifiers(ActiveStatusEffect effect)
    {
        if (effect.sourceEffect.statModifiers == null) return;
        
        foreach (var modifier in effect.sourceEffect.statModifiers)
        {
            RemoveStatModifier(modifier.statType, modifier.value * effect.stackCount, effect);
        }
    }
    
    private void UpdateEffectModifiers(ActiveStatusEffect effect)
    {
        // Remover modificadores antigos
        UnapplyEffectModifiers(effect);
        
        // Aplicar novos modificadores com stack atual
        ApplyEffectModifiers(effect);
        
        InvalidateModifiersCache();
    }
    
    private void ApplyStatModifier(StatType statType, float value, ActiveStatusEffect source)
    {
        // Aplicar diretamente aos stats se possível
        if (playerStats != null)
        {
            playerStats.AddModifier(statType, value);
        }
        else if (enemyStats != null)
        {
            // Implementar modificadores para inimigos se necessário
            // enemyStats.AddModifier(statType, value);
        }
    }
    
    private void RemoveStatModifier(StatType statType, float value, ActiveStatusEffect source)
    {
        // Remover modificador dos stats
        if (playerStats != null)
        {
            playerStats.RemoveModifier(statType, value);
        }
        else if (enemyStats != null)
        {
            // Implementar remoção de modificadores para inimigos
            // enemyStats.RemoveModifier(statType, value);
        }
    }
    
    private void InvalidateModifiersCache()
    {
        modifiersCacheInvalid = true;
    }
    
    #endregion
    
    #region Queries
    
    /// <summary>
    /// Procura um efeito por nome e caster
    /// </summary>
    public ActiveStatusEffect FindEffect(string effectName, GameObject caster = null)
    {
        return activeEffects.FirstOrDefault(e => 
            e.sourceEffect.effectName == effectName && 
            (caster == null || e.caster == caster));
    }
    
    /// <summary>
    /// Verifica se tem um efeito específico
    /// </summary>
    public bool HasEffect(string effectName, GameObject caster = null)
    {
        return FindEffect(effectName, caster) != null;
    }
    
    /// <summary>
    /// Verifica se tem algum efeito de um tipo
    /// </summary>
    public bool HasEffectOfType(StatusEffectType effectType)
    {
        return activeEffects.Any(e => e.sourceEffect.effectType == effectType);
    }
    
    /// <summary>
    /// Obtém todos os efeitos ativos
    /// </summary>
    public List<ActiveStatusEffect> GetActiveEffects()
    {
        return new List<ActiveStatusEffect>(activeEffects);
    }
    
    /// <summary>
    /// Obtém efeitos por tipo
    /// </summary>
    public List<ActiveStatusEffect> GetEffectsByType(StatusEffectType effectType)
    {
        return activeEffects.Where(e => e.sourceEffect.effectType == effectType).ToList();
    }
    
    /// <summary>
    /// Obtém total de stacks de um efeito
    /// </summary>
    public int GetEffectStacks(string effectName, GameObject caster = null)
    {
        ActiveStatusEffect effect = FindEffect(effectName, caster);
        return effect?.stackCount ?? 0;
    }
    
    /// <summary>
    /// Verifica se é imune a um efeito
    /// </summary>
    public bool IsImmuneToEffect(SkillEffect effect)
    {
        return immuneToTypes.Contains(effect.effectType) || 
               immuneToEffectNames.Contains(effect.effectName);
    }
    
    #endregion
    
    #region Public Utility
    
    /// <summary>
    /// Adiciona imunidade a um tipo de efeito
    /// </summary>
    public void AddImmunity(StatusEffectType effectType)
    {
        if (!immuneToTypes.Contains(effectType))
        {
            immuneToTypes.Add(effectType);
        }
    }
    
    /// <summary>
    /// Remove imunidade a um tipo de efeito
    /// </summary>
    public void RemoveImmunity(StatusEffectType effectType)
    {
        immuneToTypes.Remove(effectType);
    }
    
    /// <summary>
    /// Força atualização de todos os modificadores
    /// </summary>
    public void RefreshAllModifiers()
    {
        foreach (ActiveStatusEffect effect in activeEffects)
        {
            UpdateEffectModifiers(effect);
        }
    }
    
    #endregion
    
    #region Debug
    
    /// <summary>
    /// Lista todos os efeitos ativos (para debug)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugListActiveEffects()
    {
        Debug.Log($"=== EFEITOS ATIVOS ({name}) ===");
        
        if (activeEffects.Count == 0)
        {
            Debug.Log("Nenhum efeito ativo");
        }
        else
        {
            foreach (ActiveStatusEffect effect in activeEffects)
            {
                string casterName = effect.caster ? effect.caster.name : "Unknown";
                Debug.Log($"- {effect.sourceEffect.effectName} (Caster: {casterName}, Stacks: {effect.stackCount}, Tempo: {effect.remainingTime:F1}s)");
            }
        }
        
        Debug.Log("============================");
    }
    
    #endregion
}