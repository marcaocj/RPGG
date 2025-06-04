using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Centraliza a ativação/desativação de painéis de UI
/// </summary>
public class UIManager : Singleton<UIManager>
{
    [Header("Main UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject gameplayUIPanel;
    public GameObject pauseMenuPanel;
    public GameObject inventoryPanel;
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public GameObject settingsPanel;
    public GameObject questPanel;
    
    [Header("HUD Elements")]
    public PlayerHealthBarUI healthBarUI;
    public GameObject miniMapPanel;
    public GameObject skillBarPanel;
    public GameObject chatPanel;
    
    [Header("Interactive UI")]
    public GameObject tooltipPanel;
    public Text tooltipText;
    public GameObject lootWindowPanel;
    public GameObject dialoguePanel;
    
    [Header("Loading")]
    public GameObject loadingScreen;
    public Slider loadingProgressBar;
    public Text loadingText;
    
    // Estados de UI
    private Dictionary<string, GameObject> uiPanels = new Dictionary<string, GameObject>();
    private List<GameObject> activeOverlays = new List<GameObject>();
    private bool isInventoryOpen = false;
    private bool isPauseMenuOpen = false;
    
    protected override void Awake()
    {
        base.Awake();
        
        // Registrar painéis no dicionário
        RegisterUIPanels();
        
        // Inscrever nos eventos
        SubscribeToEvents();
        
        // Configurar estado inicial
        InitializeUI();
    }
    
    private void RegisterUIPanels()
    {
        if (mainMenuPanel != null)
            uiPanels["MainMenu"] = mainMenuPanel;
        if (gameplayUIPanel != null)
            uiPanels["GameplayUI"] = gameplayUIPanel;
        if (pauseMenuPanel != null)
            uiPanels["PauseMenu"] = pauseMenuPanel;
        if (inventoryPanel != null)
            uiPanels["Inventory"] = inventoryPanel;
        if (gameOverPanel != null)
            uiPanels["GameOver"] = gameOverPanel;
        if (victoryPanel != null)
            uiPanels["Victory"] = victoryPanel;
        if (settingsPanel != null)
            uiPanels["Settings"] = settingsPanel;
        if (questPanel != null)
            uiPanels["Quest"] = questPanel;
        if (tooltipPanel != null)
            uiPanels["Tooltip"] = tooltipPanel;
        if (lootWindowPanel != null)
            uiPanels["LootWindow"] = lootWindowPanel;
        if (dialoguePanel != null)
            uiPanels["Dialogue"] = dialoguePanel;
        if (loadingScreen != null)
            uiPanels["Loading"] = loadingScreen;
    }
    
    private void SubscribeToEvents()
    {
        EventManager.OnInventoryToggle += HandleInventoryToggle;
        EventManager.OnShowTooltip += ShowTooltip;
        EventManager.OnHideTooltip += HideTooltip;
        EventManager.OnGamePaused += ShowPauseMenu;
        EventManager.OnGameResumed += HidePauseMenu;
        EventManager.OnGameOver += ShowGameOverScreen;
        
        // Input events
        InputManager.OnInventoryInput += ToggleInventory;
    }
    
    private void InitializeUI()
    {
        // Esconder todos os painéis exceto o necessário
        HideAllPanels();
        
        // Mostrar painel apropriado baseado no GameManager
        if (GameManager.Instance != null)
        {
            switch (GameManager.Instance.currentGameState)
            {
                case GameManager.GameState.MainMenu:
                    ShowPanel("MainMenu");
                    break;
                case GameManager.GameState.Playing:
                    ShowGameplayUI();
                    break;
            }
        }
    }
    
    // Métodos de controle de painéis
    public void ShowPanel(string panelName)
    {
        if (uiPanels.ContainsKey(panelName))
        {
            uiPanels[panelName].SetActive(true);
            
            if (!activeOverlays.Contains(uiPanels[panelName]))
            {
                activeOverlays.Add(uiPanels[panelName]);
            }
        }
        else
        {
            Debug.LogWarning($"UI Panel '{panelName}' não encontrado!");
        }
    }
    
    public void HidePanel(string panelName)
    {
        if (uiPanels.ContainsKey(panelName))
        {
            uiPanels[panelName].SetActive(false);
            activeOverlays.Remove(uiPanels[panelName]);
        }
    }
    
    public void TogglePanel(string panelName)
    {
        if (uiPanels.ContainsKey(panelName))
        {
            bool isActive = uiPanels[panelName].activeSelf;
            if (isActive)
                HidePanel(panelName);
            else
                ShowPanel(panelName);
        }
    }
    
    public void HideAllPanels()
    {
        foreach (var panel in uiPanels.Values)
        {
            panel.SetActive(false);
        }
        activeOverlays.Clear();
    }
    
    // Métodos específicos de UI
    public void ShowGameplayUI()
    {
        HideAllPanels();
        ShowPanel("GameplayUI");
        
        // Ativar elementos do HUD
        if (healthBarUI != null)
            healthBarUI.gameObject.SetActive(true);
        if (miniMapPanel != null)
            miniMapPanel.SetActive(true);
        if (skillBarPanel != null)
            skillBarPanel.SetActive(true);
    }
    
    public void ShowMainMenu()
    {
        HideAllPanels();
        ShowPanel("MainMenu");
    }
    
    public void ShowPauseMenu()
    {
        ShowPanel("PauseMenu");
        isPauseMenuOpen = true;
    }
    
    public void HidePauseMenu()
    {
        HidePanel("PauseMenu");
        isPauseMenuOpen = false;
    }
    
    public void ShowGameOverScreen()
    {
        ShowPanel("GameOver");
    }
    
    public void ShowVictoryScreen()
    {
        ShowPanel("Victory");
    }
    
    public void ShowSettings()
    {
        ShowPanel("Settings");
    }
    
    public void HideSettings()
    {
        HidePanel("Settings");
    }
    
    // Inventário
    public void ToggleInventory()
    {
        isInventoryOpen = !isInventoryOpen;
        
        if (isInventoryOpen)
            ShowPanel("Inventory");
        else
            HidePanel("Inventory");
            
        EventManager.TriggerInventoryToggle(isInventoryOpen);
    }
    
    private void HandleInventoryToggle(bool isOpen)
    {
        isInventoryOpen = isOpen;
    }
    
    // Tooltip
    public void ShowTooltip(string text)
    {
        if (tooltipPanel != null && tooltipText != null)
        {
            tooltipText.text = text;
            ShowPanel("Tooltip");
            
            // Posicionar tooltip perto do mouse
            Vector3 mousePos = Input.mousePosition;
            tooltipPanel.transform.position = mousePos + new Vector3(10, -10, 0);
        }
    }
    
    public void HideTooltip()
    {
        HidePanel("Tooltip");
    }
    
    // Loading Screen
    public void ShowLoadingScreen(string text = "Carregando...")
    {
        if (loadingText != null)
            loadingText.text = text;
        
        ShowPanel("Loading");
    }
    
    public void HideLoadingScreen()
    {
        HidePanel("Loading");
    }
    
    public void UpdateLoadingProgress(float progress)
    {
        if (loadingProgressBar != null)
        {
            loadingProgressBar.value = progress;
        }
    }
    
    // Dialogue System
    public void ShowDialogue()
    {
        ShowPanel("Dialogue");
    }
    
    public void HideDialogue()
    {
        HidePanel("Dialogue");
    }
    
    // Loot Window
    public void ShowLootWindow()
    {
        ShowPanel("LootWindow");
    }
    
    public void HideLootWindow()
    {
        HidePanel("LootWindow");
    }
    
    // Quest Panel
    public void ShowQuestPanel()
    {
        ShowPanel("Quest");
    }
    
    public void HideQuestPanel()
    {
        HidePanel("Quest");
    }
    
    // Métodos de botão (para conectar na UI)
    public void OnPlayButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance?.StartNewGame();
    }
    
    public void OnResumeButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance?.ResumeGame();
    }
    
