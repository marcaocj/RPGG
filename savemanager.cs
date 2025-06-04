using UnityEngine;
using System.IO;
using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Collections;
using System.IO.Compression;

/// <summary>
/// Gerencia o salvamento e carregamento de dados do jogo - VERSÃO FINAL COMPLETA
/// </summary>
public class SaveManager : Singleton<SaveManager>
{
    [Header("Save Settings")]
    public string saveFileName = "savegame.json";
    public bool useEncryption = true;
    public bool compressData = true;
    public int maxSaveSlots = 5;
    
    [Header("Auto Save")]
    public bool enableAutoSave = true;
    public float autoSaveInterval = 300f; // 5 minutos
    public bool autoSaveOnLevelUp = true;
    public bool autoSaveOnQuestComplete = true;
    public bool autoSaveOnSceneChange = true;
    
    [Header("Backup")]
    public bool createBackups = true;
    public int maxBackups = 3;
    
    [Header("Debug")]
    public bool enableDebugLogs = false;
    public bool saveInEditorMode = true;
    
    // Caminho para os arquivos de save
    private string savePath;
    private string backupPath;
    
    // Timer para auto save
    private float timeSinceLastSave = 0f;
    private bool autoSaveEnabled = true;
    
    // Encryption key
    private const string ENCRYPTION_KEY = "DiabloLikeGame2024SecretKey!@#$%";
    
    // Eventos
    public System.Action OnGameSaved;
    public System.Action OnGameLoaded;
    public System.Action<string> OnSaveError;
    public System.Action<string> OnLoadError;
    public System.Action<int> OnAutoSave; // slot number
    
    // Cache de dados para otimização
    private GameSaveData lastSaveData;
    private float lastSaveTime = 0f;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Configurar caminhos de save
        SetupSavePaths();
        
        // Validar configurações
        ValidateSettings();
        
