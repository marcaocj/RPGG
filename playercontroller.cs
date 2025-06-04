using UnityEngine;

/// <summary>
/// Controla o movimento, rotação e ações básicas do jogador
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseMovementSpeed = 5f;
    public float runSpeedMultiplier = 1.5f;
    public float rotationSpeed = 10f;
    
    [Header("Physics")]
    public float gravity = -9.81f;
    public float jumpHeight = 1f;
    public LayerMask groundMask = 1;
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    
    [Header("Mouse Controls")]
    public bool useMouseForMovement = true;
    public bool useMouseForRotation = true;
    public LayerMask mouseTargetLayer = 1;
    
    // Componentes
    private CharacterController characterController;
    private PlayerStats playerStats;
    private PlayerAnimationController animationController;
    
    // Estado do movimento
    private Vector3 velocity;
    private Vector3 moveDirection;
    private Vector3 mouseWorldPosition;
    private bool isGrounded;
    private bool isRunning;
    private bool isMoving;
    
    // Input
    private Vector2 movementInput;
    
    private void Awake()
    {
        // Obter componentes
        characterController = GetComponent<CharacterController>();
        playerStats = GetComponent<PlayerStats>();
        animationController = GetComponent<PlayerAnimationController>();
        
        // Configurar groundCheck se não estiver definido
        if (groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = new Vector3(0, -1f, 0);
            groundCheck = groundCheckObj.transform;
        }
    }
    
    private void Start()
    {
        // Inscrever nos eventos de input
        SubscribeToInputEvents();
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleMovement();
        HandleRotation();
        HandleGravity();
        ApplyMovement();
        UpdateAnimations();
    }
    
    private void SubscribeToInputEvents()
    {
        if (InputManager.Instance != null)
        {
            InputManager.OnMovementInput += HandleMovementInput;
            InputManager.OnMousePositionInput += HandleMousePositionInput;
            InputManager.OnRunInput += StartRunning;
            InputManager.OnRunInputReleased += StopRunning;
        }
    }
    
    #region Input Handlers
    
    private void HandleMovementInput(Vector2 input)
    {
        movementInput = input;
    }
    
    private void HandleMousePositionInput(Vector3 worldPos)
    {
        mouseWorldPosition = worldPos;
    }
    
    private void StartRunning()
    {
        isRunning = true;
    }
    
    private void StopRunning()
    {
        isRunning = false;
    }
    
    #endregion
    
    #region Movement
    
    private void CheckGrounded()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Pequeno valor negativo para manter no chão
        }
    }
    
    private void HandleMovement()
    {
        // Calcular direção do movimento
        if (useMouseForMovement && movementInput.magnitude > 0.1f)
        {
            // Movimento híbrido: WASD + direção do mouse
            Vector3 keyboardDirection = new Vector3(movementInput.x, 0, movementInput.y).normalized;
            moveDirection = keyboardDirection;
        }
        else if (movementInput.magnitude > 0.1f)
        {
            // Movimento só com teclado
            moveDirection = new Vector3(movementInput.x, 0, movementInput.y).normalized;
        }
        else
        {
            moveDirection = Vector3.zero;
        }
        
        // Verificar se está se movendo
        isMoving = moveDirection.magnitude > 0.1f;
    }
    
    private void HandleRotation()
    {
        if (!useMouseForRotation) return;
        
        // Calcular direção para o mouse
        Vector3 directionToMouse = (mouseWorldPosition - transform.position).normalized;
        directionToMouse.y = 0; // Manter rotação apenas no plano horizontal
        
        if (directionToMouse.magnitude > 0.1f)
        {
            // Rotacionar suavemente para a direção do mouse
            Quaternion targetRotation = Quaternion.LookRotation(directionToMouse);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    private void HandleGravity()
    {
        if (!isGrounded)
        {
            velocity.y += gravity * Time.deltaTime;
        }
    }
    
    private void ApplyMovement()
    {
        // Calcular velocidade final baseada nos stats
        float finalSpeed = CalculateFinalMovementSpeed();
        
        // Aplicar movimento horizontal
        Vector3 horizontalMovement = moveDirection * finalSpeed;
        
        // Combinar movimento horizontal com velocidade vertical
        Vector3 finalMovement = horizontalMovement + new Vector3(0, velocity.y, 0);
        
        // Aplicar movimento
        characterController.Move(finalMovement * Time.deltaTime);
    }
    
    private float CalculateFinalMovementSpeed()
    {
        float speed = baseMovementSpeed;
        
        // Aplicar modificador de corrida
        if (isRunning)
        {
            speed *= runSpeedMultiplier;
        }
        
        // Aplicar bônus de velocidade dos stats
        if (playerStats != null)
        {
            speed = playerStats.FinalMovementSpeed;
        }
        
        return speed;
    }
    
    #endregion
    
    #region Animation Updates
    
    private void UpdateAnimations()
    {
        if (animationController == null) return;
        
        // Atualizar parâmetros de animação
        animationController.SetMoving(isMoving);
        animationController.SetRunning(isRunning);
        animationController.SetMovementSpeed(moveDirection.magnitude);
        animationController.SetGrounded(isGrounded);
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Move o jogador para uma posição específica (para cutscenes, teleporte, etc.)
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = true;
        velocity = Vector3.zero;
    }
    
    /// <summary>
    /// Aplica um impulso de knockback
    /// </summary>
    public void ApplyKnockback(Vector3 force, float duration = 0.3f)
    {
        StartCoroutine(KnockbackCoroutine(force, duration));
    }
    
    private System.Collections.IEnumerator KnockbackCoroutine(Vector3 force, float duration)
    {
        float timer = 0f;
        Vector3 knockbackVelocity = force;
        
        while (timer < duration)
        {
            // Aplicar knockback diminuindo ao longo do tempo
            float factor = (duration - timer) / duration;
            Vector3 knockbackMovement = knockbackVelocity * factor;
            
            characterController.Move(knockbackMovement * Time.deltaTime);
            
            timer += Time.deltaTime;
            yield return null;
        }
    }
    
    /// <summary>
    /// Permite/impede o movimento do jogador
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        enabled = enabled;
        
        if (!enabled)
        {
            moveDirection = Vector3.zero;
            velocity.x = 0;
            velocity.z = 0;
        }
    }
    
    /// <summary>
    /// Faz o jogador pular (se estiver no chão)
    /// </summary>
    public void Jump()
    {
        if (isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
    }
    
    #endregion
    
    #region Collision Detection
    
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Detectar colisões com objetos específicos
        if (hit.gameObject.CompareTag("Interactable"))
        {
            // Lógica de interação pode ser adicionada aqui
        }
    }
    
    #endregion
    
    #region Properties
    
    public bool IsMoving => isMoving;
    public bool IsRunning => isRunning;
    public bool IsGrounded => isGrounded;
    public Vector3 MoveDirection => moveDirection;
    public Vector3 Velocity => velocity;
    public Vector3 MouseWorldPosition => mouseWorldPosition;
    public float CurrentSpeed => CalculateFinalMovementSpeed();
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Desenhar ground check
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
        
        // Desenhar direção do movimento
        if (isMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, moveDirection * 2f);
        }
        
        // Desenhar posição do mouse
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(mouseWorldPosition, 0.5f);
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        if (InputManager.Instance != null)
        {
            InputManager.OnMovementInput -= HandleMovementInput;
            InputManager.OnMousePositionInput -= HandleMousePositionInput;
            InputManager.OnRunInput -= StartRunning;
            InputManager.OnRunInputReleased -= StopRunning;
        }
    }
}