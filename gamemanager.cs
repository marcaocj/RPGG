using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gerencia o estado geral do jogo (menu, gameplay, pause, game over)
/// </summary>
public class GameManager : Singleton<GameManager>
{
    [Header("Game State")]
    public GameState currentGameState = GameState.MainMenu;
    
    [Header("Player Reference")]
    public GameObject playerPrefab;
    public Transform playerSpawnPoint;
    
    [Header("Game Settings")]
    public bool isPaused = false;
    public float gameTimeMultiplier = 1f;
    
    private GameObject currentPlayer;
    
    public enum GameState
    {
        MainMenu,
        Loading,
        Playing,
        Paused,
        GameOver,
        Victory
    }
    
    protected override void Awake()
    {
        base.Awake();
        
        // Inscrever nos eventos
        EventManager.OnPlayerDeath += HandlePlayerDeath;
        EventManager.OnGamePaused += HandleGamePaused;
        EventManager.OnGameResumed += HandleGameResumed;
    }
    
    private void Start()
    {
        // Se estamos em uma cena de jogo, inicializar o gameplay
        if (SceneManager.GetActiveScene().name != "MainMenu")
        {
            StartGameplay();
        }
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void HandleInput()
    {
        // ESC para pausar/despausar
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentGameState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentGameState == GameState.Paused)
            {
                ResumeGame();
            }
        }
    }
    
    public void StartNewGame()
    {
        ChangeGameState(GameState.Loading);
        SceneLoader.Instance?.LoadScene("GameplayScene");
    }
    
    public void StartGameplay()
    {
        ChangeGameState(GameState.Playing);
        SpawnPlayer();
        
        // Configurar cursor para o jogo
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    public void PauseGame()
    {
        if (currentGameState != GameState.Playing) return;
        
        isPaused = true;
        Time.timeScale = 0f;
        ChangeGameState(GameState.Paused);
        EventManager.TriggerGamePaused();
        
        UIManager.Instance?.ShowPauseMenu();
    }
    
    public void ResumeGame()
    {
        if (currentGameState != GameState.Paused) return;
        
        isPaused = false;
        Time.timeScale = gameTimeMultiplier;
        ChangeGameState(GameState.Playing);
        EventManager.TriggerGameResumed();
        
        UIManager.Instance?.HidePauseMenu();
    }
    
    public void GameOver()
    {
        ChangeGameState(GameState.GameOver);
        Time.timeScale = 0f;
        
        EventManager.TriggerGameOver();
        UIManager.Instance?.ShowGameOverScreen();
        
        // Salvar dados se necessário
        SaveManager.Instance?.SaveGame();
    }
    
    public void Victory()
    {
        ChangeGameState(GameState.Victory);
        Time.timeScale = 0f;
        
        UIManager.Instance?.ShowVictoryScreen();
        
        // Salvar dados
        SaveManager.Instance?.SaveGame();
    }
    
    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        ChangeGameState(GameState.MainMenu);
        SceneLoader.Instance?.LoadScene("MainMenu");
    }
    
    public void QuitGame()
    {
        SaveManager.Instance?.SaveGame();
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    private void SpawnPlayer()
    {
        if (playerPrefab == null)
        {
            Debug.LogError("Player prefab não definido no GameManager!");
            return;
        }
        
        Vector3 spawnPosition = playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
        currentPlayer = Instantiate(playerPrefab, spawnPosition, Quaternion.identity);
        
        // Configurar câmera para seguir o player
        CameraController cameraController = FindObjectOfType<CameraController>();
        if (cameraController != null)
        {
            cameraController.SetTarget(currentPlayer.transform);
        }
    }
    
    private void ChangeGameState(GameState newState)
    {
        if (currentGameState == newState) return;
        
        GameState previousState = currentGameState;
        currentGameState = newState;
        
        Debug.Log($"Game State mudou de {previousState} para {newState}");
        
        OnGameStateChanged(previousState, newState);
    }
    
    private void OnGameStateChanged(GameState previousState, GameState newState)
    {
        // Lógica adicional baseada na mudança de estado
        switch (newState)
        {
            case GameState.Playing:
                Time.timeScale = gameTimeMultiplier;
                break;
                
            case GameState.Paused:
            case GameState.GameOver:
            case GameState.Victory:
                Time.timeScale = 0f;
                break;
                
            case GameState.Loading:
                // Mostrar tela de loading se necessário
                break;
        }
    }
    
    // Event Handlers
    private void HandlePlayerDeath()
    {
        GameOver();
    }
    
    private void HandleGamePaused()
    {
        // Lógica adicional quando o jogo é pausado
    }
    
    private void HandleGameResumed()
    {
        // Lógica adicional quando o jogo é retomado
    }
    
    // Propriedades públicas
    public bool IsGamePlaying => currentGameState == GameState.Playing;
    public bool IsGamePaused => currentGameState == GameState.Paused;
    public GameObject CurrentPlayer => currentPlayer;
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        EventManager.OnPlayerDeath -= HandlePlayerDeath;
        EventManager.OnGamePaused -= HandleGamePaused;
        EventManager.OnGameResumed -= HandleGameResumed;
    }
}