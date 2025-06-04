using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controla projéteis criados por skills (fireballs, flechas, etc.)
/// </summary>
public class SkillProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 10f;
    public float lifetime = 5f;
    public float damage = 25f;
    public LayerMask targetLayers = -1;
    public bool piercing = false;
    public int maxPierceTargets = 3;
    
    [Header("Homing")]
    public bool isHoming = false;
    public float homingStrength = 2f;
    public float homingRange = 10f;
    public GameObject homingTarget;
    
    [Header("Area of Effect")]
    public bool hasAOE = false;
    public float aoeRadius = 3f;
    public GameObject aoeEffectPrefab;
    
    [Header("Visual Effects")]
    public TrailRenderer trail;
    public ParticleSystem particles;
    public GameObject hitEffectPrefab;
    
    [Header("Audio")]
    public AudioClip launchSound;
    public AudioClip hitSound;
    public AudioClip flyingSound;
    
    // Dados da skill que criou este projétil
    private Skill sourceSkill;
    private GameObject caster;
    private Vector3 targetPosition;
    
    // Estado do projétil
    private Vector3 direction;
    private List<GameObject> hitTargets = new List<GameObject>();
    private bool hasHit = false;
    private float timeAlive = 0f;
    
    // Componentes
    private Rigidbody rb;
    private Collider projectileCollider;
    private AudioSource audioSource;
    
    private void Awake()
    {
        // Obter componentes
        rb = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();
        
        // Configurar collider como trigger
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }
        
        // Configurar Rigidbody
        if (rb != null)
        {
            rb.useGravity = false;
            rb.freezeRotation = true;
        }
    }
    
    private void Start()
    {
        // Tocar som de lançamento
        if (launchSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(launchSound, transform.position);
        }
        
        // Som contínuo de voo
        if (flyingSound != null && audioSource != null)
        {
            audioSource.clip = flyingSound;
            audioSource.loop = true;
            audioSource.Play();
        }
        
        // Agendar destruição após lifetime
        Destroy(gameObject, lifetime);
    }
    
    private void Update()
    {
        timeAlive += Time.deltaTime;
        
        if (!hasHit)
        {
            UpdateMovement();
            UpdateHoming();
        }
    }
    
    #region Initialization
    
    /// <summary>
    /// Inicializa o projétil com dados da skill
    /// </summary>
    public void Initialize(Skill skill, GameObject skillCaster, Vector3 target)
    {
        sourceSkill = skill;
        caster = skillCaster;
        targetPosition = target;
        
        if (skill != null)
        {
            // Configurar propriedades baseadas na skill
            damage = skill.baseDamage;
            speed = 10f; // Velocidade padrão
            
            // Configurar AOE se a skill tiver
            if (skill.areaOfEffect > 0)
            {
                hasAOE = true;
                aoeRadius = skill.areaOfEffect;
            }
        }
        
        // Calcular direção inicial
        direction = (targetPosition - transform.position).normalized;
        
        // Orientar projétil na direção do movimento
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    /// <summary>
    /// Configura projétil como homing
    /// </summary>
    public void SetHoming(GameObject target, float strength = 2f, float range = 10f)
    {
        isHoming = true;
        homingTarget = target;
        homingStrength = strength;
        homingRange = range;
    }
    
    #endregion
    
    #region Movement
    
    private void UpdateMovement()
    {
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }
        else
        {
            transform.Translate(direction * speed * Time.deltaTime, Space.World);
        }
    }
    
    private void UpdateHoming()
    {
        if (!isHoming || homingTarget == null) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, homingTarget.transform.position);
        
        // Só fazer homing se o alvo estiver dentro do range
        if (distanceToTarget <= homingRange)
        {
            Vector3 targetDirection = (homingTarget.transform.position - transform.position).normalized;
            direction = Vector3.Lerp(direction, targetDirection, homingStrength * Time.deltaTime);
            
            // Atualizar rotação
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
    
    #endregion
    
    #region Collision Detection
    
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit && !piercing) return;
        
        // Ignorar o caster
        if (other.gameObject == caster) return;
        
        // Verificar se é um alvo válido
        if (!IsValidTarget(other.gameObject)) return;
        
        // Verificar se já atingiu este alvo (para projéteis piercing)
        if (hitTargets.Contains(other.gameObject)) return;
        
        // Processar hit
        ProcessHit(other.gameObject);
        
        // Adicionar à lista de alvos atingidos
        hitTargets.Add(other.gameObject);
        
        // Verificar se deve parar
        if (!piercing || hitTargets.Count >= maxPierceTargets)
        {
            StopProjectile();
        }
    }
    
    private bool IsValidTarget(GameObject target)
    {
        // Verificar layer
        int targetLayer = 1 << target.layer;
        if ((targetLayers & targetLayer) == 0) return false;
        
        // Verificar se é um alvo apropriado baseado na skill
        if (sourceSkill != null)
        {
            switch (sourceSkill.targetType)
            {
                case TargetType.Enemy:
                    return target.CompareTag("Enemy");
                case TargetType.Ally:
                    return target.CompareTag("Player") || target.CompareTag("Ally");
                default:
                    return true;
            }
        }
        
        return true;
    }
    
    #endregion
    
    #region Hit Processing
    
    private void ProcessHit(GameObject target)
    {
        // Calcular dano final
        float finalDamage = CalculateDamage();
        
        // Aplicar dano ao alvo
        ApplyDamageToTarget(target, finalDamage);
        
        // Aplicar efeitos da skill
        ApplySkillEffects(target);
        
        // Efeitos visuais e sonoros
        CreateHitEffects(target.transform.position);
        
        // Processar AOE se habilitado
        if (hasAOE)
        {
            ProcessAOEDamage(target.transform.position);
        }
        
        Debug.Log($"Projétil atingiu {target.name} causando {finalDamage} de dano");
    }
    
    private float CalculateDamage()
    {
        float calculatedDamage = damage;
        
        // Aplicar scaling da skill se disponível
        if (sourceSkill != null && caster != null)
        {
            PlayerStats casterStats = caster.GetComponent<PlayerStats>();
            if (casterStats != null)
            {
                // Adicionar bônus baseado nos atributos do caster
                switch (sourceSkill.damageType)
                {
                    case DamageType.Physical:
                        calculatedDamage += casterStats.FinalStrength * sourceSkill.damageScaling;
                        break;
                    case DamageType.Magic:
                    case DamageType.Fire:
                    case DamageType.Cold:
                    case DamageType.Lightning:
                        calculatedDamage += casterStats.FinalIntelligence * sourceSkill.damageScaling;
                        break;
                }
            }
        }
        
        return calculatedDamage;
    }
    
    private void ApplyDamageToTarget(GameObject target, float damage)
    {
        // Aplicar dano a inimigos
        if (target.CompareTag("Enemy"))
        {
            EnemyStats enemyStats = target.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                DamageType damageType = sourceSkill?.damageType ?? DamageType.Magic;
                enemyStats.TakeDamage(damage, transform.position, damageType);
            }
        }
        // Aplicar dano/cura ao player (se for skill de cura)
        else if (target.CompareTag("Player"))
        {
            PlayerStats playerStats = target.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                if (sourceSkill?.skillType == SkillType.Heal)
                {
                    playerStats.Heal(damage);
                }
                else
                {
                    playerStats.TakeDamage(damage);
                }
            }
        }
    }
    
    private void ApplySkillEffects(GameObject target)
    {
        if (sourceSkill?.skillEffects == null) return;
        
        foreach (SkillEffect effect in sourceSkill.skillEffects)
        {
            if (effect != null)
            {
                effect.ApplyEffect(target, caster);
            }
        }
    }
    
    private void ProcessAOEDamage(Vector3 centerPosition)
    {
        // Encontrar todos os alvos na área
        Collider[] colliders = Physics.OverlapSphere(centerPosition, aoeRadius, targetLayers);
        
        foreach (Collider col in colliders)
        {
            if (col.gameObject != caster && !hitTargets.Contains(col.gameObject))
            {
                if (IsValidTarget(col.gameObject))
                {
                    // Aplicar dano reduzido para AOE
                    float aoeDamage = CalculateDamage() * 0.7f; // 70% do dano para AOE
                    ApplyDamageToTarget(col.gameObject, aoeDamage);
                    ApplySkillEffects(col.gameObject);
                }
            }
        }
        
        // Criar efeito visual de AOE
        if (aoeEffectPrefab != null)
        {
            GameObject aoeEffect = Instantiate(aoeEffectPrefab, centerPosition, Quaternion.identity);
            Destroy(aoeEffect, 3f);
        }
    }
    
    #endregion
    
    #region Visual and Audio Effects
    
    private void CreateHitEffects(Vector3 position)
    {
        // Efeito visual de impacto
        if (hitEffectPrefab != null)
        {
            GameObject hitEffect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
            Destroy(hitEffect, 2f);
        }
        
        // Som de impacto
        if (hitSound != null)
        {
            AudioManager.Instance?.PlaySFXAtPosition(hitSound, position);
        }
        
        // Parar efeitos do projétil
        if (particles != null)
        {
            particles.Stop();
        }
        
        if (trail != null)
        {
            trail.enabled = false;
        }
    }
    
    #endregion
    
    #region Projectile Control
    
    private void StopProjectile()
    {
        hasHit = true;
        
        // Parar movimento
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
        
        // Parar som de voo
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
        
        // Desativar collider
        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }
        
        // Agendar destruição
        Destroy(gameObject, 0.5f);
    }
    
    /// <summary>
    /// Força a explosão do projétil na posição atual
    /// </summary>
    public void ExplodeAtCurrentPosition()
    {
        if (hasAOE)
        {
            ProcessAOEDamage(transform.position);
        }
        
        CreateHitEffects(transform.position);
        StopProjectile();
    }
    
    /// <summary>
    /// Muda a direção do projétil
    /// </summary>
    public void SetDirection(Vector3 newDirection)
    {
        direction = newDirection.normalized;
        
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    /// <summary>
    /// Modifica a velocidade do projétil
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmosSelected()
    {
        // Desenhar área de AOE
        if (hasAOE)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, aoeRadius);
        }
        
        // Desenhar range de homing
        if (isHoming)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, homingRange);
            
            // Linha para o alvo de homing
            if (homingTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, homingTarget.transform.position);
            }
        }
        
        // Direção do movimento
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, direction * 2f);
    }
    
    #endregion
}