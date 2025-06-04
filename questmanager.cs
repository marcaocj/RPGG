using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gerencia todas as quests do jogo (ativas, completadas, disponíveis)
/// </summary>
public class QuestManager : Singleton<QuestManager>
{
    [Header("Quest Database")]
    public List<Quest> allQuests = new List<Quest>();
    
    [Header("Debug")]
    public bool enableDebugLogs = false;
    
    // Quests ativas
    private List<Quest> activeQuests = new List<Quest>();
    
    // Quests completadas
    private List<int> completedQuestIDs = new List<int>();
    
    // Quests falhadas
    private List<int> failedQuestIDs = new List<int>();
    
    // Cache de quests por ID
    private Dictionary<int, Quest> questCache = new Dictionary<int, Quest>();
    
    // Eventos
    public System.Action<Quest> OnQuestStarted;
    public System.Action<Quest> OnQuestCompleted;
    public System.Action<Quest> OnQuestFailed;
    public System.Action<Quest> OnQuestUpdated;
    public System.Action OnQuestLogChanged;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Construir cache de quests
        BuildQuestCache();
    }
    
    private void Start()
    {
        // Inscrever nos eventos globais
        SubscribeToEvents();
    }
    
    private void BuildQuestCache()
    {
        questCache.Clear();
        
        foreach (Quest quest in allQuests)
        {
            if (quest != null)
            {
                questCache[quest.questID] = quest;
            }
        }
        
        Debug.Log($"Quest cache construído com {questCache.Count} quests");
    }
    
    private void SubscribeToEvents()
    {
        // Eventos específicos para tracking de objetivos
        EventManager.OnEnemyDeath += HandleEnemyDeath;
        EventManager.OnItemPickup += HandleItemPickup;
        EventManager.OnPlayerLevelUp += HandlePlayerLevelUp;
        EventManager.OnGoldChanged += HandleGoldChanged;
    }
    
    #region Quest Management
    
    /// <summary>
    /// Inicia uma quest
    /// </summary>
    public bool StartQuest(int questID)
    {
        Quest quest = GetQuestByID(questID);
        return quest != null && StartQuest(quest);
    }
    
    /// <summary>
    /// Inicia uma quest
    /// </summary>
    public bool StartQuest(Quest quest)
    {
        if (quest == null) return false;
        
        // Verificar se já está ativa
        if (IsQuestActive(quest.questID))
        {
            Debug.Log($"Quest já está ativa: {quest.questName}");
            return false;
        }
        
        // Verificar se pode iniciar
        if (!quest.CanStartQuest())
        {
            Debug.Log($"Não é possível iniciar quest: {quest.questName}");
            return false;
        }
        
        // Criar instância da quest para runtime
        Quest questInstance = quest.CreateInstance();
        
        // Iniciar quest
        if (questInstance.StartQuest())
        {
            activeQuests.Add(questInstance);
            
            // Disparar eventos
            OnQuestStarted?.Invoke(questInstance);
            OnQuestLogChanged?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log($"Quest iniciada: {quest.questName}");
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Completa uma quest
    /// </summary>
    public bool CompleteQuest(int questID)
    {
        Quest quest = GetActiveQuest(questID);
        if (quest == null) return false;
        
        return CompleteQuest(quest);
    }
    
    /// <summary>
    /// Completa uma quest
    /// </summary>
    public bool CompleteQuest(Quest quest)
    {
        if (quest == null || !IsQuestActive(quest.questID)) return false;
        
        // Completar quest
        if (quest.CompleteQuest())
        {
            // Remover da lista ativa
            activeQuests.Remove(quest);
            
            // Adicionar à lista de completadas
            if (!completedQuestIDs.Contains(quest.questID))
            {
                completedQuestIDs.Add(quest.questID);
            }
            
            // Disparar eventos
            OnQuestCompleted?.Invoke(quest);
            OnQuestLogChanged?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log($"Quest completada: {quest.questName}");
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Falha uma quest
    /// </summary>
    public bool FailQuest(int questID)
    {
        Quest quest = GetActiveQuest(questID);
        if (quest == null) return false;
        
        return FailQuest(quest);
    }
    
    /// <summary>
    /// Falha uma quest
    /// </summary>
    public bool FailQuest(Quest quest)
    {
        if (quest == null || !IsQuestActive(quest.questID)) return false;
        
        quest.FailQuest();
        
        // Remover da lista ativa
        activeQuests.Remove(quest);
        
        // Adicionar à lista de falhadas
        if (!failedQuestIDs.Contains(quest.questID))
        {
            failedQuestIDs.Add(quest.questID);
        }
        
        // Disparar eventos
        OnQuestFailed?.Invoke(quest);
        OnQuestLogChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"Quest falhada: {quest.questName}");
        
        return true;
    }
    
    /// <summary>
    /// Abandona uma quest
    /// </summary>
    public bool AbandonQuest(int questID)
    {
        Quest quest = GetActiveQuest(questID);
        if (quest == null) return false;
        
        quest.AbandonQuest();
        activeQuests.Remove(quest);
        
        OnQuestLogChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"Quest abandonada: {quest.questName}");
        
        return true;
    }
    
    #endregion
    
    #region Quest Progress Tracking
    
    /// <summary>
    /// Atualiza progresso de uma quest específica
    /// </summary>
    public void UpdateQuestProgress(int questID, int objectiveIndex, int progress)
    {
        Quest quest = GetActiveQuest(questID);
        if (quest != null)
        {
            quest.UpdateObjective(objectiveIndex, progress);
            OnQuestUpdated?.Invoke(quest);
        }
    }
    
    /// <summary>
    /// Processa eventos globais para atualizar quests
    /// </summary>
    public void ProcessQuestEvent(ObjectiveType eventType, int targetID, int amount = 1)
    {
        foreach (Quest quest in activeQuests)
        {
            quest.UpdateObjectiveByType(eventType, targetID, amount);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandleEnemyDeath(GameObject enemy)
    {
        // Tentar obter ID do inimigo
        EnemyStats enemyStats = enemy.GetComponent<EnemyStats>();
        if (enemyStats != null)
        {
            int enemyID = enemyStats.enemyLevel; // Usar nível como ID temporário
            ProcessQuestEvent(ObjectiveType.KillEnemy, enemyID, 1);
        }
    }
    
    private void HandleItemPickup(Item item)
    {
        if (item != null)
        {
            ProcessQuestEvent(ObjectiveType.CollectItem, item.itemID, 1);
        }
    }
    
    private void HandlePlayerLevelUp(int newLevel)
    {
        ProcessQuestEvent(ObjectiveType.LevelUp, newLevel, 1);
    }
    
    private void HandleGoldChanged(int goldAmount)
    {
        // Processar objetivos relacionados a ouro se necessário
        ProcessQuestEvent(ObjectiveType.SpendGold, 0, goldAmount);
    }
    
    #endregion
    
    #region Queries
    
    /// <summary>
    /// Obtém quest por ID
    /// </summary>
    public Quest GetQuestByID(int questID)
    {
        return questCache.ContainsKey(questID) ? questCache[questID] : null;
    }
    
    /// <summary>
    /// Obtém quest ativa por ID
    /// </summary>
    public Quest GetActiveQuest(int questID)
    {
        return activeQuests.FirstOrDefault(q => q.questID == questID);
    }
    
    /// <summary>
    /// Verifica se uma quest está ativa
    /// </summary>
    public bool IsQuestActive(int questID)
    {
        return GetActiveQuest(questID) != null;
    }
    
    /// <summary>
    /// Verifica se uma quest foi completada
    /// </summary>
    public bool IsQuestCompleted(int questID)
    {
        return completedQuestIDs.Contains(questID);
    }
    
    /// <summary>
    /// Verifica se uma quest falhou
    /// </summary>
    public bool IsQuestFailed(int questID)
    {
        return failedQuestIDs.Contains(questID);
    }
    
    /// <summary>
    /// Obtém todas as quests ativas
    /// </summary>
    public List<Quest> GetActiveQuests()
    {
        return new List<Quest>(activeQuests);
    }
    
    /// <summary>
    /// Obtém quests ativas por tipo
    /// </summary>
    public List<Quest> GetActiveQuestsByType(QuestType questType)
    {
        return activeQuests.Where(q => q.questType == questType).ToList();
    }
    
    /// <summary>
    /// Obtém quests principais ativas
    /// </summary>
    public List<Quest> GetActiveMainQuests()
    {
        return activeQuests.Where(q => q.isMainQuest).ToList();
    }
    
    /// <summary>
    /// Obtém quests disponíveis para iniciar
    /// </summary>
    public List<Quest> GetAvailableQuests()
    {
        List<Quest> available = new List<Quest>();
        
        foreach (Quest quest in allQuests)
        {
            if (quest != null && 
                !IsQuestActive(quest.questID) && 
                !IsQuestCompleted(quest.questID) && 
                quest.CanStartQuest())
            {
                available.Add(quest);
            }
        }
        
        return available;
    }
    
    /// <summary>
    /// Obtém todas as quests completadas
    /// </summary>
    public List<Quest> GetCompletedQuests()
    {
        List<Quest> completed = new List<Quest>();
        
        foreach (int questID in completedQuestIDs)
        {
            Quest quest = GetQuestByID(questID);
            if (quest != null)
            {
                completed.Add(quest);
            }
        }
        
        return completed;
    }
    
    #endregion
    
    #region Quest Giver Integration
    
    /// <summary>
    /// Obtém quests disponíveis de um NPC específico
    /// </summary>
    public List<Quest> GetQuestsFromNPC(int npcID)
    {
        List<Quest> npcQuests = new List<Quest>();
        
        foreach (Quest quest in GetAvailableQuests())
        {
            if (quest.questGiverID == npcID)
            {
                npcQuests.Add(quest);
            }
        }
        
        return npcQuests;
    }
    
    /// <summary>
    /// Obtém quests que podem ser entregues a um NPC
    /// </summary>
    public List<Quest> GetCompletableQuestsForNPC(int npcID)
    {
        List<Quest> completableQuests = new List<Quest>();
        
        foreach (Quest quest in activeQuests)
        {
            if (quest.questGiverID == npcID && quest.CanCompleteQuest())
            {
                completableQuests.Add(quest);
            }
        }
        
        return completableQuests;
    }
    
    #endregion
    
    #region Statistics
    
    /// <summary>
    /// Obtém estatísticas de quests
    /// </summary>
    public QuestStatistics GetQuestStatistics()
    {
        return new QuestStatistics
        {
            totalQuests = allQuests.Count,
            activeQuests = activeQuests.Count,
            completedQuests = completedQuestIDs.Count,
            failedQuests = failedQuestIDs.Count,
            availableQuests = GetAvailableQuests().Count,
            completionRate = allQuests.Count > 0 ? (float)completedQuestIDs.Count / allQuests.Count : 0f
        };
    }
    
    #endregion
    
    #region Save/Load Support
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public QuestManagerData GetSaveData()
    {
        QuestManagerData data = new QuestManagerData();
        data.completedQuestIDs = new List<int>(completedQuestIDs);
        data.failedQuestIDs = new List<int>(failedQuestIDs);
        data.activeQuests = new List<QuestSaveData>();
        
        // Salvar quests ativas com seu progresso
        foreach (Quest quest in activeQuests)
        {
            QuestSaveData questData = new QuestSaveData
            {
                questID = quest.questID,
                status = quest.status,
                objectiveProgress = new List<int>()
            };
            
            // Salvar progresso dos objetivos
            foreach (var objective in quest.objectives)
            {
                questData.objectiveProgress.Add(objective.currentProgress);
            }
            
            data.activeQuests.Add(questData);
        }
        
        return data;
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public void LoadSaveData(QuestManagerData data)
    {
        if (data == null) return;
        
        // Limpar estado atual
        activeQuests.Clear();
        completedQuestIDs.Clear();
        failedQuestIDs.Clear();
        
        // Carregar listas
        completedQuestIDs = new List<int>(data.completedQuestIDs);
        failedQuestIDs = new List<int>(data.failedQuestIDs);
        
        // Carregar quests ativas
        foreach (QuestSaveData questSaveData in data.activeQuests)
        {
            Quest originalQuest = GetQuestByID(questSaveData.questID);
            if (originalQuest != null)
            {
                Quest questInstance = originalQuest.CreateInstance();
                questInstance.status = questSaveData.status;
                
                // Restaurar progresso dos objetivos
                for (int i = 0; i < questInstance.objectives.Count && i < questSaveData.objectiveProgress.Count; i++)
                {
                    questInstance.objectives[i].currentProgress = questSaveData.objectiveProgress[i];
                }
                
                activeQuests.Add(questInstance);
            }
        }
        
        OnQuestLogChanged?.Invoke();
        
        if (enableDebugLogs)
            Debug.Log($"Quest data carregado: {activeQuests.Count} ativas, {completedQuestIDs.Count} completadas");
    }
    
    #endregion
    
    #region Debug Methods
    
    /// <summary>
    /// Completa todas as quests ativas (para debug)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugCompleteAllQuests()
    {
        List<Quest> questsToComplete = new List<Quest>(activeQuests);
        
        foreach (Quest quest in questsToComplete)
        {
            // Completar todos os objetivos
            for (int i = 0; i < quest.objectives.Count; i++)
            {
                var objective = quest.objectives[i];
                objective.currentProgress = objective.targetAmount;
            }
            
            CompleteQuest(quest);
        }
        
        Debug.Log("Todas as quests foram completadas (DEBUG)");
    }
    
    /// <summary>
    /// Lista informações de todas as quests
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugListAllQuests()
    {
        Debug.Log("=== TODAS AS QUESTS ===");
        
        Debug.Log($"ATIVAS ({activeQuests.Count}):");
        foreach (Quest quest in activeQuests)
        {
            Debug.Log($"- {quest.questName} ({quest.status}) - {quest.GetOverallProgress():P}");
        }
        
        Debug.Log($"COMPLETADAS ({completedQuestIDs.Count}):");
        foreach (int questID in completedQuestIDs)
        {
            Quest quest = GetQuestByID(questID);
            Debug.Log($"- {quest?.questName ?? $"ID:{questID}"}");
        }
        
        Debug.Log($"DISPONÍVEIS ({GetAvailableQuests().Count}):");
        foreach (Quest quest in GetAvailableQuests())
        {
            Debug.Log($"- {quest.questName}");
        }
        
        Debug.Log("=====================");
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        EventManager.OnEnemyDeath -= HandleEnemyDeath;
        EventManager.OnItemPickup -= HandleItemPickup;
        EventManager.OnPlayerLevelUp -= HandlePlayerLevelUp;
        EventManager.OnGoldChanged -= HandleGoldChanged;
    }
}

/// <summary>
/// Dados de salvamento do QuestManager
/// </summary>
[System.Serializable]
public class QuestManagerData
{
    public List<int> completedQuestIDs = new List<int>();
    public List<int> failedQuestIDs = new List<int>();
    public List<QuestSaveData> activeQuests = new List<QuestSaveData>();
}

/// <summary>
/// Dados de salvamento de uma quest individual
/// </summary>
[System.Serializable]
public class QuestSaveData
{
    public int questID;
    public QuestStatus status;
    public List<int> objectiveProgress = new List<int>();
}

/// <summary>
/// Estatísticas de quests
/// </summary>
[System.Serializable]
public class QuestStatistics
{
    public int totalQuests;
    public int activeQuests;
    public int completedQuests;
    public int failedQuests;
    public int availableQuests;
    public float completionRate;
}
        