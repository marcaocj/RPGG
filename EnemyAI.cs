using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Estados da IA do inimigo
/// </summary>
public enum EnemyAIState
{
    Idle,           // Parado, aguardando
    Patrol,         // Patrulhando área
    Chasing,        // Perseguindo jogador
    Attacking,      // Atacando
    Returning,      // Retornando à posição inicial
    Fleeing,        // Fugindo
    Dead            // Morto
}

/// <summary>
/// Inteligência artificial dos inimigos
/// </summary>
[RequireComponent(typeof(EnemyController), typeof(EnemyStats))]
public class EnemyAI : MonoBehaviour
{
    [Header("Detection")]
    public float detectionRange = 8f;
    public float attackRange = 2f;
    public float fieldOfView = 90f;
    public LayerMask playerLayer = 1 << 6; // Layer do jogador
    public LayerMask obstacleLayer = 1 << 0; // Layer de obstáculos
    
    [Header("Combat")]
    public float attackCooldown = 2f;
    public float combatTimeout = 10f; // Tempo para sair de combate se perder o jogador
    public bool canCallForHelp = true;
    public float helpCallRange = 15f;
    
    [Header("Patrol")]
    public bool shouldPatrol = true;
    public float patrolRange = 10f;
    public float patrolWaitTime = 3f;
    public Transform[] patrolPoints;
    
    [Header("Behavior")]
    public float returnDistance = 20f; // Distância máxima da posição inicial
    public bool shouldFlee = false;
    public float fleeHealthThreshold = 0.2f; // Foge quando vida < 20%
    public float aggroDecayTime = 5f; // Tempo para esquecer o jogador
    
    [Header("Animation")]
    public string attackTrigger = "Attack";
    public string alertTrigger = "Alert";
    public string idleTrigger = "Idle";
    
    // Componentes
    private EnemyController enemyController;
    private EnemyStats enemyStats;
    private Animator animator;
    
    // Estado da IA
    private EnemyAIState currentState = EnemyAIState.Idle;
    private GameObject currentTarget;
    private Vector3 lastKnownPlayerPosition;
    private Vector3 initialPosition;
    
    // Timers
    private float lastAttackTime;
    private float lastPlayerSeenTime;
    private float stateTimer;
    private float patrolTimer;
    
    // Patrol
    private int currentPatrolIndex = 0;
    private Vector3 randomPatrolTarget;
    
    // Cache
    private Transform playerTransform;
    private PlayerStats playerStats;
    
    // Flags
    private bool isInCombat = false;
    private bool hasCalledForHelp = false;
    
    private void Awake()
    {
        // Obter componentes
        enemyController = GetComponent<EnemyController>();
        enemyStats = GetComponent<EnemyStats>();
        animator = GetComponent<Animator>();
        
        // Salvar posição inicial
        initialPosition = transform.position;
    }
    
    private void Start()
    {
        // Encontrar jogador
        FindPlayer();
        
        // Inscrever nos eventos
        SubscribeToEvents();
        
        // Iniciar IA
        ChangeState(shouldPatrol ? EnemyAIState.Patrol : EnemyAIState.Idle);
        
        // Configurar patrol inicial
        if (shouldPatrol && patrolPoints.Length == 0)
        {
            GenerateRandomPatrolTarget();
        }
    }
    
    private void Update()
    {
        if (!enemyStats.IsAlive)
        {
            if (currentState != EnemyAIState.Dead)
            {
                ChangeState(EnemyAIState.Dead);
            }
            return;
        }
        
        // Atualizar timers
        stateTimer += Time.deltaTime;
        patrolTimer += Time.deltaTime;
        
        // Detectar jogador
        DetectPlayer();
        
        // Executar comportamento baseado no estado atual
        ExecuteCurrentState();
        
        // Verificar condições de mudança de estado
        CheckStateTransitions();
    }
    
    #region Player Detection
    
