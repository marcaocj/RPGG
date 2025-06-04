using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gerencia as skills do jogador (aprendidas, cooldowns, uso)
/// </summary>
public class PlayerSkillManager : MonoBehaviour
{
    [Header("Skill Settings")]
    public List<Skill> availableSkills = new List<Skill>();
    public int maxActiveSkills = 8;
    
    [Header("Skill Points")]
    public int availableSkillPoints = 0;
    public int skillPointsPerLevel = 1;
    
    // Skills aprendidas e seus níveis
    private Dictionary<int, int> learnedSkills = new Dictionary<int, int>(); // skillID -> nível
    
    // Skills ativas (slots de ação)
    private Dictionary<int, Skill> activeSkills = new Dictionary<int, Skill>(); // slot -> skill
    
    // Sistema de cooldowns
    private CooldownManager cooldownManager;
    
    // Componentes
    private PlayerStats playerStats;
    private InputManager inputManager;
    
    // Eventos
    public System.Action<Skill> OnSkillLearned;
    public System.Action<Skill, int> OnSkillLevelUp;
    public System.Action<int, Skill> OnSkillAssigned; // slot, skill
    public System.Action<Skill> OnSkillUsed;
    public System.Action OnSkillsChanged;
    
    private void Awake()
    {
        // Obter componentes
        playerStats = GetComponent<PlayerStats>();
        cooldownManager = GetComponent<CooldownManager>();
        
        if (cooldownManager == null)
        {
            cooldownManager = gameObject.AddComponent<CooldownManager>();
        }
    }
    
    private void Start()
    {
        // Inscrever nos eventos
        SubscribeToEvents();
        
        // Inicializar slots ativos
        InitializeActiveSkills();
        
        // Configurar cooldowns iniciais
        SetupSkillCooldowns();
    }
    
    private void SubscribeToEvents()
    {
        // Eventos de input
        if (InputManager.Instance != null)
        {
            InputManager.OnSkillInput += TryUseSkill;
        }
        
        // Eventos do jogador
        EventManager.OnPlayerLevelUp += HandleLevelUp;
    }
    
    private void InitializeActiveSkills()
    {
        // Inicializar slots de skills ativas
        for (int i = 1; i <= maxActiveSkills; i++)
        {
            activeSkills[i] = null;
        }
    }
    
    private void SetupSkillCooldowns()
    {
        // Configurar timers de cooldown para skills aprendidas
        foreach (var skillPair in learnedSkills)
        {
            Skill skill = GetSkillByID(skillPair.Key);
            if (skill != null)
            {
                cooldownManager.AddTimer($"Skill_{skill.skillID}", skill.cooldownTime);
            }
        }
    }
    
    #region Skill Learning
    
    /// <summary>
    /// Aprende uma nova skill
    /// </summary>
    public bool LearnSkill(Skill skill)
    {
        if (skill == null) return false;
        
        // Verificar se já aprendeu
        if (HasLearnedSkill(skill.skillID))
        {
            Debug.Log($"Skill já aprendida: {skill.skillName}");
            return false;
        }
        
        // Verificar requisitos
        if (!CanLearnSkill(skill))
        {
            Debug.Log($"Não atende aos requisitos para aprender: {skill.skillName}");
            return false;
        }
        
        // Verificar pontos de skill
        if (availableSkillPoints < skill.skillPointCost)
        {
            Debug.Log($"Pontos de skill insuficientes para: {skill.skillName}");
            return false;
        }
        
        // Aprender skill
        learnedSkills[skill.skillID] = 1;
        availableSkillPoints -= skill.skillPointCost;
        
        // Adicionar cooldown timer
        cooldownManager.AddTimer($"Skill_{skill.skillID}", skill.cooldownTime);
        
        // Disparar eventos
        OnSkillLearned?.Invoke(skill);
        OnSkillsChanged?.Invoke();
        
        Debug.Log($"Skill aprendida: {skill.skillName}");
        return true;
    }
    
    /// <summary>
    /// Aumenta o nível de uma skill
    /// </summary>
    public bool LevelUpSkill(int skillID)
    {
        if (!HasLearnedSkill(skillID)) return false;
        
        Skill skill = GetSkillByID(skillID);
        if (skill == null) return false;
        
        int currentLevel = GetSkillLevel(skillID);
        int maxLevel = 10; // Definir nível máximo
        
        if (currentLevel >= maxLevel)
        {
            Debug.Log($"Skill já está no nível máximo: {skill.skillName}");
            return false;
        }
        
        if (availableSkillPoints < 1)
        {
            Debug.Log("Pontos de skill insuficientes");
            return false;
        }
        
        // Aumentar nível
        learnedSkills[skillID]++;
        availableSkillPoints--;
        
        // Disparar eventos
        OnSkillLevelUp?.Invoke(skill, learnedSkills[skillID]);
        OnSkillsChanged?.Invoke();
        
        Debug.Log($"Skill upada: {skill.skillName} (Nível {learnedSkills[skillID]})");
        return true;
    }
    
