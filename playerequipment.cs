using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gerencia os equipamentos do jogador (slots de equipamento)
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    [Header("Equipment Slots")]
    [SerializeField] private Dictionary<EquipmentSlot, Equipment> equippedItems = new Dictionary<EquipmentSlot, Equipment>();
    
    [Header("Visual Equipment")]
    public Transform weaponAttachPoint;
    public Transform shieldAttachPoint;
    public Transform helmetAttachPoint;
    
    // Eventos
    public System.Action<Equipment, EquipmentSlot> OnItemEquipped;
    public System.Action<Equipment, EquipmentSlot> OnItemUnequipped;
    public System.Action OnEquipmentChanged;
    
    // Componentes
    private PlayerStats playerStats;
    private PlayerInventory playerInventory;
    private PlayerAnimationController animationController;
    
    // GameObjects visuais dos equipamentos
    private Dictionary<EquipmentSlot, GameObject> visualEquipment = new Dictionary<EquipmentSlot, GameObject>();
    
    private void Awake()
    {
        // Obter componentes
        playerStats = GetComponent<PlayerStats>();
        playerInventory = GetComponent<PlayerInventory>();
        animationController = GetComponent<PlayerAnimationController>();
        
        // Inicializar dicionários
        InitializeEquipmentSlots();
    }
    
    private void Start()
    {
        // Configurar pontos de anexo se não estiverem definidos
        SetupAttachPoints();
    }
    
    private void InitializeEquipmentSlots()
    {
        // Inicializar todos os slots como vazios
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            equippedItems[slot] = null;
            visualEquipment[slot] = null;
        }
    }
    
    private void SetupAttachPoints()
    {
        // Criar pontos de anexo se não existirem
        if (weaponAttachPoint == null)
        {
            GameObject weaponPoint = new GameObject("WeaponAttachPoint");
            weaponPoint.transform.SetParent(transform);
            weaponAttachPoint = weaponPoint.transform;
        }
        
        if (shieldAttachPoint == null)
        {
            GameObject shieldPoint = new GameObject("ShieldAttachPoint");
            shieldPoint.transform.SetParent(transform);
            shieldAttachPoint = shieldPoint.transform;
        }
        
        if (helmetAttachPoint == null)
        {
            GameObject helmetPoint = new GameObject("HelmetAttachPoint");
            helmetPoint.transform.SetParent(transform);
            helmetAttachPoint = helmetPoint.transform;
        }
    }
    
    #region Equipment Management
    
    /// <summary>
    /// Equipa um item
    /// </summary>
    public bool EquipItem(Equipment equipment)
    {
        if (equipment == null) return false;
        
        // Verificar se pode equipar
        if (!CanEquipItem(equipment))
        {
            Debug.Log($"Não é possível equipar {equipment.itemName}");
            return false;
        }
        
        EquipmentSlot targetSlot = equipment.equipmentSlot;
        
        // Desequipar item atual se houver
        if (equippedItems[targetSlot] != null)
        {
            UnequipItem(targetSlot);
        }
        
        // Equipar novo item
        equippedItems[targetSlot] = equipment;
        
        // Aplicar bônus do equipamento
        if (playerStats != null)
        {
            equipment.ApplyBonuses(playerStats);
        }
        
        // Criar visual do equipamento
        CreateEquipmentVisual(equipment, targetSlot);
        
        // Atualizar animações se for arma
        UpdateWeaponAnimations(equipment);
        
        // Disparar eventos
        OnItemEquipped?.Invoke(equipment, targetSlot);
        OnEquipmentChanged?.Invoke();
        
        Debug.Log($"Equipado: {equipment.itemName} no slot {targetSlot}");
        return true;
    }
    
    /// <summary>
    /// Desequipa um item
    /// </summary>
    public bool UnequipItem(EquipmentSlot slot)
    {
        if (!equippedItems.ContainsKey(slot) || equippedItems[slot] == null)
        {
            return false;
        }
        
        Equipment equipment = equippedItems[slot];
        
        // Remover bônus do equipamento
        if (playerStats != null)
        {
            equipment.RemoveBonuses(playerStats);
        }
        
        // Remover visual
        DestroyEquipmentVisual(slot);
        
        // Tentar adicionar de volta ao inventário
        if (playerInventory != null)
        {
            if (!playerInventory.AddItem(equipment, 1))
            {
                // Se inventário está cheio, dropar no chão
                DropItemOnGround(equipment);
            }
        }
        
        // Remover do slot
        equippedItems[slot] = null;
        
        // Disparar eventos
        OnItemUnequipped?.Invoke(equipment, slot);
        OnEquipmentChanged?.Invoke();
        
        Debug.Log($"Desequipado: {equipment.itemName} do slot {slot}");
        return true;
    }
    
    /// <summary>
    /// Desequipa todos os itens
    /// </summary>
    public void UnequipAllItems()
    {
        List<EquipmentSlot> slotsToUnequip = new List<EquipmentSlot>();
        
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value != null)
            {
                slotsToUnequip.Add(kvp.Key);
            }
        }
        
        foreach (EquipmentSlot slot in slotsToUnequip)
        {
            UnequipItem(slot);
        }
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Verifica se pode equipar um item
    /// </summary>
    public bool CanEquipItem(Equipment equipment)
    {
        if (equipment == null) return false;
        
        // Verificar se atende aos requisitos
        if (playerStats != null && !equipment.CanEquip(playerStats))
        {
            return false;
        }
        
        // Verificar se o slot é válido
        if (!equippedItems.ContainsKey(equipment.equipmentSlot))
        {
            return false;
        }
        
        // Verificar armas de duas mãos
        if (equipment.equipmentSlot == EquipmentSlot.TwoHanded)
        {
            // Não pode ter nada na mão principal ou secundária
            return equippedItems[EquipmentSlot.MainHand] == null && 
                   equippedItems[EquipmentSlot.OffHand] == null;
        }
        
        if (equipment.equipmentSlot == EquipmentSlot.MainHand || equipment.equipmentSlot == EquipmentSlot.OffHand)
        {
            // Não pode ter arma de duas mãos equipada
            return equippedItems[EquipmentSlot.TwoHanded] == null;
        }
        
        return true;
    }
    
    #endregion
    
    #region Visual Equipment
    
    private void CreateEquipmentVisual(Equipment equipment, EquipmentSlot slot)
    {
        if (equipment.equipmentModel == null) return;
        
        Transform attachPoint = GetAttachPointForSlot(slot);
        if (attachPoint == null) return;
        
        // Destruir visual anterior
        DestroyEquipmentVisual(slot);
        
        // Criar novo visual
        GameObject visual = Instantiate(equipment.equipmentModel, attachPoint);
        visualEquipment[slot] = visual;
        
        // Configurar posição e rotação local
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
    }
    
    private void DestroyEquipmentVisual(EquipmentSlot slot)
    {
        if (visualEquipment.ContainsKey(slot) && visualEquipment[slot] != null)
        {
            Destroy(visualEquipment[slot]);
            visualEquipment[slot] = null;
        }
    }
    
    private Transform GetAttachPointForSlot(EquipmentSlot slot)
    {
        switch (slot)
        {
            case EquipmentSlot.MainHand:
            case EquipmentSlot.TwoHanded:
                return weaponAttachPoint;
            case EquipmentSlot.OffHand:
                return shieldAttachPoint;
            case EquipmentSlot.Helmet:
                return helmetAttachPoint;
            default:
                return transform; // Usar transform do player como fallback
        }
    }
    
    private void UpdateWeaponAnimations(Equipment equipment)
    {
        if (animationController == null || equipment.equipmentType != EquipmentType.Weapon)
            return;
        
        // Atualizar controller de animação se a arma tiver um
        if (equipment.weaponAnimatorController != null)
        {
            Animator animator = animationController.AnimatorComponent;
            if (animator != null)
            {
                animator.runtimeAnimatorController = equipment.weaponAnimatorController;
            }
        }
    }
    
    #endregion
    
    #region Item Dropping
    
    private void DropItemOnGround(Equipment equipment)
    {
        if (equipment?.worldModel == null) return;
        
        Vector3 dropPosition = transform.position + transform.forward * 2f;
        GameObject droppedItem = Instantiate(equipment.worldModel, dropPosition, Quaternion.identity);
        
        // Adicionar componente LootItem
        LootItem lootComponent = droppedItem.GetComponent<LootItem>();
        if (lootComponent == null)
        {
            lootComponent = droppedItem.AddComponent<LootItem>();
        }
        
        lootComponent.Initialize(equipment, 1);
        
        Debug.Log($"Item dropado no chão: {equipment.itemName}");
    }
    
    #endregion
    
    #region Queries
    
    /// <summary>
    /// Obtém item equipado em um slot
    /// </summary>
    public Equipment GetEquippedItem(EquipmentSlot slot)
    {
        return equippedItems.ContainsKey(slot) ? equippedItems[slot] : null;
    }
    
    /// <summary>
    /// Verifica se tem item equipado em um slot
    /// </summary>
    public bool HasItemEquipped(EquipmentSlot slot)
    {
        return GetEquippedItem(slot) != null;
    }
    
    /// <summary>
    /// Obtém todos os itens equipados
    /// </summary>
    public Dictionary<EquipmentSlot, Equipment> GetAllEquippedItems()
    {
        Dictionary<EquipmentSlot, Equipment> equipped = new Dictionary<EquipmentSlot, Equipment>();
        
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value != null)
            {
                equipped[kvp.Key] = kvp.Value;
            }
        }
        
        return equipped;
    }
    
    /// <summary>
    /// Obtém arma principal equipada
    /// </summary>
    public Equipment GetMainWeapon()
    {
        Equipment twoHanded = GetEquippedItem(EquipmentSlot.TwoHanded);
        if (twoHanded != null) return twoHanded;
        
        return GetEquippedItem(EquipmentSlot.MainHand);
    }
    
    /// <summary>
    /// Obtém arma secundária/escudo
    /// </summary>
    public Equipment GetOffHandItem()
    {
        return GetEquippedItem(EquipmentSlot.OffHand);
    }
    
    /// <summary>
    /// Calcula DPS total das armas equipadas
    /// </summary>
    public float GetTotalWeaponDPS()
    {
        float totalDPS = 0f;
        
        Equipment mainWeapon = GetMainWeapon();
        if (mainWeapon != null)
        {
            totalDPS += mainWeapon.GetDPS();
        }
        
        Equipment offHand = GetOffHandItem();
        if (offHand != null && offHand.equipmentType == EquipmentType.Weapon)
        {
            totalDPS += offHand.GetDPS() * 0.5f; // Arma secundária com 50% de eficiência
        }
        
        return totalDPS;
    }
    
    /// <summary>
    /// Obtém alcance da arma equipada
    /// </summary>
    public float GetWeaponRange()
    {
        Equipment weapon = GetMainWeapon();
        return weapon?.weaponRange ?? 1f;
    }
    
    #endregion
    
    #region Equipment Sets
    
    /// <summary>
    /// Verifica se tem set completo (exemplo de funcionalidade)
    /// </summary>
    public bool HasCompleteArmorSet()
    {
        return HasItemEquipped(EquipmentSlot.Helmet) &&
               HasItemEquipped(EquipmentSlot.Chest) &&
               HasItemEquipped(EquipmentSlot.Legs) &&
               HasItemEquipped(EquipmentSlot.Boots) &&
               HasItemEquipped(EquipmentSlot.Gloves);
    }
    
    /// <summary>
    /// Conta peças de armadura equipadas
    /// </summary>
    public int GetArmorPieceCount()
    {
        int count = 0;
        
        EquipmentSlot[] armorSlots = {
            EquipmentSlot.Helmet,
            EquipmentSlot.Chest,
            EquipmentSlot.Legs,
            EquipmentSlot.Boots,
            EquipmentSlot.Gloves
        };
        
        foreach (EquipmentSlot slot in armorSlots)
        {
            if (HasItemEquipped(slot)) count++;
        }
        
        return count;
    }
    
    #endregion
    
    #region Save/Load Support
    
    /// <summary>
    /// Obtém dados para salvamento
    /// </summary>
    public EquipmentData GetSaveData()
    {
        EquipmentData data = new EquipmentData();
        data.equippedItems = new Dictionary<EquipmentSlot, int>();
        
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value != null)
            {
                data.equippedItems[kvp.Key] = kvp.Value.itemID;
            }
        }
        
        return data;
    }
    
    /// <summary>
    /// Carrega dados salvos
    /// </summary>
    public void LoadSaveData(EquipmentData data)
    {
        if (data == null) return;
        
        // Desequipar tudo primeiro
        UnequipAllItems();
        
        // Equipar itens salvos
        foreach (var kvp in data.equippedItems)
        {
            Equipment equipment = ItemDatabase.GetItemByID(kvp.Value) as Equipment;
            if (equipment != null)
            {
                equippedItems[kvp.Key] = equipment;
                
                // Aplicar bônus
                if (playerStats != null)
                {
                    equipment.ApplyBonuses(playerStats);
                }
                
                // Criar visual
                CreateEquipmentVisual(equipment, kvp.Key);
            }
        }
        
        OnEquipmentChanged?.Invoke();
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Troca equipamentos entre dois slots (para reorganização)
    /// </summary>
    public void SwapEquipment(EquipmentSlot slot1, EquipmentSlot slot2)
    {
        if (!equippedItems.ContainsKey(slot1) || !equippedItems.ContainsKey(slot2))
            return;
        
        Equipment temp = equippedItems[slot1];
        equippedItems[slot1] = equippedItems[slot2];
        equippedItems[slot2] = temp;
        
        // Recriar visuais
        if (equippedItems[slot1] != null)
            CreateEquipmentVisual(equippedItems[slot1], slot1);
        else
            DestroyEquipmentVisual(slot1);
            
        if (equippedItems[slot2] != null)
            CreateEquipmentVisual(equippedItems[slot2], slot2);
        else
            DestroyEquipmentVisual(slot2);
        
        OnEquipmentChanged?.Invoke();
    }
    
    /// <summary>
    /// Obtém informações de tooltip para um slot
    /// </summary>
    public string GetSlotTooltip(EquipmentSlot slot)
    {
        Equipment equipment = GetEquippedItem(slot);
        if (equipment == null)
        {
            return $"Slot vazio: {slot}";
        }
        
        return equipment.GetTooltipText();
    }
    
    #endregion
    
    #region Debug
    
    /// <summary>
    /// Lista todos os itens equipados (para debug)
    /// </summary>
    public void DebugListEquippedItems()
    {
        Debug.Log("=== EQUIPAMENTOS ===");
        foreach (var kvp in equippedItems)
        {
            if (kvp.Value != null)
            {
                Debug.Log($"{kvp.Key}: {kvp.Value.itemName}");
            }
        }
        Debug.Log("==================");
    }
    
    #endregion
}

/// <summary>
/// Dados de salvamento dos equipamentos
/// </summary>
[System.Serializable]
public class EquipmentData
{
    public Dictionary<EquipmentSlot, int> equippedItems = new Dictionary<EquipmentSlot, int>();
}