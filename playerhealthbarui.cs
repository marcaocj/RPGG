using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Controla a interface de saúde e mana do jogador
/// </summary>
public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("Health Bar")]
    public Slider healthSlider;
    public Image healthFill;
    public Text healthText;
    public Color healthColor = Color.red;
    public Color lowHealthColor = Color.darkRed;
    public float lowHealthThreshold = 0.25f;
    
    [Header("Mana Bar")]
    public Slider manaSlider;
    public Image manaFill;
    public Text manaText;
    public Color manaColor = Color.blue;
    public Color lowManaColor = Color.darkBlue;
    public float lowManaThreshold = 0.2f;
    
    [Header("Animation Settings")]
    public bool smoothTransitions = true;
    public float transitionSpeed = 5f;
    public bool showDamageNumbers = true;
    public GameObject damageNumberPrefab;
    
    [Header("Warning Effects")]
    public bool flashOnLowHealth = true;
    public float flashSpeed = 2f;
    public AudioClip lowHealthSound;
    public AudioClip criticalHealthSound;
    
    [Header("Regeneration Indicators")]
    public Image healthRegenIndicator;
    public Image manaRegenIndicator;
    public Color regenColor = Color.green;
    
    // Estado atual
    private float currentHealthDisplay = 1f;
    private float currentManaDisplay = 1f;
    private float targetHealthDisplay = 1f;
    private float targetManaDisplay = 1f;
    
    // Referências
    private PlayerStats playerStats;
    private bool isLowHealth = false;
    private bool isCriticalHealth = false;
    private Coroutine flashCoroutine;
    
    // Cache para performance
    private Camera mainCamera;
    
    private void Awake()
    {
        // Configurar componentes se não estiverem definidos
        SetupUIComponents();
        
        // Obter referência da câmera
        mainCamera = Camera.main;
        if (mainCamera == null)
            mainCamera = FindObjectOfType<Camera>();
    }
    
    private void Start()
    {
        // Encontrar PlayerStats
        FindPlayerStats();
        
        // Inscrever nos eventos
        SubscribeToEvents();
        
        // Configurar estado inicial
        InitializeUI();
    }
    
    private void Update()
    {
        if (smoothTransitions)
        {
            UpdateSmoothTransitions();
        }
        
        UpdateRegenIndicators();
    }
    
    #region Setup Methods
    
    private void SetupUIComponents()
    {
        // Configurar sliders se não estiverem definidos
        if (healthSlider == null)
            healthSlider = transform.Find("HealthSlider")?.GetComponent<Slider>();
        
        if (manaSlider == null)
            manaSlider = transform.Find("ManaSlider")?.GetComponent<Slider>();
        
        // Configurar fills
        if (healthFill == null && healthSlider != null)
            healthFill = healthSlider.fillRect?.GetComponent<Image>();
        
        if (manaFill == null && manaSlider != null)
            manaFill = manaSlider.fillRect?.GetComponent<Image>();
        
        // Configurar cores iniciais
        if (healthFill != null)
            healthFill.color = healthColor;
        
        if (manaFill != null)
            manaFill.color = manaColor;
    }
    
    private void FindPlayerStats()
    {
        if (GameManager.Instance?.CurrentPlayer != null)
        {
            playerStats = GameManager.Instance.CurrentPlayer.GetComponent<PlayerStats>();
        }
        
        if (playerStats == null)
        {
            // Tentar encontrar na cena
            playerStats = FindObjectOfType<PlayerStats>();
        }
    }
    
    private void SubscribeToEvents()
    {
        EventManager.OnPlayerHealthChanged += UpdateHealthDisplay;
        EventManager.OnPlayerManaChanged += UpdateManaDisplay;
        EventManager.OnDamageDealt += ShowDamageNumber;
    }
    
    private void InitializeUI()
    {
        if (playerStats != null)
        {
            // Configurar valores iniciais
            UpdateHealthDisplay(playerStats.currentHealth, playerStats.FinalMaxHealth);
            UpdateManaDisplay(playerStats.currentMana, playerStats.FinalMaxMana);
        }
        else
        {
            // Valores padrão
            UpdateHealthDisplay(100f, 100f);
            UpdateManaDisplay(50f, 50f);
        }
    }
    
    #endregion
    
    #region Health Display
    
    private void UpdateHealthDisplay(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0) return;
        
        float healthPercentage = currentHealth / maxHealth;
        targetHealthDisplay = healthPercentage;
        
        if (!smoothTransitions)
        {
            currentHealthDisplay = targetHealthDisplay;
            ApplyHealthUI();
        }
        
        // Verificar estados de saúde baixa
        CheckHealthWarnings(healthPercentage);
        
        // Atualizar texto
        if (healthText != null)
        {
            healthText.text = $"{Mathf.Ceil(currentHealth)}/{Mathf.Ceil(maxHealth)}";
        }
    }
    
    private void ApplyHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.value = currentHealthDisplay;
        }
        
        // Atualizar cor baseada na porcentagem
        if (healthFill != null)
        {
            Color targetColor = currentHealthDisplay <= lowHealthThreshold ? lowHealthColor : healthColor;
            healthFill.color = targetColor;
        }
    }
    
    private void CheckHealthWarnings(float healthPercentage)
    {
        bool wasLowHealth = isLowHealth;
        bool wasCriticalHealth = isCriticalHealth;
        
        isLowHealth = healthPercentage <= lowHealthThreshold;
        isCriticalHealth = healthPercentage <= 0.1f; // 10% crítico
        
        // Sons de aviso
        if (!wasLowHealth && isLowHealth && lowHealthSound != null)
        {
            AudioManager.Instance?.PlaySFX(lowHealthSound);
        }
        
        if (!wasCriticalHealth && isCriticalHealth && criticalHealthSound != null)
        {
            AudioManager.Instance?.PlaySFX(criticalHealthSound);
        }
        
        // Efeito de flash
        if (flashOnLowHealth && isCriticalHealth && flashCoroutine == null)
        {
            flashCoroutine = StartCoroutine(FlashHealthBar());
        }
        else if (!isCriticalHealth && flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
    }
    
    #endregion
    
    #region Mana Display
    
    private void UpdateManaDisplay(float currentMana, float maxMana)
    {
        if (maxMana <= 0) return;
        
        float manaPercentage = currentMana / maxMana;
        targetManaDisplay = manaPercentage;
        
        if (!smoothTransitions)
        {
            currentManaDisplay = targetManaDisplay;
            ApplyManaUI();
        }
        
        // Atualizar texto
        if (manaText != null)
        {
            manaText.text = $"{Mathf.Ceil(currentMana)}/{Mathf.Ceil(maxMana)}";
        }
    }
    
    private void ApplyManaUI()
    {
        if (manaSlider != null)
        {
            manaSlider.value = currentManaDisplay;
        }
        
        // Atualizar cor baseada na porcentagem
        if (manaFill != null)
        {
            Color targetColor = currentManaDisplay <= lowManaThreshold ? lowManaColor : manaColor;
            manaFill.color = targetColor;
        }
    }
    
    #endregion
    
    #region Smooth Transitions
    
    private void UpdateSmoothTransitions()
    {
        bool healthChanged = false;
        bool manaChanged = false;
        
        // Transição suave da saúde
        if (Mathf.Abs(currentHealthDisplay - targetHealthDisplay) > 0.001f)
        {
            currentHealthDisplay = Mathf.Lerp(currentHealthDisplay, targetHealthDisplay, transitionSpeed * Time.deltaTime);
            healthChanged = true;
        }
        
        // Transição suave da mana
        if (Mathf.Abs(currentManaDisplay - targetManaDisplay) > 0.001f)
        {
            currentManaDisplay = Mathf.Lerp(currentManaDisplay, targetManaDisplay, transitionSpeed * Time.deltaTime);
            manaChanged = true;
        }
        
        // Aplicar mudanças
        if (healthChanged)
        {
            ApplyHealthUI();
        }
        
        if (manaChanged)
        {
            ApplyManaUI();
        }
    }
    
    #endregion
    
    #region Regeneration Indicators
    
    private void UpdateRegenIndicators()
    {
        if (playerStats == null) return;
        
        // Indicador de regeneração de saúde
        if (healthRegenIndicator != null)
        {
            bool isRegenerating = playerStats.currentHealth < playerStats.FinalMaxHealth;
            healthRegenIndicator.gameObject.SetActive(isRegenerating);
            
            if (isRegenerating)
            {
                // Animação de pulsação
                float alpha = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
                Color color = regenColor;
                color.a = alpha;
                healthRegenIndicator.color = color;
            }
        }
        
        // Indicador de regeneração de mana
        if (manaRegenIndicator != null)
        {
            bool isRegenerating = playerStats.currentMana < playerStats.FinalMaxMana;
            manaRegenIndicator.gameObject.SetActive(isRegenerating);
            
            if (isRegenerating)
            {
                // Animação de pulsação
                float alpha = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
                Color color = regenColor;
                color.a = alpha;
                manaRegenIndicator.color = color;
            }
        }
    }
    
    #endregion
    
    #region Damage Numbers
    
    private void ShowDamageNumber(float damage, Vector3 worldPosition)
    {
        if (!showDamageNumbers || damageNumberPrefab == null || mainCamera == null) return;
        
        // Converter posição do mundo para tela
        Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
        
        // Verificar se está na tela
        if (screenPosition.z > 0 && screenPosition.x >= 0 && screenPosition.x <= Screen.width && 
            screenPosition.y >= 0 && screenPosition.y <= Screen.height)
        {
            // Instanciar número de dano
            GameObject damageNumber = Instantiate(damageNumberPrefab, transform.parent);
            
            // Configurar posição
            RectTransform rectTransform = damageNumber.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.position = screenPosition;
            }
            
            // Configurar texto
            Text damageText = damageNumber.GetComponent<Text>();
            if (damageText != null)
            {
                damageText.text = Mathf.Ceil(damage).ToString();
            }
            
            // Animar e destruir
            StartCoroutine(AnimateDamageNumber(damageNumber));
        }
    }
    
    private IEnumerator AnimateDamageNumber(GameObject damageNumber)
    {
        RectTransform rectTransform = damageNumber.GetComponent<RectTransform>();
        Text damageText = damageNumber.GetComponent<Text>();
        
        if (rectTransform == null) yield break;
        
        Vector3 startPosition = rectTransform.position;
        Vector3 endPosition = startPosition + Vector3.up * 100f;
        
        float duration = 1.5f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            
            // Movimento para cima
            rectTransform.position = Vector3.Lerp(startPosition, endPosition, progress);
            
            // Fade out
            if (damageText != null)
            {
                Color color = damageText.color;
                color.a = 1f - progress;
                damageText.color = color;
            }
            
            yield return null;
        }
        
        Destroy(damageNumber);
    }
    
    #endregion
    
    #region Flash Effect
    
    private IEnumerator FlashHealthBar()
    {
        while (isCriticalHealth)
        {
            if (healthFill != null)
            {
                // Flash entre cor normal e cor crítica
                float flash = (Mathf.Sin(Time.time * flashSpeed) + 1f) * 0.5f;
                healthFill.color = Color.Lerp(lowHealthColor, Color.white, flash * 0.3f);
            }
            
            yield return null;
        }
        
        // Restaurar cor normal
        if (healthFill != null)
        {
            healthFill.color = isLowHealth ? lowHealthColor : healthColor;
        }
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Atualiza manualmente a UI
    /// </summary>
    public void RefreshUI()
    {
        if (playerStats != null)
        {
            UpdateHealthDisplay(playerStats.currentHealth, playerStats.FinalMaxHealth);
            UpdateManaDisplay(playerStats.currentMana, playerStats.FinalMaxMana);
        }
    }
    
    /// <summary>
    /// Define se deve mostrar números de dano
    /// </summary>
    public void SetShowDamageNumbers(bool show)
    {
        showDamageNumbers = show;
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        EventManager.OnPlayerHealthChanged -= UpdateHealthDisplay;
        EventManager.OnPlayerManaChanged -= UpdateManaDisplay;
        EventManager.OnDamageDealt -= ShowDamageNumber;
        
        // Parar corrotinas
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }
    }
}