    /// <summary>
    /// Verifica se pode aprender uma skill
    /// </summary>
    public bool CanLearnSkill(Skill skill)
    {
        if (skill == null) return false;
        
        // Verificar nível
        if (playerStats != null && playerStats.level < skill.levelRequirement)
        {
            return false;
        }
        
        // Verificar skills pré-requisito
        foreach (Skill prerequisite in skill.prerequisiteSkills)
        {
            if (prerequisite != null && !HasLearnedSkill(prerequisite.skillID))
            {
                return false;
            }
        }
        
        return true;
    }
    
    #endregion
    
    #region Active Skills
    
    /// <summary>
    /// Atribui uma skill a um slot ativo
    /// </summary>
    public bool AssignSkillToSlot(int slotNumber, Skill skill)
    {
        if (slotNumber < 1 || slotNumber > maxActiveSkills) return false;
        
        if (skill != null && !HasLearnedSkill(skill.skillID))
        {
            Debug.Log($"Skill não aprendida: {skill.skillName}");
            return false;
        }
        
        activeSkills[slotNumber] = skill;
        
        OnSkillAssigned?.Invoke(slotNumber, skill);
        OnSkillsChanged?.Invoke();
        
        Debug.Log($"Skill atribuída ao slot {slotNumber}: {skill?.skillName ?? "Vazio"}");
        return true;
    }
    
    /// <summary>
    /// Remove skill de um slot
    /// </summary>
    public void RemoveSkillFromSlot(int slotNumber)
    {
        AssignSkillToSlot(slotNumber, null);
    }
    
    /// <summary>
    /// Obtém skill de um slot
    /// </summary>
    public Skill GetSkillInSlot(int slotNumber)
    {
        return activeSkills.ContainsKey(slotNumber) ? activeSkills[slotNumber] : null;
    }
    
    #endregion
    
    #region Skill Usage
    
    /// <summary>
    /// Tenta usar uma skill por slot
    /// </summary>
    public void TryUseSkill(int slotNumber)
    {
        Skill skill = GetSkillInSlot(slotNumber);
        if (skill != null)
        {
            TryUseSkill(skill);
        }
    }
    
    /// <summary>
    /// Tenta usar uma skill específica
    /// </summary>
    public bool TryUseSkill(Skill skill)
    {
        if (skill == null) return false;
        
        // Verificar se aprendeu a skill
        if (!HasLearnedSkill(skill.skillID))
        {
            Debug.Log($"Skill não aprendida: {skill.skillName}");
            return false;
        }
        
        // Verificar cooldown
        if (!IsSkillReady(skill.skillID))
        {
            Debug.Log($"Skill em cooldown: {skill.skillName}");
            return false;
        }
        
        // Verificar se pode usar
        if (!skill.CanUse(gameObject))
        {
            return false;
        }
        
        // Usar skill
        Vector3 targetPosition = GetTargetPosition();
        GameObject targetObject = GetTargetObject();
        
        bool skillUsed = skill.UseSkill(gameObject, targetPosition, targetObject);
        
        if (skillUsed)
        {
            OnSkillUsed?.Invoke(skill);
        }
        
        return skillUsed;
    }
    
    /// <summary>
    /// Inicia cooldown de uma skill
    /// </summary>
    public void StartSkillCooldown(int skillID, float cooldownTime)
    {
        cooldownManager.TryUseTimer($"Skill_{skillID}", cooldownTime);
    }
    
    /// <summary>
    /// Verifica se uma skill está pronta para uso
    /// </summary>
    public bool IsSkillReady(int skillID)
    {
        return cooldownManager.CanUseTimer($"Skill_{skillID}");
    }
    
    /// <summary>
    /// Obtém tempo restante de cooldown de uma skill
    /// </summary>
    public float GetSkillCooldownRemaining(int skillID)
    {
        CooldownTimer timer = cooldownManager.GetTimer($"Skill_{skillID}");
        return timer?.RemainingTime ?? 0f;
    }
    
    #endregion
    
    #region Targeting
    
