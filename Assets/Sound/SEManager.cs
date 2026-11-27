using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SEを鳴らす係。シーンに置かなくても、最初に使った時に自動でできる。
/// 使い方:  SEManager.Play(SEManager.Bank.uiClick);
///          SEManager.PlayAt(SEManager.Bank.doorOpen, transform.position);
/// </summary>
public class SEManager : MonoBehaviour
{
    private static SEManager instance;
    private static SEBank bank;
    private static bool bankLoaded;

    [SerializeField] private int voiceCount = 24;

    private readonly List<AudioSource> voices = new List<AudioSource>();
    private int nextVoice;

    /// <summary>Resources/SEBank を読み込む。無ければ空のBankを作る。</summary>
    public static SEBank Bank
    {
        get
        {
            if (!bankLoaded)
            {
                bankLoaded = true;
                bank = Resources.Load<SEBank>("SEBank");
                if (bank == null)
                {
                    Debug.LogWarning("[SE] Resources/SEBank が見つかりません。Tools/SE/1 を実行してください。");
                    bank = ScriptableObject.CreateInstance<SEBank>();
                }
            }
            return bank;
        }
    }

    public static float SEVolume => Bank.masterVolume * Bank.seVolume;

    /// <summary>true の間、ゲーム中のループ音(息、心臓、ライトのジー音)を消す。ゲームオーバー用。</summary>
    public static bool SilenceWorld { get; set; }
    public static float BGMVolume => Bank.masterVolume * Bank.bgmVolume;

    private static SEManager Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject go = new GameObject("[SEManager]");
                instance = go.AddComponent<SEManager>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        bank = null;
        bankLoaded = false;
        SilenceWorld = false;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadMode)
    {
        SilenceWorld = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        for (int i = 0; i < voiceCount; i++)
        {
            GameObject v = new GameObject("Voice" + i);
            v.transform.SetParent(transform, false);
            AudioSource s = v.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.dopplerLevel = 0f;
            voices.Add(s);
        }
    }

    private AudioSource NextVoice()
    {
        for (int i = 0; i < voices.Count; i++)
        {
            int idx = (nextVoice + i) % voices.Count;
            if (!voices[idx].isPlaying)
            {
                nextVoice = (idx + 1) % voices.Count;
                return voices[idx];
            }
        }
        AudioSource s = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Count;
        return s;
    }

    /// <summary>2Dで鳴らす(UI、自分の音など)</summary>
    public static AudioSource Play(SoundGroup g, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (g == null || !g.HasClip) return null;
        AudioSource s = Instance.NextVoice();
        s.transform.position = Vector3.zero;
        s.spatialBlend = 0f;
        float gain;
        s.clip = g.Pick(out gain);
        s.volume = g.volume * gain * volumeScale * SEVolume;
        s.pitch = g.PickPitch() * pitchScale;
        s.Play();
        return s;
    }

    /// <summary>3Dで鳴らす(その場所から聞こえる)</summary>
    public static AudioSource PlayAt(SoundGroup g, Vector3 position, float maxDistance = 25f, float volumeScale = 1f, float pitchScale = 1f)
    {
        if (g == null || !g.HasClip) return null;
        AudioSource s = Instance.NextVoice();
        s.transform.position = position;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = maxDistance;
        float gain;
        s.clip = g.Pick(out gain);
        s.volume = g.volume * gain * volumeScale * SEVolume;
        s.pitch = g.PickPitch() * pitchScale;
        s.Play();
        return s;
    }

    /// <summary>決まったAudioSourceで1回鳴らす(敵の声など、動く物に付けたい時)</summary>
    public static void PlayOn(AudioSource source, SoundGroup g, float volumeScale = 1f)
    {
        if (source == null || g == null || !g.HasClip) return;
        float gain;
        AudioClip c = g.Pick(out gain);
        source.pitch = g.PickPitch();
        source.PlayOneShot(c, g.volume * gain * volumeScale * SEVolume);
    }

    /// <summary>ループ用のAudioSourceを作る</summary>
    public static AudioSource CreateLoopSource(GameObject owner, SoundGroup g, bool is3D, float maxDistance = 15f)
    {
        AudioSource s = owner.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.dopplerLevel = 0f;
        s.volume = 0f;
        s.spatialBlend = is3D ? 1f : 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1f;
        s.maxDistance = maxDistance;
        if (g != null && g.HasClip)
        {
            s.clip = g.Pick();
            s.pitch = g.PickPitch();
        }
        return s;
    }

    /// <summary>ループ音の音量をなめらかに目標へ近づける</summary>
    public static void FadeLoop(AudioSource s, float target, float speed, bool randomStart = true)
    {
        if (s == null || s.clip == null) return;
        if (SilenceWorld && !s.ignoreListenerPause) target = 0f;
        s.volume = Mathf.MoveTowards(s.volume, target, speed * Time.unscaledDeltaTime);
        if (s.volume > 0.001f && !s.isPlaying)
        {
            if (randomStart) s.time = Random.Range(0f, s.clip.length * 0.9f);
            s.Play();
        }
        else if (s.volume <= 0.001f && s.isPlaying)
        {
            s.Stop();
        }
    }
}
