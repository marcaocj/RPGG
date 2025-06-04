using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Representa um timer de cooldown individual
/// </summary>
[System.Serializable]
public class CooldownTimer
{
    public string timerName;
    public float baseCooldown;
    public float remainingTime;
    public bool isActive;
    public System.DateTime lastUsedTime;
    
    public CooldownTimer(string name, float cooldown)
    {
        timerName = name;
        baseCooldown = cooldown;
        remainingTime = 0f;
        isActive = false;
        lastUsedTime = System.DateTime.Now;
    }
    
    /// <summary>
    /// Verifica se o cooldown terminou
    /// </summary>
    public bool IsReady => remainingTime <= 0f;
    
    /// <summary>
    /// Progresso do cooldown (0 = pronto, 1 = acabou de usar)
    /// </summary>
    public float Progress => baseCooldown > 0 ? Mathf.Clamp01(remainingTime / baseCooldown) : 0f;
    
    /// <summary>
    /// Tempo restante em segundos
    /// </summary>
    public float RemainingTime => Mathf.Max(0f, remainingTime);
    
    /// <summary>
    /// Inicia o cooldown
    /// </summary>
    public void StartCooldown(float customCooldown = -1f)
    {
        float cooldownTime = customCooldown > 0 ? customCooldown : baseCooldown;
        remainingTime = cooldownTime;
        isActive = true;
        lastUsedTime = System.DateTime.Now;
    }
    
    /// <summary>
    /// Atualiza o timer
    /// </summary>
    public void Update(float deltaTime)
    {
        if (isActive && remainingTime > 0f)
        {
            remainingTime -= deltaTime;
            
            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isActive = false;
            }
        }
    }
    
    /// <summary>
    /// Força o fim do cooldown
    /// </summary>
    public void Reset()
    {
        remainingTime = 0f;
        isActive = false;
    }
    
    /// <summary>
    /// Reduz o tempo de cooldown
    /// </summary>
    public void ReduceCooldown(float amount)
    {
        remainingTime = Mathf.Max(0f, remainingTime - amount);
        if (remainingTime <= 0f)
        {
            isActive = false;
        }
    }
}

/// <summary>
/// Gerencia múltiplos cooldowns (skills, itens, habilidades especiais)
/// </summary>
public class CooldownManager : MonoBehaviour
{
    [Header("Settings")]
    public bool enableDebugLogs = false;
    public float globalCooldownReduction = 0f; // Redução global de cooldown (0-1)
    
    [Header("Debug Info")]
    [SerializeField] private List<CooldownTimer> activeTimers = new List<CooldownTimer>();
    
    // Dicionário para acesso rápido aos timers
    private Dictionary<string, CooldownTimer> timerDictionary = new Dictionary<string, CooldownTimer>();
    
    // Eventos
    public System.Action<string> OnCooldownStarted;
    public System.Action<string> OnCooldownFinished;
    public System.Action<string, float> OnCooldownProgress; // timer name, progress (0-1)
    
    private void Update()
    {
        UpdateAllTimers();
    }
    
    #region Timer Management
    
    /// <summary>
    /// Adiciona um novo timer de cooldown
    /// </summary>
    public void AddTimer(string timerName, float cooldownTime)
    {
        if (string.IsNullOrEmpty(timerName))
        {
            Debug.LogWarning("Nome do timer não pode ser vazio!");
            return;
        }
        
        if (timerDictionary.ContainsKey(timerName))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"Timer '{timerName}' já existe! Atualizando cooldown base.");
            
