using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

/// <summary>
/// Tipos de áudio no jogo
/// </summary>
public enum AudioType
{
    SFX,
    Music,
    Voice,
    Ambient,
    UI
}

/// <summary>
/// Configuração de um clip de áudio
/// </summary>
[System.Serializable]
public class AudioClipData
{
    public AudioClip clip;
    public AudioType audioType = AudioType.SFX;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0.1f, 3f)] public float pitch = 1f;
    public bool loop = false;
    public bool playOnAwake = false;
    public float spatialBlend = 0f; // 0 = 2D, 1 = 3D
}

/// <summary>
/// Pool de AudioSources para otimização
/// </summary>
public class AudioSourcePool
{
    private Queue<AudioSource> availableSources = new Queue<AudioSource>();
    private List<AudioSource> allSources = new List<AudioSource>();
    private Transform parent;
    
    public AudioSourcePool(Transform parentTransform, int initialSize = 10)
    {
        parent = parentTransform;
        
        // Criar AudioSources iniciais
        for (int i = 0; i < initialSize; i++)
        {
            CreateNewAudioSource();
        }
    }
    
    private AudioSource CreateNewAudioSource()
    {
        GameObject audioObject = new GameObject($"PooledAudioSource_{allSources.Count}");
        audioObject.transform.SetParent(parent);
        
        AudioSource audioSource = audioObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        
        allSources.Add(audioSource);
        availableSources.Enqueue(audioSource);
        
        return audioSource;
    }
    
    public AudioSource GetAudioSource()
    {
        if (availableSources.Count == 0)
        {
            return CreateNewAudioSource();
        }
        
        return availableSources.Dequeue();
    }
    
    public void ReturnAudioSource(AudioSource audioSource)
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
            availableSources.Enqueue(audioSource);
        }
    }
    
    public void StopAllSources()
    {
        foreach (AudioSource source in allSources)
        {
            if (source != null)
            {
                source.Stop();
            }
        }
    }
}

/// <summary>
/// Gerencia todo o sistema de áudio do jogo
/// </summary>
public class AudioManager : Singleton<AudioManager>
{
    [Header("Audio Mixer")]
    public AudioMixerGroup masterMixerGroup;
    public AudioMixerGroup musicMixerGroup;
    public AudioMixerGroup sfxMixerGroup;
    public AudioMixerGroup voiceMixerGroup;
    public AudioMixerGroup ambientMixerGroup;
    public AudioMixerGroup uiMixerGroup;
    