        if (enableDebugLogs)
            Debug.Log($"SaveManager inicializado. Save path: {savePath}");
    }
    
    private void Start()
    {
        // Inscrever nos eventos relevantes
        SubscribeToEvents();
        
        // Carregar configurações salvas
        LoadSaveSettings();
        
        // Executar limpeza inicial
        CleanupSaveFiles();
    }
    
    private void Update()
    {
        // Auto save
        if (enableAutoSave && autoSaveEnabled && ShouldAutoSave())
        {
            timeSinceLastSave += Time.deltaTime;
            
            if (timeSinceLastSave >= autoSaveInterval)
            {
                PerformAutoSave();
                timeSinceLastSave = 0f;
            }
        }
    }
    
    private void SetupSavePaths()
    {
        // Caminho principal de saves
        savePath = Path.Combine(Application.persistentDataPath, "Saves");
        
        // Caminho de backups
        backupPath = Path.Combine(savePath, "Backups");
        
        // Criar diretórios se não existirem
        try
        {
            if (!Directory.Exists(savePath))
            {
                Directory.CreateDirectory(savePath);
            }
            
            if (createBackups && !Directory.Exists(backupPath))
            {
                Directory.CreateDirectory(backupPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Erro ao criar diretórios de save: {e.Message}");
        }
    }
    
    private void ValidateSettings()
    {
        maxSaveSlots = Mathf.Clamp(maxSaveSlots, 1, 20);
        autoSaveInterval = Mathf.Max(autoSaveInterval, 10f);
        maxBackups = Mathf.Clamp(maxBackups, 1, 10);
    }
    
    private void SubscribeToEvents()
    {
        // Auto save em eventos específicos
        if (autoSaveOnLevelUp)
        {
            EventManager.OnPlayerLevelUp += HandlePlayerLevelUp;
        }
        
        if (autoSaveOnQuestComplete)
        {
            EventManager.OnQuestCompleted += HandleQuestCompleted;
        }
        
        if (autoSaveOnSceneChange)
        {
            EventManager.OnSceneLoaded += HandleSceneLoaded;
        }
        
        // Salvar quando o jogo é pausado
        EventManager.OnGamePaused += HandleGamePaused;
    }
    
    private void HandlePlayerLevelUp(int level)
    {
        if (enableAutoSave)
        {
            PerformAutoSave();
        }
    }
    
    private void HandleQuestCompleted(Quest quest)
    {
        if (quest != null && quest.isMainQuest && enableAutoSave)
        {
            PerformAutoSave();
        }
    }
    
    private void HandleSceneLoaded(string sceneName)
    {
        if (enableAutoSave)
        {
            PerformAutoSave();
        }
    }
    
    private void HandleGamePaused()
    {
        if (enableAutoSave)
        {
            StartCoroutine(DelayedAutoSave(2f));
        }
    }
    
    private IEnumerator DelayedAutoSave(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        PerformAutoSave();
    }
    
    private bool ShouldAutoSave()
    {
        // Não salvar se não está jogando
        if (GameManager.Instance == null || !GameManager.Instance.IsGamePlaying) return false;
        
        // Não salvar no editor se desabilitado
        if (Application.isEditor && !saveInEditorMode) return false;
        
        return true;
    }
    
    #region Save Game
    
    /// <summary>
    /// Salva o jogo no slot padrão (0)
    /// </summary>
    public bool SaveGame()
    {
        return SaveGame(0);
    }
    
    /// <summary>
    /// Salva o jogo em um slot específico
    /// </summary>
    public bool SaveGame(int slot)
    {
        if (!IsValidSlot(slot))
        {
            LogError($"Slot de save inválido: {slot}");
            return false;
        }
        
        try
        {
            // Coletar dados de todos os sistemas
            GameSaveData saveData = CollectSaveData();
            
            if (saveData == null)
            {
                LogError("Falha ao coletar dados para salvamento");
                return false;
            }
            
            // Criar backup se habilitado
            if (createBackups && HasSaveFile(slot))
            {
                CreateBackup(slot);
            }
            
            // Capturar screenshot
            CaptureScreenshotForSave(slot);
            
            // Converter para JSON
            string json = JsonUtility.ToJson(saveData, true);
            
            // Aplicar compressão se habilitada
            if (compressData)
            {
                json = CompressData(json);
            }
            
            // Aplicar criptografia se habilitada
            if (useEncryption)
            {
                json = EncryptData(json);
            }
            
            // Escrever arquivo
            string fileName = GetSaveFileName(slot);
            string filePath = Path.Combine(savePath, fileName);
            
            File.WriteAllText(filePath, json, Encoding.UTF8);
            
            // Salvar metadados
            SaveMetadata(slot, saveData);
            
            // Cache dos dados salvos
            lastSaveData = saveData;
            lastSaveTime = Time.time;
            
            // Disparar evento
            OnGameSaved?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log($"Jogo salvo no slot {slot}: {filePath}");
            
            return true;
        }
        catch (Exception e)
        {
            string error = $"Erro ao salvar jogo no slot {slot}: {e.Message}";
            LogError(error);
            OnSaveError?.Invoke(error);
            return false;
        }
    }
    
    /// <summary>
    /// Auto save silencioso
    /// </summary>
    public void PerformAutoSave()
    {
        if (!autoSaveEnabled) return;
        
        bool success = SaveGame(0); // Auto save sempre no slot 0
        
        if (success)
        {
            OnAutoSave?.Invoke(0);
            
            if (enableDebugLogs)
                Debug.Log("Auto save realizado com sucesso");
        }
    }
    
    /// <summary>
    /// Salva rapidamente apenas dados críticos
    /// </summary>
    public bool QuickSave()
    {
        try
        {
            // Coletar apenas dados essenciais
            GameSaveData quickData = CollectQuickSaveData();
            
            string json = JsonUtility.ToJson(quickData, false);
            if (compressData) json = CompressData(json);
            if (useEncryption) json = EncryptData(json);
            
            string quickSavePath = Path.Combine(savePath, "quicksave.json");
            File.WriteAllText(quickSavePath, json, Encoding.UTF8);
            
            if (enableDebugLogs)
                Debug.Log("Quick save realizado");
            
            return true;
        }
        catch (Exception e)
        {
            LogError($"Erro no quick save: {e.Message}");
            return false;
        }
    }
    
    #endregion
    
    #region Load Game
    
    /// <summary>
    /// Carrega o jogo do slot padrão (0)
    /// </summary>
    public bool LoadGame()
    {
        return LoadGame(0);
    }
    
    /// <summary>
    /// Carrega o jogo de um slot específico
    /// </summary>
    public bool LoadGame(int slot)
    {
        if (!IsValidSlot(slot))
        {
            LogError($"Slot de save inválido: {slot}");
            return false;
        }
        
        string fileName = GetSaveFileName(slot);
        string filePath = Path.Combine(savePath, fileName);
        
        if (!File.Exists(filePath))
        {
            string error = $"Arquivo de save não encontrado: {fileName}";
            LogError(error);
            OnLoadError?.Invoke(error);
            return false;
        }
        
        try
        {
            // Ler arquivo
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            
            // Descriptografar se necessário
            if (useEncryption)
            {
                json = DecryptData(json);
            }
            
            // Descomprimir se necessário
            if (compressData)
            {
                json = DecompressData(json);
            }
            
            // Validar JSON
            if (string.IsNullOrEmpty(json))
            {
                throw new Exception("Dados de save corrompidos ou vazios");
            }
            
            // Converter de JSON
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
            
            if (saveData == null)
            {
                throw new Exception("Falha ao deserializar dados de save");
            }
            
            // Validar versão do save
            if (!IsCompatibleSaveVersion(saveData.saveVersion))
            {
                string warning = $"Versão do save incompatível: {saveData.saveVersion}. Tentando carregar mesmo assim...";
                Debug.LogWarning(warning);
            }
            
            // Aplicar dados aos sistemas
            ApplySaveData(saveData);
            
            // Cache dos dados carregados
            lastSaveData = saveData;
            
            // Disparar evento
            OnGameLoaded?.Invoke();
            
            if (enableDebugLogs)
                Debug.Log($"Jogo carregado do slot {slot}: {filePath}");
            
            return true;
        }
        catch (Exception e)
        {
            string error = $"Erro ao carregar jogo do slot {slot}: {e.Message}";
            LogError(error);
            OnLoadError?.Invoke(error);
            
            // Tentar carregar backup se disponível
            return TryLoadBackup(slot);
        }
    }
    
    /// <summary>
    /// Carrega quick save
    /// </summary>
    public bool LoadQuickSave()
    {
        string quickSavePath = Path.Combine(savePath, "quicksave.json");
        
        if (!File.Exists(quickSavePath))
        {
            LogError("Quick save não encontrado");
            return false;
        }
        
        try
        {
            string json = File.ReadAllText(quickSavePath, Encoding.UTF8);
            if (useEncryption) json = DecryptData(json);
            if (compressData) json = DecompressData(json);
            
            GameSaveData quickData = JsonUtility.FromJson<GameSaveData>(json);
            ApplySaveData(quickData);
            
            if (enableDebugLogs)
                Debug.Log("Quick save carregado");
            
            return true;
        }
        catch (Exception e)
        {
            LogError($"Erro ao carregar quick save: {e.Message}");
            return false;
        }
    }
    
    #endregion
    
    #region Data Collection
    
    private GameSaveData CollectSaveData()
    {
        try
        {
            GameSaveData saveData = new GameSaveData();
            
            // Dados básicos
            saveData.saveVersion = Application.version;
            saveData.saveDate = DateTime.Now.ToBinary();
            saveData.playTime = Time.time;
            saveData.unityVersion = Application.unityVersion;
            
            // Coletar dados do player
            GameObject player = GameManager.Instance?.CurrentPlayer;
            if (player != null)
            {
                CollectPlayerData(saveData, player);
            }
            
            // Coletar dados de sistemas
            CollectSystemsData(saveData);
            
            return saveData;
        }
        catch (Exception e)
        {
            LogError($"Erro ao coletar dados de save: {e.Message}");
            return null;
        }
    }
    
    private void CollectPlayerData(GameSaveData saveData, GameObject player)
    {
        // Player Stats
        PlayerStats playerStats = player.GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            saveData.playerData = new PlayerSaveData
            {
                level = playerStats.level,
                experience = playerStats.experience,
                currentHealth = playerStats.currentHealth,
                currentMana = playerStats.currentMana,
                strength = playerStats.strength,
                dexterity = playerStats.dexterity,
                intelligence = playerStats.intelligence,
                vitality = playerStats.vitality,
                availableStatPoints = playerStats.availableStatPoints,
                position = player.transform.position,
                rotation = player.transform.rotation
            };
        }
        
        // Inventário
        PlayerInventory inventory = player.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            saveData.inventoryData = inventory.GetSaveData();
        }
        
        // Equipamentos
        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        if (equipment != null)
        {
            saveData.equipmentData = equipment.GetSaveData();
        }
        
        // Skills
        PlayerSkillManager skillManager = player.GetComponent<PlayerSkillManager>();
        if (skillManager != null)
        {
            saveData.skillData = skillManager.GetSaveData();
        }
    }
    
    private void CollectSystemsData(GameSaveData saveData)
    {
        // Quests
        if (QuestManager.Instance != null)
        {
            saveData.questData = QuestManager.Instance.GetSaveData();
        }
        
        // Estado do mundo
        saveData.worldData = CollectWorldData();
        
        // Configurações do jogo
        saveData.gameSettings = CollectGameSettings();
        
        // Dados de progresso
        saveData.progressData = CollectProgressData();
    }
    
    private GameSaveData CollectQuickSaveData()
    {
        GameSaveData quickData = new GameSaveData();
        
        // Apenas dados essenciais para quick save
        quickData.saveVersion = Application.version;
        quickData.saveDate = DateTime.Now.ToBinary();
        quickData.playTime = Time.time;
        
        GameObject player = GameManager.Instance?.CurrentPlayer;
        if (player != null)
        {
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                quickData.playerData = new PlayerSaveData
                {
                    level = playerStats.level,
                    experience = playerStats.experience,
                    currentHealth = playerStats.currentHealth,
                    currentMana = playerStats.currentMana,
                    position = player.transform.position,
                    rotation = player.transform.rotation
                };
            }
        }
        
        quickData.worldData = new WorldSaveData
        {
            currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        };
        
        return quickData;
    }
    
    private WorldSaveData CollectWorldData()
    {
        WorldSaveData worldData = new WorldSaveData();
        
        // Cena atual
        worldData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        // Tempo de jogo
        worldData.gameTime = Time.time;
        
        // Estados específicos do mundo
        worldData.openedChests = new List<string>();
        worldData.discoveredLocations = new List<string>();
        worldData.defeatedBosses = new List<string>();
        
        // Coletar dados de objetos persistentes na cena
        PersistentObject[] persistentObjects = FindObjectsOfType<PersistentObject>();
        worldData.persistentObjectsData = new List<PersistentObjectData>();
        
        foreach (PersistentObject obj in persistentObjects)
        {
            if (obj != null)
            {
                worldData.persistentObjectsData.Add(obj.GetSaveData());
            }
        }
        
        return worldData;
    }
    
    private GameSettingsData CollectGameSettings()
    {
        GameSettingsData settings = new GameSettingsData();
        
        // Configurações de áudio
        if (AudioManager.Instance != null)
        {
            settings.masterVolume = AudioManager.Instance.masterVolume;
            settings.musicVolume = AudioManager.Instance.musicVolume;
            settings.sfxVolume = AudioManager.Instance.sfxVolume;
            settings.ambientVolume = AudioManager.Instance.ambientVolume;
        }
        
        // Configurações de vídeo
        settings.screenWidth = Screen.width;
        settings.screenHeight = Screen.height;
        settings.fullScreen = Screen.fullScreen;
        settings.vSync = QualitySettings.vSyncCount > 0;
        settings.qualityLevel = QualitySettings.GetQualityLevel();
        
        // Configurações de gameplay
        settings.difficulty = GameManager.Instance?.currentDifficulty ?? 1;
        settings.language = Application.systemLanguage.ToString();
        
        return settings;
    }
    
    private ProgressData CollectProgressData()
    {
        ProgressData progressData = new ProgressData();
        
        // Estatísticas de jogo
        progressData.totalPlayTime = Time.time;
        progressData.enemiesKilled = 0; // Implementar contador global
        progressData.questsCompleted = QuestManager.Instance?.GetQuestStatistics().completedQuests ?? 0;
        progressData.itemsCollected = 0; // Implementar contador global
        progressData.goldEarned = 0; // Implementar contador global
        
        // Conquistas
        progressData.unlockedAchievements = new List<string>();
        
        return progressData;
    }
    
    #endregion
    
    #region Data Application
    
    private void ApplySaveData(GameSaveData saveData)
    {
        if (saveData == null) return;
        
        try
        {
            // Aplicar dados do player
            ApplyPlayerData(saveData);
            
            // Aplicar dados de sistemas
            ApplySystemsData(saveData);
            
            // Aplicar configurações
            if (saveData.gameSettings != null)
            {
                ApplyGameSettings(saveData.gameSettings);
            }
        }
        catch (Exception e)
        {
            LogError($"Erro ao aplicar dados de save: {e.Message}");
            throw;
        }
    }
    
    private void ApplyPlayerData(GameSaveData saveData)
    {
        GameObject player = GameManager.Instance?.CurrentPlayer;
        if (player != null && saveData.playerData != null)
        {
            // Player Stats
            PlayerStats playerStats = player.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                var data = saveData.playerData;
                playerStats.level = data.level;
                playerStats.experience = data.experience;
                playerStats.currentHealth = data.currentHealth;
                playerStats.currentMana = data.currentMana;
                playerStats.strength = data.strength;
                playerStats.dexterity = data.dexterity;
                playerStats.intelligence = data.intelligence;
                playerStats.vitality = data.vitality;
                playerStats.availableStatPoints = data.availableStatPoints;
                
                // Atualizar eventos
                EventManager.TriggerPlayerHealthChanged(data.currentHealth, playerStats.FinalMaxHealth);
                EventManager.TriggerPlayerManaChanged(data.currentMana, playerStats.FinalMaxMana);
            }
            
            // Posição do player
            player.transform.position = saveData.playerData.position;
            player.transform.rotation = saveData.playerData.rotation;
            
            // Inventário
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null && saveData.inventoryData != null)
            {
                inventory.LoadSaveData(saveData.inventoryData);
            }
            
            // Equipamentos
            PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
            if (equipment != null && saveData.equipmentData != null)
            {
                equipment.LoadSaveData(saveData.equipmentData);
            }
            
            // Skills
            PlayerSkillManager skillManager = player.GetComponent<PlayerSkillManager>();
            if (skillManager != null && saveData.skillData != null)
            {
                skillManager.LoadSaveData(saveData.skillData);
            }
        }
    }
    
    private void ApplySystemsData(GameSaveData saveData)
    {
        // Aplicar dados de quests
        if (QuestManager.Instance != null && saveData.questData != null)
        {
            QuestManager.Instance.LoadSaveData(saveData.questData);
        }
        
        // Aplicar dados do mundo
        if (saveData.worldData != null)
        {
            ApplyWorldData(saveData.worldData);
        }
    }
    
    private void ApplyWorldData(WorldSaveData worldData)
    {
        // Aplicar dados de objetos persistentes
        if (worldData.persistentObjectsData != null)
        {
            PersistentObject[] persistentObjects = FindObjectsOfType<PersistentObject>();
            
            foreach (PersistentObject obj in persistentObjects)
            {
                if (obj != null)
                {
                    var data = worldData.persistentObjectsData.Find(d => d.objectId == obj.objectId);
                    if (data != null)
                    {
                        obj.LoadSaveData(data);
                    }
                }
            }
        }
        
        // Carregar cena se necessário
        if (!string.IsNullOrEmpty(worldData.currentScene))
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (currentScene != worldData.currentScene && SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(worldData.currentScene);
            }
        }
    }
    
    private void ApplyGameSettings(GameSettingsData settings)
    {
        // Configurações de áudio
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(settings.masterVolume);
            AudioManager.Instance.SetMusicVolume(settings.musicVolume);
            AudioManager.Instance.SetSFXVolume(settings.sfxVolume);
            AudioManager.Instance.SetAmbientVolume(settings.ambientVolume);
        }
        
        // Configurações de vídeo
        if (settings.screenWidth > 0 && settings.screenHeight > 0)
        {
            Screen.SetResolution(settings.screenWidth, settings.screenHeight, settings.fullScreen);
        }
        
        QualitySettings.vSyncCount = settings.vSync ? 1 : 0;
        
        if (settings.qualityLevel >= 0 && settings.qualityLevel < QualitySettings.names.Length)
        {
            QualitySettings.SetQualityLevel(settings.qualityLevel);
        }
    }
    
    #endregion
    
    #region Backup System
    
    private void CreateBackup(int slot)
    {
        if (!createBackups) return;
        
        try
        {
            string sourceFile = Path.Combine(savePath, GetSaveFileName(slot));
            if (!File.Exists(sourceFile)) return;
            
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"backup_slot{slot}_{timestamp}.json";
            string backupFilePath = Path.Combine(backupPath, backupFileName);
            
            File.Copy(sourceFile, backupFilePath);
            
            // Limpar backups antigos
            CleanOldBackups(slot);
            
            if (enableDebugLogs)
                Debug.Log($"Backup criado: {backupFileName}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Falha ao criar backup: {e.Message}");
        }
    }
    
    private void CleanOldBackups(int slot)
    {
        try
        {
            if (!Directory.Exists(backupPath)) return;
            
            string[] backupFiles = Directory.GetFiles(backupPath, $"backup_slot{slot}_*.json");
            
            if (backupFiles.Length > maxBackups)
            {
                // Ordenar por data de criação
                Array.Sort(backupFiles, (x, y) => File.GetCreationTime(x).CompareTo(File.GetCreationTime(y)));
                
                // Deletar backups mais antigos
                int filesToDelete = backupFiles.Length - maxBackups;
                for (int i = 0; i < filesToDelete; i++)
                {
                    if (File.Exists(backupFiles[i]))
                    {
                        File.Delete(backupFiles[i]);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao limpar backups antigos: {e.Message}");
        }
    }
    
    private bool TryLoadBackup(int slot)
    {
        if (!createBackups || !Directory.Exists(backupPath)) return false;
        
        try
        {
            string[] backupFiles = Directory.GetFiles(backupPath, $"backup_slot{slot}_*.json");
            
            if (backupFiles.Length == 0) return false;
            
            // Pegar o backup mais recente
            Array.Sort(backupFiles, (x, y) => File.GetCreationTime(y).CompareTo(File.GetCreationTime(x)));
            string latestBackup = backupFiles[0];
            
            // Tentar carregar backup
            string json = File.ReadAllText(latestBackup, Encoding.UTF8);
            if (useEncryption) json = DecryptData(json);
            if (compressData) json = DecompressData(json);
            
            GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
            ApplySaveData(saveData);
            
            Debug.LogWarning($"Carregado do backup: {Path.GetFileName(latestBackup)}");
            return true;
        }
        catch (Exception e)
        {
            LogError($"Erro ao carregar backup: {e.Message}");
            return false;
        }
    }
    
    #endregion
    
    #region Save Metadata
    
    private void SaveMetadata(int slot, GameSaveData saveData)
    {
        try
        {
            SaveMetadata metadata = new SaveMetadata
            {
                slot = slot,
                saveDate = DateTime.Now,
                playerLevel = saveData?.playerData?.level ?? 1,
                playTime = saveData?.playTime ?? 0f,
                currentScene = saveData?.worldData?.currentScene ?? "Unknown",
                saveVersion = saveData?.saveVersion ?? Application.version,
                fileSize = GetSaveFileSize(slot),
                isValid = true,
                screenshotPath = GetScreenshotPath(slot)
            };
            
            string metadataJson = JsonUtility.ToJson(metadata, true);
            string metadataPath = Path.Combine(savePath, $"metadata_{slot}.json");
            
            File.WriteAllText(metadataPath, metadataJson, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Não foi possível salvar metadados: {e.Message}");
        }
    }
    
    private long GetSaveFileSize(int slot)
    {
        try
        {
            string filePath = Path.Combine(savePath, GetSaveFileName(slot));
            if (File.Exists(filePath))
            {
                return new FileInfo(filePath).Length;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao obter tamanho do arquivo: {e.Message}");
        }
        
        return 0;
    }
    
    #endregion
    
    #region Utility Methods
    
    private string GetSaveFileName(int slot)
    {
        return $"save_slot_{slot}.json";
    }
    
    private bool IsValidSlot(int slot)
    {
        return slot >= 0 && slot < maxSaveSlots;
    }
    
    /// <summary>
    /// Verifica se existe um save em um slot
    /// </summary>
    public bool HasSaveFile(int slot)
    {
        if (!IsValidSlot(slot)) return false;
        
        string fileName = GetSaveFileName(slot);
        string filePath = Path.Combine(savePath, fileName);
        
        return File.Exists(filePath);
    }
    
    /// <summary>
    /// Obtém metadados de um save
    /// </summary>
    public SaveMetadata GetSaveMetadata(int slot)
    {
        if (!IsValidSlot(slot)) return null;
        
        string metadataPath = Path.Combine(savePath, $"metadata_{slot}.json");
        
        if (!File.Exists(metadataPath)) 
        {
            // Tentar criar metadados básicos se o save existir
            if (HasSaveFile(slot))
            {
                return CreateBasicMetadata(slot);
            }
            return null;
        }
        
        try
        {
            string json = File.ReadAllText(metadataPath, Encoding.UTF8);
            return JsonUtility.FromJson<SaveMetadata>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao ler metadados do slot {slot}: {e.Message}");
            return CreateBasicMetadata(slot);
        }
    }
    
    private SaveMetadata CreateBasicMetadata(int slot)
    {
        try
        {
            string filePath = Path.Combine(savePath, GetSaveFileName(slot));
            if (!File.Exists(filePath)) return null;
            
            FileInfo fileInfo = new FileInfo(filePath);
            
            return new SaveMetadata
            {
                slot = slot,
                saveDate = fileInfo.LastWriteTime,
                playerLevel = 0,
                playTime = 0f,
                currentScene = "Unknown",
                saveVersion = "Unknown",
                fileSize = fileInfo.Length,
                isValid = false
            };
        }
        catch
        {
            return null;
        }
    }
    
    /// <summary>
    /// Obtém informações de todos os saves
    /// </summary>
    public SaveSlotInfo[] GetAllSaveSlots()
    {
        SaveSlotInfo[] slots = new SaveSlotInfo[maxSaveSlots];
        
        for (int i = 0; i < maxSaveSlots; i++)
        {
            slots[i] = new SaveSlotInfo
            {
                slotIndex = i,
                hasData = HasSaveFile(i),
                metadata = GetSaveMetadata(i),
                isCorrupted = HasSaveFile(i) && !ValidateSaveFile(i)
            };
        }
        
        return slots;
    }
    
    /// <summary>
    /// Deleta um save
    /// </summary>
    public bool DeleteSave(int slot)
    {
        if (!IsValidSlot(slot)) return false;
        
        try
        {
            string saveFile = Path.Combine(savePath, GetSaveFileName(slot));
            string metadataFile = Path.Combine(savePath, $"metadata_{slot}.json");
            string screenshotFile = Path.Combine(savePath, $"save_screenshot_{slot}.png");
            
            if (File.Exists(saveFile))
                File.Delete(saveFile);
            
            if (File.Exists(metadataFile))
                File.Delete(metadataFile);
                
            if (File.Exists(screenshotFile))
                File.Delete(screenshotFile);
            
            // Deletar backups relacionados
            if (createBackups && Directory.Exists(backupPath))
            {
                string[] backupFiles = Directory.GetFiles(backupPath, $"backup_slot{slot}_*.json");
                foreach (string backupFile in backupFiles)
                {
                    if (File.Exists(backupFile))
                        File.Delete(backupFile);
                }
            }
            
            if (enableDebugLogs)
                Debug.Log($"Save deletado do slot {slot}");
            
            return true;
        }
        catch (Exception e)
        {
            LogError($"Erro ao deletar save do slot {slot}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Verifica se uma versão de save é compatível
    /// </summary>
    private bool IsCompatibleSaveVersion(string saveVersion)
    {
        // Implementar lógica de compatibilidade baseada na versão
        // Por exemplo, aceitar saves da mesma versão maior
        return !string.IsNullOrEmpty(saveVersion);
    }
    
    #endregion
    
    #region Encryption/Compression
    
    private string EncryptData(string data)
    {
        try
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] keyBytes = Encoding.UTF8.GetBytes(ENCRYPTION_KEY);
            
            using (Aes aes = Aes.Create())
            {
                aes.Key = ResizeKey(keyBytes, 32); // AES-256
                aes.GenerateIV();
                
                using (var encryptor = aes.CreateEncryptor())
                using (var msEncrypt = new MemoryStream())
                {
                    // Escrever IV no início
                    msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                    
                    using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        csEncrypt.Write(dataBytes, 0, dataBytes.Length);
                        csEncrypt.FlushFinalBlock();
                    }
                    
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }
        catch (Exception e)
        {
            LogError($"Erro na criptografia: {e.Message}");
            return data; // Retornar dados não criptografados em caso de erro
        }
    }
    
    private string DecryptData(string encryptedData)
    {
        try
        {
            byte[] dataBytes = Convert.FromBase64String(encryptedData);
            byte[] keyBytes = Encoding.UTF8.GetBytes(ENCRYPTION_KEY);
            
            using (Aes aes = Aes.Create())
            {
                aes.Key = ResizeKey(keyBytes, 32); // AES-256
                
                // Extrair IV do início dos dados
                byte[] iv = new byte[16];
                Array.Copy(dataBytes, 0, iv, 0, 16);
                aes.IV = iv;
                
                using (var decryptor = aes.CreateDecryptor())
                using (var msDecrypt = new MemoryStream(dataBytes, 16, dataBytes.Length - 16))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                using (var srDecrypt = new StreamReader(csDecrypt))
                {
                    return srDecrypt.ReadToEnd();
                }
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Dados corrompidos ou chave de criptografia inválida: {e.Message}");
        }
    }
    
    private byte[] ResizeKey(byte[] key, int size)
    {
        byte[] resizedKey = new byte[size];
        for (int i = 0; i < size; i++)
        {
            resizedKey[i] = key[i % key.Length];
        }
        return resizedKey;
    }
    
    private string CompressData(string data)
    {
        try
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(dataBytes, 0, dataBytes.Length);
                }
                return Convert.ToBase64String(output.ToArray());
            }
        }
        catch (Exception e)
        {
            LogError($"Erro na compressão: {e.Message}");
            return data; // Retornar dados não comprimidos em caso de erro
        }
    }
    
    private string DecompressData(string compressedData)
    {
        try
        {
            byte[] compressedBytes = Convert.FromBase64String(compressedData);
            
            using (var input = new MemoryStream(compressedBytes))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
        }
        catch (Exception e)
        {
            throw new Exception($"Erro na descompressão: {e.Message}");
        }
    }
    
    #endregion
    
    #region Save Settings
    
    /// <summary>
    /// Salva configurações do SaveManager
    /// </summary>
    private void SaveSaveSettings()
    {
        try
        {
            SaveManagerSettings settings = new SaveManagerSettings
            {
                enableAutoSave = this.enableAutoSave,
                autoSaveInterval = this.autoSaveInterval,
                useEncryption = this.useEncryption,
                createBackups = this.createBackups,
                maxBackups = this.maxBackups
            };
            
            string json = JsonUtility.ToJson(settings, true);
            string settingsPath = Path.Combine(savePath, "savemanager_settings.json");
            
            File.WriteAllText(settingsPath, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao salvar configurações do SaveManager: {e.Message}");
        }
    }
    
    /// <summary>
    /// Carrega configurações do SaveManager
    /// </summary>
    private void LoadSaveSettings()
    {
        try
        {
            string settingsPath = Path.Combine(savePath, "savemanager_settings.json");
            
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath, Encoding.UTF8);
                SaveManagerSettings settings = JsonUtility.FromJson<SaveManagerSettings>(json);
                
                enableAutoSave = settings.enableAutoSave;
                autoSaveInterval = settings.autoSaveInterval;
                useEncryption = settings.useEncryption;
                createBackups = settings.createBackups;
                maxBackups = settings.maxBackups;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao carregar configurações do SaveManager: {e.Message}");
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Habilita/desabilita auto save
    /// </summary>
    public void SetAutoSaveEnabled(bool enabled)
    {
        autoSaveEnabled = enabled;
        
        if (enableDebugLogs)
            Debug.Log($"Auto save {(enabled ? "habilitado" : "desabilitado")}");
    }
    
    /// <summary>
    /// Define intervalo de auto save
    /// </summary>
    public void SetAutoSaveInterval(float interval)
    {
        autoSaveInterval = Mathf.Max(interval, 10f);
        timeSinceLastSave = 0f; // Reset timer
    }
    
    /// <summary>
    /// Verifica se há dados não salvos
    /// </summary>
    public bool HasUnsavedChanges()
    {
        if (lastSaveData == null) return true;
        
        // Comparar com dados atuais (implementação simplificada)
        return Time.time - lastSaveTime > autoSaveInterval;
    }
    
    /// <summary>
    /// Exporta save para arquivo externo
    /// </summary>
    public bool ExportSave(int slot, string exportPath)
    {
        if (!HasSaveFile(slot)) return false;
        
        try
        {
            string sourceFile = Path.Combine(savePath, GetSaveFileName(slot));
            File.Copy(sourceFile, exportPath, true);
            
            if (enableDebugLogs)
                Debug.Log($"Save exportado para: {exportPath}");
            
            return true;
        }
        catch (Exception e)
        {
            LogError($"Erro ao exportar save: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Importa save de arquivo externo
    /// </summary>
    public bool ImportSave(string importPath, int slot)
    {
        if (!File.Exists(importPath) || !IsValidSlot(slot)) return false;
        
        try
        {
            string targetFile = Path.Combine(savePath, GetSaveFileName(slot));
            File.Copy(importPath, targetFile, true);
            
            if (enableDebugLogs)
                Debug.Log($"Save importado do arquivo: {importPath}");
            
            return true;
        }
        catch (Exception e)
        {
            LogError($"Erro ao importar save: {e.Message}");
            return false;
        }
    }
    
    #endregion
    
    #region Save Validation
    
    /// <summary>
    /// Valida a integridade de um arquivo de save
    /// </summary>
    public bool ValidateSaveFile(int slot)
    {
        if (!HasSaveFile(slot)) return false;
        
        try
        {
            string fileName = GetSaveFileName(slot);
            string filePath = Path.Combine(savePath, fileName);
            
            // Verificar se o arquivo não está corrompido
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            
            if (useEncryption)
            {
                json = DecryptData(json);
            }
            
            if (compressData)
            {
                json = DecompressData(json);
            }
            
            // Tentar deserializar para verificar integridade
            GameSaveData testData = JsonUtility.FromJson<GameSaveData>(json);
            
            return testData != null && !string.IsNullOrEmpty(testData.saveVersion);
        }
        catch (Exception e)
        {
            LogError($"Save corrompido no slot {slot}: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Repara um save corrompido usando backup
    /// </summary>
    public bool RepairCorruptedSave(int slot)
    {
        if (!createBackups) return false;
        
        try
        {
            if (!Directory.Exists(backupPath)) return false;
            
            string[] backupFiles = Directory.GetFiles(backupPath, $"backup_slot{slot}_*.json");
            
            if (backupFiles.Length == 0) return false;
            
            // Ordenar por data de criação (mais recente primeiro)
            Array.Sort(backupFiles, (x, y) => File.GetCreationTime(y).CompareTo(File.GetCreationTime(x)));
            
            // Tentar cada backup até encontrar um válido
            foreach (string backupFile in backupFiles)
            {
                try
                {
                    string targetFile = Path.Combine(savePath, GetSaveFileName(slot));
                    File.Copy(backupFile, targetFile, true);
                    
                    // Verificar se o restore funcionou
                    if (ValidateSaveFile(slot))
                    {
                        Debug.Log($"Save reparado usando backup: {Path.GetFileName(backupFile)}");
                        return true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Falha ao usar backup {backupFile}: {e.Message}");
                }
            }
            
            return false;
        }
        catch (Exception e)
        {
            LogError($"Erro ao reparar save: {e.Message}");
            return false;
        }
    }
    
    #endregion
    
    #region Screenshots
    
    /// <summary>
    /// Captura screenshot para o save
    /// </summary>
    public void CaptureScreenshotForSave(int slot)
    {
        if (!IsValidSlot(slot)) return;
        
        StartCoroutine(CaptureScreenshotCoroutine(slot));
    }
    
    private IEnumerator CaptureScreenshotCoroutine(int slot)
    {
        // Aguardar o final do frame
        yield return new WaitForEndOfFrame();
        
        try
        {
            string screenshotName = $"save_screenshot_{slot}.png";
            string screenshotPath = Path.Combine(savePath, screenshotName);
            
            // Capturar screenshot usando ScreenCapture
            ScreenCapture.CaptureScreenshot(screenshotPath);
            
            if (enableDebugLogs)
                Debug.Log($"Screenshot capturado para slot {slot}: {screenshotName}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Erro ao capturar screenshot: {e.Message}");
        }
    }
    
    /// <summary>
    /// Obtém caminho do screenshot de um save
    /// </summary>
    public string GetScreenshotPath(int slot)
    {
        if (!IsValidSlot(slot)) return null;
        
        string screenshotName = $"save_screenshot_{slot}.png";
        string screenshotPath = Path.Combine(savePath, screenshotName);
        
        return File.Exists(screenshotPath) ? screenshotPath : null;
    }
    
    #endregion
    
    #region Cleanup and Maintenance
    
    /// <summary>
    /// Limpa arquivos de save temporários e corrompidos
    /// </summary>
    public void CleanupSaveFiles()
    {
        try
        {
            if (!Directory.Exists(savePath)) return;
            
            // Limpar arquivos temporários
            string[] tempFiles = Directory.GetFiles(savePath, "*.tmp");
            foreach (string file in tempFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            
            // Remover screenshots órfãos
            for (int i = 0; i < maxSaveSlots; i++)
            {
                string screenshotPath = Path.Combine(savePath, $"save_screenshot_{i}.png");
                if (File.Exists(screenshotPath) && !HasSaveFile(i))
                {
                    File.Delete(screenshotPath);
                }
            }
            
            // Limpar backups muito antigos (mais de 30 dias)
            if (createBackups && Directory.Exists(backupPath))
            {
                string[] backupFiles = Directory.GetFiles(backupPath, "backup_*.json");
                DateTime cutoffDate = DateTime.Now.AddDays(-30);
                
                foreach (string file in backupFiles)
                {
                    if (File.GetCreationTime(file) < cutoffDate)
                    {
                        File.Delete(file);
                    }
                }
            }
            
            if (enableDebugLogs)
                Debug.Log("Limpeza de arquivos de save concluída");
        }
        catch (Exception e)
        {
            LogError($"Erro na limpeza de arquivos: {e.Message}");
        }
    }
    
    #endregion
    
    #region Debug Methods
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugPrintSavePath()
    {
        Debug.Log($"Save Path: {savePath}");
        Debug.Log($"Backup Path: {backupPath}");
        Debug.Log($"Directory Exists: {Directory.Exists(savePath)}");
        
        if (Directory.Exists(savePath))
        {
            string[] files = Directory.GetFiles(savePath);
            Debug.Log($"Files in save directory: {files.Length}");
            foreach (string file in files)
            {
                Debug.Log($"- {Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)");
            }
        }
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugDeleteAllSaves()
    {
        for (int i = 0; i < maxSaveSlots; i++)
        {
            DeleteSave(i);
        }
        
        // Deletar quick save também
        string quickSavePath = Path.Combine(savePath, "quicksave.json");
        if (File.Exists(quickSavePath))
            File.Delete(quickSavePath);
            
        Debug.Log("Todos os saves foram deletados (DEBUG)");
    }
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugValidateAllSaves()
    {
        Debug.Log("=== VALIDAÇÃO DE SAVES ===");
        
        for (int i = 0; i < maxSaveSlots; i++)
        {
            if (HasSaveFile(i))
            {
                try
                {
                    bool isValid = ValidateSaveFile(i);
                    SaveMetadata metadata = GetSaveMetadata(i);
                    Debug.Log($"Slot {i}: {(isValid ? "VÁLIDO" : "INVÁLIDO")} - {metadata?.saveDate}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Slot {i}: ERRO - {e.Message}");
                }
            }
            else
            {
                Debug.Log($"Slot {i}: VAZIO");
            }
        }
        
        Debug.Log("========================");
    }
    
    #endregion
    
    #region Error Handling
    
    private void LogError(string message)
    {
        if (enableDebugLogs)
        {
            Debug.LogError($"[SaveManager] {message}");
        }
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Salvar automaticamente ao sair do jogo se habilitado
        if (enableAutoSave && GameManager.Instance != null && GameManager.Instance.IsGamePlaying)
        {
            SaveGame();
        }
        
        // Salvar configurações do SaveManager
        SaveSaveSettings();
        
        // Executar limpeza básica
        CleanupSaveFiles();
        
        // Desinscrever dos eventos
        if (autoSaveOnLevelUp)
        {
            EventManager.OnPlayerLevelUp -= HandlePlayerLevelUp;
        }
        
        if (autoSaveOnQuestComplete)
        {
            EventManager.OnQuestCompleted -= HandleQuestCompleted;
        }
        
        if (autoSaveOnSceneChange)
        {
            EventManager.OnSceneLoaded -= HandleSceneLoaded;
        }
        
        EventManager.OnGamePaused -= HandleGamePaused;
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        // Salvar quando o jogo é pausado (importante para mobile)
        if (pauseStatus && enableAutoSave)
        {
            SaveGame();
        }
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        // Salvar quando perde foco
        if (!hasFocus && enableAutoSave)
        {
            SaveGame();
        }
    }
}

/// <summary>
/// Dados principais de save do jogo - VERSÃO COMPLETA
/// </summary>
[System.Serializable]
public class GameSaveData
{
    [Header("Save Info")]
    public string saveVersion;
    public long saveDate;
    public float playTime;
    public string unityVersion;
    
    [Header("Player Data")]
    public PlayerSaveData playerData;
    public InventoryData inventoryData;
    public EquipmentData equipmentData;
    public SkillManagerData skillData;
    
    [Header("Game Systems")]
    public QuestManagerData questData;
    public WorldSaveData worldData;
    public GameSettingsData gameSettings;
    public ProgressData progressData;
}

/// <summary>
/// Dados do player para save - VERSÃO COMPLETA
/// </summary>
[System.Serializable]
public class PlayerSaveData
{
    [Header("Level & Experience")]
    public int level;
    public int experience;
    public int availableStatPoints;
    
    [Header("Health & Mana")]
    public float currentHealth;
    public float currentMana;
    
    [Header("Attributes")]
    public int strength;
    public int dexterity;
    public int intelligence;
    public int vitality;
    
    [Header("Position")]
    public Vector3 position;
    public Quaternion rotation;
    
    [Header("Optional Data")]
    public float lastRegenTime;
    public List<string> appliedBuffs = new List<string>();
}

/// <summary>
/// Dados do mundo para save - VERSÃO COMPLETA
/// </summary>
[System.Serializable]
public class WorldSaveData
{
    [Header("Scene Info")]
    public string currentScene;
    public float gameTime;
    
    [Header("World State")]
    public List<string> openedChests = new List<string>();
    public List<string> discoveredLocations = new List<string>();
    public List<string> defeatedBosses = new List<string>();
    public List<string> unlockedAreas = new List<string>();
    
    [Header("Persistent Objects")]
    public List<PersistentObjectData> persistentObjectsData = new List<PersistentObjectData>();
    
    [Header("Weather & Time")]
    public int currentWeather = 0;
    public float timeOfDay = 12f; // 0-24 horas
    public int currentSeason = 0; // 0=Spring, 1=Summer, 2=Fall, 3=Winter
}

/// <summary>
/// Dados de objetos persistentes
/// </summary>
[System.Serializable]
public class PersistentObjectData
{
    public string objectId;
    public Vector3 position;
    public Quaternion rotation;
    public bool isActive;
    public List<CustomDataEntry> customData = new List<CustomDataEntry>();
}

/// <summary>
/// Entrada de dados customizados para objetos persistentes
/// </summary>
[System.Serializable]
public class CustomDataEntry
{
    public string key;
    public string value;
    public string dataType; // "int", "float", "bool", "string"
    
    public CustomDataEntry(string key, object value)
    {
        this.key = key;
        this.value = value.ToString();
        this.dataType = value.GetType().Name.ToLower();
    }
    
    public T GetValue<T>()
    {
        try
        {
            return (T)System.Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default(T);
        }
    }
}

/// <summary>
/// Configurações do jogo para save - VERSÃO COMPLETA
/// </summary>
[System.Serializable]
public class GameSettingsData
{
    [Header("Audio Settings")]
    public float masterVolume = 1f;
    public float musicVolume = 0.7f;
    public float sfxVolume = 1f;
    public float voiceVolume = 1f;
    public float ambientVolume = 0.5f;
    
    [Header("Video Settings")]
    public int screenWidth = 1920;
    public int screenHeight = 1080;
    public bool fullScreen = true;
    public bool vSync = true;
    public int qualityLevel = 2;
    public float brightness = 1f;
    
    [Header("Gameplay Settings")]
    public int difficulty = 1; // 0=Easy, 1=Normal, 2=Hard, 3=Expert
    public string language = "English";
    public bool showDamageNumbers = true;
    public bool showHealthBars = true;
    public bool autoPause = true;
    
    [Header("Control Settings")]
    public float mouseSensitivity = 1f;
    public bool invertMouseY = false;
    public List<KeyBindingData> keyBindings = new List<KeyBindingData>();
}

/// <summary>
/// Dados de key binding
/// </summary>
[System.Serializable]
public class KeyBindingData
{
    public string actionName;
    public KeyCode keyCode;
    
    public KeyBindingData(string action, KeyCode key)
    {
        actionName = action;
        keyCode = key;
    }
}

/// <summary>
/// Dados de progresso/estatísticas
/// </summary>
[System.Serializable]
public class ProgressData
{
    [Header("Time Statistics")]
    public float totalPlayTime;
    public float sessionPlayTime;
    
    [Header("Combat Statistics")]
    public int enemiesKilled;
    public int bossesDefeated;
    public float totalDamageDealt;
    public float totalDamageTaken;
    public int criticalHits;
    
    [Header("Exploration Statistics")]
    public int areasDiscovered;
    public int chestsOpened;
    public float distanceTraveled;
    
    [Header("Character Statistics")]
    public int questsCompleted;
    public int itemsCollected;
    public int goldEarned;
    public int goldSpent;
    public int skillsLearned;
    public int levelsGained;
    
    [Header("Achievements")]
    public List<string> unlockedAchievements = new List<string>();
    public List<AchievementProgressData> achievementProgress = new List<AchievementProgressData>();
}

/// <summary>
/// Dados de progresso de conquista
/// </summary>
[System.Serializable]
public class AchievementProgressData
{
    public string achievementId;
    public int currentProgress;
    public int targetProgress;
    public bool isUnlocked;
    
    public AchievementProgressData(string id, int current, int target)
    {
        achievementId = id;
        currentProgress = current;
        targetProgress = target;
        isUnlocked = current >= target;
    }
}

/// <summary>
/// Metadados de um save - VERSÃO COMPLETA
/// </summary>
[System.Serializable]
public class SaveMetadata
{
    [Header("Save Info")]
    public int slot;
    public DateTime saveDate;
    public string saveVersion;
    public long fileSize;
    public bool isValid;
    
    [Header("Player Info")]
    public int playerLevel;
    public float playTime;
    public string currentScene;
    public string playerName;
    
    [Header("Progress Info")]
    public int questsCompleted;
    public int enemiesKilled;
    public float completionPercentage;
    
    [Header("Screenshot")]
    public string screenshotPath; // Caminho para screenshot do save
}

/// <summary>
/// Informações de um slot de save - VERSÃO COMPLETA
/// </summary>
[System.Serializable]
public class SaveSlotInfo
{
    public int slotIndex;
    public bool hasData;
    public SaveMetadata metadata;
    public bool isCorrupted;
    public string displayName;
    
    public string GetDisplayText()
    {
        if (!hasData) return "Slot Vazio";
        
        if (isCorrupted) return "Dados Corrompidos";
        
        if (metadata != null)
        {
            string timeText = FormatPlayTime(metadata.playTime);
            return $"Nível {metadata.playerLevel} - {metadata.currentScene}\n" +
                   $"{metadata.saveDate:dd/MM/yyyy HH:mm} - {timeText}";
        }
        
        return "Dados Inválidos";
    }
    
    public string GetDetailedInfo()
    {
        if (!hasData || metadata == null) return "Sem dados";
        
        string timeText = FormatPlayTime(metadata.playTime);
        string sizeText = FormatFileSize(metadata.fileSize);
        
        return $"Nível: {metadata.playerLevel}\n" +
               $"Tempo de Jogo: {timeText}\n" +
               $"Localização: {metadata.currentScene}\n" +
               $"Quests: {metadata.questsCompleted}\n" +
               $"Salvo em: {metadata.saveDate:dd/MM/yyyy HH:mm}\n" +
               $"Tamanho: {sizeText}";
    }
    
    private string FormatPlayTime(float seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(seconds);
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}h {time.Minutes}m";
        }
        else
        {
            return $"{time.Minutes}m {time.Seconds}s";
        }
    }
    
    private string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
        return $"{bytes / (1024 * 1024)} MB";
    }
}

/// <summary>
/// Configurações do SaveManager
/// </summary>
[System.Serializable]
public class SaveManagerSettings
{
    public bool enableAutoSave = true;
    public float autoSaveInterval = 300f;
    public bool useEncryption = true;
    public bool createBackups = true;
    public int maxBackups = 3;
}

/// <summary>
/// Classe base para objetos persistentes no mundo
/// </summary>
public class PersistentObject : MonoBehaviour
{
    [Header("Persistent Object")]
    public string objectId;
    
    [Header("Save Data")]
    public bool savePosition = true;
    public bool saveRotation = true;
    public bool saveActiveState = true;
    
    // Dados customizados para subclasses
    protected Dictionary<string, object> customSaveData = new Dictionary<string, object>();
    
    private void Awake()
    {
        // Gerar ID único se não estiver definido
        if (string.IsNullOrEmpty(objectId))
        {
            objectId = $"{gameObject.name}_{transform.position.x:F0}_{transform.position.z:F0}";
        }
    }
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public virtual PersistentObjectData GetSaveData()
    {
        PersistentObjectData data = new PersistentObjectData();
        data.objectId = objectId;
        
        if (savePosition) data.position = transform.position;
        if (saveRotation) data.rotation = transform.rotation;
        if (saveActiveState) data.isActive = gameObject.activeInHierarchy;
        
        // Adicionar dados customizados
        data.customData = new List<CustomDataEntry>();
        foreach (var kvp in customSaveData)
        {
            data.customData.Add(new CustomDataEntry(kvp.Key, kvp.Value));
        }
        
        // Permitir que classes filhas adicionem dados customizados
        AddCustomSaveData(data);
        
        return data;
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public virtual void LoadSaveData(PersistentObjectData data)
    {
        if (data == null) return;
        
        if (savePosition) transform.position = data.position;
        if (saveRotation) transform.rotation = data.rotation;
        if (saveActiveState) gameObject.SetActive(data.isActive);
        
        // Carregar dados customizados
        customSaveData.Clear();
        foreach (var entry in data.customData)
        {
            switch (entry.dataType)
            {
                case "int32":
                    customSaveData[entry.key] = entry.GetValue<int>();
                    break;
                case "single":
                    customSaveData[entry.key] = entry.GetValue<float>();
                    break;
                case "boolean":
                    customSaveData[entry.key] = entry.GetValue<bool>();
                    break;
                default:
                    customSaveData[entry.key] = entry.value;
                    break;
            }
        }
        
        // Permitir que classes filhas carreguem dados customizados
        LoadCustomSaveData(data);
    }
    
    /// <summary>
    /// Override para adicionar dados customizados
    /// </summary>
    protected virtual void AddCustomSaveData(PersistentObjectData data)
    {
        // Implementar em classes filhas se necessário
    }
    
    /// <summary>
    /// Override para carregar dados customizados
    /// </summary>
    protected virtual void LoadCustomSaveData(PersistentObjectData data)
    {
        // Implementar em classes filhas se necessário
    }
    
    /// <summary>
    /// Adiciona dados customizados para salvamento
    /// </summary>
    protected void AddCustomData(string key, object value)
    {
        customSaveData[key] = value;
    }
    
    /// <summary>
    /// Obtém dados customizados
    /// </summary>
    protected T GetCustomData<T>(string key, T defaultValue = default(T))
    {
        if (customSaveData.ContainsKey(key))
        {
            try
            {
                return (T)customSaveData[key];
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }
    
    /// <summary>
    /// Verifica se tem dados customizados
    /// </summary>
    protected bool HasCustomData(string key)
    {
        return customSaveData.ContainsKey(key);
    }
}