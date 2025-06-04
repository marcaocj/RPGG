using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Script de exemplo mostrando como configurar um inimigo completo
/// </summary>
public class EnemySetupExample : MonoBehaviour
{
    [Header("Configuração do Inimigo")]
    public string enemyName = "Goblin";
    public int level = 1;
    
    void Start()
    {
        SetupEnemyComponents();
    }
    
    void SetupEnemyComponents()
    {
        // 1. Configurar NavMeshAgent (necessário para EnemyController)
        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
        }
        
        // 2. Configurar EnemyStats
        EnemyStats enemyStats = GetComponent<EnemyStats>();
        if (enemyStats == null)
        {
            enemyStats = gameObject.AddComponent<EnemyStats>();
        }
        
        // Configurar stats baseado no tipo de inimigo
        ConfigureStatsForEnemyType(enemyStats);
        
        // 3. Configurar EnemyController
        EnemyController enemyController = GetComponent<EnemyController>();
        if (enemyController == null)
        {
            enemyController = gameObject.AddComponent<EnemyController>();
        }
        
        // 4. Configurar EnemyAI
        EnemyAI enemyAI = GetComponent<EnemyAI>();
        if (enemyAI == null)
        {
            enemyAI = gameObject.AddComponent<EnemyAI>();
        }
        
        // Configurar IA baseado no comportamento desejado
        ConfigureAIForEnemyType(enemyAI);
        
        // 5. Configurar tags e layers
        gameObject.tag = "Enemy";
        gameObject.layer = LayerMask.NameToLayer("Enemy"); // Assumindo que existe uma layer "Enemy"
        
        // 6. Configurar colliders se necessário
        SetupColliders();
    }
    
    void ConfigureStatsForEnemyType(EnemyStats stats)
    {
        switch (enemyName.ToLower())
        {
            case "goblin":
                stats.enemyLevel = level;
                stats.maxHealth = 50f * level;
                stats.damage = 10 * level;
                stats.armor = 2 * level;
                stats.movementSpeed = 3.5f;
                stats.attackSpeed = 1.2f;
                stats.criticalChance = 5f;
                stats.experienceReward = 15 * level;
                stats.goldReward = 5 * level;
                break;
                
            case "orc":
                stats.enemyLevel = level;
                stats.maxHealth = 100f * level;
                stats.damage = 15 * level;
                stats.armor = 5 * level;
                stats.movementSpeed = 2.8f;
                stats.attackSpeed = 0.8f;
                stats.criticalChance = 3f;
                stats.experienceReward = 25 * level;
                stats.goldReward = 10 * level;
                break;
                
            case "skeleton":
                stats.enemyLevel = level;
                stats.maxHealth = 30f * level;
                stats.damage = 12 * level;
                stats.armor = 1 * level;
                stats.movementSpeed = 4f;
                stats.attackSpeed = 1.5f;
                stats.criticalChance = 8f;
                stats.experienceReward = 12 * level;
                stats.goldReward = 3 * level;
                // Resistência a poison
                stats.poisonResistance = 50f;
                break;
        }
    }
    
    void ConfigureAIForEnemyType(EnemyAI ai)
    {
        switch (enemyName.ToLower())
        {
            case "goblin":
                // Goblins são covardes
                ai.shouldFlee = true;
                ai.fleeHealthThreshold = 0.3f;
                ai.detectionRange = 6f;
                ai.attackRange = 1.5f;
                ai.shouldPatrol = true;
                ai.patrolRange = 8f;
                ai.canCallForHelp = true;
                break;
                
            case "orc":
                // Orcs são agressivos
                ai.shouldFlee = false;
                ai.detectionRange = 10f;
                ai.attackRange = 2f;
                ai.shouldPatrol = true;
                ai.patrolRange = 12f;
                ai.canCallForHelp = true;
                ai.combatTimeout = 15f; // Persistem mais tempo
                break;
                
            case "skeleton":
                // Skeletons são implacáveis
                ai.shouldFlee = false;
                ai.detectionRange = 8f;
                ai.attackRange = 1.8f;
                ai.shouldPatrol = false; // Ficam parados até detectar
                ai.canCallForHelp = false;
                ai.combatTimeout = 20f; // Nunca desistem
                break;
        }
    }
    
    void SetupColliders()
    {
        // Collider principal para física
        CapsuleCollider mainCollider = GetComponent<CapsuleCollider>();
        if (mainCollider == null)
        {
            mainCollider = gameObject.AddComponent<CapsuleCollider>();
            mainCollider.height = 2f;
            mainCollider.radius = 0.5f;
            mainCollider.center = new Vector3(0, 1f, 0);
        }
        
        // Trigger collider para detecção de ataques do jogador
        SphereCollider triggerCollider = gameObject.AddComponent<SphereCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.radius = 0.8f;
        triggerCollider.center = new Vector3(0, 1f, 0);
    }
    
    // Métodos para teste
    [ContextMenu("Test Take Damage")]
    void TestTakeDamage()
    {
        EnemyStats stats = GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.TakeDamage(25f, transform.position + Vector3.forward);
        }
    }
    
    [ContextMenu("Test Force Kill")]
    void TestForceKill()
    {
        EnemyStats stats = GetComponent<EnemyStats>();
        if (stats != null)
        {
            stats.ForceKill();
        }
    }
    
    [ContextMenu("Debug AI Info")]
    void TestDebugAI()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null)
        {
            ai.DebugAIInfo();
        }
    }
}