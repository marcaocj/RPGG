using UnityEngine;
using System.Collections;

/// <summary>
/// Representa um item físico no mundo que pode ser coletado
/// </summary>
public class LootItem : MonoBehaviour, IInteractable
{
    [Header("Loot Settings")]
    public Item item;
    public int quantity = 1;
    public float pickupRange = 2f;
    public bool autoPickup = false;
    public float autoPickupDelay = 0.5f;
    
    [Header("Visual Effects")]
    public GameObject glowEffect;
    public float bobHeight = 0.3f;
    public float bobSpeed = 2f;
    public bool enableBobbing = true;
    
    [Header("Audio")]
    public AudioClip pickupSound;
    
    [Header("Lifetime")]
    public float lifetime = 300f; // 5 minutos
    public bool fadeBeforeDestroy = true;
    public float fadeTime = 5f;
    
    // Estado interno
    private bool isPickedUp = false;
    private bool playerNearby = false;
    private Vector3 originalPosition;
    private float timeAlive = 0f;
    
    // Componentes
    private Renderer itemRenderer;
    private Collider itemCollider;
    private Rigidbody itemRigidbody;
    
    // Efeitos visuais
    private Material originalMaterial;
    private bool isFading = false;
    
    private void Awake()
    {
        // Obter componentes
        itemRenderer = GetComponentInChildren<Renderer>();
        itemCollider = GetComponent<Collider>();
        itemRigidbody = GetComponent<Rigidbody>();
        
        // Configurar collider como trigger para detecção
        if (itemCollider != null)
        {
            itemCollider.isTrigger = true;
        }
        
        // Salvar material original
        if (itemRenderer != null)
        {
            originalMaterial = itemRenderer.material;
        }
        
        // Salvar posição inicial para bobbing
        originalPosition = transform.position;
    }
    
    private void Start()
    {
        // Configurar efeito de glow se não estiver definido
        SetupGlowEffect();
        
        // Iniciar bobbing se habilitado
        if (enableBobbing)
        {
            StartCoroutine(BobbingAnimation());
        }
        
        // Agendar destruição após lifetime
        if (lifetime > 0f)
        {
            Invoke(nameof(StartFadeAndDestroy), lifetime - fadeTime);
        }
    }
    
    private void Update()
    {
        timeAlive += Time.deltaTime;
        
        // Auto pickup se player estiver próximo
        if (autoPickup && playerNearby && !isPickedUp)
        {
            StartCoroutine(AutoPickupCoroutine());
        }
        
        // Atualizar efeito de glow baseado na proximidade do player
        UpdateGlowEffect();
    }
    
    #region Initialization
    
    /// <summary>
    /// Inicializa o loot item com dados específicos
    /// </summary>
    public void Initialize(Item itemData, int itemQuantity)
    {
        item = itemData;
        quantity = itemQuantity;
        
        if (item != null)
        {
            // Configurar visual baseado no item
            SetupVisualForItem();
            
            // Configurar som se não estiver definido
            if (pickupSound == null && item.pickupSound != null)
            {
                pickupSound = item.pickupSound;
            }
        }
    }
    
