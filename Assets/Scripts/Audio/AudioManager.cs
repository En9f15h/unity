using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AudioVolumeChannel
{
    Master,
    BGM,
    SFX,
    UI,
    Transition
}

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    private const string StartSceneName = "StartScene";
    private const string LobbySceneName = "LobbyScene";
    private const string CharacterSelectSceneName = "CharacterSelectScene";
    private const string LegacySelectCharacterSceneName = "SelectCharacterScene";
    private const string MasterVolumePrefKey = "Audio.MasterVolume";
    private const string BgmVolumePrefKey = "Audio.BGMVolume";
    private const string SfxVolumePrefKey = "Audio.SFXVolume";
    private const string LegacyUiVolumePrefKey = "Audio.UIVolume";
    private const string TransitionVolumePrefKey = "Audio.TransitionVolume";

    private static AudioManager instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource transitionSource;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float transitionVolume = 1f;
    [SerializeField] private bool saveVolumeSettings = true;
    [SerializeField] private bool playSceneBgmOnSceneLoad = true;
    [SerializeField] private bool debugAudio;

    [Header("Audio Source Pool")]
    [SerializeField, Min(1)] private int initialOneShotPoolSize = 12;
    [SerializeField, Min(1)] private int maxOneShotPoolSize = 32;
    [SerializeField] private Transform oneShotPoolRoot;

    [Header("BGM")]
    [SerializeField] private AudioClip startSceneBGM;
    [SerializeField] private AudioClip lobbyBGM;
    [SerializeField] private AudioClip selectCharacterBGM;
    [SerializeField] private AudioClip[] battleBGMs;
    [SerializeField] private AudioClip victoryBGM;
    [SerializeField] private AudioClip defeatBGM;

    [Header("UI")]
    [SerializeField] private AudioClip startSceneButton;
    [SerializeField] private AudioClip lobbyButton;

    [Header("Shared Actions")]
    [SerializeField] private AudioClip[] bluff;

    [Header("Transition")]
    [SerializeField] private AudioClip slideIn;
    [SerializeField] private AudioClip collision;
    [SerializeField] private AudioClip end;

    [Header("Knight Actions")]
    [SerializeField] private AudioClip knightMovement;
    [SerializeField] private AudioClip knightLight;
    [SerializeField] private AudioClip knightHeavyAttackCharge;
    [SerializeField] private AudioClip knightHeavyAttack;
    [SerializeField] private AudioClip knightLow;
    [SerializeField] private AudioClip knightParry;
    [SerializeField] private AudioClip knightDefense;
    [SerializeField] private AudioClip knightJump;
    [SerializeField] private AudioClip knightUltimate;

    [Header("Knight Combat")]
    [SerializeField] private AudioClip knightAttackHitEnemy;
    [SerializeField] private AudioClip knightParrySuccess;

    [Header("Knight Energy")]
    [SerializeField] private AudioClip knightEnergyGain;
    [SerializeField] private AudioClip knightEnergyFull;

    [Header("Oracle Actions")]
    [SerializeField] private AudioClip oracleMovement;
    [SerializeField] private AudioClip oracleBolt;
    [SerializeField] private AudioClip oracleRiftCharge;
    [SerializeField] private AudioClip oracleRift;
    [SerializeField] private AudioClip oracleShift;
    [SerializeField] private AudioClip oracleWard;
    [SerializeField] private AudioClip oracleFade;
    [SerializeField] private AudioClip oracleSight;

    [Header("Oracle Combat")]
    [SerializeField] private AudioClip oracleAttackHitEnemy;
    [SerializeField] private AudioClip oracleWardSuccess;
    [SerializeField] private AudioClip oracleFadeSuccess;

    [Header("Oracle Energy")]
    [SerializeField] private AudioClip oracleEnergyGain;
    [SerializeField] private AudioClip oracleEnergyFull;

    [Header("Special")]
    [SerializeField] private AudioClip[] hit;
    [SerializeField, HideInInspector] private AudioClip knightHit;
    [SerializeField, HideInInspector] private AudioClip oracleHit;
    [SerializeField] private AudioClip knightUltimateClash;
    [SerializeField] private AudioClip victory;
    [SerializeField] private AudioClip defeat;
    [SerializeField] private AudioClip enemyDisconnect;

    private Coroutine bgmFadeRoutine;
    private AudioClip scheduledBgmClip;
    private double scheduledBgmDspTime = -1d;
    private int lastBattleBgmIndex = -1;
    private string lastResultAudioKey;
    private bool isRuntimeFallback;
    private readonly List<AudioSource> oneShotPool = new List<AudioSource>();
    private readonly Dictionary<AudioSource, AudioVolumeChannel> oneShotChannels = new Dictionary<AudioSource, AudioVolumeChannel>();
    private int nextOneShotPoolIndex;

    public event Action<AudioVolumeChannel, float> VolumeChanged;

    public static AudioManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);

            if (instance == null)
            {
                GameObject obj = new GameObject("AudioManager");
                instance = obj.AddComponent<AudioManager>();
                instance.isRuntimeFallback = true;
            }

            if (!instance.gameObject.activeSelf)
                instance.gameObject.SetActive(true);

            return instance;
        }
    }

    public static bool TryGetInstance(out AudioManager manager)
    {
        if (instance == null)
            instance = FindFirstObjectByType<AudioManager>(FindObjectsInactive.Include);

        manager = instance;
        return manager != null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (instance.isRuntimeFallback && HasAnyAssignedClip())
            {
                Destroy(instance.gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        instance = this;
        gameObject.name = "AudioManager";
        DontDestroyOnLoad(gameObject);
        LoadSavedVolumeSettings();
        EnsureAudioSources();
    }

    private void OnValidate()
    {
        ClampAudioSettings();

        if (Application.isPlaying && instance == this)
            ApplyAudioVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (playSceneBgmOnSceneLoad)
            PlaySceneBGM(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetResultAudioGate();

        if (playSceneBgmOnSceneLoad)
            PlaySceneBGM(scene.name);
    }

    public void PlaySceneBGM(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return;

        if (sceneName == StartSceneName)
        {
            PlayStartSceneBGM();
            return;
        }

        if (sceneName == LobbySceneName)
        {
            PlayLobbyBGM();
            return;
        }

        if (sceneName == CharacterSelectSceneName || sceneName == LegacySelectCharacterSceneName)
            PlaySelectCharacterBGM();
    }

    public bool PlayStartSceneBGM()
    {
        return PlayBGM(startSceneBGM, true);
    }

    public bool PlayLobbyBGM()
    {
        return PlayBGM(lobbyBGM, true);
    }

    public bool PlaySelectCharacterBGM()
    {
        return PlayBGM(selectCharacterBGM, true);
    }

    public bool PlayRandomBattleBGM()
    {
        int index = SelectBattleBGMIndex();
        return PlayBattleBGMIndex(index);
    }

    public int SelectBattleBGMIndex()
    {
        if (battleBGMs == null || battleBGMs.Length == 0)
            return -1;

        int validCount = 0;
        for (int i = 0; i < battleBGMs.Length; i++)
        {
            if (battleBGMs[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return -1;

        int index = -1;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int candidate = UnityEngine.Random.Range(0, battleBGMs.Length);
            if (battleBGMs[candidate] == null)
                continue;

            index = candidate;
            if (validCount <= 1 || candidate != lastBattleBgmIndex)
                break;
        }

        if (index < 0)
        {
            for (int i = 0; i < battleBGMs.Length; i++)
            {
                if (battleBGMs[i] != null)
                {
                    index = i;
                    break;
                }
            }
        }

        return index;
    }

    public bool PlayBattleBGMIndex(int index)
    {
        if (!TryGetBattleBGM(index, out AudioClip clip))
            return false;

        lastBattleBgmIndex = index;
        return PlayBGM(clip, true);
    }

    public bool ScheduleBattleBGMIndex(int index, double dspStartTime)
    {
        if (!TryGetBattleBGM(index, out AudioClip clip))
            return false;

        lastBattleBgmIndex = index;
        return ScheduleBGM(clip, true, dspStartTime);
    }

    public bool HasBattleBGMIndex(int index)
    {
        return TryGetBattleBGM(index, out _);
    }

    public bool PlayBGM(AudioClip clip, bool loop = true, bool forceRestart = false)
    {
        if (clip == null)
        {
            LogMissingClip("BGM");
            return false;
        }

        EnsureAudioSources();
        StopBGMFadeRoutine();

        if (!forceRestart && bgmSource.clip == clip && bgmSource.isPlaying && bgmSource.loop == loop)
        {
            bgmSource.volume = GetEffectiveVolume(AudioVolumeChannel.BGM);
            return true;
        }

        scheduledBgmClip = null;
        scheduledBgmDspTime = -1d;
        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = GetEffectiveVolume(AudioVolumeChannel.BGM);
        bgmSource.Play();
        return true;
    }

    public bool ScheduleBGM(AudioClip clip, bool loop, double dspStartTime)
    {
        if (clip == null)
        {
            LogMissingClip("scheduled BGM");
            return false;
        }

        EnsureAudioSources();
        StopBGMFadeRoutine();

        double safeDspStart = Math.Max(AudioSettings.dspTime, dspStartTime);
        if (scheduledBgmClip == clip &&
            bgmSource.clip == clip &&
            Math.Abs(scheduledBgmDspTime - safeDspStart) < 0.05d)
        {
            return true;
        }

        bgmSource.Stop();
        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = GetEffectiveVolume(AudioVolumeChannel.BGM);
        bgmSource.PlayScheduled(safeDspStart);
        scheduledBgmClip = clip;
        scheduledBgmDspTime = safeDspStart;
        return true;
    }

    public void StopBGM()
    {
        EnsureAudioSources();
        StopBGMFadeRoutine();
        scheduledBgmClip = null;
        scheduledBgmDspTime = -1d;
        bgmSource.Stop();
    }

    public void FadeOutBGM(float duration)
    {
        EnsureAudioSources();
        StopBGMFadeRoutine();

        if (duration <= 0f)
        {
            StopBGM();
            return;
        }

        bgmFadeRoutine = StartCoroutine(FadeOutBGMRoutine(duration));
    }

    public void PlayStartSceneButton()
    {
        PlayUI(startSceneButton, "StartSceneButton");
    }

    public void PlayLobbyButton()
    {
        PlayUI(lobbyButton, "LobbyButton");
    }

    public void PlayBluff()
    {
        PlayRandomSFX(bluff, "Bluff");
    }

    public void PlayTransitionSlideIn()
    {
        PlayTransition(slideIn, "TransitionSlideIn");
    }

    public void PlayTransitionCollision()
    {
        PlayTransition(collision, "TransitionCollision");
    }

    public void PlayTransitionEnd()
    {
        PlayTransition(end, "TransitionEnd");
    }

    public void PlayActionStart(CharacterUnit unit, ActionType action, bool releaseNow)
    {
        if (unit == null)
            return;

        PlayActionStart(unit.className, action, releaseNow);
    }

    public void PlayActionStart(CharacterClassConfig config, ActionType action, bool releaseNow)
    {
        if (config == null)
            return;

        PlayActionStart(config.className, action, releaseNow);
    }

    public void PlayActionStart(string className, ActionType action, bool releaseNow)
    {
        if (action == ActionType.None)
            return;

        if (IsOracleAction(action) || IsOracleClassName(className))
        {
            PlayOracleActionStart(action, releaseNow);
            return;
        }

        if (IsKnightAction(action) || IsKnightClassName(className))
            PlayKnightActionStart(action, releaseNow);
    }

    public void PlayAttackHitEnemy(CharacterUnit attacker)
    {
        if (attacker == null)
            return;

        if (IsOracleClassName(attacker.className))
            PlayOracleAttackHitEnemy();
        else if (IsKnightClassName(attacker.className))
            PlayKnightAttackHitEnemy();
    }

    public void PlayHit(CharacterUnit target)
    {
        if (target == null)
            return;

        PlayHit();
    }

    public void PlayHit()
    {
        AudioClip clip = SelectRandomClip(hit);
        if (clip == null)
            clip = SelectLegacyHitClip();

        PlaySFX(clip, "Hit");
    }

    public void PlayEnergyChanged(CharacterUnit unit, int before, int after, int maxEnergy)
    {
        if (unit == null || after <= before)
            return;

        bool reachedFull = maxEnergy > 0 && before < maxEnergy && after >= maxEnergy;
        if (IsOracleClassName(unit.className))
        {
            PlayOracleEnergyGain();
            if (reachedFull)
                PlayOracleEnergyFull();
            return;
        }

        if (IsKnightClassName(unit.className))
        {
            PlayKnightEnergyGain();
            if (reachedFull)
                PlayKnightEnergyFull();
        }
    }

    public void PlayKnightMovement()
    {
        PlaySFX(knightMovement, "KnightMovement");
    }

    public void PlayKnightLight()
    {
        PlaySFX(knightLight, "KnightLight");
    }

    public void PlayKnightHeavyAttackCharge()
    {
        PlaySFX(knightHeavyAttackCharge, "KnightHeavyAttackCharge");
    }

    public void PlayKnightHeavyAttack()
    {
        PlaySFX(knightHeavyAttack, "KnightHeavyAttack");
    }

    public void PlayKnightLow()
    {
        PlaySFX(knightLow, "KnightLow");
    }

    public void PlayKnightParry()
    {
        PlaySFX(knightParry, "KnightParry");
    }

    public void PlayKnightDefense()
    {
        PlaySFX(knightDefense, "KnightDefense");
    }

    public void PlayKnightJump()
    {
        PlaySFX(knightJump, "KnightJump");
    }

    public void PlayKnightUltimate()
    {
        PlaySFX(knightUltimate, "KnightUltimate");
    }

    public void PlayKnightAttackHitEnemy()
    {
        PlaySFX(knightAttackHitEnemy, "KnightAttackHitEnemy");
    }

    public void PlayKnightHit()
    {
        PlayHit();
    }

    public void PlayKnightParrySuccess()
    {
        PlaySFX(knightParrySuccess, "KnightParrySuccess");
    }

    public void PlayKnightEnergyGain()
    {
        PlaySFX(knightEnergyGain, "KnightEnergyGain");
    }

    public void PlayKnightEnergyFull()
    {
        PlaySFX(knightEnergyFull, "KnightEnergyFull");
    }

    public void PlayOracleMovement()
    {
        PlaySFX(oracleMovement, "OracleMovement");
    }

    public void PlayOracleBolt()
    {
        PlaySFX(oracleBolt, "OracleBolt");
    }

    public void PlayOracleRiftCharge()
    {
        PlaySFX(oracleRiftCharge, "OracleRiftCharge");
    }

    public void PlayOracleRift()
    {
        PlaySFX(oracleRift, "OracleRift");
    }

    public void PlayOracleShift()
    {
        PlaySFX(oracleShift, "OracleShift");
    }

    public void PlayOracleWard()
    {
        PlaySFX(oracleWard, "OracleWard");
    }

    public void PlayOracleFade()
    {
        PlaySFX(oracleFade, "OracleFade");
    }

    public void PlayOracleSight()
    {
        PlaySFX(oracleSight, "OracleSight");
    }

    public void PlayOracleAttackHitEnemy()
    {
        PlaySFX(oracleAttackHitEnemy, "OracleAttackHitEnemy");
    }

    public void PlayOracleHit()
    {
        PlayHit();
    }

    public void PlayOracleWardSuccess()
    {
        PlaySFX(oracleWardSuccess, "OracleWardSuccess");
    }

    public void PlayOracleFadeSuccess()
    {
        PlaySFX(oracleFadeSuccess, "OracleFadeSuccess");
    }

    public void PlayOracleEnergyGain()
    {
        PlaySFX(oracleEnergyGain, "OracleEnergyGain");
    }

    public void PlayOracleEnergyFull()
    {
        PlaySFX(oracleEnergyFull, "OracleEnergyFull");
    }

    public void PlayKnightUltimateClash()
    {
        PlaySFX(knightUltimateClash, "KnightUltimateClash");
    }

    public void PlayVictoryResult()
    {
        PlayResultAudio("Victory", victoryBGM, victory);
    }

    public void PlayDefeatResult()
    {
        PlayResultAudio("Defeat", defeatBGM, defeat);
    }

    public void PlayEnemyDisconnectResult()
    {
        PlayResultAudio("EnemyDisconnect", null, enemyDisconnect);
    }

    public void PlayResult(bool localWon, bool isDraw, bool opponentLeft)
    {
        if (opponentLeft)
        {
            PlayEnemyDisconnectResult();
            return;
        }

        if (isDraw)
            return;

        if (localWon)
            PlayVictoryResult();
        else
            PlayDefeatResult();
    }

    public void ResetResultAudioGate()
    {
        lastResultAudioKey = null;
    }

    public float GetVolume(AudioVolumeChannel channel)
    {
        switch (channel)
        {
            case AudioVolumeChannel.BGM:
                return bgmVolume;
            case AudioVolumeChannel.SFX:
            case AudioVolumeChannel.UI:
                return sfxVolume;
            case AudioVolumeChannel.Transition:
                return transitionVolume;
            default:
                return masterVolume;
        }
    }

    public void SetVolume(AudioVolumeChannel channel, float volume)
    {
        float clamped = Mathf.Clamp01(volume);
        switch (channel)
        {
            case AudioVolumeChannel.BGM:
                if (Mathf.Approximately(bgmVolume, clamped))
                    return;
                bgmVolume = clamped;
                break;
            case AudioVolumeChannel.SFX:
            case AudioVolumeChannel.UI:
                if (Mathf.Approximately(sfxVolume, clamped))
                    return;
                sfxVolume = clamped;
                break;
            case AudioVolumeChannel.Transition:
                if (Mathf.Approximately(transitionVolume, clamped))
                    return;
                transitionVolume = clamped;
                break;
            default:
                if (Mathf.Approximately(masterVolume, clamped))
                    return;
                masterVolume = clamped;
                break;
        }

        ApplyAudioVolumes();
        SaveVolumeSettings();
        VolumeChanged?.Invoke(channel, clamped);

        if (channel == AudioVolumeChannel.SFX)
            VolumeChanged?.Invoke(AudioVolumeChannel.UI, clamped);
        else if (channel == AudioVolumeChannel.UI)
            VolumeChanged?.Invoke(AudioVolumeChannel.SFX, clamped);
    }

    public void SetMasterVolume(float volume)
    {
        SetVolume(AudioVolumeChannel.Master, volume);
    }

    public void SetBGMVolume(float volume)
    {
        SetVolume(AudioVolumeChannel.BGM, volume);
    }

    public void SetSFXVolume(float volume)
    {
        SetVolume(AudioVolumeChannel.SFX, volume);
    }

    public void SetUIVolume(float volume)
    {
        SetVolume(AudioVolumeChannel.UI, volume);
    }

    public void SetTransitionVolume(float volume)
    {
        SetVolume(AudioVolumeChannel.Transition, volume);
    }

    private void PlayKnightActionStart(ActionType action, bool releaseNow)
    {
        switch (action)
        {
            case ActionType.MoveForward:
            case ActionType.MoveBackward:
                PlayKnightMovement();
                break;

            case ActionType.LightAttack:
                PlayKnightLight();
                break;

            case ActionType.HeavyAttack:
                if (releaseNow)
                    PlayKnightHeavyAttack();
                else
                    PlayKnightHeavyAttackCharge();
                break;

            case ActionType.LowAttack:
                PlayKnightLow();
                break;

            case ActionType.Parry:
                PlayKnightParry();
                break;

            case ActionType.Defense:
                PlayKnightDefense();
                break;

            case ActionType.Jump:
                PlayKnightJump();
                break;

            case ActionType.Ultimate:
                PlayKnightUltimate();
                break;
        }
    }

    private void PlayOracleActionStart(ActionType action, bool releaseNow)
    {
        switch (action)
        {
            case ActionType.MoveForward:
            case ActionType.MoveBackward:
                PlayOracleMovement();
                break;

            case ActionType.Bolt:
                PlayOracleBolt();
                break;

            case ActionType.Rift:
                if (releaseNow)
                    PlayOracleRift();
                else
                    PlayOracleRiftCharge();
                break;

            case ActionType.Shift:
                PlayOracleShift();
                break;

            case ActionType.Ward:
                PlayOracleWard();
                break;

            case ActionType.Fade:
                PlayOracleFade();
                break;

            case ActionType.Sight:
                PlayOracleSight();
                break;
        }
    }

    private bool TryGetBattleBGM(int index, out AudioClip clip)
    {
        clip = null;
        if (battleBGMs == null || index < 0 || index >= battleBGMs.Length)
            return false;

        clip = battleBGMs[index];
        return clip != null;
    }

    private void PlayResultAudio(string resultKey, AudioClip bgmClip, AudioClip sfxClip)
    {
        if (lastResultAudioKey == resultKey)
            return;

        lastResultAudioKey = resultKey;

        if (bgmClip != null)
            PlayBGM(bgmClip, false, true);

        PlaySFX(sfxClip, resultKey);
    }

    private void PlaySFX(AudioClip clip, string debugName)
    {
        PlayOneShot(AudioVolumeChannel.SFX, clip, debugName);
    }

    private void PlayUI(AudioClip clip, string debugName)
    {
        PlayOneShot(AudioVolumeChannel.SFX, clip, debugName);
    }

    private void PlayRandomSFX(AudioClip[] clips, string debugName)
    {
        PlaySFX(SelectRandomClip(clips), debugName);
    }

    private void PlayTransition(AudioClip clip, string debugName)
    {
        PlayOneShot(AudioVolumeChannel.Transition, clip, debugName);
    }

    private void PlayOneShot(AudioVolumeChannel channel, AudioClip clip, string debugName)
    {
        if (clip == null)
        {
            LogMissingClip(debugName);
            return;
        }

        EnsureAudioSources();

        AudioSource source = GetOneShotAudioSource(debugName);
        if (source == null)
            return;

        AudioSource templateSource = GetTemplateSource(channel);
        ConfigureOneShotAudioSource(source, templateSource, channel);
        source.Stop();
        source.clip = clip;
        source.loop = false;
        source.Play();
    }

    private AudioClip SelectRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int targetIndex = UnityEngine.Random.Range(0, validCount);
        int currentIndex = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
                continue;

            if (currentIndex == targetIndex)
                return clips[i];

            currentIndex++;
        }

        return null;
    }

    private AudioClip SelectLegacyHitClip()
    {
        if (knightHit == null)
            return oracleHit;

        if (oracleHit == null)
            return knightHit;

        return UnityEngine.Random.Range(0, 2) == 0 ? knightHit : oracleHit;
    }

    private IEnumerator FadeOutBGMRoutine(float duration)
    {
        float startVolume = bgmSource != null ? bgmSource.volume : 0f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.0001f, duration));
            if (bgmSource != null)
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);

            yield return null;
        }

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.volume = GetEffectiveVolume(AudioVolumeChannel.BGM);
        }

        scheduledBgmClip = null;
        scheduledBgmDspTime = -1d;
        bgmFadeRoutine = null;
    }

    private void StopBGMFadeRoutine()
    {
        if (bgmFadeRoutine == null)
            return;

        StopCoroutine(bgmFadeRoutine);
        bgmFadeRoutine = null;

        if (bgmSource != null)
            bgmSource.volume = GetEffectiveVolume(AudioVolumeChannel.BGM);
    }

    private void EnsureAudioSources()
    {
        ClampAudioSettings();
        bgmSource = EnsureAudioSource(bgmSource, "BGM", true, GetEffectiveVolume(AudioVolumeChannel.BGM));
        sfxSource = EnsureAudioSource(sfxSource, "SFX", false, GetEffectiveVolume(AudioVolumeChannel.SFX));
        uiSource = EnsureAudioSource(uiSource, "UI", false, GetEffectiveVolume(AudioVolumeChannel.SFX));
        transitionSource = EnsureAudioSource(transitionSource, "Transition", false, GetEffectiveVolume(AudioVolumeChannel.Transition));
        EnsureOneShotPool();
    }

    private AudioSource EnsureAudioSource(AudioSource source, string objectName, bool loop, float volume)
    {
        if (source == null)
        {
            Transform child = transform.Find(objectName);
            if (child == null)
            {
                GameObject childObject = new GameObject(objectName);
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            source = child.GetComponent<AudioSource>();
            if (source == null)
                source = child.gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        return source;
    }

    private void EnsureOneShotPool()
    {
        if (oneShotPoolRoot == null)
        {
            Transform child = transform.Find("OneShotPool");
            if (child == null)
            {
                GameObject childObject = new GameObject("OneShotPool");
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            oneShotPoolRoot = child;
        }

        CompactOneShotPool();
        while (oneShotPool.Count < initialOneShotPoolSize)
            CreateOneShotAudioSource(oneShotPool.Count);
    }

    private AudioSource GetOneShotAudioSource(string debugName)
    {
        CompactOneShotPool();

        for (int i = 0; i < oneShotPool.Count; i++)
        {
            AudioSource source = oneShotPool[i];
            if (source != null && !source.isPlaying)
                return source;
        }

        if (oneShotPool.Count < maxOneShotPoolSize)
            return CreateOneShotAudioSource(oneShotPool.Count);

        if (oneShotPool.Count == 0)
            return null;

        int index = Mathf.Abs(nextOneShotPoolIndex) % oneShotPool.Count;
        nextOneShotPoolIndex = (index + 1) % oneShotPool.Count;
        AudioSource reusedSource = oneShotPool[index];
        if (reusedSource != null && debugAudio)
            Debug.Log("[AudioManager] Reusing busy pooled AudioSource for: " + debugName, this);

        return reusedSource;
    }

    private AudioSource CreateOneShotAudioSource(int index)
    {
        Transform parent = oneShotPoolRoot != null ? oneShotPoolRoot : transform;
        GameObject sourceObject = new GameObject("OneShot_" + index.ToString("00"));
        sourceObject.transform.SetParent(parent, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        oneShotPool.Add(source);
        return source;
    }

    private void ConfigureOneShotAudioSource(AudioSource source, AudioSource templateSource, AudioVolumeChannel channel)
    {
        if (source == null)
            return;

        oneShotChannels[source] = channel;
        source.playOnAwake = false;
        source.loop = false;
        source.volume = GetEffectiveVolume(channel);
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;

        if (templateSource == null)
            return;

        source.outputAudioMixerGroup = templateSource.outputAudioMixerGroup;
        source.priority = templateSource.priority;
        source.pitch = templateSource.pitch;
        source.panStereo = templateSource.panStereo;
        source.spatialBlend = templateSource.spatialBlend;
        source.reverbZoneMix = templateSource.reverbZoneMix;
        source.bypassEffects = templateSource.bypassEffects;
        source.bypassListenerEffects = templateSource.bypassListenerEffects;
        source.bypassReverbZones = templateSource.bypassReverbZones;
    }

    private void CompactOneShotPool()
    {
        for (int i = oneShotPool.Count - 1; i >= 0; i--)
        {
            if (oneShotPool[i] != null)
                continue;

            oneShotPool.RemoveAt(i);
        }

        maxOneShotPoolSize = Mathf.Max(1, maxOneShotPoolSize);
        initialOneShotPoolSize = Mathf.Clamp(initialOneShotPoolSize, 1, maxOneShotPoolSize);
    }

    private AudioSource GetTemplateSource(AudioVolumeChannel channel)
    {
        switch (channel)
        {
            case AudioVolumeChannel.UI:
                return sfxSource != null ? sfxSource : uiSource;
            case AudioVolumeChannel.Transition:
                return transitionSource;
            default:
                return sfxSource;
        }
    }

    private void ApplyAudioVolumes()
    {
        if (bgmSource != null && bgmFadeRoutine == null)
            bgmSource.volume = GetEffectiveVolume(AudioVolumeChannel.BGM);

        if (sfxSource != null)
            sfxSource.volume = GetEffectiveVolume(AudioVolumeChannel.SFX);

        if (uiSource != null)
            uiSource.volume = GetEffectiveVolume(AudioVolumeChannel.SFX);

        if (transitionSource != null)
            transitionSource.volume = GetEffectiveVolume(AudioVolumeChannel.Transition);

        for (int i = 0; i < oneShotPool.Count; i++)
        {
            AudioSource source = oneShotPool[i];
            if (source == null)
                continue;

            if (oneShotChannels.TryGetValue(source, out AudioVolumeChannel channel))
                source.volume = GetEffectiveVolume(channel);
        }
    }

    private float GetEffectiveVolume(AudioVolumeChannel channel)
    {
        switch (channel)
        {
            case AudioVolumeChannel.BGM:
                return Mathf.Clamp01(masterVolume * bgmVolume);
            case AudioVolumeChannel.SFX:
            case AudioVolumeChannel.UI:
                return Mathf.Clamp01(masterVolume * sfxVolume);
            case AudioVolumeChannel.Transition:
                return Mathf.Clamp01(masterVolume * transitionVolume);
            default:
                return Mathf.Clamp01(masterVolume);
        }
    }

    private void LoadSavedVolumeSettings()
    {
        if (!saveVolumeSettings)
        {
            ClampAudioSettings();
            return;
        }

        masterVolume = PlayerPrefs.GetFloat(MasterVolumePrefKey, masterVolume);
        bgmVolume = PlayerPrefs.GetFloat(BgmVolumePrefKey, bgmVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumePrefKey, sfxVolume);
        if (!PlayerPrefs.HasKey(SfxVolumePrefKey) && PlayerPrefs.HasKey(LegacyUiVolumePrefKey))
            sfxVolume = PlayerPrefs.GetFloat(LegacyUiVolumePrefKey, sfxVolume);
        transitionVolume = PlayerPrefs.GetFloat(TransitionVolumePrefKey, transitionVolume);
        ClampAudioSettings();
    }

    private void SaveVolumeSettings()
    {
        if (!saveVolumeSettings)
            return;

        PlayerPrefs.SetFloat(MasterVolumePrefKey, masterVolume);
        PlayerPrefs.SetFloat(BgmVolumePrefKey, bgmVolume);
        PlayerPrefs.SetFloat(SfxVolumePrefKey, sfxVolume);
        PlayerPrefs.DeleteKey(LegacyUiVolumePrefKey);
        PlayerPrefs.SetFloat(TransitionVolumePrefKey, transitionVolume);
        PlayerPrefs.Save();
    }

    private void ClampAudioSettings()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        bgmVolume = Mathf.Clamp01(bgmVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        transitionVolume = Mathf.Clamp01(transitionVolume);
        maxOneShotPoolSize = Mathf.Max(1, maxOneShotPoolSize);
        initialOneShotPoolSize = Mathf.Clamp(initialOneShotPoolSize, 1, maxOneShotPoolSize);
    }

    private bool HasAnyAssignedClip()
    {
        if (startSceneBGM != null ||
            lobbyBGM != null ||
            selectCharacterBGM != null ||
            victoryBGM != null ||
            defeatBGM != null ||
            startSceneButton != null ||
            lobbyButton != null ||
            HasAnyClip(bluff) ||
            slideIn != null ||
            collision != null ||
            end != null ||
            knightMovement != null ||
            knightLight != null ||
            knightHeavyAttackCharge != null ||
            knightHeavyAttack != null ||
            knightLow != null ||
            knightParry != null ||
            knightDefense != null ||
            knightJump != null ||
            knightUltimate != null ||
            knightAttackHitEnemy != null ||
            HasAnyClip(hit) ||
            knightHit != null ||
            knightParrySuccess != null ||
            knightEnergyGain != null ||
            knightEnergyFull != null ||
            oracleMovement != null ||
            oracleBolt != null ||
            oracleRiftCharge != null ||
            oracleRift != null ||
            oracleShift != null ||
            oracleWard != null ||
            oracleFade != null ||
            oracleSight != null ||
            oracleAttackHitEnemy != null ||
            oracleHit != null ||
            oracleWardSuccess != null ||
            oracleFadeSuccess != null ||
            oracleEnergyGain != null ||
            oracleEnergyFull != null ||
            knightUltimateClash != null ||
            victory != null ||
            defeat != null ||
            enemyDisconnect != null)
        {
            return true;
        }

        if (battleBGMs == null)
            return false;

        for (int i = 0; i < battleBGMs.Length; i++)
        {
            if (battleBGMs[i] != null)
                return true;
        }

        return false;
    }

    private bool HasAnyClip(AudioClip[] clips)
    {
        if (clips == null)
            return false;

        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
                return true;
        }

        return false;
    }

    private bool IsKnightClassName(string className)
    {
        return string.Equals(NormalizeClassName(className), "knight", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsOracleClassName(string className)
    {
        string normalized = NormalizeClassName(className);
        return string.Equals(normalized, "oracle", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "fortuneteller", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsKnightAction(ActionType action)
    {
        switch (action)
        {
            case ActionType.LightAttack:
            case ActionType.HeavyAttack:
            case ActionType.LowAttack:
            case ActionType.Parry:
            case ActionType.Defense:
            case ActionType.Ultimate:
                return true;

            default:
                return false;
        }
    }

    private bool IsOracleAction(ActionType action)
    {
        switch (action)
        {
            case ActionType.Bolt:
            case ActionType.Rift:
            case ActionType.Shift:
            case ActionType.Ward:
            case ActionType.Fade:
            case ActionType.Sight:
                return true;

            default:
                return false;
        }
    }

    private string NormalizeClassName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    private void LogMissingClip(string clipName)
    {
        if (!debugAudio)
            return;

        Debug.Log("[AudioManager] Missing AudioClip: " + clipName, this);
    }
}