    [Header("Music Settings")]
    public AudioSource musicSource;
    public AudioSource ambientSource;
    [Range(0f, 1f)] public float musicFadeDuration = 1f;
    
    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Range(0f, 1f)] public float ambientVolume = 0.5f;
    [Range(0f, 1f)] public float uiVolume = 0.8f;
    
    [Header("Audio Pool")]
    public int audioSourcePoolSize = 20;
    
    [Header("Audio Library")]
    public List<AudioClipData> audioLibrary = new List<AudioClipData>();
    
    // Estado interno
    private AudioSourcePool audioSourcePool;
    private Dictionary<string, AudioClipData> audioClipDatabase = new Dictionary<string, AudioClipData>();
    private List<AudioSource> activeSources = new List<AudioSource>();
    private Coroutine currentMusicFade;
    private AudioClip currentMusicClip;
    private AudioClip currentAmbientClip;
    
    // Cache de settings
    private Dictionary<AudioType, float> volumeCache = new Dictionary<AudioType, float>();
    
    protected override void Awake()
    {
        base.Awake();
        
        // Configurar AudioSources principais
        SetupMainAudioSources();
        
        // Criar pool de AudioSources
        audioSourcePool = new AudioSourcePool(transform, audioSourcePoolSize);
        
        // Construir database de áudio
        BuildAudioDatabase();
        
        // Configurar cache de volumes
        UpdateVolumeCache();
    }
    
    private void Start()
    {
        // Aplicar configurações iniciais
        ApplyVolumeSettings();
        
        // Inscrever nos eventos
        EventManager.OnGamePaused += OnGamePaused;
        EventManager.OnGameResumed += OnGameResumed;
    }
    
    private void SetupMainAudioSources()
    {
        // Configurar Music Source
        if (musicSource == null)
        {
            GameObject musicObject = new GameObject("MusicSource");
            musicObject.transform.SetParent(transform);
            musicSource = musicObject.AddComponent<AudioSource>();
        }
        
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.outputAudioMixerGroup = musicMixerGroup;
        
        // Configurar Ambient Source
        if (ambientSource == null)
        {
            GameObject ambientObject = new GameObject("AmbientSource");
            ambientObject.transform.SetParent(transform);
            ambientSource = ambientObject.AddComponent<AudioSource>();
        }
        
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.outputAudioMixerGroup = ambientMixerGroup;
    }
    
    private void BuildAudioDatabase()
    {
        audioClipDatabase.Clear();
        
        foreach (AudioClipData clipData in audioLibrary)
        {
            if (clipData.clip != null)
            {
                audioClipDatabase[clipData.clip.name] = clipData;
            }
        }
        
        Debug.Log($"Audio database construído com {audioClipDatabase.Count} clips");
    }
    
    private void UpdateVolumeCache()
    {
        volumeCache[AudioType.Music] = musicVolume;
        volumeCache[AudioType.SFX] = sfxVolume;
        volumeCache[AudioType.Voice] = voiceVolume;
        volumeCache[AudioType.Ambient] = ambientVolume;
        volumeCache[AudioType.UI] = uiVolume;
    }
    
    #region Public API - Music
    
    /// <summary>
    /// Toca música de fundo
    /// </summary>
    public void PlayMusic(AudioClip musicClip, bool fadeIn = true)
    {
        if (musicClip == null) return;
        
        if (currentMusicFade != null)
        {
            StopCoroutine(currentMusicFade);
        }
        
        if (fadeIn && musicSource.isPlaying)
        {
            currentMusicFade = StartCoroutine(FadeMusicCoroutine(musicClip));
        }
        else
        {
            musicSource.clip = musicClip;
            musicSource.volume = musicVolume * masterVolume;
            musicSource.Play();
        }
        
        currentMusicClip = musicClip;
    }
    
    /// <summary>
    /// Para a música atual
    /// </summary>
    public void StopMusic(bool fadeOut = true)
    {
        if (currentMusicFade != null)
        {
            StopCoroutine(currentMusicFade);
        }
        
        if (fadeOut && musicSource.isPlaying)
        {
            currentMusicFade = StartCoroutine(FadeOutMusicCoroutine());
        }
        else
        {
            musicSource.Stop();
        }
        
        currentMusicClip = null;
    }
    
    /// <summary>
    /// Pausa/retoma a música
    /// </summary>
    public void SetMusicPaused(bool paused)
    {
        if (paused)
        {
            musicSource.Pause();
        }
        else
        {
            musicSource.UnPause();
        }
    }
    
    #endregion
    
    #region Public API - Ambient
    
    /// <summary>
    /// Toca som ambiente
    /// </summary>
    public void PlayAmbient(AudioClip ambientClip, bool fadeIn = true)
    {
        if (ambientClip == null) return;
        
        if (fadeIn && ambientSource.isPlaying)
        {
            StartCoroutine(FadeAmbientCoroutine(ambientClip));
        }
        else
        {
            ambientSource.clip = ambientClip;
            ambientSource.volume = ambientVolume * masterVolume;
            ambientSource.Play();
        }
        
        currentAmbientClip = ambientClip;
    }
    
    /// <summary>
    /// Para som ambiente
    /// </summary>
    public void StopAmbient(bool fadeOut = true)
    {
        if (fadeOut && ambientSource.isPlaying)
        {
            StartCoroutine(FadeOutAmbientCoroutine());
        }
        else
        {
            ambientSource.Stop();
        }
        
        currentAmbientClip = null;
    }
    
    #endregion
    
    #region Public API - SFX
    
    /// <summary>
    /// Toca um efeito sonoro
    /// </summary>
    public AudioSource PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f, bool loop = false)
    {
        if (clip == null) return null;
        
        AudioSource audioSource = audioSourcePool.GetAudioSource();
        
        audioSource.clip = clip;
        audioSource.volume = volume * sfxVolume * masterVolume;
        audioSource.pitch = pitch;
        audioSource.loop = loop;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.outputAudioMixerGroup = sfxMixerGroup;
        
        audioSource.Play();
        
        activeSources.Add(audioSource);
        
        if (!loop)
        {
            StartCoroutine(ReturnAudioSourceWhenFinished(audioSource));
        }
        
        return audioSource;
    }
    
    /// <summary>
    /// Toca SFX por nome do clip
    /// </summary>
    public AudioSource PlaySFX(string clipName, float volumeMultiplier = 1f)
    {
        if (audioClipDatabase.TryGetValue(clipName, out AudioClipData clipData))
        {
            return PlaySFX(clipData.clip, clipData.volume * volumeMultiplier, clipData.pitch, clipData.loop);
        }
        
        Debug.LogWarning($"Clip de áudio não encontrado: {clipName}");
        return null;
    }
    
    /// <summary>
    /// Toca SFX em uma posição 3D específica
    /// </summary>
    public AudioSource PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return null;
        
        AudioSource audioSource = audioSourcePool.GetAudioSource();
        
        audioSource.transform.position = position;
        audioSource.clip = clip;
        audioSource.volume = volume * sfxVolume * masterVolume;
        audioSource.pitch = pitch;
        audioSource.spatialBlend = 1f; // 3D
        audioSource.outputAudioMixerGroup = sfxMixerGroup;
        
        audioSource.Play();
        
        activeSources.Add(audioSource);
        StartCoroutine(ReturnAudioSourceWhenFinished(audioSource));
        
        return audioSource;
    }
    
    /// <summary>
    /// Toca SFX 3D por nome
    /// </summary>
    public AudioSource PlaySFXAtPosition(string clipName, Vector3 position, float volumeMultiplier = 1f)
    {
        if (audioClipDatabase.TryGetValue(clipName, out AudioClipData clipData))
        {
            return PlaySFXAtPosition(clipData.clip, position, clipData.volume * volumeMultiplier, clipData.pitch);
        }
        
        Debug.LogWarning($"Clip de áudio não encontrado: {clipName}");
        return null;
    }
    
    #endregion
    
    #region Public API - Voice
    
    /// <summary>
    /// Toca voz/diálogo
    /// </summary>
    public AudioSource PlayVoice(AudioClip voiceClip, float volume = 1f)
    {
        if (voiceClip == null) return null;
        
        AudioSource audioSource = audioSourcePool.GetAudioSource();
        
        audioSource.clip = voiceClip;
        audioSource.volume = volume * voiceVolume * masterVolume;
        audioSource.pitch = 1f;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.outputAudioMixerGroup = voiceMixerGroup;
        
        audioSource.Play();
        
        activeSources.Add(audioSource);
        StartCoroutine(ReturnAudioSourceWhenFinished(audioSource));
        
        return audioSource;
    }
    
    #endregion
    
    #region Public API - UI
    
    /// <summary>
    /// Toca som de UI
    /// </summary>
    public AudioSource PlayUI(AudioClip uiClip, float volume = 1f)
    {
        if (uiClip == null) return null;
        
        AudioSource audioSource = audioSourcePool.GetAudioSource();
        
        audioSource.clip = uiClip;
        audioSource.volume = volume * uiVolume * masterVolume;
        audioSource.pitch = 1f;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D
        audioSource.outputAudioMixerGroup = uiMixerGroup;
        
        audioSource.Play();
        
        activeSources.Add(audioSource);
        StartCoroutine(ReturnAudioSourceWhenFinished(audioSource));
        
        return audioSource;
    }
    
    /// <summary>
    /// Toca som de UI por nome
    /// </summary>
    public AudioSource PlayUI(string clipName, float volumeMultiplier = 1f)
    {
        if (audioClipDatabase.TryGetValue(clipName, out AudioClipData clipData))
        {
            return PlayUI(clipData.clip, clipData.volume * volumeMultiplier);
        }
        
        Debug.LogWarning($"Clip de UI não encontrado: {clipName}");
        return null;
    }
    
    #endregion
    
    #region Volume Management
    
    /// <summary>
    /// Define volume master
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyVolumeSettings();
        
        if (masterMixerGroup != null)
        {
            masterMixerGroup.audioMixer.SetFloat("MasterVolume", Mathf.Log10(masterVolume) * 20);
        }
    }
    
    /// <summary>
    /// Define volume da música
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        musicSource.volume = musicVolume * masterVolume;
        volumeCache[AudioType.Music] = musicVolume;
        
        if (musicMixerGroup != null)
        {
            musicMixerGroup.audioMixer.SetFloat("MusicVolume", Mathf.Log10(musicVolume) * 20);
        }
    }
    
    /// <summary>
    /// Define volume dos efeitos sonoros
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        volumeCache[AudioType.SFX] = sfxVolume;
        
        if (sfxMixerGroup != null)
        {
            sfxMixerGroup.audioMixer.SetFloat("SFXVolume", Mathf.Log10(sfxVolume) * 20);
        }
    }
    
    /// <summary>
    /// Define volume da voz
    /// </summary>
    public void SetVoiceVolume(float volume)
    {
        voiceVolume = Mathf.Clamp01(volume);
        volumeCache[AudioType.Voice] = voiceVolume;
        
        if (voiceMixerGroup != null)
        {
            voiceMixerGroup.audioMixer.SetFloat("VoiceVolume", Mathf.Log10(voiceVolume) * 20);
        }
    }
    
    /// <summary>
    /// Define volume ambiente
    /// </summary>
    public void SetAmbientVolume(float volume)
    {
        ambientVolume = Mathf.Clamp01(volume);
        ambientSource.volume = ambientVolume * masterVolume;
        volumeCache[AudioType.Ambient] = ambientVolume;
        
        if (ambientMixerGroup != null)
        {
            ambientMixerGroup.audioMixer.SetFloat("AmbientVolume", Mathf.Log10(ambientVolume) * 20);
        }
    }
    
    /// <summary>
    /// Define volume da UI
    /// </summary>
    public void SetUIVolume(float volume)
    {
        uiVolume = Mathf.Clamp01(volume);
        volumeCache[AudioType.UI] = uiVolume;
        
        if (uiMixerGroup != null)
        {
            uiMixerGroup.audioMixer.SetFloat("UIVolume", Mathf.Log10(uiVolume) * 20);
        }
    }
    
    private void ApplyVolumeSettings()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume * masterVolume;
        }
        
        if (ambientSource != null)
        {
            ambientSource.volume = ambientVolume * masterVolume;
        }
        
        UpdateVolumeCache();
    }
    
    #endregion
    
    #region Audio Control
    
    /// <summary>
    /// Para todos os sons ativos
    /// </summary>
    public void StopAllSounds()
    {
        StopMusic(false);
        StopAmbient(false);
        StopAllSFX();
    }
    
    /// <summary>
    /// Para todos os efeitos sonoros
    /// </summary>
    public void StopAllSFX()
    {
        audioSourcePool.StopAllSources();
        
        foreach (AudioSource source in activeSources)
        {
            if (source != null)
            {
                source.Stop();
            }
        }
        
        activeSources.Clear();
    }
    
    /// <summary>
    /// Pausa todos os sons
    /// </summary>
    public void PauseAllSounds()
    {
        SetMusicPaused(true);
        
        if (ambientSource.isPlaying)
        {
            ambientSource.Pause();
        }
        
        foreach (AudioSource source in activeSources)
        {
            if (source != null && source.isPlaying)
            {
                source.Pause();
            }
        }
    }
    
    /// <summary>
    /// Retoma todos os sons pausados
    /// </summary>
    public void ResumeAllSounds()
    {
        SetMusicPaused(false);
        
        if (ambientSource.clip != null)
        {
            ambientSource.UnPause();
        }
        
        foreach (AudioSource source in activeSources)
        {
            if (source != null)
            {
                source.UnPause();
            }
        }
    }
    
    #endregion
    
    #region Fade Effects
    
    private IEnumerator FadeMusicCoroutine(AudioClip newClip)
    {
        float fadeTime = musicFadeDuration;
        float originalVolume = musicSource.volume;
        
        // Fade out
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(originalVolume, 0f, t / fadeTime);
            yield return null;
        }
        
        // Trocar música
        musicSource.clip = newClip;
        musicSource.Play();
        
        // Fade in
        float targetVolume = musicVolume * masterVolume;
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeTime);
            yield return null;
        }
        
        musicSource.volume = targetVolume;
        currentMusicFade = null;
    }
    
    private IEnumerator FadeOutMusicCoroutine()
    {
        float fadeTime = musicFadeDuration;
        float originalVolume = musicSource.volume;
        
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            musicSource.volume = Mathf.Lerp(originalVolume, 0f, t / fadeTime);
            yield return null;
        }
        
        musicSource.Stop();
        musicSource.volume = originalVolume;
        currentMusicFade = null;
    }
    
    private IEnumerator FadeAmbientCoroutine(AudioClip newClip)
    {
        float fadeTime = musicFadeDuration;
        float originalVolume = ambientSource.volume;
        
        // Fade out
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            ambientSource.volume = Mathf.Lerp(originalVolume, 0f, t / fadeTime);
            yield return null;
        }
        
        // Trocar som ambiente
        ambientSource.clip = newClip;
        ambientSource.Play();
        
        // Fade in
        float targetVolume = ambientVolume * masterVolume;
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            ambientSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeTime);
            yield return null;
        }
        
        ambientSource.volume = targetVolume;
    }
    
    private IEnumerator FadeOutAmbientCoroutine()
    {
        float fadeTime = musicFadeDuration;
        float originalVolume = ambientSource.volume;
        
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            ambientSource.volume = Mathf.Lerp(originalVolume, 0f, t / fadeTime);
            yield return null;
        }
        
        ambientSource.Stop();
        ambientSource.volume = originalVolume;
    }
    
    #endregion
    
    #region Utility
    
    private IEnumerator ReturnAudioSourceWhenFinished(AudioSource audioSource)
    {
        yield return new WaitWhile(() => audioSource.isPlaying);
        
        activeSources.Remove(audioSource);
        audioSourcePool.ReturnAudioSource(audioSource);
    }
    
    /// <summary>
    /// Obtém volume por tipo de áudio
    /// </summary>
    public float GetVolumeForType(AudioType audioType)
    {
        return volumeCache.TryGetValue(audioType, out float volume) ? volume : 1f;
    }
    
    /// <summary>
    /// Verifica se música está tocando
    /// </summary>
    public bool IsMusicPlaying()
    {
        return musicSource != null && musicSource.isPlaying;
    }
    
    /// <summary>
    /// Verifica se som ambiente está tocando
    /// </summary>
    public bool IsAmbientPlaying()
    {
        return ambientSource != null && ambientSource.isPlaying;
    }
    
    /// <summary>
    /// Obtém música atual
    /// </summary>
    public AudioClip GetCurrentMusic()
    {
        return currentMusicClip;
    }
    
    /// <summary>
    /// Obtém som ambiente atual
    /// </summary>
    public AudioClip GetCurrentAmbient()
    {
        return currentAmbientClip;
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnGamePaused()
    {
        PauseAllSounds();
    }
    
    private void OnGameResumed()
    {
        ResumeAllSounds();
    }
    
    #endregion
    
    #region Audio Library Management
    
    /// <summary>
    /// Adiciona clip à biblioteca
    /// </summary>
    public void AddClipToLibrary(AudioClipData clipData)
    {
        if (clipData?.clip != null)
        {
            audioLibrary.Add(clipData);
            audioClipDatabase[clipData.clip.name] = clipData;
        }
    }
    
    /// <summary>
    /// Remove clip da biblioteca
    /// </summary>
    public void RemoveClipFromLibrary(string clipName)
    {
        audioClipDatabase.Remove(clipName);
        audioLibrary.RemoveAll(clip => clip.clip?.name == clipName);
    }
    
    /// <summary>
    /// Verifica se clip existe na biblioteca
    /// </summary>
    public bool HasClip(string clipName)
    {
        return audioClipDatabase.ContainsKey(clipName);
    }
    
    #endregion
    
    #region Debug
    
    /// <summary>
    /// Lista informações de áudio ativo (para debug)
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void DebugAudioInfo()
    {
        Debug.Log("=== AUDIO MANAGER INFO ===");
        Debug.Log($"Master Volume: {masterVolume:F2}");
        Debug.Log($"Music: {(IsMusicPlaying() ? currentMusicClip?.name : "None")} (Volume: {musicVolume:F2})");
        Debug.Log($"Ambient: {(IsAmbientPlaying() ? currentAmbientClip?.name : "None")} (Volume: {ambientVolume:F2})");
        Debug.Log($"Active SFX Sources: {activeSources.Count}");
        Debug.Log($"Audio Library Size: {audioLibrary.Count}");
        Debug.Log("========================");
    }
    
    #endregion
    
    private void OnDestroy()
    {
        // Desinscrever dos eventos
        EventManager.OnGamePaused -= OnGamePaused;
        EventManager.OnGameResumed -= OnGameResumed;
        
        // Parar todas as coroutines
        if (currentMusicFade != null)
        {
            StopCoroutine(currentMusicFade);
        }
    }
}