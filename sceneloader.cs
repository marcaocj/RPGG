using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// Carrega cenas com transições e efeitos
/// </summary>
public class SceneLoader : Singleton<SceneLoader>
{
    [Header("Loading Settings")]
    public float minimumLoadingTime = 2f;
    public bool useLoadingScreen = true;
    
    // Estado do carregamento
    private bool isLoading = false;
    private string targetScene = "";
    
    protected override void Awake()
    {
        base.Awake();
    }
    
    /// <summary>
    /// Carrega uma nova cena
    /// </summary>
    /// <param name="sceneName">Nome da cena</param>
    /// <param name="showLoadingScreen">Mostrar tela de loading</param>
    public void LoadScene(string sceneName, bool showLoadingScreen = true)
    {
        if (isLoading)
        {
            Debug.LogWarning("Já existe um carregamento em andamento!");
            return;
        }
        
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("Nome da cena não pode ser vazio!");
            return;
        }
        
        targetScene = sceneName;
        
        if (showLoadingScreen && useLoadingScreen)
        {
            StartCoroutine(LoadSceneWithLoadingScreen());
        }
        else
        {
            StartCoroutine(LoadSceneDirectly());
        }
    }
    
    /// <summary>
    /// Carrega cena por índice
    /// </summary>
    /// <param name="sceneIndex">Índice da cena</param>
    /// <param name="showLoadingScreen">Mostrar tela de loading</param>
    public void LoadScene(int sceneIndex, bool showLoadingScreen = true)
    {
        if (sceneIndex < 0 || sceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"Índice de cena inválido: {sceneIndex}");
            return;
        }
        
        // Converter índice para nome da cena
        string scenePath = SceneUtility.GetScenePathByBuildIndex(sceneIndex);
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
        
        LoadScene(sceneName, showLoadingScreen);
    }
    
    /// <summary>
    /// Recarrega a cena atual
    /// </summary>
    public void ReloadCurrentScene()
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        LoadScene(currentSceneName);
    }
    
    /// <summary>
    /// Carrega a próxima cena na build
    /// </summary>
    public void LoadNextScene()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int nextIndex = (currentIndex + 1) % SceneManager.sceneCountInBuildSettings;
        LoadScene(nextIndex);
    }
    
    /// <summary>
    /// Carrega a cena anterior na build
    /// </summary>
    public void LoadPreviousScene()
    {
        int currentIndex = SceneManager.GetActiveScene().buildIndex;
        int previousIndex = currentIndex - 1;
        
        if (previousIndex < 0)
            previousIndex = SceneManager.sceneCountInBuildSettings - 1;
            
        LoadScene(previousIndex);
    }
    
    private IEnumerator LoadSceneWithLoadingScreen()
    {
        isLoading = true;
        
        // Mostrar tela de loading
        UIManager.Instance?.ShowLoadingScreen($"Carregando {targetScene}...");
        
        // Aguardar um frame para a UI aparecer
        yield return null;
        
        // Iniciar carregamento assíncrono
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene);
        loadOperation.allowSceneActivation = false;
        
        float startTime = Time.time;
        
        // Atualizar progresso do loading
        while (!loadOperation.isDone)
        {
            // Calcular progresso (0.9 é quando Unity termina de carregar)
            float progress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            
            // Atualizar UI
            UIManager.Instance?.UpdateLoadingProgress(progress);
            
            // Se carregamento terminou e tempo mínimo passou
            if (loadOperation.progress >= 0.9f)
            {
                float elapsedTime = Time.time - startTime;
                
                if (elapsedTime >= minimumLoadingTime)
                {
                    // Completar carregamento
                    UIManager.Instance?.UpdateLoadingProgress(1f);
                    yield return new WaitForSeconds(0.1f); // Pequena pausa para mostrar 100%
                    
                    loadOperation.allowSceneActivation = true;
                }
            }
            
            yield return null;
        }
        
        // Esconder tela de loading
        UIManager.Instance?.HideLoadingScreen();
        
        // Executar ações pós-carregamento
        OnSceneLoaded();
        
        isLoading = false;
    }
    
    private IEnumerator LoadSceneDirectly()
    {
        isLoading = true;
        
        // Carregamento direto sem loading screen
        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(targetScene);
        
        yield return loadOperation;
        
        OnSceneLoaded();
        
        isLoading = false;
    }
    
    private void OnSceneLoaded()
    {
        // Ações a serem executadas após carregar uma cena
        string loadedSceneName = SceneManager.GetActiveScene().name;
        
        Debug.Log($"Cena carregada: {loadedSceneName}");
        
        // Configurar música baseada na cena
        SetSceneMusic(loadedSceneName);
        
        // Configurar estado do jogo baseado na cena
        SetGameStateForScene(loadedSceneName);
        
        // Forçar coleta de lixo após carregamento
        System.GC.Collect();
    }
    
    private void SetSceneMusic(string sceneName)
    {
        if (AudioManager.Instance == null) return;
        
        switch (sceneName.ToLower())
        {
            case "mainmenu":
                AudioManager.Instance.PlayMainMenuMusic();
                break;
            case "gameplayscene":
            case "level1":
            case "level2":
                AudioManager.Instance.PlayGameplayMusic();
                break;
            case "victory":
                AudioManager.Instance.PlayVictoryMusic();
                break;
            case "gameover":
                AudioManager.Instance.PlayGameOverMusic();
                break;
            default:
                // Manter música atual ou parar
                break;
        }
    }
    
    private void SetGameStateForScene(string sceneName)
    {
        if (GameManager.Instance == null) return;
        
        switch (sceneName.ToLower())
        {
            case "mainmenu":
                GameManager.Instance.currentGameState = GameManager.GameState.MainMenu;
                break;
            case "gameplayscene":
            case "level1":
            case "level2":
                GameManager.Instance.currentGameState = GameManager.GameState.Playing;
                break;
        }
    }
    
    /// <summary>
    /// Verifica se uma cena existe na build
    /// </summary>
    /// <param name="sceneName">Nome da cena</param>
    /// <returns>True se a cena existe</returns>
    public bool SceneExists(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            
            if (name.Equals(sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Obtém o nome da cena atual
    /// </summary>
    /// <returns>Nome da cena atual</returns>
    public string GetCurrentSceneName()
    {
        return SceneManager.GetActiveScene().name;
    }
    
    /// <summary>
    /// Obtém o índice da cena atual
    /// </summary>
    /// <returns>Índice da cena atual</returns>
    public int GetCurrentSceneIndex()
    {
        return SceneManager.GetActiveScene().buildIndex;
    }
    
    // Propriedades públicas
    public bool IsLoading => isLoading;
    public string TargetScene => targetScene;
    
    // Métodos de conveniência para cenas específicas
    public void LoadMainMenu()
    {
        LoadScene("MainMenu");
    }
    
    public void LoadGameplayScene()
    {
        LoadScene("GameplayScene");
    }
    
    public void LoadLevel(int levelNumber)
    {
        LoadScene($"Level{levelNumber}");
    }
}