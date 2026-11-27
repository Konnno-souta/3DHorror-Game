using System;
using UnityEngine;

/// <summary>
/// 1種類の音(複数入れると、毎回ランダムで1つ選ぶ)
/// </summary>
[Serializable]
public class SoundGroup
{
    public AudioClip[] clips = new AudioClip[0];
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("ピッチのランダム幅(最小)")]
    [Range(0.3f, 2f)] public float pitchMin = 0.95f;
    [Tooltip("ピッチのランダム幅(最大)")]
    [Range(0.3f, 2f)] public float pitchMax = 1.05f;

    [Tooltip("音ごとの大きさの補正(Tools/SE/5 が自動で入れる)")]
    [HideInInspector] public float[] gains = new float[0];

    private int lastIndex = -1;

    public bool HasClip
    {
        get
        {
            if (clips == null) return false;
            for (int i = 0; i < clips.Length; i++) if (clips[i] != null) return true;
            return false;
        }
    }

    /// <summary>ランダムで1つ選ぶ。前回と同じ音はなるべく避ける。</summary>
    public AudioClip Pick()
    {
        float g;
        return Pick(out g);
    }

    /// <summary>ランダムで1つ選び、その音の大きさの補正も返す</summary>
    public AudioClip Pick(out float gain)
    {
        gain = 1f;
        AudioClip c = PickInternal();
        if (c != null && gains != null && lastIndex >= 0 && lastIndex < gains.Length && gains[lastIndex] > 0f)
        {
            gain = gains[lastIndex];
        }
        return c;
    }

    private AudioClip PickInternal()
    {
        if (!HasClip) return null;
        int count = clips.Length;
        for (int tries = 0; tries < 6; tries++)
        {
            int i = UnityEngine.Random.Range(0, count);
            if (clips[i] == null) continue;
            if (count > 1 && i == lastIndex) continue;
            lastIndex = i;
            return clips[i];
        }
        for (int i = 0; i < count; i++) if (clips[i] != null) { lastIndex = i; return clips[i]; }
        return null;
    }

    public float PickPitch()
    {
        return UnityEngine.Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
    }
}

/// <summary>
/// ゲーム中の全部のSEをまとめた入れ物。
/// Assets/Resources/SEBank.asset に置くと自動で読み込まれる。
/// Tools/SE/1 で自動作成・自動割り当てできる。
/// </summary>
[CreateAssetMenu(menuName = "Horror/SE Bank", fileName = "SEBank")]
public class SEBank : ScriptableObject
{
    [Header("全体の音量")]
    [Range(0f, 1f)] public float masterVolume = 0.9f;
    [Range(0f, 1f)] public float seVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    [Header("プレイヤー")]
    public SoundGroup footstepWalk = new SoundGroup { volume = 0.22f, pitchMin = 0.9f, pitchMax = 1.05f };
    public SoundGroup footstepRun = new SoundGroup { volume = 0.32f, pitchMin = 1.05f, pitchMax = 1.2f };
    public SoundGroup land = new SoundGroup { volume = 0.35f };
    public SoundGroup breathTired = new SoundGroup { volume = 0.35f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup heartbeat = new SoundGroup { volume = 0.45f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup damage = new SoundGroup { volume = 0.7f };
    public SoundGroup death = new SoundGroup { volume = 0.8f, pitchMin = 0.8f, pitchMax = 0.85f };
    public SoundGroup heal = new SoundGroup { volume = 0.4f };

    [Header("懐中電灯")]
    public SoundGroup flashlightOn = new SoundGroup { volume = 0.35f };
    public SoundGroup flashlightOff = new SoundGroup { volume = 0.35f, pitchMin = 0.85f, pitchMax = 0.95f };
    public SoundGroup flashlightEmpty = new SoundGroup { volume = 0.3f, pitchMin = 0.7f, pitchMax = 0.75f };
    public SoundGroup batteryLosing = new SoundGroup { volume = 0.25f };
    public SoundGroup batteryRecharge = new SoundGroup { volume = 0.4f };

    [Header("部屋のライト(電気の音。今は使っていない)")]
    public SoundGroup lightHum = new SoundGroup { volume = 0.0f, pitchMin = 0.97f, pitchMax = 1.03f };
    public SoundGroup lightFlicker = new SoundGroup { volume = 0.0f };
    public SoundGroup lightSpark = new SoundGroup { volume = 0.0f };

    [Header("ドア・カードリーダー")]
    public SoundGroup doorOpen = new SoundGroup { volume = 0.55f };
    public SoundGroup doorClose = new SoundGroup { volume = 0.55f };
    public SoundGroup doorLocked = new SoundGroup { volume = 0.45f };
    public SoundGroup doorUnlock = new SoundGroup { volume = 0.5f };

    [Header("アイテム・インベントリ")]
    public SoundGroup pickup = new SoundGroup { volume = 0.45f };
    public SoundGroup inventoryOpen = new SoundGroup { volume = 0.3f };
    public SoundGroup inventoryClose = new SoundGroup { volume = 0.3f };
    public SoundGroup inventoryMove = new SoundGroup { volume = 0.18f, pitchMin = 1f, pitchMax = 1.05f };
    public SoundGroup inventoryUse = new SoundGroup { volume = 0.4f };

    [Header("敵")]
    public SoundGroup enemyStep = new SoundGroup { volume = 0.55f, pitchMin = 0.6f, pitchMax = 0.75f };
    public SoundGroup enemyIdle = new SoundGroup { volume = 0.55f, pitchMin = 0.8f, pitchMax = 0.95f };
    public SoundGroup enemyChase = new SoundGroup { volume = 0.8f, pitchMin = 0.85f, pitchMax = 0.95f };
    public SoundGroup enemyAttack = new SoundGroup { volume = 0.85f };
    public SoundGroup enemyHurt = new SoundGroup { volume = 0.7f };
    public SoundGroup enemyDeath = new SoundGroup { volume = 0.8f, pitchMin = 0.8f, pitchMax = 0.85f };

    [Header("UI・演出")]
    public SoundGroup uiHover = new SoundGroup { volume = 0.15f };
    public SoundGroup uiClick = new SoundGroup { volume = 0.35f };
    public SoundGroup gameStart = new SoundGroup { volume = 0.6f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup stinger = new SoundGroup { volume = 0.55f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup gameOver = new SoundGroup { volume = 0.7f, pitchMin = 1f, pitchMax = 1f };

    [Header("BGM・環境音(ループ)")]
    public SoundGroup bgmTitle = new SoundGroup { volume = 0.45f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup bgmGame = new SoundGroup { volume = 0.25f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup bgmChase = new SoundGroup { volume = 0.45f, pitchMin = 1f, pitchMax = 1f };
    public SoundGroup ambientRoom = new SoundGroup { volume = 0.0f, pitchMin = 1f, pitchMax = 1f };
}