            timerDictionary[timerName].baseCooldown = cooldownTime;
            return;
        }
        
        CooldownTimer newTimer = new CooldownTimer(timerName, cooldownTime);
        timerDictionary[timerName] = newTimer;
        activeTimers.Add(newTimer);
        
        if (enableDebugLogs)
            Debug.Log($"Timer adicionado: {timerName} ({cooldownTime}s)");
    }
    
    /// <summary>
    /// Remove um timer
    /// </summary>
    public void RemoveTimer(string timerName)
    {
        if (timerDictionary.ContainsKey(timerName))
        {
            CooldownTimer timer = timerDictionary[timerName];
            timerDictionary.Remove(timerName);
            activeTimers.Remove(timer);
            
            if (enableDebugLogs)
                Debug.Log($"Timer removido: {timerName}");
        }
    }
    
    /// <summary>
    /// Verifica se um timer pode ser usado (não está em cooldown)
    /// </summary>
    public bool CanUseTimer(string timerName)
    {
        if (!timerDictionary.ContainsKey(timerName))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"Timer '{timerName}' não encontrado!");
            return false;
        }
        
        return timerDictionary[timerName].IsReady;
    }
    
    /// <summary>
    /// Tenta usar um timer (inicia o cooldown se possível)
    /// </summary>
    public bool TryUseTimer(string timerName, float customCooldown = -1f)
    {
        if (!timerDictionary.ContainsKey(timerName))
        {
            if (enableDebugLogs)
                Debug.LogWarning($"Timer '{timerName}' não encontrado!");
            return false;
        }
        
        CooldownTimer timer = timerDictionary[timerName];
        
        if (!timer.IsReady)
        {
            if (enableDebugLogs)
                Debug.Log($"Timer '{timerName}' ainda em cooldown! Restam {timer.RemainingTime:F1}s");
            return false;
        }
        
        // Aplicar redução global de cooldown
        float finalCooldown = customCooldown > 0 ? customCooldown : timer.baseCooldown;
        if (globalCooldownReduction > 0f)
        {
            finalCooldown *= (1f - globalCooldownReduction);
        }
        
        timer.StartCooldown(finalCooldown);
        
        // Disparar evento
        OnCooldownStarted?.Invoke(timerName);
        
        if (enableDebugLogs)
            Debug.Log($"Timer '{timerName}' usado! Cooldown: {finalCooldown:F1}s");
        
        return true;
    }
    
    /// <summary>
    /// Força o uso de um timer (ignora se está em cooldown)
    /// </summary>
    public void ForceUseTimer(string timerName, float customCooldown = -1f)
    {
        if (!timerDictionary.ContainsKey(timerName))
        {
            AddTimer(timerName, customCooldown > 0 ? customCooldown : 1f);
        }
        
        CooldownTimer timer = timerDictionary[timerName];
        float finalCooldown = customCooldown > 0 ? customCooldown : timer.baseCooldown;
        
        if (globalCooldownReduction > 0f)
        {
            finalCooldown *= (1f - globalCooldownReduction);
        }
        
        timer.StartCooldown(finalCooldown);
        OnCooldownStarted?.Invoke(timerName);
        
        if (enableDebugLogs)
            Debug.Log($"Timer '{timerName}' forçado! Cooldown: {finalCooldown:F1}s");
    }
    
    #endregion
    
    #region Timer Queries
    
    /// <summary>
    /// Obtém um timer específico
    /// </summary>
    public CooldownTimer GetTimer(string timerName)
    {
        return timerDictionary.ContainsKey(timerName) ? timerDictionary[timerName] : null;
    }
    
    /// <summary>
    /// Obtém tempo restante de um timer
    /// </summary>
    public float GetRemainingTime(string timerName)
    {
        CooldownTimer timer = GetTimer(timerName);
        return timer?.RemainingTime ?? 0f;
    }
    
    /// <summary>
    /// Obtém progresso de um timer (0 = pronto, 1 = acabou de usar)
    /// </summary>
    public float GetProgress(string timerName)
    {
        CooldownTimer timer = GetTimer(timerName);
        return timer?.Progress ?? 0f;
    }
    
    /// <summary>
    /// Verifica se um timer existe
    /// </summary>
    public bool HasTimer(string timerName)
    {
        return timerDictionary.ContainsKey(timerName);
    }
    
    /// <summary>
    /// Obtém todos os nomes de timers ativos
    /// </summary>
    public string[] GetAllTimerNames()
    {
        string[] names = new string[timerDictionary.Count];
        timerDictionary.Keys.CopyTo(names, 0);
        return names;
    }
    
    /// <summary>
    /// Conta quantos timers estão em cooldown
    /// </summary>
    public int GetActiveTimerCount()
    {
        int count = 0;
        foreach (var timer in timerDictionary.Values)
        {
            if (!timer.IsReady)
                count++;
        }
        return count;
    }
    
    #endregion
    
    #region Timer Manipulation
    
    /// <summary>
    /// Reseta um timer específico (remove cooldown)
    /// </summary>
    public void ResetTimer(string timerName)
    {
        CooldownTimer timer = GetTimer(timerName);
        if (timer != null)
        {
            bool wasActive = !timer.IsReady;
            timer.Reset();
            
            if (wasActive)
            {
                OnCooldownFinished?.Invoke(timerName);
            }
            
            if (enableDebugLogs)
                Debug.Log($"Timer '{timerName}' resetado!");
        }
    }
    
    /// <summary>
    /// Reseta todos os timers
    /// </summary>
    public void ResetAllTimers()
    {
        foreach (var timer in timerDictionary.Values)
        {
            bool wasActive = !timer.IsReady;
            timer.Reset();
            
            if (wasActive)
            {
                OnCooldownFinished?.Invoke(timer.timerName);
            }
        }
        
        if (enableDebugLogs)
            Debug.Log("Todos os timers foram resetados!");
    }
    
    /// <summary>
    /// Reduz o cooldown de um timer
    /// </summary>
    public void ReduceCooldown(string timerName, float amount)
    {
        CooldownTimer timer = GetTimer(timerName);
        if (timer != null)
        {
            bool wasActive = !timer.IsReady;
            timer.ReduceCooldown(amount);
            
            if (wasActive && timer.IsReady)
            {
                OnCooldownFinished?.Invoke(timerName);
            }
            
            if (enableDebugLogs)
                Debug.Log($"Cooldown de '{timerName}' reduzido em {amount}s");
        }
    }
    
    /// <summary>
    /// Reduz o cooldown de todos os timers
    /// </summary>
    public void ReduceAllCooldowns(float amount)
    {
        foreach (var timer in timerDictionary.Values)
        {
            bool wasActive = !timer.IsReady;
            timer.ReduceCooldown(amount);
            
            if (wasActive && timer.IsReady)
            {
                OnCooldownFinished?.Invoke(timer.timerName);
            }
        }
        
        if (enableDebugLogs)
            Debug.Log($"Todos os cooldowns reduzidos em {amount}s");
    }
    
    /// <summary>
    /// Define redução global de cooldown
    /// </summary>
    public void SetGlobalCooldownReduction(float reduction)
    {
        globalCooldownReduction = Mathf.Clamp01(reduction);
        
        if (enableDebugLogs)
            Debug.Log($"Redução global de cooldown definida para {globalCooldownReduction:P}");
    }
    
    #endregion
    
    #region Update System
    
    private void UpdateAllTimers()
    {
        float deltaTime = Time.deltaTime;
        
        for (int i = activeTimers.Count - 1; i >= 0; i--)
        {
            CooldownTimer timer = activeTimers[i];
            bool wasActive = !timer.IsReady;
            
            timer.Update(deltaTime);
            
            // Verificar se o timer acabou de terminar
            if (wasActive && timer.IsReady)
            {
                OnCooldownFinished?.Invoke(timer.timerName);
                
                if (enableDebugLogs)
                    Debug.Log($"Cooldown de '{timer.timerName}' terminou!");
            }
            
            // Disparar evento de progresso se estiver ativo
            if (!timer.IsReady)
            {
                OnCooldownProgress?.Invoke(timer.timerName, timer.Progress);
            }
        }
    }
    
    #endregion
    
    #region Special Timer Types
    
    /// <summary>
    /// Cria um timer que se auto-destroi após usar
    /// </summary>
    public void CreateOneTimeTimer(string timerName, float cooldownTime)
    {
        AddTimer(timerName, cooldownTime);
        
        // Remover timer quando terminar
        System.Action<string> onFinished = null;
        onFinished = (name) => {
            if (name == timerName)
            {
                RemoveTimer(timerName);
                OnCooldownFinished -= onFinished;
            }
        };
        
        OnCooldownFinished += onFinished;
    }
    
    /// <summary>
    /// Cria um timer global de cooldown (afeta todas as ações)
    /// </summary>
    public void TriggerGlobalCooldown(float duration)
    {
        const string GLOBAL_TIMER = "GlobalCooldown";
        
        if (!HasTimer(GLOBAL_TIMER))
        {
            AddTimer(GLOBAL_TIMER, duration);
        }
        
        ForceUseTimer(GLOBAL_TIMER, duration);
    }
    
    /// <summary>
    /// Verifica se está em cooldown global
    /// </summary>
    public bool IsInGlobalCooldown()
    {
        return HasTimer("GlobalCooldown") && !CanUseTimer("GlobalCooldown");
    }
    
    #endregion
    
    #region Save/Load Support
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public CooldownManagerData GetSaveData()
    {
        CooldownManagerData data = new CooldownManagerData();
        data.timerData = new List<CooldownTimerData>();
        
        foreach (var timer in timerDictionary.Values)
        {
            data.timerData.Add(new CooldownTimerData
            {
                timerName = timer.timerName,
                baseCooldown = timer.baseCooldown,
                remainingTime = timer.remainingTime,
                isActive = timer.isActive
            });
        }
        
        data.globalCooldownReduction = globalCooldownReduction;
        return data;
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public void LoadSaveData(CooldownManagerData data)
    {
        if (data == null) return;
        
        // Limpar timers existentes
        timerDictionary.Clear();
        activeTimers.Clear();
        
        // Carregar timers salvos
        foreach (var timerData in data.timerData)
        {
            CooldownTimer timer = new CooldownTimer(timerData.timerName, timerData.baseCooldown);
            timer.remainingTime = timerData.remainingTime;
            timer.isActive = timerData.isActive;
            
            timerDictionary[timer.timerName] = timer;
            activeTimers.Add(timer);
        }
        
        globalCooldownReduction = data.globalCooldownReduction;
        
        if (enableDebugLogs)
            Debug.Log($"CooldownManager carregado com {timerDictionary.Count} timers");
    }
    
    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// Lista todos os timers e seus estados (para debug)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugListAllTimers()
    {
        Debug.Log("=== COOLDOWN TIMERS ===");
        
        if (timerDictionary.Count == 0)
        {
            Debug.Log("Nenhum timer ativo");
        }
        else
        {
            foreach (var timer in timerDictionary.Values)
            {
                string status = timer.IsReady ? "PRONTO" : $"COOLDOWN ({timer.RemainingTime:F1}s)";
                Debug.Log($"- {timer.timerName}: {status} | Base: {timer.baseCooldown}s");
            }
        }
        
        Debug.Log($"Redução Global: {globalCooldownReduction:P}");
        Debug.Log("=====================");
    }
    
    /// <summary>
    /// Força terminar todos os cooldowns (para debug)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugFinishAllCooldowns()
    {
        ResetAllTimers();
        Debug.Log("Todos os cooldowns foram forçados a terminar (DEBUG)");
    }
    
    #endregion
}

/// <summary>
/// Dados de salvamento do CooldownManager
/// </summary>
[System.Serializable]
public class CooldownManagerData
{
    public List<CooldownTimerData> timerData = new List<CooldownTimerData>();
    public float globalCooldownReduction = 0f;
}

/// <summary>
/// Dados de salvamento de um timer individual
/// </summary>
[System.Serializable]
public class CooldownTimerData
{
    public string timerName;
    public float baseCooldown;
    public float remainingTime;
    public bool isActive;
}