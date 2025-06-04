using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Controla o movimento e ações básicas dos inimigos
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(EnemyStats))]
public class EnemyController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float baseMovementSpeed = 3.5f;
    public float runSpeedMultiplier = 1.5f;
    public float rotationSpeed = 5f;
    public float stoppingDistance = 1.5f;
    
    [Header("Physics")]
    public LayerMask groundMask = 1;
    public float groundCheckDistance = 1.1f;
    
    [Header("Animation")]
    public string movingParameter = "IsMoving";
    public string runningParameter = "IsRunning";
    public string movementSpeedParameter = "MovementSpeed";
    public string groundedParameter = "IsGrounded";
    
    // Componentes
    private NavMeshAgent navMeshAgent;
    private EnemyStats enemyStats;
    private EnemyAI enemyAI;
    private Animator animator;
    private Rigidbody rb;
    
    // Estado do movimento
    private Vector3 targetPosition;
    private GameObject targetObject;
    private bool isMoving = false;
    private bool isRunning = false;
    private bool isGrounded = true;
    private bool movementEnabled = true;
    
    // Cache de parâmetros do animator
    private int movingHash;
    private int runningHash;
    private int movementSpeedHash;
    private int groundedHash;
    
    private void Awake()
    {
        // Obter componentes
        navMeshAgent = GetComponent<NavMeshAgent>();
        enemyStats = GetComponent<EnemyStats>();
        enemyAI = GetComponent<EnemyAI>();
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        
        // Cache dos hashes dos parâmetros do animator
        if (animator != null)
        {
            movingHash = Animator.StringToHash(movingParameter);
            runningHash = Animator.StringToHash(runningParameter);
            movementSpeedHash = Animator.StringToHash(movementSpeedParameter);
            groundedHash = Animator.StringToHash(groundedParameter);
        }
    }
    
    private void Start()
    {
        SetupNavMeshAgent();
        
        // Inscrever nos eventos do EnemyStats
        if (enemyStats != null)
        {
            enemyStats.OnDeath += HandleDeath;
            enemyStats.OnDamageTaken += HandleDamageTaken;
        }
    }
    
    private void Update()
    {
        if (!movementEnabled || !enemyStats.IsAlive) return;
        
        CheckGrounded();
        UpdateMovement();
        UpdateAnimations();
    }
    
    #region Setup
    
    private void SetupNavMeshAgent()
    {
        if (navMeshAgent != null && enemyStats != null)
        {
            navMeshAgent.speed = enemyStats.movementSpeed;
            navMeshAgent.stoppingDistance = stoppingDistance;
            navMeshAgent.acceleration = 8f;
            navMeshAgent.angularSpeed = rotationSpeed * 60f; // Converter para graus por segundo
        }
    }
    
    #endregion
    
    #region Movement Control
    
    /// <summary>
    /// Move para uma posição específica
    /// </summary>
    public void MoveTo(Vector3 destination)
    {
        if (!movementEnabled || navMeshAgent == null) return;
        
        targetPosition = destination;
        targetObject = null;
        
        navMeshAgent.SetDestination(destination);
        isMoving = true;
    }
    
    /// <summary>
    /// Segue um objeto alvo
    /// </summary>
    public void MoveToTarget(GameObject target)
    {
        if (!movementEnabled || target == null) return;
        
        targetObject = target;
        MoveTo(target.transform.position);
    }
    
    /// <summary>
    /// Para o movimento
    /// </summary>
    public void StopMovement()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.ResetPath();
        }
        
        isMoving = false;
        isRunning = false;
        targetObject = null;
    }
    
    /// <summary>
    /// Define se deve correr
    /// </summary>
    public void SetRunning(bool running)
    {
        isRunning = running;
        
        if (navMeshAgent != null && enemyStats != null)
        {
            float speed = enemyStats.movementSpeed;
            if (running)
            {
                speed *= runSpeedMultiplier;
            }
            
            navMeshAgent.speed = speed;
        }
    }
    
    /// <summary>
    /// Habilita/desabilita movimento
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        
        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = enabled;
        }
        
        if (!enabled)
        {
            StopMovement();
        }
    }
    
    /// <summary>
    /// Aplica knockback
    /// </summary>
    public void ApplyKnockback(Vector3 force, float duration = 0.3f)
    {
        if (rb != null)
        {
            // Desabilitar temporariamente o NavMeshAgent
            if (navMeshAgent != null)
            {
                navMeshAgent.enabled = false;
            }
            
            // Aplicar força
            rb.AddForce(force, ForceMode.Impulse);
            
            // Reativar NavMeshAgent após o knockback
            TimeManager.Instance?.CreateTimer(duration, () => {
                if (navMeshAgent != null && enemyStats.IsAlive)
                {
                    navMeshAgent.enabled = true;
                }
            });
        }
    }
    
    #endregion
    
    #region Movement Update
    
    private void UpdateMovement()
    {
        if (navMeshAgent == null) return;
        
        // Verificar se chegou ao destino
        if (isMoving && !navMeshAgent.pathPending)
        {
            if (navMeshAgent.remainingDistance < 0.5f)
            {
                if (!navMeshAgent.hasPath || navMeshAgent.velocity.sqrMagnitude < 0.1f)
                {
                    isMoving = false;
                    
                    // Notificar AI que chegou ao destino
                    if (enemyAI != null)
                    {
                        enemyAI.OnReachedDestination();
                    }
                }
            }
        }
        
        // Atualizar target se seguindo um objeto
        if (targetObject != null && isMoving)
        {
            float distanceToTarget = Vector3.Distance(transform.position, targetObject.transform.position);
            
            // Atualizar destino se o alvo se moveu significativamente
            if (distanceToTarget > navMeshAgent.stoppingDistance + 2f)
            {
                MoveTo(targetObject.transform.position);
            }
        }
        
        // Rotacionar para a direção do movimento se não estiver usando NavMeshAgent para rotação
        if (isMoving && navMeshAgent.velocity.magnitude > 0.1f)
        {
            Vector3 direction = navMeshAgent.velocity.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    private void CheckGrounded()
    {
        // Verificar se está no chão
        RaycastHit hit;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        
        isGrounded = Physics.Raycast(rayOrigin, Vector3.down, out hit, groundCheckDistance, groundMask);
    }
    
    #endregion
    
    #region Animation
    
    private void UpdateAnimations()
    {
        if (animator == null) return;
        
        // Calcular velocidade de movimento
        float movementSpeed = navMeshAgent != null ? navMeshAgent.velocity.magnitude : 0f;
        float normalizedSpeed = movementSpeed / (enemyStats?.movementSpeed ?? 1f);
        
        // Atualizar parâmetros do animator
        if (HasParameter(movingHash))
        {
            animator.SetBool(movingHash, isMoving && movementSpeed > 0.1f);
        }
        
        if (HasParameter(runningHash))
        {
            animator.SetBool(runningHash, isRunning);
        }
        
        if (HasParameter(movementSpeedHash))
        {
            animator.SetFloat(movementSpeedHash, normalizedSpeed);
        }
        
        if (HasParameter(groundedHash))
        {
            animator.SetBool(groundedHash, isGrounded);
        }
    }
    
    private bool HasParameter(int paramHash)
    {
        if (animator == null || animator.parameters.Length == 0) return false;
        
        foreach (var param in animator.parameters)
        {
            if (param.nameHash == paramHash)
                return true;
        }
        return false;
    }
    
    #endregion
    
    #region Combat Integration
    
    /// <summary>
    /// Rotaciona para olhar para um alvo
    /// </summary>
    public void LookAtTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // Manter rotação apenas no plano horizontal
        
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Para movimento para realizar ataque
    /// </summary>
    public void PrepareForAttack()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
        }
    }
    
    /// <summary>
    /// Retoma movimento após ataque
    /// </summary>
    public void ResumeMovementAfterAttack()
    {
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = false;
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void HandleDeath()
    {
        // Parar movimento quando morrer
        SetMovementEnabled(false);
        
        // Trigger animação de morte
        if (animator != null)
        {
            animator.SetTrigger("Death");
        }
    }
    
    private void HandleDamageTaken(float damage, Vector3 damagePosition)
    {
        // Notificar AI sobre dano recebido
        if (enemyAI != null)
        {
            enemyAI.OnTakeDamage(damage, damagePosition);
        }
        
        // Trigger animação de hit se houver
        if (animator != null && HasParameter(Animator.StringToHash("Hit")))
        {
            animator.SetTrigger("Hit");
        }
    }
    
    #endregion
    
    #region Public Properties
    
    public bool IsMoving => isMoving;
    public bool IsRunning => isRunning;
    public bool IsGrounded => isGrounded;
    public bool MovementEnabled => movementEnabled;
    public Vector3 Velocity => navMeshAgent?.velocity ?? Vector3.zero;
    public float CurrentSpeed => navMeshAgent?.velocity.magnitude ?? 0f;
    public GameObject TargetObject => targetObject;
    public Vector3 TargetPosition => targetPosition;
    public float DistanceToTarget => targetObject ? Vector3.Distance(transform.position, targetObject.transform.position) : float.MaxValue;
    
    #endregion
    
    #region Pathfinding Queries
    
    /// <summary>
    /// Verifica se uma posição é acessível via pathfinding
    /// </summary>
    public bool IsPositionReachable(Vector3 position)
    {
        if (navMeshAgent == null) return false;
        
        NavMeshPath path = new NavMeshPath();
        return navMeshAgent.CalculatePath(position, path) && path.status == NavMeshPathStatus.PathComplete;
    }
    
    /// <summary>
    /// Obtém a distância do caminho até uma posição
    /// </summary>
    public float GetPathDistanceTo(Vector3 position)
    {
        if (navMeshAgent == null) return float.MaxValue;
        
        NavMeshPath path = new NavMeshPath();
        if (navMeshAgent.CalculatePath(position, path) && path.status == NavMeshPathStatus.PathComplete)
        {
            float distance = 0f;
            for (int i = 1; i < path.corners.Length; i++)
            {
                distance += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            }
            return distance;
        }
        
        return float.MaxValue;
    }
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Desenhar destino atual
        if (isMoving)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(targetPosition, 0.5f);
            Gizmos.DrawLine(transform.position, targetPosition);
        }
        
        // Desenhar stopping distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
        
        // Desenhar caminho do NavMeshAgent
        if (navMeshAgent != null && navMeshAgent.hasPath)
        {
            Gizmos.color = Color.green;
            Vector3[] corners = navMeshAgent.path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                Gizmos.DrawLine(corners[i - 1], corners[i]);
            }
        }
        
        // Desenhar ground check
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawRay(rayOrigin, Vector3.down * groundCheckDistance);
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        if (enemyStats != null)
        {
            enemyStats.OnDeath -= HandleDeath;
            enemyStats.OnDamageTaken -= HandleDamageTaken;
        }
    }
}