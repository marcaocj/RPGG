using UnityEngine;
using System;

/// <summary>
/// Gerencia todos os inputs do jogador (movimento, ataque, inventário, etc.)
/// </summary>
public class InputManager : Singleton<InputManager>
{
    [Header("Input Settings")]
    public bool inputEnabled = true;
    
    // Eventos de Input
    public static event Action<Vector2> OnMovementInput;
    public static event Action<Vector3> OnMousePositionInput;
    public static event Action OnPrimaryAttackInput;
    public static event Action OnSecondaryAttackInput;
    public static event Action<int> OnSkillInput; // número da skill (1-8)
    public static event Action OnInventoryInput;
    public static event Action OnInteractInput;
    public static event Action OnRunInput;
    public static event Action OnRunInputReleased;
    
    // Input states
    private Vector2 movementInput;
    private Vector3 mouseWorldPosition;
    private bool isRunning;
    
    // Configurações de input
    [Header("Movement Keys")]
    public KeyCode moveUpKey = KeyCode.W;
    public KeyCode moveDownKey = KeyCode.S;
    public KeyCode moveLeftKey = KeyCode.A;
    public KeyCode moveRightKey = KeyCode.D;
    
    [Header("Action Keys")]
    public KeyCode runKey = KeyCode.LeftShift;
    public KeyCode inventoryKey = KeyCode.I;
    public KeyCode interactKey = KeyCode.E;
    
    [Header("Skill Keys")]
    public KeyCode skill1Key = KeyCode.Alpha1;
    public KeyCode skill2Key = KeyCode.Alpha2;
    public KeyCode skill3Key = KeyCode.Alpha3;
    public KeyCode skill4Key = KeyCode.Alpha4;
    public KeyCode skill5Key = KeyCode.Q;
    public KeyCode skill6Key = KeyCode.W;
    public KeyCode skill7Key = KeyCode.E;
    public KeyCode skill8Key = KeyCode.R;
    
    private Camera mainCamera;
    
    protected override void Awake()
    {
        base.Awake();
        mainCamera = Camera.main;
    }
    
    private void Start()
    {
        if (mainCamera == null)
        {
            mainCamera = FindObjectOfType<Camera>();
        }
    }
    
    private void Update()
    {
        if (!inputEnabled || GameManager.Instance.IsGamePaused) return;
        
        HandleMovementInput();
        HandleMouseInput();
        HandleActionInput();
        HandleSkillInput();
        HandleUIInput();
    }
    
    private void HandleMovementInput()
    {
        float horizontal = 0f;
        float vertical = 0f;
        
        // Movimento horizontal
        if (Input.GetKey(moveRightKey))
            horizontal += 1f;
        if (Input.GetKey(moveLeftKey))
            horizontal -= 1f;
            
        // Movimento vertical
        if (Input.GetKey(moveUpKey))
            vertical += 1f;
        if (Input.GetKey(moveDownKey))
            vertical -= 1f;
        
        movementInput = new Vector2(horizontal, vertical).normalized;
        OnMovementInput?.Invoke(movementInput);
        
        // Run input
        if (Input.GetKeyDown(runKey))
        {
            isRunning = true;
            OnRunInput?.Invoke();
        }
        else if (Input.GetKeyUp(runKey))
        {
            isRunning = false;
            OnRunInputReleased?.Invoke();
        }
    }
    
    private void HandleMouseInput()
    {
        if (mainCamera == null) return;
        
        // Converter posição do mouse para world position
        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = mainCamera.transform.position.y; // Distância da câmera
        
        Ray ray = mainCamera.ScreenPointToRay(mouseScreenPosition);
        
        // Raycast para o plano do chão (assumindo Y = 0)
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            mouseWorldPosition = hit.point;
        }
        else
        {
            // Fallback: calcular posição no plano Y = 0
            float distance = -ray.origin.y / ray.direction.y;
            mouseWorldPosition = ray.origin + ray.direction * distance;
        }
        
        OnMousePositionInput?.Invoke(mouseWorldPosition);
        
        // Ataques com mouse
        if (Input.GetMouseButtonDown(0)) // Botão esquerdo
        {
            OnPrimaryAttackInput?.Invoke();
        }
        
        if (Input.GetMouseButtonDown(1)) // Botão direito
        {
            OnSecondaryAttackInput?.Invoke();
        }
    }
    
    private void HandleActionInput()
    {
        // Inventário
        if (Input.GetKeyDown(inventoryKey))
        {
            OnInventoryInput?.Invoke();
        }
        
        // Interação
        if (Input.GetKeyDown(interactKey))
        {
            OnInteractInput?.Invoke();
        }
    }
    
    private void HandleSkillInput()
    {
        // Skills 1-4 (números)
        if (Input.GetKeyDown(skill1Key))
            OnSkillInput?.Invoke(1);
        if (Input.GetKeyDown(skill2Key))
            OnSkillInput?.Invoke(2);
        if (Input.GetKeyDown(skill3Key))
            OnSkillInput?.Invoke(3);
        if (Input.GetKeyDown(skill4Key))
            OnSkillInput?.Invoke(4);
            
        // Skills 5-8 (QWER)
        if (Input.GetKeyDown(skill5Key))
            OnSkillInput?.Invoke(5);
        if (Input.GetKeyDown(skill6Key))
            OnSkillInput?.Invoke(6);
        if (Input.GetKeyDown(skill7Key))
            OnSkillInput?.Invoke(7);
        if (Input.GetKeyDown(skill8Key))
            OnSkillInput?.Invoke(8);
    }
    
    private void HandleUIInput()
    {
        // Inputs específicos de UI podem ser adicionados aqui
        // Por exemplo, teclas de atalho para diferentes painéis
    }
    
    // Métodos públicos para acessar estados de input
    public Vector2 GetMovementInput() => movementInput;
    public Vector3 GetMouseWorldPosition() => mouseWorldPosition;
    public bool IsRunning() => isRunning;
    
    // Métodos para habilitar/desabilitar input
    public void EnableInput()
    {
        inputEnabled = true;
    }
    
    public void DisableInput()
    {
        inputEnabled = false;
        movementInput = Vector2.zero;
        OnMovementInput?.Invoke(movementInput);
    }
    
    // Método para verificar se uma tecla específica foi pressionada
    public bool GetKeyDown(KeyCode key)
    {
        return inputEnabled && Input.GetKeyDown(key);
    }
    
    public bool GetKey(KeyCode key)
    {
        return inputEnabled && Input.GetKey(key);
    }
    
    public bool GetKeyUp(KeyCode key)
    {
        return inputEnabled && Input.GetKeyUp(key);
    }
    
    private void OnDestroy()
    {
        // Limpar eventos
        OnMovementInput = null;
        OnMousePositionInput = null;
        OnPrimaryAttackInput = null;
        OnSecondaryAttackInput = null;
        OnSkillInput = null;
        OnInventoryInput = null;
        OnInteractInput = null;
        OnRunInput = null;
        OnRunInputReleased = null;
    }
}