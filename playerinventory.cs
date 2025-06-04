using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Gerencia o inventário do jogador (itens, ouro, etc.)
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory Settings")]
    public int maxSlots = 40;
    public int maxStackSize = 99;
    public int goldAmount = 0;
    
    [Header("Quick Use Slots")]
    public int quickUseSlots = 8;
    
    // Lista de slots do inventário
    private List<InventorySlot> inventorySlots = new List<InventorySlot>();
    private List<InventorySlot> quickUseSlots_List = new List<InventorySlot>();
    
    // Eventos
    public System.Action OnInventoryChanged;
    public System.Action<int> OnGoldChanged;
    public System.Action<Item, int> OnItemAdded;
    public System.Action<Item, int> OnItemRemoved;
    
    // Componentes
    private PlayerStats playerStats;
    
    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        InitializeInventory();
    }
    
    private void Start()
    {
        // Inscrever nos eventos
        EventManager.OnItemPickup += HandleItemPickup;
        EventManager.OnGoldChanged += HandleGoldChanged;
    }
    
    private void InitializeInventory()
    {
        // Inicializar slots do inventário principal
        inventorySlots.Clear();
        for (int i = 0; i < maxSlots; i++)
        {
            inventorySlots.Add(new InventorySlot());
        }
        
        // Inicializar slots de uso rápido
        quickUseSlots_List.Clear();
        for (int i = 0; i < quickUseSlots; i++)
        {
            quickUseSlots_List.Add(new InventorySlot());
        }
        
        // Disparar evento inicial
        OnGoldChanged?.Invoke(goldAmount);
    }
    
    #region Item Management
    
    /// <summary>
    /// Adiciona um item ao inventário
    /// </summary>
    public bool AddItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;
        
        int remainingQuantity = quantity;
        
        // Tentar empilhar em slots existentes primeiro
        if (item.maxStackSize > 1)
        {
            remainingQuantity = TryStackItem(item, remainingQuantity);
        }
        
        // Se ainda sobrou quantidade, tentar adicionar em slots vazios
        while (remainingQuantity > 0)
        {
            int slotIndex = FindEmptySlot();
            if (slotIndex == -1)
            {
                Debug.Log("Inventário cheio!");
                return false; // Inventário cheio
            }
            
            int quantityToAdd = Mathf.Min(remainingQuantity, item.maxStackSize);
            inventorySlots[slotIndex].SetItem(item, quantityToAdd);
            remainingQuantity -= quantityToAdd;
        }
        
        // Disparar eventos
        OnItemAdded?.Invoke(item, quantity);
        OnInventoryChanged?.Invoke();
        
        Debug.Log($"Adicionado ao inventário: {item.itemName} x{quantity}");
        return true;
    }
    
    /// <summary>
    /// Remove um item do inventário
    /// </summary>
    public bool RemoveItem(Item item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;
        
        int remainingToRemove = quantity;
        
        // Procurar e remover dos slots
        for (int i = 0; i < inventorySlots.Count && remainingToRemove > 0; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot.HasItem && slot.item.itemID == item.itemID)
            {
                int quantityToRemove = Mathf.Min(remainingToRemove, slot.quantity);
                slot.RemoveQuantity(quantityToRemove);
                remainingToRemove -= quantityToRemove;
                
                if (slot.quantity <= 0)
                {
                    slot.Clear();
                }
            }
        }
        
        // Verificar se removeu tudo
        if (remainingToRemove > 0)
        {
            Debug.Log($"Não foi possível remover {remainingToRemove} de {item.itemName}");
            return false;
        }
        
        // Disparar eventos
        OnItemRemoved?.Invoke(item, quantity);
        OnInventoryChanged?.Invoke();
        
        return true;
    }
    
    /// <summary>
    /// Remove item por ID
    /// </summary>
    public bool RemoveItem(int itemID, int quantity = 1)
    {
        Item item = GetItemByID(itemID);
        return item != null && RemoveItem(item, quantity);
    }
    
    /// <summary>
    /// Usa um item do inventário
    /// </summary>
    public bool UseItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inventorySlots.Count) return false;
        
        InventorySlot slot = inventorySlots[slotIndex];
        if (!slot.HasItem) return false;
        
        Item item = slot.item;
        
        // Tentar usar o item
        bool itemConsumed = item.UseItem(gameObject);
        
        if (itemConsumed)
        {
            slot.RemoveQuantity(1);
            if (slot.quantity <= 0)
            {
                slot.Clear();
            }
            
            OnInventoryChanged?.Invoke();
        }
        
        return itemConsumed;
    }
    
    /// <summary>
    /// Usa item dos slots de uso rápido
    /// </summary>
    public bool UseQuickSlotItem(int quickSlotIndex)
    {
        if (quickSlotIndex < 0 || quickSlotIndex >= quickUseSlots_List.Count) return false;
        
        InventorySlot slot = quickUseSlots_List[quickSlotIndex];
        if (!slot.HasItem) return false;
        
        Item item = slot.item;
        bool itemConsumed = item.UseItem(gameObject);
        
        if (itemConsumed)
        {
            slot.RemoveQuantity(1);
            if (slot.quantity <= 0)
            {
                slot.Clear();
            }
            
            OnInventoryChanged?.Invoke();
        }
        
        return itemConsumed;
    }
    
    #endregion
    
    #region Gold Management
    
    /// <summary>
    /// Adiciona ouro
    /// </summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        
        goldAmount += amount;
        OnGoldChanged?.Invoke(goldAmount);
        EventManager.TriggerGoldChanged(goldAmount);
        
        Debug.Log($"Ouro adicionado: +{amount}. Total: {goldAmount}");
    }
    
    /// <summary>
    /// Remove ouro
    /// </summary>
    public bool RemoveGold(int amount)
    {
        if (amount <= 0 || goldAmount < amount) return false;
        
        goldAmount -= amount;
        OnGoldChanged?.Invoke(goldAmount);
        EventManager.TriggerGoldChanged(goldAmount);
        
        Debug.Log($"Ouro removido: -{amount}. Total: {goldAmount}");
        return true;
    }
    
    /// <summary>
    /// Verifica se tem ouro suficiente
    /// </summary>
    public bool HasEnoughGold(int amount)
    {
        return goldAmount >= amount;
    }
    
    #endregion
    
    #region Quick Use Slots
    
    /// <summary>
    /// Atribui item a um slot de uso rápido
    /// </summary>
    public bool AssignToQuickSlot(int inventorySlotIndex, int quickSlotIndex)
    {
        if (inventorySlotIndex < 0 || inventorySlotIndex >= inventorySlots.Count) return false;
        if (quickSlotIndex < 0 || quickSlotIndex >= quickUseSlots_List.Count) return false;
        
        InventorySlot inventorySlot = inventorySlots[inventorySlotIndex];
        if (!inventorySlot.HasItem) return false;
        
        InventorySlot quickSlot = quickUseSlots_List[quickSlotIndex];
        
        // Mover item para slot rápido
        quickSlot.SetItem(inventorySlot.item, inventorySlot.quantity);
        inventorySlot.Clear();
        
        OnInventoryChanged?.Invoke();
        return true;
    }
    
    /// <summary>
    /// Remove item do slot rápido
    /// </summary>
    public bool RemoveFromQuickSlot(int quickSlotIndex)
    {
        if (quickSlotIndex < 0 || quickSlotIndex >= quickUseSlots_List.Count) return false;
        
        InventorySlot quickSlot = quickUseSlots_List[quickSlotIndex];
        if (!quickSlot.HasItem) return false;
        
        // Tentar mover de volta para inventário principal
        if (AddItem(quickSlot.item, quickSlot.quantity))
        {
            quickSlot.Clear();
            OnInventoryChanged?.Invoke();
            return true;
        }
        
        return false;
    }
    
    #endregion
    
    #region Helper Methods
    
    private int TryStackItem(Item item, int quantity)
    {
        int remaining = quantity;
        
        for (int i = 0; i < inventorySlots.Count && remaining > 0; i++)
        {
            InventorySlot slot = inventorySlots[i];
            if (slot.HasItem && slot.item.CanStackWith(item) && slot.quantity < item.maxStackSize)
            {
                int spaceAvailable = item.maxStackSize - slot.quantity;
                int quantityToAdd = Mathf.Min(remaining, spaceAvailable);
                
                slot.AddQuantity(quantityToAdd);
                remaining -= quantityToAdd;
            }
        }
        
        return remaining;
    }
    
    private int FindEmptySlot()
    {
        for (int i = 0; i < inventorySlots.Count; i++)
        {
            if (!inventorySlots[i].HasItem)
            {
                return i;
            }
        }
        return -1;
    }
    
    #endregion
    
    #region Queries
    
    /// <summary>
    /// Verifica se tem um item específico
    /// </summary>
    public bool HasItem(int itemID)
    {
        return GetItemQuantity(itemID) > 0;
    }
    
    /// <summary>
    /// Verifica se tem quantidade suficiente de um item
    /// </summary>
    public bool HasItem(int itemID, int requiredQuantity)
    {
        return GetItemQuantity(itemID) >= requiredQuantity;
    }
    
    /// <summary>
    /// Obtém a quantidade total de um item
    /// </summary>
    public int GetItemQuantity(int itemID)
    {
        int totalQuantity = 0;
        
        foreach (InventorySlot slot in inventorySlots)
        {
            if (slot.HasItem && slot.item.itemID == itemID)
            {
                totalQuantity += slot.quantity;
            }
        }
        
        return totalQuantity;
    }
    
    /// <summary>
    /// Obtém item por ID
    /// </summary>
    public Item GetItemByID(int itemID)
    {
        foreach (InventorySlot slot in inventorySlots)
        {
            if (slot.HasItem && slot.item.itemID == itemID)
            {
                return slot.item;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Obtém todos os itens de um tipo
    /// </summary>
    public List<Item> GetItemsByType(ItemType itemType)
    {
        List<Item> items = new List<Item>();
        
        foreach (InventorySlot slot in inventorySlots)
        {
            if (slot.HasItem && slot.item.itemType == itemType)
            {
                items.Add(slot.item);
            }
        }
        
        return items;
    }
    
    /// <summary>
    /// Conta slots vazios
    /// </summary>
    public int GetEmptySlotCount()
    {
        return inventorySlots.Count(slot => !slot.HasItem);
    }
    
    /// <summary>
    /// Verifica se inventário está cheio
    /// </summary>
    public bool IsInventoryFull()
    {
        return GetEmptySlotCount() == 0;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandleItemPickup(Item item)
    {
        AddItem(item, 1);
    }
    
    private void HandleGoldChanged(int newAmount)
    {
        // Este evento vem de fora, atualizar o valor local se necessário
        if (newAmount != goldAmount)
        {
            goldAmount = newAmount;
            OnGoldChanged?.Invoke(goldAmount);
        }
    }
    
    #endregion
    
    #region Save/Load Support
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public InventoryData GetSaveData()
    {
        InventoryData data = new InventoryData();
        data.goldAmount = goldAmount;
        data.inventorySlots = new List<InventorySlotData>();
        data.quickUseSlots = new List<InventorySlotData>();
        
        // Salvar slots do inventário
        foreach (InventorySlot slot in inventorySlots)
        {
            data.inventorySlots.Add(slot.GetSaveData());
        }
        
        // Salvar slots de uso rápido
        foreach (InventorySlot slot in quickUseSlots_List)
        {
            data.quickUseSlots.Add(slot.GetSaveData());
        }
        
        return data;
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public void LoadSaveData(InventoryData data)
    {
        if (data == null) return;
        
        goldAmount = data.goldAmount;
        
        // Carregar slots do inventário
        for (int i = 0; i < inventorySlots.Count && i < data.inventorySlots.Count; i++)
        {
            inventorySlots[i].LoadSaveData(data.inventorySlots[i]);
        }
        
        // Carregar slots de uso rápido
        for (int i = 0; i < quickUseSlots_List.Count && i < data.quickUseSlots.Count; i++)
        {
            quickUseSlots_List[i].LoadSaveData(data.quickUseSlots[i]);
        }
        
        // Disparar eventos
        OnGoldChanged?.Invoke(goldAmount);
        OnInventoryChanged?.Invoke();
    }
    
    #endregion
    
    #region Properties
    
    public int GoldAmount => goldAmount;
    public List<InventorySlot> InventorySlots => inventorySlots;
    public List<InventorySlot> QuickUseSlots => quickUseSlots_List;
    public bool HasEmptySlots => GetEmptySlotCount() > 0;
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        EventManager.OnItemPickup -= HandleItemPickup;
        EventManager.OnGoldChanged -= HandleGoldChanged;
    }
}

/// <summary>
/// Dados de salvamento do inventário
/// </summary>
[System.Serializable]
public class InventoryData
{
    public int goldAmount;
    public List<InventorySlotData> inventorySlots;
    public List<InventorySlotData> quickUseSlots;
}