    public void OnRestartButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance?.RestartGame();
    }
    
    public void OnMainMenuButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance?.ReturnToMainMenu();
    }
    
    public void OnQuitButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        GameManager.Instance?.QuitGame();
    }
    
    public void OnSettingsButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        ShowSettings();
    }
    
    public void OnCloseSettingsButtonClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        HideSettings();
    }
    
    // Propriedades públicas
    public bool IsInventoryOpen => isInventoryOpen;
    public bool IsPauseMenuOpen => isPauseMenuOpen;
    public bool HasActiveOverlays => activeOverlays.Count > 0;
    
    // Método para verificar se alguma UI está bloqueando input
    public bool IsUIBlockingInput()
    {
        return isPauseMenuOpen || isInventoryOpen || HasActiveOverlays;
    }
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        EventManager.OnInventoryToggle -= HandleInventoryToggle;
        EventManager.OnShowTooltip -= ShowTooltip;
        EventManager.OnHideTooltip -= HideTooltip;
        EventManager.OnGamePaused -= ShowPauseMenu;
        EventManager.OnGameResumed -= HidePauseMenu;
        EventManager.OnGameOver -= ShowGameOverScreen;
        
        if (InputManager.Instance != null)
        {
            InputManager.OnInventoryInput -= ToggleInventory;
        }
    }
}