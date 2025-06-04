using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enums para o sistema de quests
/// </summary>
public enum QuestStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    Abandoned
}

public enum QuestType
{
    Main,
    Side,
    Daily,
    Repeatable
}

public enum ObjectiveType
{
    KillEnemy,
    CollectItem,
    ReachLocation,
    TalkToNPC,
    LevelUp,
    SpendGold,
    EquipItem,
    UseSkill,
    DeliverItem,
    Custom
}

/// <summary>
/// Objetivo de uma quest
/// </summary>
[System.Serializable]
public class QuestObjective
{
    [Header("Objective Info")]
    public string description;
    public ObjectiveType objectiveType;
    public int targetID; // ID do item/inimigo/NPC
    public int targetAmount = 1;
    public int currentProgress = 0;
    
    [Header("Optional")]
    public Vector3 targetLocation;
    public float locationRadius = 5f;
    
    public bool IsCompleted => currentProgress >= targetAmount;
    public float ProgressPercentage => targetAmount > 0 ? (float)currentProgress / targetAmount : 0f;
    
    /// <summary>
    /// Atualiza o progresso do objetivo
    /// </summary>
    public bool UpdateProgress(int amount)
    {
        int oldProgress = currentProgress;
        currentProgress = Mathf.Clamp(currentProgress + amount, 0, targetAmount);
        
        return currentProgress != oldProgress;
    }
    
    /// <summary>
    /// Verifica se o objetivo pode ser atualizado com determinados parâmetros
    /// </summary>
    public bool CanUpdateWith(ObjectiveType type, int id)
    {
        return objectiveType == type && (targetID == id || targetID == 0);
    }
}

/// <summary>
/// Recompensa de uma quest
/// </summary>
[System.Serializable]
public class QuestReward
{
    [Header("Experience & Gold")]
    public int experienceReward = 0;
    public int goldReward = 0;
    
    [Header("Items")]
    public List<ItemReward> itemRewards = new List<ItemReward>();
    
    [Header("Optional")]
    public int skillPointsReward = 0;
    public List<int> unlockQuestIDs = new List<int>();
}

[System.Serializable]
public class ItemReward
{
    public Item item;
    public int quantity = 1;
    public float dropChance = 1f; // 0-1
}

/// <summary>
/// ScriptableObject que define uma quest
/// </summary>
[CreateAssetMenu(fileName = "New Quest", menuName = "Game/Quest", order = 1)]
public class Quest : ScriptableObject
{
    [Header("Basic Info")]
    public int questID;
    public string questName;
    [TextArea(3, 5)]
    public string description;
    public bool isMainQuest = false;
    public QuestType questType = QuestType.Side;
    
    [Header("Requirements")]
    public int levelRequirement = 1;
    public List<Quest> prerequisiteQuests = new List<Quest>();
    public List<int> requiredItems = new List<int>();
    
    [Header("Quest Giver")]
    public int questGiverID;
    public string questGiverName;
    
    [Header("Objectives")]
    public List<QuestObjective> objectives = new List<QuestObjective>();
    
    [Header("Rewards")]
    public QuestReward rewards;
    
    [Header("Runtime Data - Don't Edit")]
    public QuestStatus status = QuestStatus.NotStarted;
    public bool canBeAbandoned = true;
    public bool isRepeatable = false;
    
    // Eventos específicos desta quest
    public System.Action<Quest> OnQuestStarted;
    public System.Action<Quest> OnQuestCompleted;
    public System.Action<Quest> OnQuestFailed;
    public System.Action<Quest> OnObjectiveCompleted;
    
    /// <summary>
    /// Cria uma instância runtime da quest
    /// </summary>
    public Quest CreateInstance()
    {
        Quest instance = Instantiate(this);
        instance.status = QuestStatus.NotStarted;
        
        // Resetar progresso dos objetivos
        foreach (var objective in instance.objectives)
        {
            objective.currentProgress = 0;
        }
        
        return instance;
    }
    
    #region Quest State Management
    
