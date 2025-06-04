using UnityEngine;

/// <summary>
/// Classe base para todos os itens do jogo
/// </summary>
[CreateAssetMenu(fileName = "New Item", menuName = "Game/Items/Basic Item")]
public class Item : ScriptableObject
{
    [Header("Basic Info")]
    public int itemID;
    public string itemName;
    [TextArea(3, 5)]
    public string description;
    public Sprite icon;
    
    [Header("Item Properties")]
    public ItemType itemType;
    public ItemRarity rarity = ItemRarity.Common;
    public int maxStackSize = 1;
    public int sellPrice = 10;
    public int buyPrice = 20;
    
    [Header("Visual")]
    public GameObject worldModel; // Modelo 3D para quando está no chão
    public Material rarityMaterial; // Material baseado na raridade
    
    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip dropSound;
    public AudioClip useSound;
    
    // Propriedades calculadas
    public virtual string GetTooltipText()
    {
        string tooltip = $"<color={GetRarityColor()}><b>{itemName}</b></color>\n";
        tooltip += $"<i>{GetRarityText()}</i>\n\n";
        tooltip += description;
        
        if (sellPrice > 0)
        {
            tooltip += $"\n\n<color=yellow>Valor: {sellPrice} ouro</color>";
        }
        
        return tooltip;
    }
    
    public virtual string GetRarityColor()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return "white";
            case ItemRarity.Uncommon: return "green";
            case ItemRarity.Rare: return "blue";
            case ItemRarity.Epic: return "purple";
            case ItemRarity.Legendary: return "orange";
            case ItemRarity.Mythic: return "red";
            default: return "white";
        }
    }
    
    public virtual string GetRarityText()
    {
        switch (rarity)
        {
            case ItemRarity.Common: return "Comum";
            case ItemRarity.Uncommon: return "Incomum";
            case ItemRarity.Rare: return "Raro";
            case ItemRarity.Epic: return "Épico";
            case ItemRarity.Legendary: return "Lendário";
            case ItemRarity.Mythic: return "Mítico";
            default: return "Comum";
        }
    }
    
    /// <summary>
    /// Verifica se o item pode ser empilhado com outro
    /// </summary>
    public virtual bool CanStackWith(Item other)
    {
        if (other == null) return false;
        return itemID == other.itemID && maxStackSize > 1;
    }
    
    /// <summary>
    /// Ação executada quando o item é usado
    /// </summary>
    public virtual bool UseItem(GameObject user)
    {
        Debug.Log($"Usando item: {itemName}");
        
        // Tocar som de uso
        if (useSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(useSound);
        }
        
        return false; // Retorna true se o item foi consumido
    }
    
    /// <summary>
    /// Ação executada quando o item é coletado
    /// </summary>
    public virtual void OnPickup(GameObject picker)
    {
        Debug.Log($"Item coletado: {itemName}");
        
        // Tocar som de coleta
        if (pickupSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(pickupSound);
        }
        
        // Disparar evento
        EventManager.TriggerItemPickup(this);
    }
    
    /// <summary>
    /// Ação executada quando o item é dropado
    /// </summary>
    public virtual void OnDrop(Vector3 dropPosition)
    {
        Debug.Log($"Item dropado: {itemName}");
        
        // Tocar som de drop
        if (dropSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(dropSound);
        }
        
        // Disparar evento
        EventManager.TriggerItemDropped(this);
    }
    
    /// <summary>
    /// Cria uma cópia do item
    /// </summary>
    public virtual Item CreateCopy()
    {
        return Instantiate(this);
    }
}

/// <summary>
/// Tipos de itens
/// </summary>
public enum ItemType
{
    Consumable,     // Poções, comida
    Equipment,      // Armas, armaduras
    Material,       // Materiais de craft
    Quest,          // Itens de quest
    Key,           // Chaves
    Currency,      // Moedas, gemas
    Misc           // Outros
}

/// <summary>
/// Raridades dos itens
/// </summary>
public enum ItemRarity
{
    Common,        // Branco
    Uncommon,      // Verde
    Rare,          // Azul
    Epic,          // Roxo
    Legendary,     // Laranja
    Mythic         // Vermelho
}