    private Vector3 GetTargetPosition()
    {
        // Usar posição do mouse no mundo
        return InputManager.Instance?.GetMouseWorldPosition() ?? transform.position + transform.forward * 3f;
    }
    
    private GameObject GetTargetObject()
    {
        // Implementar detecção de alvo sob o mouse
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return null;
        
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 50f))
        {
            return hit.collider.gameObject;
        }
        
        return null;
    }
    
    #endregion
    
    #region Queries
    
    /// <summary>
    /// Verifica se aprendeu uma skill
    /// </summary>
    public bool HasLearnedSkill(int skillID)
    {
        return learnedSkills.ContainsKey(skillID);
    }
    
    /// <summary>
    /// Obtém nível de uma skill
    /// </summary>
    public int GetSkillLevel(int skillID)
    {
        return learnedSkills.ContainsKey(skillID) ? learnedSkills[skillID] : 0;
    }
    
    /// <summary>
    /// Obtém skill por ID
    /// </summary>
    public Skill GetSkillByID(int skillID)
    {
        return availableSkills.FirstOrDefault(s => s.skillID == skillID);
    }
    
    /// <summary>
    /// Obtém todas as skills aprendidas
    /// </summary>
    public List<Skill> GetLearnedSkills()
    {
        List<Skill> learned = new List<Skill>();
        
        foreach (int skillID in learnedSkills.Keys)
        {
            Skill skill = GetSkillByID(skillID);
            if (skill != null)
            {
                learned.Add(skill);
            }
        }
        
        return learned;
    }
    
    /// <summary>
    /// Obtém skills que podem ser aprendidas
    /// </summary>
    public List<Skill> GetAvailableSkillsToLearn()
    {
        return availableSkills.Where(s => !HasLearnedSkill(s.skillID) && CanLearnSkill(s)).ToList();
    }
    
    /// <summary>
    /// Obtém mapeamento dos slots ativos
    /// </summary>
    public Dictionary<int, Skill> GetActiveSkillMapping()
    {
        return new Dictionary<int, Skill>(activeSkills);
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandleLevelUp(int newLevel)
    {
        // Ganhar pontos de skill ao subir de nível
        availableSkillPoints += skillPointsPerLevel;
        
        Debug.Log($"Level up! Pontos de skill ganhos: {skillPointsPerLevel}. Total: {availableSkillPoints}");
    }
    
    #endregion
    
    #region Save/Load Support
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public SkillManagerData GetSaveData()
    {
        SkillManagerData data = new SkillManagerData();
        data.availableSkillPoints = availableSkillPoints;
        data.learnedSkills = new Dictionary<int, int>(learnedSkills);
        data.activeSkills = new Dictionary<int, int>();
        
        // Salvar skills ativas
        foreach (var kvp in activeSkills)
        {
            if (kvp.Value != null)
            {
                data.activeSkills[kvp.Key] = kvp.Value.skillID;
            }
        }
        
        return data;
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public void LoadSaveData(SkillManagerData data)
    {
        if (data == null) return;
        
        availableSkillPoints = data.availableSkillPoints;
        learnedSkills = new Dictionary<int, int>(data.learnedSkills);
        
        // Carregar skills ativas
        foreach (var kvp in data.activeSkills)
        {
            Skill skill = GetSkillByID(kvp.Value);
            if (skill != null)
            {
                activeSkills[kvp.Key] = skill;
            }
        }
        
        // Reconfigurar cooldowns
        SetupSkillCooldowns();
        
        OnSkillsChanged?.Invoke();
    }
    
    #endregion
    
    #region Debug
    
    /// <summary>
    /// Lista todas as skills aprendidas (para debug)
    /// </summary>
    public void DebugListLearnedSkills()
    {
        Debug.Log("=== SKILLS APRENDIDAS ===");
        foreach (var kvp in learnedSkills)
        {
            Skill skill = GetSkillByID(kvp.Key);
            if (skill != null)
            {
                Debug.Log($"{skill.skillName} - Nível {kvp.Value}");
            }
        }
        Debug.Log($"Pontos disponíveis: {availableSkillPoints}");
        Debug.Log("========================");
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        if (InputManager.Instance != null)
        {
            InputManager.OnSkillInput -= TryUseSkill;
        }
        
        EventManager.OnPlayerLevelUp -= HandleLevelUp;
    }
}

/// <summary>
/// Dados de salvamento do gerenciador de skills
/// </summary>
[System.Serializable]
public class SkillManagerData
{
    public int availableSkillPoints;
    public Dictionary<int, int> learnedSkills = new Dictionary<int, int>();
    public Dictionary<int, int> activeSkills = new Dictionary<int, int>();
}