    /// <summary>
    /// Verifica se a quest pode ser iniciada
    /// </summary>
    public bool CanStartQuest()
    {
        if (status != QuestStatus.NotStarted) return false;
        
        // Verificar nível
        GameObject player = GameManager.Instance?.CurrentPlayer;
        if (player != null)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null && playerStats.level < levelRequirement)
            {
                return false;
            }
        }
        
        // Verificar quests pré-requisito
        if (!PrerequisiteQuestsMet()) return false;
        
        // Verificar itens requeridos
        if (!RequiredItemsAvailable()) return false;
        
        return true;
    }
    
    /// <summary>
    /// Inicia a quest
    /// </summary>
    public bool StartQuest()
    {
        if (!CanStartQuest()) return false;
        
        status = QuestStatus.InProgress;
        
        // Consumir itens requeridos
        ConsumeRequiredItems();
        
        // Disparar evento
        OnQuestStarted?.Invoke(this);
        EventManager.TriggerQuestStarted(this);
        
        Debug.Log($"Quest iniciada: {questName}");
        return true;
    }
    
    /// <summary>
    /// Verifica se a quest pode ser completada
    /// </summary>
    public bool CanCompleteQuest()
    {
        if (status != QuestStatus.InProgress) return false;
        
        // Verificar se todos os objetivos foram completados
        return objectives.All(obj => obj.IsCompleted);
    }
    
    /// <summary>
    /// Completa a quest
    /// </summary>
    public bool CompleteQuest()
    {
        if (!CanCompleteQuest()) return false;
        
        status = QuestStatus.Completed;
        
        // Dar recompensas
        GiveRewards();
        
        // Disparar eventos
        OnQuestCompleted?.Invoke(this);
        EventManager.TriggerQuestCompleted(this);
        
        Debug.Log($"Quest completada: {questName}");
        return true;
    }
    
    /// <summary>
    /// Falha a quest
    /// </summary>
    public void FailQuest()
    {
        if (status != QuestStatus.InProgress) return;
        
        status = QuestStatus.Failed;
        
        OnQuestFailed?.Invoke(this);
        EventManager.TriggerQuestFailed(this);
        
        Debug.Log($"Quest falhada: {questName}");
    }
    
    /// <summary>
    /// Abandona a quest
    /// </summary>
    public bool AbandonQuest()
    {
        if (!canBeAbandoned || status != QuestStatus.InProgress) return false;
        
        status = QuestStatus.Abandoned;
        
        Debug.Log($"Quest abandonada: {questName}");
        return true;
    }
    
    #endregion
    
    #region Objective Management
    
    /// <summary>
    /// Atualiza um objetivo específico
    /// </summary>
    public bool UpdateObjective(int objectiveIndex, int progress)
    {
        if (status != QuestStatus.InProgress) return false;
        if (objectiveIndex < 0 || objectiveIndex >= objectives.Count) return false;
        
        QuestObjective objective = objectives[objectiveIndex];
        bool wasCompleted = objective.IsCompleted;
        
        if (objective.UpdateProgress(progress))
        {
            // Verificar se o objetivo foi completado agora
            if (!wasCompleted && objective.IsCompleted)
            {
                OnObjectiveCompleted?.Invoke(this);
                Debug.Log($"Objetivo completado: {objective.description}");
            }
            
            // Verificar se a quest toda foi completada
            if (CanCompleteQuest())
            {
                CompleteQuest();
            }
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Atualiza objetivos baseado no tipo e ID
    /// </summary>
    public bool UpdateObjectiveByType(ObjectiveType type, int targetID, int amount = 1)
    {
        if (status != QuestStatus.InProgress) return false;
        
        bool anyUpdated = false;
        
        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjective objective = objectives[i];
            
            if (objective.CanUpdateWith(type, targetID) && !objective.IsCompleted)
            {
                if (UpdateObjective(i, amount))
                {
                    anyUpdated = true;
                }
            }
        }
        
        return anyUpdated;
    }
    
    #endregion
    
    #region Requirements Check
    
    private bool PrerequisiteQuestsMet()
    {
        QuestManager questManager = QuestManager.Instance;
        if (questManager == null)
        {
            Debug.LogWarning("QuestManager não encontrado! Assumindo que pré-requisitos foram atendidos.");
            return true;
        }
        
        foreach (Quest prerequisite in prerequisiteQuests)
        {
            if (prerequisite != null && !questManager.IsQuestCompleted(prerequisite.questID))
            {
                return false;
            }
        }
        
        return true;
    }
    
    private bool RequiredItemsAvailable()
    {
        GameObject currentPlayer = GameManager.Instance?.CurrentPlayer;
        if (currentPlayer == null)
        {
            Debug.LogWarning("Player não encontrado! Assumindo que itens estão disponíveis.");
            return true;
        }
        
        PlayerInventory inventory = currentPlayer.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("PlayerInventory não encontrado! Assumindo que itens estão disponíveis.");
            return true;
        }
        
        foreach (int itemID in requiredItems)
        {
            if (!inventory.HasItem(itemID))
            {
                return false;
            }
        }
        
        return true;
    }
    
    private void ConsumeRequiredItems()
    {
        GameObject currentPlayer = GameManager.Instance?.CurrentPlayer;
        if (currentPlayer == null) return;
        
        PlayerInventory inventory = currentPlayer.GetComponent<PlayerInventory>();
        if (inventory == null) return;
        
        foreach (int itemID in requiredItems)
        {
            inventory.RemoveItem(itemID, 1);
        }
    }
    
    #endregion
    
    #region Rewards
    
    private void GiveRewards()
    {
        GameObject player = GameManager.Instance?.CurrentPlayer;
        if (player == null) return;
        
        // Dar experiência
        if (rewards.experienceReward > 0)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.GainExperience(rewards.experienceReward);
            }
        }
        
        // Dar ouro
        if (rewards.goldReward > 0)
        {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddGold(rewards.goldReward);
            }
        }
        
        // Dar itens
        foreach (ItemReward itemReward in rewards.itemRewards)
        {
            if (itemReward.item != null && Random.value <= itemReward.dropChance)
            {
                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    inventory.AddItem(itemReward.item, itemReward.quantity);
                }
            }
        }
        
        // Dar pontos de skill
        if (rewards.skillPointsReward > 0)
        {
            PlayerSkillManager skillManager = player.GetComponent<PlayerSkillManager>();
            if (skillManager != null)
            {
                skillManager.availableSkillPoints += rewards.skillPointsReward;
            }
        }
        
        // Desbloquear outras quests
        foreach (int questID in rewards.unlockQuestIDs)
        {
            // Lógica para desbloquear outras quests pode ser implementada aqui
            Debug.Log($"Quest desbloqueada: {questID}");
        }
    }
    
    #endregion
    
    #region Progress Info
    
    /// <summary>
    /// Obtém progresso geral da quest (0-1)
    /// </summary>
    public float GetOverallProgress()
    {
        if (objectives.Count == 0) return status == QuestStatus.Completed ? 1f : 0f;
        
        float totalProgress = 0f;
        foreach (var objective in objectives)
        {
            totalProgress += objective.ProgressPercentage;
        }
        
        return totalProgress / objectives.Count;
    }
    
    /// <summary>
    /// Obtém texto de status da quest
    /// </summary>
    public string GetStatusText()
    {
        switch (status)
        {
            case QuestStatus.NotStarted: return "Não Iniciada";
            case QuestStatus.InProgress: return $"Em Progresso ({GetOverallProgress():P0})";
            case QuestStatus.Completed: return "Completada";
            case QuestStatus.Failed: return "Falhada";
            case QuestStatus.Abandoned: return "Abandonada";
            default: return "Desconhecido";
        }
    }
    
    /// <summary>
    /// Obtém descrição detalhada do progresso
    /// </summary>
    public string GetProgressDescription()
    {
        if (status != QuestStatus.InProgress) return GetStatusText();
        
        string description = $"{questName} - {GetStatusText()}\n\n";
        
        for (int i = 0; i < objectives.Count; i++)
        {
            var objective = objectives[i];
            string checkbox = objective.IsCompleted ? "✓" : "○";
            description += $"{checkbox} {objective.description} ({objective.currentProgress}/{objective.targetAmount})\n";
        }
        
        return description;
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Valida os dados da quest (para debug)
    /// </summary>
    public bool ValidateQuest()
    {
        bool isValid = true;
        
        if (questID <= 0)
        {
            Debug.LogError($"Quest {name} tem ID inválido: {questID}");
            isValid = false;
        }
        
        if (string.IsNullOrEmpty(questName))
        {
            Debug.LogError($"Quest {name} não tem nome definido");
            isValid = false;
        }
        
        if (objectives.Count == 0)
        {
            Debug.LogWarning($"Quest {questName} não tem objetivos definidos");
        }
        
        return isValid;
    }
    
    #endregion
}