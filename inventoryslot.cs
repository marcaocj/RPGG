using UnityEngine;

/// <summary>
/// Representa um slot individual do inventário
/// </summary>
[System.Serializable]
public class InventorySlot
{
    [SerializeField] private Item _item;
    [SerializeField] private int _quantity;
    
    public Item item => _item;
    public int quantity => _quantity;
    public bool HasItem => _item != null && _quantity > 0;
    public bool IsEmpty => _item == null || _quantity <= 0;
    
    public InventorySlot()
    {
        Clear();
    }
    
    public InventorySlot(Item item, int quantity)
    {
        SetItem(item, quantity);
    }
    
    /// <summary>
    /// Define item e quantidade no slot
    /// </summary>
    public void SetItem(Item newItem, int newQuantity)
    {
        _item = newItem;
        _quantity = Mathf.Max(0, newQuantity);
        
        if (_item != null && _quantity > _item.maxStackSize)
        {
            _quantity = _item.maxStackSize;
        }
    }
    
    /// <summary>
    /// Adiciona quantidade ao slot
    /// </summary>
    public int AddQuantity(int amount)
    {
        if (_item == null || amount <= 0) return 0;
        
        int maxCanAdd = _item.maxStackSize - _quantity;
        int actualAdded = Mathf.Min(amount, maxCanAdd);
        
        _quantity += actualAdded;
        return actualAdded;
    }
    
    /// <summary>
    /// Remove quantidade do slot
    /// </summary>
    public int RemoveQuantity(int amount)
    {
        if (amount <= 0) return 0;
        
        int actualRemoved = Mathf.Min(amount, _quantity);
        _quantity -= actualRemoved;
        
        if (_quantity <= 0)
        {
            Clear();
        }
        
        return actualRemoved;
    }
    
    /// <summary>
    /// Limpa o slot
    /// </summary>
    public void Clear()
    {
        _item = null;
        _quantity = 0;
    }
    
    /// <summary>
    /// Verifica se pode empilhar com outro item
    /// </summary>
    public bool CanStackWith(Item otherItem)
    {
        if (!HasItem || otherItem == null) return false;
        return _item.CanStackWith(otherItem) && _quantity < _item.maxStackSize;
    }
    
    /// <summary>
    /// Obtém espaço disponível para empilhamento
    /// </summary>
    public int GetAvailableSpace()
    {
        if (!HasItem) return 0;
        return _item.maxStackSize - _quantity;
    }
    
    /// <summary>
    /// Cria uma cópia do slot
    /// </summary>
    public InventorySlot CreateCopy()
    {
        return new InventorySlot(_item, _quantity);
    }
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public InventorySlotData GetSaveData()
    {
        return new InventorySlotData
        {
            itemID = HasItem ? _item.itemID : -1,
            quantity = _quantity
        };
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public void LoadSaveData(InventorySlotData data)
    {
        if (data == null || data.itemID < 0)
        {
            Clear();
            return;
        }
        
        // Aqui você precisaria de um sistema para carregar itens por ID
        // Por exemplo, um ItemDatabase
        Item loadedItem = ItemDatabase.GetItemByID(data.itemID);
        SetItem(loadedItem, data.quantity);
    }
    
    /// <summary>
    /// Converte para string para debug
    /// </summary>
    public override string ToString()
    {
        if (!HasItem)
            return "Empty Slot";
        
        return $"{_item.itemName} x{_quantity}";
    }
}

/// <summary>
/// Dados de salvamento do slot
/// </summary>
[System.Serializable]
public class InventorySlotData
{
    public int itemID = -1;
    public int quantity = 0;
}

/// <summary>
/// Database de itens (placeholder - implementar conforme necessário)
/// </summary>
public static class ItemDatabase
{
    // Esta seria uma implementação mais robusta em um jogo real
    // Por enquanto, retorna null como placeholder
    public static Item GetItemByID(int itemID)
    {
        // Implementar carregamento de item por ID
        // Pode ser de Resources, ScriptableObjects, etc.
        return Resources.Load<Item>($"Items/Item_{itemID}");
    }
}