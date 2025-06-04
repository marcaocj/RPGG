using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Para manipulação de tempo (ex: slow motion, timers de skills)
/// </summary>
public class TimeManager : Singleton<TimeManager>
{
    [Header("Time Settings")]
    public float normalTimeScale = 1f;
    public float slowMotionScale = 0.3f;
    public float fastForwardScale = 2f;
    
    // Estado atual do tempo
    private float currentTimeScale = 1f;
    private bool isSlowMotion = false;
    private bool isFastForward = false;
    private bool isPaused = false;
    
    // Sistema de timers
    private List<GameTimer> activeTimers = new List<GameTimer>();
    private int timerIdCounter = 0;
    
    // Efeitos temporais
    private Coroutine slowMotionCoroutine;
    private Coroutine fastForwardCoroutine;
    
    protected override void Awake()
    {
        base.Awake();
        currentTimeScale = normalTimeScale;
    }
    
    private void Update()
    {
        UpdateTimers();
    }
    
    #region Time Scale Control
    
    /// <summary>
    /// Define a escala de tempo
    /// </summary>
    /// <param name="scale">Nova escala de tempo</param>
    public void SetTimeScale(float scale)
    {
        currentTimeScale = Mathf.Clamp(scale, 0f, 10f);
        
        if (!isPaused)
        {
            Time.timeScale = currentTimeScale;
        }
    }
    
    /// <summary>
    /// Restaura o tempo normal
    /// </summary>
    public void ResetTimeScale()
    {
        SetTimeScale(normalTimeScale);
        isSlowMotion = false;
        isFastForward = false;
    }
    
    /// <summary>
    /// Ativa slow motion por um tempo determinado
    /// </summary>
    /// <param name="duration">Duração em segundos</param>
    public void StartSlowMotion(float duration)
    {
        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
        }
        
