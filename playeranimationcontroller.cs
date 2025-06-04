using UnityEngine;

/// <summary>
/// Controla as animações do jogador baseado nas ações e movimento
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("Animation Parameters")]
    public string movingParameter = "IsMoving";
    public string runningParameter = "IsRunning";
    public string movementSpeedParameter = "MovementSpeed";
    public string groundedParameter = "IsGrounded";
    public string attackParameter = "Attack";
    public string attackTypeParameter = "AttackType";
    public string skillParameter = "Skill";
    public string skillIDParameter = "SkillID";
    public string hurtParameter = "Hurt";
    public string deathParameter = "Death";
    public string jumpParameter = "Jump";
    
    [Header("Animation Settings")]
    public float animationSmoothTime = 0.1f;
    public float attackAnimationSpeed = 1f;
    public float skillAnimationSpeed = 1f;
    
    // Componentes
    private Animator animator;
    private PlayerController playerController;
    private PlayerCombat playerCombat;
    
    // Estados de animação
    private bool isPlayingSkill = false;
    private bool isPlayingAttack = false;
    private float currentAnimationSpeed;
    
    // Hash dos parâmetros para performance
    private int movingHash;
    private int runningHash;
    private int movementSpeedHash;
    private int groundedHash;
    private int attackHash;
    private int attackTypeHash;
    private int skillHash;
    private int skillIDHash;
    private int hurtHash;
    private int deathHash;
    private int jumpHash;
    
    private void Awake()
    {
        // Obter componentes
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        playerCombat = GetComponent<PlayerCombat>();
        
        // Cache dos hashes dos parâmetros
        CacheParameterHashes();
    }
    
    private void Start()
    {
        // Inscrever nos eventos
        SubscribeToEvents();
        
        // Configurar velocidade inicial
        currentAnimationSpeed = 1f;
        animator.speed = currentAnimationSpeed;
    }
    
    private void CacheParameterHashes()
    {
        movingHash = Animator.StringToHash(movingParameter);
        runningHash = Animator.StringToHash(runningParameter);
        movementSpeedHash = Animator.StringToHash(movementSpeedParameter);
        groundedHash = Animator.StringToHash(groundedParameter);
        attackHash = Animator.StringToHash(attackParameter);
        attackTypeHash = Animator.StringToHash(attackTypeParameter);
        skillHash = Animator.StringToHash(skillParameter);
        skillIDHash = Animator.StringToHash(skillIDParameter);
        hurtHash = Animator.StringToHash(hurtParameter);
        deathHash = Animator.StringToHash(deathParameter);
        jumpHash = Animator.StringToHash(jumpParameter);
    }
    
    private void SubscribeToEvents()
    {
        // Eventos de combate
        if (InputManager.Instance != null)
        {
            InputManager.OnPrimaryAttackInput += TriggerPrimaryAttack;
            InputManager.OnSecondaryAttackInput += TriggerSecondaryAttack;
            InputManager.OnSkillInput += TriggerSkillAnimation;
        }
        
        // Eventos do jogador
        EventManager.OnPlayerDeath += TriggerDeathAnimation;
        EventManager.OnDamageDealt += TriggerHurtAnimation;
        EventManager.OnSkillUsed += OnSkillUsed;
    }
    
    #region Movement Animations
    
    public void SetMoving(bool isMoving)
    {
        if (HasParameter(movingHash))
        {
            animator.SetBool(movingHash, isMoving);
        }
    }
    
    public void SetRunning(bool isRunning)
    {
        if (HasParameter(runningHash))
        {
            animator.SetBool(runningHash, isRunning);
        }
    }
    
    public void SetMovementSpeed(float speed)
    {
        if (HasParameter(movementSpeedHash))
        {
            // Suavizar a transição da velocidade de movimento
            float currentSpeed = animator.GetFloat(movementSpeedHash);
            float targetSpeed = speed;
            float smoothedSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime / animationSmoothTime);
            
            animator.SetFloat(movementSpeedHash, smoothedSpeed);
        }
    }
    
    public void SetGrounded(bool isGrounded)
    {
        if (HasParameter(groundedHash))
        {
            animator.SetBool(groundedHash, isGrounded);
        }
    }
    
    public void TriggerJump()
    {
        if (HasParameter(jumpHash))
        {
            animator.SetTrigger(jumpHash);
        }
    }
    
    #endregion
    
    #region Combat Animations
    
    private void TriggerPrimaryAttack()
    {
        if (CanPlayCombatAnimation())
        {
            TriggerAttackAnimation(0); // 0 = ataque primário
        }
    }
    
    private void TriggerSecondaryAttack()
    {
        if (CanPlayCombatAnimation())
        {
            TriggerAttackAnimation(1); // 1 = ataque secundário
        }
    }
    
    public void TriggerAttackAnimation(int attackType)
    {
        if (!CanPlayCombatAnimation()) return;
        
        isPlayingAttack = true;
        
        // Definir tipo de ataque
        if (HasParameter(attackTypeHash))
        {
            animator.SetInteger(attackTypeHash, attackType);
        }
        
        // Trigger do ataque
        if (HasParameter(attackHash))
        {
            animator.SetTrigger(attackHash);
        }
        
        // Ajustar velocidade da animação baseada no attack speed
        SetAnimationSpeed(attackAnimationSpeed * GetAttackSpeedMultiplier());
        
        // Timer para resetar estado
        TimeManager.Instance?.CreateTimer(GetCurrentClipLength(), () => {
            isPlayingAttack = false;
            ResetAnimationSpeed();
        });
    }
    
    private void TriggerSkillAnimation(int skillID)
    {
        if (!CanPlayCombatAnimation()) return;
        
        TriggerSkillAnimation(skillID, skillAnimationSpeed);
    }
    
    public void TriggerSkillAnimation(int skillID, float animSpeed = 1f)
    {
        if (!CanPlayCombatAnimation()) return;
        
        isPlayingSkill = true;
        
        // Definir ID da skill
        if (HasParameter(skillIDHash))
        {
            animator.SetInteger(skillIDHash, skillID);
        }
        
        // Trigger da skill
        if (HasParameter(skillHash))
        {
            animator.SetTrigger(skillHash);
        }
        
        // Ajustar velocidade da animação
        SetAnimationSpeed(animSpeed);
        
        // Timer para resetar estado
        float clipLength = GetCurrentClipLength();
        TimeManager.Instance?.CreateTimer(clipLength / animSpeed, () => {
            isPlayingSkill = false;
            ResetAnimationSpeed();
        });
    }
    
    public void TriggerHurtAnimation(float damage, Vector3 position)
    {
        // Só tocar animação de hurt se o dano for significativo
        if (damage > 5f && HasParameter(hurtHash))
        {
            animator.SetTrigger(hurtHash);
        }
    }
    
    public void TriggerDeathAnimation()
    {
        if (HasParameter(deathHash))
        {
            animator.SetTrigger(deathHash);
            
            // Parar todas as outras animações
            isPlayingAttack = false;
            isPlayingSkill = false;
            
            // Velocidade normal para animação de morte
            ResetAnimationSpeed();
        }
    }
    
    #endregion
    
    #region Skill Animations
    
    private void OnSkillUsed(string skillName)
    {
        // Mapear nomes de skills para IDs de animação
        int skillID = GetSkillAnimationID(skillName);
        if (skillID >= 0)
        {
            TriggerSkillAnimation(skillID);
        }
    }
    
    private int GetSkillAnimationID(string skillName)
    {
        // Mapear skills para IDs de animação
        switch (skillName.ToLower())
        {
            case "fireball": return 1;
            case "heal": return 2;
            case "charge": return 3;
            case "shield": return 4;
            case "teleport": return 5;
            default: return -1;
        }
    }
    
    #endregion
    
    #region Animation Speed Control
    
    private void SetAnimationSpeed(float speed)
    {
        currentAnimationSpeed = speed;
        animator.speed = currentAnimationSpeed;
    }
    
    private void ResetAnimationSpeed()
    {
        currentAnimationSpeed = 1f;
        animator.speed = currentAnimationSpeed;
    }
    
    private float GetAttackSpeedMultiplier()
    {
        if (playerController?.GetComponent<PlayerStats>() != null)
        {
            PlayerStats stats = playerController.GetComponent<PlayerStats>();
            return stats.FinalAttackSpeed;
        }
        return 1f;
    }
    
    #endregion
    
    #region Utility Methods
    
    private bool CanPlayCombatAnimation()
    {
        // Verificar se pode tocar animações de combate
        return !isPlayingSkill && !isPlayingAttack && animator.enabled;
    }
    
    private bool HasParameter(int parameterHash)
    {
        // Verificar se o parâmetro existe no Animator
        if (animator.parameters.Length == 0) return false;
        
        foreach (var param in animator.parameters)
        {
            if (param.nameHash == parameterHash)
                return true;
        }
        return false;
    }
    
    private float GetCurrentClipLength()
    {
        // Obter duração do clip atual
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.length;
    }
    
    public bool IsPlayingAnimation(string animationName)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsName(animationName);
    }
    
    public float GetAnimationProgress()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.normalizedTime;
    }
    
    #endregion
    
    #region Animation Events
    
    // Estes métodos são chamados pelos Animation Events no Unity
    public void OnAttackStart()
    {
        // Evento quando ataque inicia
        Debug.Log("Ataque iniciado");
    }
    
    public void OnAttackHit()
    {
        // Evento quando ataque deve causar dano (frame específico)
        if (playerCombat != null)
        {
            playerCombat.ExecuteAttack();
        }
    }
    
    public void OnAttackEnd()
    {
        // Evento quando ataque termina
        isPlayingAttack = false;
        ResetAnimationSpeed();
        Debug.Log("Ataque finalizado");
    }
    
    public void OnSkillCast()
    {
        // Evento quando skill é executada (frame específico)
        Debug.Log("Skill executada");
    }
    
    public void OnSkillEnd()
    {
        // Evento quando skill termina
        isPlayingSkill = false;
        ResetAnimationSpeed();
        Debug.Log("Skill finalizada");
    }
    
    public void OnFootstep()
    {
        // Evento para sons de passos
        // AudioManager.Instance?.PlayFootstepSound();
    }
    
    #endregion
    
    #region Public Properties
    
    public bool IsPlayingAttack => isPlayingAttack;
    public bool IsPlayingSkill => isPlayingSkill;
    public bool IsPlayingCombatAnimation => isPlayingAttack || isPlayingSkill;
    public float CurrentAnimationSpeed => currentAnimationSpeed;
    public Animator AnimatorComponent => animator;
    
    #endregion
    
    #region Layer Control
    
    public void SetLayerWeight(int layerIndex, float weight)
    {
        if (layerIndex < animator.layerCount)
        {
            animator.SetLayerWeight(layerIndex, weight);
        }
    }
    
    public void PlayAnimationOnLayer(string animationName, int layer = 0)
    {
        animator.Play(animationName, layer);
    }
    
    #endregion
    
    #region Debug
    
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogAnimationEvent(string eventName)
    {
        Debug.Log($"[PlayerAnimationController] {eventName} - Frame: {Time.frameCount}");
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        if (InputManager.Instance != null)
        {
            InputManager.OnPrimaryAttackInput -= TriggerPrimaryAttack;
            InputManager.OnSecondaryAttackInput -= TriggerSecondaryAttack;
            InputManager.OnSkillInput -= TriggerSkillAnimation;
        }
        
        EventManager.OnPlayerDeath -= TriggerDeathAnimation;
        EventManager.OnDamageDealt -= TriggerHurtAnimation;
        EventManager.OnSkillUsed -= OnSkillUsed;
    }
}