    private void FindPlayer()
    {
        GameObject player = GameManager.Instance?.CurrentPlayer;
        if (player != null)
        {
            playerTransform = player.transform;
            playerStats = player.GetComponent<PlayerStats>();
        }
        
        // Fallback: procurar por tag
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
                playerStats = playerObj.GetComponent<PlayerStats>();
            }
        }
    }
    
    private void DetectPlayer()
    {
        if (playerTransform == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        
        // Verificar se está dentro do range de detecção
        if (distanceToPlayer <= detectionRange)
        {
            // Verificar campo de visão
            Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            
            if (angleToPlayer <= fieldOfView / 2f)
            {
                // Verificar se não há obstáculos
                if (HasLineOfSight(playerTransform.position))
                {
                    // Jogador detectado
                    currentTarget = playerTransform.gameObject;
                    lastKnownPlayerPosition = playerTransform.position;
                    lastPlayerSeenTime = Time.time;
                    
                    // Entrar em combate se não estava
                    if (!isInCombat && currentState != EnemyAIState.Chasing && currentState != EnemyAIState.Attacking)
                    {
                        EnterCombat();
                    }
                }
            }
        }
        
        // Verificar timeout de combate
        if (isInCombat && Time.time - lastPlayerSeenTime > combatTimeout)
        {
            ExitCombat();
        }
    }
    
    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f; // Altura dos olhos
        Vector3 rayDirection = (targetPosition - rayOrigin).normalized;
        float rayDistance = Vector3.Distance(rayOrigin, targetPosition);
        
        return !Physics.Raycast(rayOrigin, rayDirection, rayDistance, obstacleLayer);
    }
    
    #endregion
    
    #region State Machine
    
    private void ChangeState(EnemyAIState newState)
    {
        if (currentState == newState) return;
        
        // Exit current state
        ExitState(currentState);
        
        // Change state
        EnemyAIState previousState = currentState;
        currentState = newState;
        stateTimer = 0f;
        
        // Enter new state
        EnterState(newState);
        
        Debug.Log($"{gameObject.name}: {previousState} -> {newState}");
    }
    
    private void EnterState(EnemyAIState state)
    {
        switch (state)
        {
            case EnemyAIState.Idle:
                enemyController.StopMovement();
                TriggerAnimation(idleTrigger);
                break;
                
            case EnemyAIState.Patrol:
                SetNextPatrolTarget();
                break;
                
            case EnemyAIState.Chasing:
                enemyController.SetRunning(true);
                TriggerAnimation(alertTrigger);
                break;
                
            case EnemyAIState.Attacking:
                enemyController.PrepareForAttack();
                break;
                
            case EnemyAIState.Returning:
                enemyController.SetRunning(false);
                enemyController.MoveTo(initialPosition);
                break;
                
            case EnemyAIState.Fleeing:
                enemyController.SetRunning(true);
                FleeFromPlayer();
                break;
                
            case EnemyAIState.Dead:
                enemyController.SetMovementEnabled(false);
                break;
        }
    }
    
    private void ExitState(EnemyAIState state)
    {
        switch (state)
        {
            case EnemyAIState.Attacking:
                enemyController.ResumeMovementAfterAttack();
                break;
                
            case EnemyAIState.Chasing:
            case EnemyAIState.Fleeing:
                enemyController.SetRunning(false);
                break;
        }
    }
    
    private void ExecuteCurrentState()
    {
        switch (currentState)
        {
            case EnemyAIState.Idle:
                ExecuteIdleState();
                break;
                
            case EnemyAIState.Patrol:
                ExecutePatrolState();
                break;
                
            case EnemyAIState.Chasing:
                ExecuteChasingState();
                break;
                
            case EnemyAIState.Attacking:
                ExecuteAttackingState();
                break;
                
            case EnemyAIState.Returning:
                ExecuteReturningState();
                break;
                
            case EnemyAIState.Fleeing:
                ExecuteFleeingState();
                break;
        }
    }
    
    #endregion
    
    #region State Behaviors
    
    private void ExecuteIdleState()
    {
        // Ficar parado por um tempo, depois patrulhar
        if (shouldPatrol && stateTimer > patrolWaitTime)
        {
            ChangeState(EnemyAIState.Patrol);
        }
    }
    
    private void ExecutePatrolState()
    {
        // Verificar se chegou ao ponto de patrulha
        if (!enemyController.IsMoving)
        {
            if (patrolTimer >= patrolWaitTime)
            {
                SetNextPatrolTarget();
                patrolTimer = 0f;
            }
        }
    }
    
    private void ExecuteChasingState()
    {
        if (currentTarget != null)
        {
            // Perseguir jogador
            enemyController.MoveToTarget(currentTarget);
            
            // Olhar para o jogador
            enemyController.LookAtTarget(currentTarget.transform.position);
            
            // Atualizar última posição conhecida
            lastKnownPlayerPosition = currentTarget.transform.position;
        }
        else
        {
            // Ir para última posição conhecida
            enemyController.MoveTo(lastKnownPlayerPosition);
        }
    }
    
    private void ExecuteAttackingState()
    {
        if (currentTarget != null)
        {
            // Olhar para o alvo
            enemyController.LookAtTarget(currentTarget.transform.position);
            
            // Atacar se passou o cooldown
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }
        
        // Voltar a perseguir após ataque
        if (stateTimer > 1f)
        {
            ChangeState(EnemyAIState.Chasing);
        }
    }
    
    private void ExecuteReturningState()
    {
        // Verificar se chegou na posição inicial
        if (!enemyController.IsMoving || Vector3.Distance(transform.position, initialPosition) < 2f)
        {
            // Resetar estado
            currentTarget = null;
            hasCalledForHelp = false;
            ChangeState(shouldPatrol ? EnemyAIState.Patrol : EnemyAIState.Idle);
        }
    }
    
    private void ExecuteFleeingState()
    {
        // Continuar fugindo por um tempo
        if (stateTimer > 5f || enemyStats.HealthPercentage > fleeHealthThreshold)
        {
            // Parar de fugir e tentar retornar
            ChangeState(EnemyAIState.Returning);
        }
        else if (!enemyController.IsMoving)
        {
            // Continuar fugindo se parou
            FleeFromPlayer();
        }
    }
    
    #endregion
    
    #region State Transitions
    
    private void CheckStateTransitions()
    {
        // Morto
        if (!enemyStats.IsAlive)
        {
            ChangeState(EnemyAIState.Dead);
            return;
        }
        
        // Fugir se vida baixa
        if (shouldFlee && enemyStats.HealthPercentage <= fleeHealthThreshold && currentState != EnemyAIState.Fleeing)
        {
            ChangeState(EnemyAIState.Fleeing);
            return;
        }
        
        // Verificar se deve retornar
        float distanceFromHome = Vector3.Distance(transform.position, initialPosition);
        if (distanceFromHome > returnDistance && currentState != EnemyAIState.Returning)
        {
            ExitCombat();
            ChangeState(EnemyAIState.Returning);
            return;
        }
        
        // Transições baseadas em combate
        if (isInCombat && currentTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, currentTarget.transform.position);
            
            // Atacar se próximo o suficiente
            if (distanceToPlayer <= attackRange && currentState != EnemyAIState.Attacking)
            {
                ChangeState(EnemyAIState.Attacking);
            }
            // Perseguir se fora de alcance de ataque
            else if (distanceToPlayer > attackRange && currentState != EnemyAIState.Chasing)
            {
                ChangeState(EnemyAIState.Chasing);
            }
        }
    }
    
    #endregion
    
    #region Combat
    
    private void EnterCombat()
    {
        isInCombat = true;
        
        // Chamar ajuda se possível
        if (canCallForHelp && !hasCalledForHelp)
        {
            CallForHelp();
            hasCalledForHelp = true;
        }
        
        // Mudar para perseguição
        ChangeState(EnemyAIState.Chasing);
    }
    
    private void ExitCombat()
    {
        isInCombat = false;
        currentTarget = null;
        
        // Retornar ou patrulhar
        ChangeState(EnemyAIState.Returning);
    }
    
    private void PerformAttack()
    {
        if (currentTarget == null) return;
        
        // Trigger animação de ataque
        TriggerAnimation(attackTrigger);
        
        // Aplicar dano ao jogador
        PlayerStats targetStats = currentTarget.GetComponent<PlayerStats>();
        if (targetStats != null)
        {
            float damage = CalculateAttackDamage();
            targetStats.TakeDamage(damage);
            
            // Evento de dano
            EventManager.TriggerDamageDealt(damage, currentTarget.transform.position);
        }
        
        // Som de ataque
        AudioManager.Instance?.PlaySFXAtPosition(enemyStats.attackSound, transform.position);
        
        Debug.Log($"{gameObject.name} atacou {currentTarget.name} causando {CalculateAttackDamage()} de dano");
    }
    
    private float CalculateAttackDamage()
    {
        float baseDamage = enemyStats.damage;
        
        // Verificar crítico
        bool isCritical = Random.Range(0f, 100f) <= enemyStats.criticalChance;
        if (isCritical)
        {
            baseDamage *= (enemyStats.criticalDamage / 100f);
        }
        
        return baseDamage;
    }
    
    private void CallForHelp()
    {
        // Encontrar outros inimigos próximos
        Collider[] nearbyEnemies = Physics.OverlapSphere(transform.position, helpCallRange);
        
        foreach (Collider col in nearbyEnemies)
        {
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                EnemyAI otherAI = col.GetComponent<EnemyAI>();
                if (otherAI != null && !otherAI.isInCombat)
                {
                    otherAI.RespondToHelpCall(currentTarget);
                }
            }
        }
        
        Debug.Log($"{gameObject.name} chamou por ajuda!");
    }
    
    /// <summary>
    /// Responde ao chamado de ajuda de outro inimigo
    /// </summary>
    public void RespondToHelpCall(GameObject target)
    {
        if (target != null && !isInCombat)
        {
            currentTarget = target;
            lastKnownPlayerPosition = target.transform.position;
            lastPlayerSeenTime = Time.time;
            EnterCombat();
            
            Debug.Log($"{gameObject.name} respondeu ao chamado de ajuda!");
        }
    }
    
    #endregion
    
    #region Patrol
    
    private void SetNextPatrolTarget()
    {
        if (patrolPoints.Length > 0)
        {
            // Usar pontos de patrulha definidos
            currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Length;
            enemyController.MoveTo(patrolPoints[currentPatrolIndex].position);
        }
        else
        {
            // Gerar ponto aleatório
            GenerateRandomPatrolTarget();
            enemyController.MoveTo(randomPatrolTarget);
        }
    }
    
    private void GenerateRandomPatrolTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRange;
        randomDirection += initialPosition;
        randomDirection.y = initialPosition.y; // Manter na mesma altura
        
        // Verificar se o ponto é acessível
        if (enemyController.IsPositionReachable(randomDirection))
        {
            randomPatrolTarget = randomDirection;
        }
        else
        {
            // Fallback: usar posição inicial
            randomPatrolTarget = initialPosition;
        }
    }
    
    #endregion
    
    #region Flee Behavior
    
    private void FleeFromPlayer()
    {
        if (playerTransform == null) return;
        
        // Calcular direção oposta ao jogador
        Vector3 fleeDirection = (transform.position - playerTransform.position).normalized;
        Vector3 fleeTarget = transform.position + fleeDirection * 10f;
        
        // Verificar se a posição é acessível
        if (enemyController.IsPositionReachable(fleeTarget))
        {
            enemyController.MoveTo(fleeTarget);
        }
        else
        {
            // Tentar direções alternativas
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector3 alternativeDirection = Quaternion.Euler(0, angle, 0) * fleeDirection;
                Vector3 alternativeTarget = transform.position + alternativeDirection * 5f;
                
                if (enemyController.IsPositionReachable(alternativeTarget))
                {
                    enemyController.MoveTo(alternativeTarget);
                    break;
                }
            }
        }
    }
    
    #endregion
    
    #region Animation
    
    private void TriggerAnimation(string triggerName)
    {
        if (animator != null && !string.IsNullOrEmpty(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }
    
    #endregion
    
    #region Event Handlers
    
    private void SubscribeToEvents()
    {
        if (enemyStats != null)
        {
            enemyStats.OnDeath += HandleDeath;
        }
    }
    
    private void HandleDeath()
    {
        ChangeState(EnemyAIState.Dead);
    }
    
    /// <summary>
    /// Chamado pelo EnemyStats quando recebe dano
    /// </summary>
    public void OnTakeDamage(float damage, Vector3 damageSource)
    {
        // Se não estava em combate, entrar em combate
        if (!isInCombat)
        {
            // Tentar encontrar o atacante
            GameObject attacker = FindAttackerFromPosition(damageSource);
            if (attacker != null)
            {
                currentTarget = attacker;
                lastKnownPlayerPosition = attacker.transform.position;
                lastPlayerSeenTime = Time.time;
                EnterCombat();
            }
        }
        
        // Resetar timer de aggro
        lastPlayerSeenTime = Time.time;
    }
    
    /// <summary>
    /// Chamado pelo EnemyController quando chega ao destino
    /// </summary>
    public void OnReachedDestination()
    {
        // Lógica específica baseada no estado atual
        switch (currentState)
        {
            case EnemyAIState.Chasing:
                // Se chegou na última posição conhecida do jogador mas não o vê
                if (currentTarget == null || Time.time - lastPlayerSeenTime > 2f)
                {
                    // Procurar ao redor
                    StartCoroutine(SearchAroundArea());
                }
                break;
                
            case EnemyAIState.Patrol:
                // Aguardar um pouco no ponto de patrulha
                patrolTimer = 0f;
                break;
        }
    }
    
    private IEnumerator SearchAroundArea()
    {
        Vector3 searchCenter = lastKnownPlayerPosition;
        
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f;
            Vector3 searchDirection = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 searchTarget = searchCenter + searchDirection * 3f;
            
            if (enemyController.IsPositionReachable(searchTarget))
            {
                enemyController.MoveTo(searchTarget);
                
                // Aguardar chegar ao ponto
                yield return new WaitUntil(() => !enemyController.IsMoving);
                yield return new WaitForSeconds(1f);
                
                // Se encontrou o jogador durante a busca, parar
                if (currentTarget != null && Time.time - lastPlayerSeenTime < 1f)
                {
                    yield break;
                }
            }
        }
        
        // Se não encontrou o jogador, sair de combate
        if (Time.time - lastPlayerSeenTime > 5f)
        {
            ExitCombat();
        }
    }
    
    private GameObject FindAttackerFromPosition(Vector3 damageSource)
    {
        // Procurar jogador próximo à fonte do dano
        if (playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(damageSource, playerTransform.position);
            if (distanceToPlayer < 5f)
            {
                return playerTransform.gameObject;
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Força o inimigo a atacar um alvo específico
    /// </summary>
    public void SetTarget(GameObject target)
    {
        currentTarget = target;
        if (target != null)
        {
            lastKnownPlayerPosition = target.transform.position;
            lastPlayerSeenTime = Time.time;
            
            if (!isInCombat)
            {
                EnterCombat();
            }
        }
    }
    
    /// <summary>
    /// Remove o alvo atual
    /// </summary>
    public void ClearTarget()
    {
        currentTarget = null;
        ExitCombat();
    }
    
    /// <summary>
    /// Define novos pontos de patrulha
    /// </summary>
    public void SetPatrolPoints(Transform[] newPatrolPoints)
    {
        patrolPoints = newPatrolPoints;
        currentPatrolIndex = 0;
        
        if (currentState == EnemyAIState.Patrol)
        {
            SetNextPatrolTarget();
        }
    }
    
    /// <summary>
    /// Ativa/desativa patrulhamento
    /// </summary>
    public void SetPatrolEnabled(bool enabled)
    {
        shouldPatrol = enabled;
        
        if (!enabled && currentState == EnemyAIState.Patrol)
        {
            ChangeState(EnemyAIState.Idle);
        }
        else if (enabled && currentState == EnemyAIState.Idle)
        {
            ChangeState(EnemyAIState.Patrol);
        }
    }
    
    #endregion
    
    #region Public Properties
    
    public EnemyAIState CurrentState => currentState;
    public GameObject CurrentTarget => currentTarget;
    public bool IsInCombat => isInCombat;
    public Vector3 LastKnownPlayerPosition => lastKnownPlayerPosition;
    public float TimeSincePlayerSeen => Time.time - lastPlayerSeenTime;
    public float DistanceFromHome => Vector3.Distance(transform.position, initialPosition);
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Desenhar range de detecção
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        
        // Desenhar range de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // Desenhar campo de visão
        Gizmos.color = Color.blue;
        Vector3 leftBoundary = Quaternion.AngleAxis(-fieldOfView / 2f, Vector3.up) * transform.forward;
        Vector3 rightBoundary = Quaternion.AngleAxis(fieldOfView / 2f, Vector3.up) * transform.forward;
        Gizmos.DrawRay(transform.position, leftBoundary * detectionRange);
        Gizmos.DrawRay(transform.position, rightBoundary * detectionRange);
        
        // Desenhar posição inicial
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(initialPosition, 1f);
        Gizmos.DrawLine(transform.position, initialPosition);
        
        // Desenhar pontos de patrulha
        if (patrolPoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                if (patrolPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(patrolPoints[i].position, 0.5f);
                    
                    // Desenhar linha para o próximo ponto
                    int nextIndex = (i + 1) % patrolPoints.Length;
                    if (patrolPoints[nextIndex] != null)
                    {
                        Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[nextIndex].position);
                    }
                }
            }
        }
        
        // Desenhar última posição conhecida do jogador
        if (isInCombat)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownPlayerPosition, 0.5f);
            Gizmos.DrawLine(transform.position, lastKnownPlayerPosition);
        }
        
        // Desenhar range de patrulha
        if (shouldPatrol)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(initialPosition, patrolRange);
        }
        
        // Desenhar range de ajuda
        if (canCallForHelp)
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawWireSphere(transform.position, helpCallRange);
        }
    }
    
    /// <summary>
    /// Exibe informações de debug da IA
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugAIInfo()
    {
        Debug.Log($"=== {gameObject.name} AI INFO ===");
        Debug.Log($"Estado: {currentState}");
        Debug.Log($"Em Combate: {isInCombat}");
        Debug.Log($"Alvo: {(currentTarget ? currentTarget.name : "Nenhum")}");
        Debug.Log($"Distância de Casa: {DistanceFromHome:F1}");
        Debug.Log($"Tempo desde viu jogador: {TimeSincePlayerSeen:F1}s");
        Debug.Log($"Vida: {enemyStats.HealthPercentage:P}");
        Debug.Log("============================");
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        if (enemyStats != null)
        {
            enemyStats.OnDeath -= HandleDeath;
        }
    }
}

/// <summary>
/// Dados de informação de dano para comunicação entre componentes
/// </summary>
public struct DamageInfo
{
    public float damage;
    public Vector3 source;
    public DamageType damageType;
    
    public DamageInfo(float damage, Vector3 source, DamageType damageType = DamageType.Physical)
    {
        this.damage = damage;
        this.source = source;
        this.damageType = damageType;
    }
}