        slowMotionCoroutine = StartCoroutine(SlowMotionCoroutine(duration));
    }
    
    /// <summary>
    /// Ativa fast forward por um tempo determinado
    /// </summary>
    /// <param name="duration">Duração em segundos</param>
    public void StartFastForward(float duration)
    {
        if (fastForwardCoroutine != null)
        {
            StopCoroutine(fastForwardCoroutine);
        }
        
        fastForwardCoroutine = StartCoroutine(FastForwardCoroutine(duration));
    }
    
    /// <summary>
    /// Para o slow motion
    /// </summary>
    public void StopSlowMotion()
    {
        if (slowMotionCoroutine != null)
        {
            StopCoroutine(slowMotionCoroutine);
            slowMotionCoroutine = null;
        }
        
        isSlowMotion = false;
        ResetTimeScale();
    }
    
    /// <summary>
    /// Para o fast forward
    /// </summary>
    public void StopFastForward()
    {
        if (fastForwardCoroutine != null)
        {
            StopCoroutine(fastForwardCoroutine);
            fastForwardCoroutine = null;
        }
        
        isFastForward = false;
        ResetTimeScale();
    }
    
    /// <summary>
    /// Pausa o tempo do jogo
    /// </summary>
    public void PauseTime()
    {
        isPaused = true;
        Time.timeScale = 0f;
    }
    
    /// <summary>
    /// Retoma o tempo do jogo
    /// </summary>
    public void ResumeTime()
    {
        isPaused = false;
        Time.timeScale = currentTimeScale;
    }
    
    private IEnumerator SlowMotionCoroutine(float duration)
    {
        isSlowMotion = true;
        SetTimeScale(slowMotionScale);
        
        yield return new WaitForSecondsRealtime(duration);
        
        isSlowMotion = false;
        ResetTimeScale();
        slowMotionCoroutine = null;
    }
    
    private IEnumerator FastForwardCoroutine(float duration)
    {
        isFastForward = true;
        SetTimeScale(fastForwardScale);
        
        yield return new WaitForSecondsRealtime(duration);
        
        isFastForward = false;
        ResetTimeScale();
        fastForwardCoroutine = null;
    }
    
    #endregion
    
    #region Timer System
    
    /// <summary>
    /// Cria um timer que executa uma ação após um tempo
    /// </summary>
    /// <param name="duration">Duração em segundos</param>
    /// <param name="onComplete">Ação a ser executada</param>
    /// <param name="useRealTime">Usar tempo real (ignora Time.timeScale)</param>
    /// <returns>ID do timer</returns>
    public int CreateTimer(float duration, System.Action onComplete, bool useRealTime = false)
    {
        GameTimer timer = new GameTimer
        {
            id = ++timerIdCounter,
            duration = duration,
            timeRemaining = duration,
            onComplete = onComplete,
            useRealTime = useRealTime,
            isActive = true
        };
        
        activeTimers.Add(timer);
        return timer.id;
    }
    
    /// <summary>
    /// Cria um timer que executa uma ação repetidamente
    /// </summary>
    /// <param name="interval">Intervalo entre execuções</param>
    /// <param name="onTick">Ação a ser executada</param>
    /// <param name="totalDuration">Duração total (-1 para infinito)</param>
    /// <param name="useRealTime">Usar tempo real</param>
    /// <returns>ID do timer</returns>
    public int CreateRepeatingTimer(float interval, System.Action onTick, float totalDuration = -1f, bool useRealTime = false)
    {
        GameTimer timer = new GameTimer
        {
            id = ++timerIdCounter,
            duration = interval,
            timeRemaining = interval,
            onComplete = onTick,
            totalDuration = totalDuration,
            useRealTime = useRealTime,
            isRepeating = true,
            isActive = true
        };
        
        activeTimers.Add(timer);
        return timer.id;
    }
    
    /// <summary>
    /// Cancela um timer
    /// </summary>
    /// <param name="timerId">ID do timer</param>
    public void CancelTimer(int timerId)
    {
        for (int i = activeTimers.Count - 1; i >= 0; i--)
        {
            if (activeTimers[i].id == timerId)
            {
                activeTimers.RemoveAt(i);
                break;
            }
        }
    }
    
    /// <summary>
    /// Pausa um timer
    /// </summary>
    /// <param name="timerId">ID do timer</param>
    public void PauseTimer(int timerId)
    {
        GameTimer timer = GetTimer(timerId);
        if (timer != null)
        {
            timer.isActive = false;
        }
    }
    
    /// <summary>
    /// Retoma um timer pausado
    /// </summary>
    /// <param name="timerId">ID do timer</param>
    public void ResumeTimer(int timerId)
    {
        GameTimer timer = GetTimer(timerId);
        if (timer != null)
        {
            timer.isActive = true;
        }
    }
    
    /// <summary>
    /// Obtém o tempo restante de um timer
    /// </summary>
    /// <param name="timerId">ID do timer</param>
    /// <returns>Tempo restante em segundos</returns>
    public float GetTimerTimeRemaining(int timerId)
    {
        GameTimer timer = GetTimer(timerId);
        return timer?.timeRemaining ?? 0f;
    }
    
    /// <summary>
    /// Verifica se um timer está ativo
    /// </summary>
    /// <param name="timerId">ID do timer</param>
    /// <returns>True se o timer está ativo</returns>
    public bool IsTimerActive(int timerId)
    {
        GameTimer timer = GetTimer(timerId);
        return timer?.isActive ?? false;
    }
    
    private GameTimer GetTimer(int timerId)
    {
        return activeTimers.Find(t => t.id == timerId);
    }
    
    private void UpdateTimers()
    {
        float deltaTime = Time.deltaTime;
        float realDeltaTime = Time.unscaledDeltaTime;
        
        for (int i = activeTimers.Count - 1; i >= 0; i--)
        {
            GameTimer timer = activeTimers[i];
            
            if (!timer.isActive) continue;
            
            float timeToUse = timer.useRealTime ? realDeltaTime : deltaTime;
            timer.timeRemaining -= timeToUse;
            
            if (timer.totalDuration > 0)
            {
                timer.totalTimeElapsed += timeToUse;
            }
            
            if (timer.timeRemaining <= 0f)
            {
                // Executar ação
                timer.onComplete?.Invoke();
                
                if (timer.isRepeating)
                {
                    // Verificar se deve continuar repetindo
                    if (timer.totalDuration > 0 && timer.totalTimeElapsed >= timer.totalDuration)
                    {
                        // Timer expirou
                        activeTimers.RemoveAt(i);
                    }
                    else
                    {
                        // Resetar para próxima execução
                        timer.timeRemaining = timer.duration;
                    }
                }
                else
                {
                    // Timer único, remover
                    activeTimers.RemoveAt(i);
                }
            }
        }
    }
    
    /// <summary>
    /// Cancela todos os timers
    /// </summary>
    public void CancelAllTimers()
    {
        activeTimers.Clear();
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Converte tempo do jogo para tempo real
    /// </summary>
    /// <param name="gameTime">Tempo do jogo</param>
    /// <returns>Tempo real</returns>
    public float GameTimeToRealTime(float gameTime)
    {
        return gameTime / currentTimeScale;
    }
    
    /// <summary>
    /// Converte tempo real para tempo do jogo
    /// </summary>
    /// <param name="realTime">Tempo real</param>
    /// <returns>Tempo do jogo</returns>
    public float RealTimeToGameTime(float realTime)
    {
        return realTime * currentTimeScale;
    }
    
    /// <summary>
    /// Obtém o deltaTime apropriado baseado no tipo
    /// </summary>
    /// <param name="useRealTime">Se deve usar tempo real</param>
    /// <returns>Delta time</returns>
    public float GetDeltaTime(bool useRealTime = false)
    {
        return useRealTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
    
    #endregion
    
    #region Properties
    
    public float CurrentTimeScale => currentTimeScale;
    public bool IsSlowMotion => isSlowMotion;
    public bool IsFastForward => isFastForward;
    public bool IsPaused => isPaused;
    public int ActiveTimerCount => activeTimers.Count;
    
    #endregion
    
    #region GameTimer Class
    
    [System.Serializable]
    private class GameTimer
    {
        public int id;
        public float duration;
        public float timeRemaining;
        public float totalDuration = -1f;
        public float totalTimeElapsed = 0f;
        public System.Action onComplete;
        public bool useRealTime;
        public bool isRepeating;
        public bool isActive;
    }
    
    #endregion
}