    private void SetupVisualForItem()
    {
        if (item == null) return;
        
        // Se tem modelo específico, substituir
        if (item.worldModel != null && item.worldModel != gameObject)
        {
            // Instanciar modelo do item como filho
            GameObject model = Instantiate(item.worldModel, transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            
            // Atualizar referência do renderer
            itemRenderer = model.GetComponent<Renderer>();
            if (itemRenderer == null)
                itemRenderer = model.GetComponentInChildren<Renderer>();
        }
        
        // Configurar material baseado na raridade
        if (itemRenderer != null && item.rarityMaterial != null)
        {
            itemRenderer.material = item.rarityMaterial;
            originalMaterial = item.rarityMaterial;
        }
    }
    
    private void SetupGlowEffect()
    {
        if (glowEffect == null && item != null)
        {
            // Criar efeito de glow simples se não estiver definido
            GameObject glow = new GameObject("GlowEffect");
            glow.transform.SetParent(transform);
            glow.transform.localPosition = Vector3.zero;
            
            // Adicionar luz ponto simples
            Light glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.range = pickupRange;
            glowLight.intensity = 0.5f;
            glowLight.color = GetRarityColor();
            
            glowEffect = glow;
        }
    }
    
    private Color GetRarityColor()
    {
        if (item == null) return Color.white;
        
        switch (item.rarity)
        {
            case ItemRarity.Common: return Color.white;
            case ItemRarity.Uncommon: return Color.green;
            case ItemRarity.Rare: return Color.blue;
            case ItemRarity.Epic: return Color.magenta;
            case ItemRarity.Legendary: return Color.yellow;
            case ItemRarity.Mythic: return Color.red;
            default: return Color.white;
        }
    }
    
    #endregion
    
    #region Interaction
    
    /// <summary>
    /// Implementação da interface IInteractable
    /// </summary>
    public bool CanInteract(GameObject interactor)
    {
        return !isPickedUp && item != null && interactor.CompareTag("Player");
    }
    
    /// <summary>
    /// Executa a interação (pickup)
    /// </summary>
    public void Interact(GameObject interactor)
    {
        if (CanInteract(interactor))
        {
            PickupItem(interactor);
        }
    }
    
    /// <summary>
    /// Obtém texto de interação
    /// </summary>
    public string GetInteractionText()
    {
        if (item == null) return "Pegar item";
        
        string text = $"Pegar {item.itemName}";
        if (quantity > 1)
        {
            text += $" x{quantity}";
        }
        
        return text;
    }
    
    /// <summary>
    /// Coleta o item
    /// </summary>
    public void PickupItem(GameObject picker)
    {
        if (isPickedUp || item == null) return;
        
        PlayerInventory inventory = picker.GetComponent<PlayerInventory>();
        if (inventory == null) return;
        
        // Tentar adicionar ao inventário
        bool addedSuccessfully = inventory.AddItem(item, quantity);
        
        if (addedSuccessfully)
        {
            isPickedUp = true;
            
            // Efeitos visuais e sonoros
            PlayPickupEffects();
            
            // Chamar evento do item
            item.OnPickup(picker);
            
            // Destruir objeto
            StartCoroutine(DestroyAfterPickup());
        }
        else
        {
            // Mostrar mensagem de inventário cheio
            Debug.Log("Inventário cheio!");
            // Aqui você poderia mostrar uma UI indicando que o inventário está cheio
        }
    }
    
    #endregion
    
    #region Trigger Detection
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = true;
            
            // Destacar item quando player se aproxima
            HighlightItem(true);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNearby = false;
            
            // Remover destaque
            HighlightItem(false);
        }
    }
    
    #endregion
    
    #region Visual Effects
    
    private IEnumerator BobbingAnimation()
    {
        while (!isPickedUp)
        {
            float newY = originalPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(originalPosition.x, newY, originalPosition.z);
            yield return null;
        }
    }
    
    private void UpdateGlowEffect()
    {
        if (glowEffect != null)
        {
            Light glowLight = glowEffect.GetComponent<Light>();
            if (glowLight != null)
            {
                // Pulsação baseada no tempo
                float pulse = (Mathf.Sin(Time.time * 3f) + 1f) * 0.5f;
                glowLight.intensity = playerNearby ? 1f : 0.3f + pulse * 0.2f;
            }
        }
    }
    
    private void HighlightItem(bool highlight)
    {
        if (itemRenderer == null) return;
        
        if (highlight)
        {
            // Aumentar brilho ou mudar cor para destacar
            if (itemRenderer.material != null)
            {
                Color highlightColor = originalMaterial.color * 1.5f;
                itemRenderer.material.color = highlightColor;
            }
        }
        else
        {
            // Restaurar cor original
            if (originalMaterial != null)
            {
                itemRenderer.material.color = originalMaterial.color;
            }
        }
    }
    
    #endregion
    
    #region Pickup Effects
    
    private void PlayPickupEffects()
    {
        // Som de pickup
        if (pickupSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(pickupSound, transform.position);
        }
        
        // Efeito visual simples
        if (itemRenderer != null)
        {
            StartCoroutine(PickupFlashEffect());
        }
        
        // Efeito de partículas se disponível
        ParticleSystem particles = GetComponent<ParticleSystem>();
        if (particles != null)
        {
            particles.Play();
        }
    }
    
    private IEnumerator PickupFlashEffect()
    {
        Color originalColor = itemRenderer.material.color;
        Color flashColor = Color.white;
        
        float flashDuration = 0.2f;
        float elapsed = 0f;
        
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            
            Color currentColor = Color.Lerp(flashColor, originalColor, t);
            itemRenderer.material.color = currentColor;
            
            yield return null;
        }
    }
    
    #endregion
    
    #region Auto Pickup
    
    private IEnumerator AutoPickupCoroutine()
    {
        yield return new WaitForSeconds(autoPickupDelay);
        
        if (playerNearby && !isPickedUp)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                PickupItem(player);
            }
        }
    }
    
    #endregion
    
    #region Lifetime Management
    
    private void StartFadeAndDestroy()
    {
        if (isPickedUp) return;
        
        if (fadeBeforeDestroy)
        {
            StartCoroutine(FadeAndDestroy());
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private IEnumerator FadeAndDestroy()
    {
        isFading = true;
        float elapsed = 0f;
        
        Color originalColor = itemRenderer != null ? itemRenderer.material.color : Color.white;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            
            if (itemRenderer != null)
            {
                Color fadeColor = originalColor;
                fadeColor.a = alpha;
                itemRenderer.material.color = fadeColor;
            }
            
            // Fade do glow effect também
            if (glowEffect != null)
            {
                Light glowLight = glowEffect.GetComponent<Light>();
                if (glowLight != null)
                {
                    glowLight.intensity *= alpha;
                }
            }
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
    
    private IEnumerator DestroyAfterPickup()
    {
        // Pequeno delay antes de destruir para permitir efeitos
        yield return new WaitForSeconds(0.3f);
        Destroy(gameObject);
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Define se o item deve ser coletado automaticamente
    /// </summary>
    public void SetAutoPickup(bool enabled, float delay = 0.5f)
    {
        autoPickup = enabled;
        autoPickupDelay = delay;
    }
    
    /// <summary>
    /// Força o pickup do item pelo player mais próximo
    /// </summary>
    public void ForcePickup()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PickupItem(player);
        }
    }
    
    /// <summary>
    /// Obtém informações do loot para UI
    /// </summary>
    public LootInfo GetLootInfo()
    {
        return new LootInfo
        {
            item = item,
            quantity = quantity,
            rarity = item?.rarity ?? ItemRarity.Common,
            timeRemaining = lifetime - timeAlive
        };
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmosSelected()
    {
        // Desenhar range de pickup
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
        
        // Indicador de auto pickup
        if (autoPickup)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, pickupRange * 0.5f);
        }
    }
    
    #endregion
}

/// <summary>
/// Interface para objetos interagíveis
/// </summary>
public interface IInteractable
{
    bool CanInteract(GameObject interactor);
    void Interact(GameObject interactor);
    string GetInteractionText();
}

/// <summary>
/// Informações de loot para UI
/// </summary>
[System.Serializable]
public class LootInfo
{
    public Item item;
    public int quantity;
    public ItemRarity rarity;
    public float timeRemaining;
}