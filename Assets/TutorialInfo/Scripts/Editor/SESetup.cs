using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SEの準備をまとめてやるツール。
///  Tools/SE/1 SEBankを作る(空いている所に音を自動で入れる)
///  Tools/SE/1b SEBankの音を全部入れ直す
///  Tools/SE/2 このシーンにSEを付ける
///  Tools/SE/3 SEBankを開く
/// </summary>
public static class SESetup
{
    private const string BankPath = "Assets/Resources/SEBank.asset";

    private class Rule
    {
        public string field;
        public int max;
        public int skip;
        public string[] options;
        public Rule(string field, int max, int skip, params string[] options)
        {
            this.field = field; this.max = max; this.skip = skip; this.options = options;
        }
    }

    // options は左から順に探す。"a&b" は a と b の両方がパスに入っている物。
    private static readonly Rule[] Rules =
    {
        new Rule("footstepWalk", 5, 0, "footstep_concrete", "footstep"),
        new Rule("footstepRun", 5, 0, "footstep_concrete", "footstep"),
        new Rule("land", 2, 0, "impactsoft_heavy", "impactgeneric_light"),
        new Rule("breathTired", 1, 0, "04_player/breath", "breath"),
        new Rule("heartbeat", 1, 0, "04_player/heartbeat", "heartbeat"),
        new Rule("damage", 3, 0, "impactpunch_heavy", "impactplank_medium"),
        new Rule("death", 1, 0, "lowfrequency_explosion", "impactbell_heavy"),
        new Rule("heal", 1, 0, "interfacesounds&maximize_", "interfacesounds&confirmation_"),

        new Rule("flashlightOn", 1, 0, "uiaudio&switch", "interfacesounds&switch_"),
        new Rule("flashlightOff", 1, 1, "uiaudio&switch", "interfacesounds&switch_"),
        new Rule("flashlightEmpty", 1, 0, "uiaudio&click", "interfacesounds&click_"),
        new Rule("batteryLosing", 2, 0, "interfacesounds&glitch_", "computernoise_"),
        new Rule("batteryRecharge", 1, 0, "scifisounds&forcefield_", "interfacesounds&confirmation_"),


        new Rule("doorOpen", 2, 0, "scifisounds&dooropen_", "03_door/metaldoor&open", "dooropen"),
        new Rule("doorClose", 2, 0, "scifisounds&doorclose_", "03_door/metaldoor&close", "doorclose"),
        new Rule("doorLocked", 1, 0, "interfacesounds&error_", "03_door/metaldoor&lock"),
        new Rule("doorUnlock", 1, 0, "interfacesounds&confirmation_", "unlock"),

        new Rule("pickup", 2, 0, "rpgaudio&cloth", "rpgaudio&handlesmallleather", "interfacesounds&drop_"),
        new Rule("inventoryOpen", 1, 0, "interfacesounds&open_"),
        new Rule("inventoryClose", 1, 0, "interfacesounds&close_"),
        new Rule("inventoryMove", 2, 0, "interfacesounds&tick_", "interfacesounds&select_"),
        new Rule("inventoryUse", 1, 0, "interfacesounds&confirmation_", "interfacesounds&drop_"),

        new Rule("enemyStep", 3, 0, "impactsoft_heavy", "footstep_carpet", "footstep"),
        new Rule("enemyIdle", 6, 0, "05_enemy/silentgrowl", "05_enemy/snarl"),
        new Rule("enemyChase", 2, 0, "05_enemy/evilcreature", "05_enemy/snarl"),
        new Rule("enemyAttack", 2, 0, "monster-snarl-attack", "05_enemy/snarl"),
        new Rule("enemyHurt", 4, 0, "05_enemy/monsterpack", "05_enemy/snarl"),
        new Rule("enemyDeath", 2, 0, "05_enemy/monsterpack&die", "05_enemy/monsterpack&death", "05_enemy/monsterpack"),

        new Rule("uiHover", 1, 0, "interfacesounds&select_", "uiaudio&rollover"),
        new Rule("uiClick", 1, 0, "interfacesounds&click_", "uiaudio&click"),
        new Rule("gameStart", 1, 0, "07_stinger/scare", "lowfrequency_explosion"),
        new Rule("stinger", 2, 0, "07_stinger/scare"),
        new Rule("gameOver", 1, 0, "07_stinger/gameover"),

        new Rule("bgmTitle", 1, 0, "06_bgm/title"),
        new Rule("bgmGame", 1, 0, "06_bgm/game"),
        new Rule("bgmChase", 1, 0, "06_bgm/chase"),
    };

    // ------------------------------------------------------------
    [MenuItem("Tools/SE/1 SEBankを作る(空いている所に音を自動で入れる)")]
    public static void CreateBank() { BuildBank(false); }

    [MenuItem("Tools/SE/1b SEBankの音を全部入れ直す")]
    public static void RebuildBank()
    {
        if (!EditorUtility.DisplayDialog("SE", "SEBank の音を、全部入れ直します。手で入れた音も上書きされます。", "入れ直す", "やめる")) return;
        BuildBank(true);
    }

    [MenuItem("Tools/SE/4 風の音(環境音)を消す")]
    public static void RemoveAmbient()
    {
        SEBank bank = AssetDatabase.LoadAssetAtPath<SEBank>(BankPath);
        if (bank != null)
        {
            Undo.RecordObject(bank, "SE");
            bank.ambientRoom.clips = new AudioClip[0];
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
        }
        foreach (BGMPlayer b in Find<BGMPlayer>())
        {
            SerializedObject so = new SerializedObject(b);
            SerializedProperty p = so.FindProperty("playAmbient");
            if (p != null) { p.boolValue = false; so.ApplyModifiedProperties(); }
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("SE", "風の音(環境音)を消しました。", "OK");
    }

    [MenuItem("Tools/SE/3 SEBankを開く")]
    public static void SelectBank()
    {
        SEBank bank = AssetDatabase.LoadAssetAtPath<SEBank>(BankPath);
        if (bank == null) { CreateBank(); bank = AssetDatabase.LoadAssetAtPath<SEBank>(BankPath); }
        Selection.activeObject = bank;
        EditorGUIUtility.PingObject(bank);
    }

    private static SEBank LoadOrCreateBank()
    {
        SEBank bank = AssetDatabase.LoadAssetAtPath<SEBank>(BankPath);
        if (bank != null) return bank;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        bank = ScriptableObject.CreateInstance<SEBank>();
        AssetDatabase.CreateAsset(bank, BankPath);
        return bank;
    }

    private static void BuildBank(bool overwrite)
    {
        SEBank bank = LoadOrCreateBank();

        // 全部の AudioClip のパスを集める(小文字、/ 区切り)
        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);

        SerializedObject so = new SerializedObject(bank);
        List<string> filled = new List<string>();
        List<string> missing = new List<string>();
        HashSet<string> used = new HashSet<string>();

        foreach (Rule r in Rules)
        {
            SerializedProperty clipsProp = so.FindProperty(r.field + ".clips");
            if (clipsProp == null) { Debug.LogWarning("[SE] 項目が見つかりません: " + r.field); continue; }

            bool has = false;
            for (int i = 0; i < clipsProp.arraySize; i++)
            {
                if (clipsProp.GetArrayElementAtIndex(i).objectReferenceValue != null) { has = true; break; }
            }
            if (has && !overwrite) { filled.Add(r.field + " (そのまま)"); continue; }

            List<string> found = FindClips(paths, r);
            if (found.Count == 0)
            {
                missing.Add(r.field);
                if (overwrite) clipsProp.arraySize = 0;
                continue;
            }

            clipsProp.arraySize = found.Count;
            for (int i = 0; i < found.Count; i++)
            {
                AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(found[i]);
                clipsProp.GetArrayElementAtIndex(i).objectReferenceValue = c;
                used.Add(found[i]);
            }
            filled.Add(r.field + " = " + Path.GetFileName(found[0]) + (found.Count > 1 ? " ほか" + (found.Count - 1) + "個" : ""));
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(bank);

        int fixedCount = 0;
        foreach (string p in used) if (FixImport(p)) fixedCount++;

        ClearElectricInBank(bank);
        NormalizeBank(bank);

        AssetDatabase.SaveAssets();
        Selection.activeObject = bank;

        Debug.Log("[SE] SEBank を作りました: " + BankPath + "\n入った音:\n  " + string.Join("\n  ", filled) +
                  (missing.Count > 0 ? "\n\nまだ音が無い(なくても動きます):\n  " + string.Join("\n  ", missing) : "") +
                  "\n\n読み込み設定を直した音: " + fixedCount + "個");
        EditorUtility.DisplayDialog("SE", "SEBank を作りました。\n音が入った項目: " + (filled.Count) + "\nまだ無い項目: " + missing.Count +
                                    "\n\n次に Tools/SE/2 で、シーンにSEを付けてください。", "OK");
    }

    private static List<string> FindClips(List<string> paths, Rule r)
    {
        List<string> result = new List<string>();
        foreach (string opt in r.options)
        {
            string[] parts = opt.ToLowerInvariant().Split('&');
            int skipped = 0;
            foreach (string p in paths)
            {
                string lower = p.Replace('\\', '/').ToLowerInvariant();
                bool ok = true;
                foreach (string part in parts) { if (!lower.Contains(part)) { ok = false; break; } }
                if (!ok) continue;
                if (skipped < r.skip) { skipped++; continue; }
                result.Add(p);
                if (result.Count >= r.max) break;
            }
            if (result.Count > 0) break;
        }
        return result;
    }

    /// <summary>短い音はすぐ鳴るように、長い音は少ないメモリで鳴るようにする</summary>
    private static bool FixImport(string path)
    {
        AudioImporter imp = AssetImporter.GetAtPath(path) as AudioImporter;
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (imp == null || clip == null) return false;

        AudioImporterSampleSettings st = imp.defaultSampleSettings;
        AudioClipLoadType want;
        if (clip.length > 10f) want = AudioClipLoadType.Streaming;
        else if (clip.length > 2f) want = AudioClipLoadType.CompressedInMemory;
        else want = AudioClipLoadType.DecompressOnLoad;

        if (st.loadType == want) return false;
        st.loadType = want;
        st.compressionFormat = AudioCompressionFormat.Vorbis;
        st.quality = want == AudioClipLoadType.Streaming ? 0.6f : 0.8f;
        imp.defaultSampleSettings = st;
        imp.loadInBackground = want != AudioClipLoadType.DecompressOnLoad;
        imp.SaveAndReimport();
        return true;
    }

    // ------------------------------------------------------------
    [MenuItem("Tools/SE/5 電気の音を消して、音量をおまかせで整える")]
    public static void CleanAndBalance()
    {
        SEBank bank = LoadOrCreateBank();
        ApplyVolumePreset(bank);
        ClearElectricInBank(bank);
        int normalized = NormalizeBank(bank);
        int prefabs = RemoveElectricInPrefabs();
        int sceneLights = RemoveElectricInScene();
        int doors = 0;
        foreach (var d in Find<KeyDoor>()) doors += SetupDoor(d, bank);
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        string msg = "電気の音を消したライト(シーン): " + sceneLights +
                     "\n電気の音を消したプレハブ: " + prefabs +
                     "\n大きさをそろえた音: " + normalized +
                     "\n全部の音量をおまかせの値にしました";
        Debug.Log("[SE] " + msg);
        EditorUtility.DisplayDialog("SE", msg + "\n\n他のシーンでも、このメニューを押してから保存してください。", "OK");
    }

    // おまかせの音量(ホラー向け: 足音やUIは小さめ、敵と演出ははっきり)
    private static void ApplyVolumePreset(SEBank b)
    {
        Undo.RecordObject(b, "SE volume");
        b.masterVolume = 0.9f; b.seVolume = 1f; b.bgmVolume = 0.5f;
        Set(b.footstepWalk, 0.22f, 0.9f, 1.05f);
        Set(b.footstepRun, 0.32f, 1.05f, 1.2f);
        Set(b.land, 0.35f, 0.95f, 1.05f);
        Set(b.breathTired, 0.35f, 1f, 1f);
        Set(b.heartbeat, 0.45f, 1f, 1f);
        Set(b.damage, 0.7f, 0.95f, 1.05f);
        Set(b.death, 0.8f, 0.8f, 0.85f);
        Set(b.heal, 0.4f, 1f, 1.05f);
        Set(b.flashlightOn, 0.35f, 0.97f, 1.03f);
        Set(b.flashlightOff, 0.35f, 0.85f, 0.95f);
        Set(b.flashlightEmpty, 0.3f, 0.7f, 0.75f);
        Set(b.batteryLosing, 0.25f, 0.95f, 1.05f);
        Set(b.batteryRecharge, 0.4f, 1f, 1.05f);
        Set(b.doorOpen, 0.55f, 0.95f, 1.05f);
        Set(b.doorClose, 0.55f, 0.95f, 1.05f);
        Set(b.doorLocked, 0.45f, 0.95f, 1f);
        Set(b.doorUnlock, 0.5f, 1f, 1f);
        Set(b.pickup, 0.45f, 0.95f, 1.05f);
        Set(b.inventoryOpen, 0.3f, 1f, 1f);
        Set(b.inventoryClose, 0.3f, 0.95f, 0.95f);
        Set(b.inventoryMove, 0.18f, 1f, 1.05f);
        Set(b.inventoryUse, 0.4f, 1f, 1f);
        Set(b.enemyStep, 0.55f, 0.6f, 0.75f);
        Set(b.enemyIdle, 0.55f, 0.8f, 0.95f);
        Set(b.enemyChase, 0.8f, 0.85f, 0.95f);
        Set(b.enemyAttack, 0.85f, 0.9f, 1f);
        Set(b.enemyHurt, 0.7f, 0.9f, 1f);
        Set(b.enemyDeath, 0.8f, 0.8f, 0.85f);
        Set(b.uiHover, 0.15f, 0.98f, 1.02f);
        Set(b.uiClick, 0.35f, 1f, 1f);
        Set(b.gameStart, 0.6f, 1f, 1f);
        Set(b.stinger, 0.55f, 1f, 1f);
        Set(b.gameOver, 0.7f, 1f, 1f);
        Set(b.bgmTitle, 0.45f, 1f, 1f);
        Set(b.bgmGame, 0.25f, 1f, 1f);
        Set(b.bgmChase, 0.45f, 1f, 1f);
    }

    private static void Set(SoundGroup g, float volume, float pmin, float pmax)
    {
        g.volume = volume; g.pitchMin = pmin; g.pitchMax = pmax;
    }

    private static void ClearElectricInBank(SEBank b)
    {
        b.lightHum.clips = new AudioClip[0]; b.lightHum.gains = new float[0];
        b.lightFlicker.clips = new AudioClip[0]; b.lightFlicker.gains = new float[0];
        b.lightSpark.clips = new AudioClip[0]; b.lightSpark.gains = new float[0];
        b.ambientRoom.clips = new AudioClip[0]; b.ambientRoom.gains = new float[0];
        EditorUtility.SetDirty(b);
    }

    /// <summary>
    /// 音ファイルごとの大きさの違いをそろえる。大きすぎる音だけ小さくする(小さい音は大きくできない)。
    /// 長い音(BGMや心臓)はそのまま。
    /// </summary>
    private static int NormalizeBank(SEBank b)
    {
        const float target = 0.22f;   // 一番うるさい瞬間(50ms)の平均の大きさの目標
        int count = 0;
        foreach (var f in typeof(SEBank).GetFields())
        {
            if (f.FieldType != typeof(SoundGroup)) continue;
            SoundGroup g = (SoundGroup)f.GetValue(b);
            if (g == null || g.clips == null) continue;
            g.gains = new float[g.clips.Length];
            for (int i = 0; i < g.clips.Length; i++)
            {
                g.gains[i] = 1f;
                AudioClip c = g.clips[i];
                if (c == null || c.length > 10f) continue;
                float loud = MeasureLoudness(c);
                if (loud <= 0.0001f) continue;
                g.gains[i] = Mathf.Clamp(target / loud, 0.15f, 1f);
                count++;
            }
        }
        EditorUtility.SetDirty(b);
        return count;
    }

    private static float MeasureLoudness(AudioClip c)
    {
        try
        {
            if (c.loadState != AudioDataLoadState.Loaded) c.LoadAudioData();
            int ch = Mathf.Max(1, c.channels);
            float[] data = new float[c.samples * ch];
            if (!c.GetData(data, 0)) return 0f;
            int win = Mathf.Max(1, (int)(c.frequency * 0.05f)) * ch;
            float best = 0f;
            for (int start = 0; start + win <= data.Length; start += win / 2)
            {
                double sum = 0;
                for (int k = start; k < start + win; k++) sum += data[k] * data[k];
                float rms = Mathf.Sqrt((float)(sum / win));
                if (rms > best) best = rms;
            }
            if (best <= 0f && data.Length > 0)
            {
                double sum = 0;
                for (int k = 0; k < data.Length; k++) sum += data[k] * data[k];
                best = Mathf.Sqrt((float)(sum / data.Length));
            }
            return best;
        }
        catch { return 0f; }
    }

    /// <summary>シーンのライトから、電気の音(ジー音・バチッ音)を外す</summary>
    private static int RemoveElectricInScene()
    {
        int n = 0;
        foreach (var l in Find<LightSE>())
        {
            Undo.DestroyObjectImmediate(l);
            n++;
        }
        foreach (var b in Find<BrokenLight>())
        {
            if (CleanBrokenLight(b, true)) n++;
        }
        // シーンに直接置いてある、電気・ファン・うなりの音も止める
        foreach (var a in Find<AudioSource>())
        {
            if (a.clip == null || !a.enabled) continue;
            if (a.GetComponentInParent<BGMPlayer>() != null) continue;
            if (IsElectricName(a.clip.name))
            {
                Undo.RecordObject(a, "SE");
                a.enabled = false;
                n++;
            }
        }
        return n;
    }

    /// <summary>プレハブ(BrokenLight.prefab など)からも電気の音を外す</summary>
    private static int RemoveElectricInPrefabs()
    {
        int n = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) continue;
            if (asset.GetComponentInChildren<BrokenLight>(true) == null && asset.GetComponentInChildren<LightSE>(true) == null) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            foreach (var l in root.GetComponentsInChildren<LightSE>(true)) { Object.DestroyImmediate(l); changed = true; }
            foreach (var b in root.GetComponentsInChildren<BrokenLight>(true)) changed |= CleanBrokenLight(b, false);
            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); n++; }
            PrefabUtility.UnloadPrefabContents(root);
        }
        return n;
    }

    private static readonly string[] ElectricWords = { "fan", "hum", "buzz", "electr", "spark", "zap", "flicker", "rumble", "light" };

    private static bool IsElectricName(string clipName)
    {
        string n = clipName.ToLowerInvariant();
        foreach (string w in ElectricWords) if (n.Contains(w)) return true;
        return false;
    }

    private static bool CleanBrokenLight(BrokenLight light, bool undo)
    {
        SerializedObject so = new SerializedObject(light);
        bool changed = false;
        SerializedProperty pop = so.FindProperty("popClip");
        if (pop != null && pop.objectReferenceValue != null) { pop.objectReferenceValue = null; changed = true; }
        SerializedProperty buzz = so.FindProperty("buzzSource");
        if (buzz != null && buzz.objectReferenceValue is AudioSource s)
        {
            if (undo) Undo.RecordObject(s, "SE");
            if (s.clip != null || s.playOnAwake) { s.clip = null; s.playOnAwake = false; changed = true; }
        }
        if (undo) so.ApplyModifiedProperties(); else so.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    // ------------------------------------------------------------
    [MenuItem("Tools/SE/2 このシーンにSEを付ける")]
    public static void SetupScene()
    {
        SEBank bank = AssetDatabase.LoadAssetAtPath<SEBank>(BankPath);
        if (bank == null)
        {
            BuildBank(false);
            bank = AssetDatabase.LoadAssetAtPath<SEBank>(BankPath);
        }

        List<string> log = new List<string>();
        int count;

        count = 0;
        foreach (var p in Find<HorrorPlayerController>())
        {
            count += Add<PlayerSE>(p.gameObject);
            count += Add<HeartbeatSE>(p.gameObject);
        }
        log.Add("プレイヤー(足音・息・心臓・ダメージ): " + count);

        count = 0;
        foreach (var f in Find<HorrorCameraFollow>()) count += Add<FlashlightSE>(f.gameObject);
        log.Add("懐中電灯: " + count);

        count = 0;
        foreach (var e in Find<ShadowlopEnemyAI>()) count += Add<EnemySE>(e.gameObject);
        log.Add("敵: " + count);

        count = 0;
        foreach (var inv in Find<InventoryUI>()) count += Add<InventorySE>(inv.gameObject);
        log.Add("インベントリ: " + count);

        log.Add("電気の音を消したライト: " + RemoveElectricInScene());

        count = 0;
        foreach (var d in Find<KeyDoor>()) count += SetupDoor(d, bank);
        log.Add("ドア(音を入れた): " + count);

        count = 0;
        foreach (var btn in Find<Button>())
        {
            bool added = Add<UIButtonSE>(btn.gameObject) > 0;
            if (added) count++;
            bool isStart = btn.GetComponent<StartButton>() != null || btn.name.ToLowerInvariant().Contains("start");
            if (isStart)
            {
                SerializedObject so = new SerializedObject(btn.GetComponent<UIButtonSE>());
                so.FindProperty("kind").enumValueIndex = (int)UIButtonSE.Kind.Start;
                so.ApplyModifiedProperties();
            }
        }
        log.Add("ボタン: " + count);

        count = 0;
        foreach (var g in Find<GameOverScreen>()) count += Add<GameOverSE>(g.gameObject);
        log.Add("ゲームオーバー: " + count);

        // BGM
        BGMPlayer bgm = Object.FindFirstObjectByType<BGMPlayer>(FindObjectsInactive.Include);
        bool isTitle = EditorSceneManager.GetActiveScene().name.ToLowerInvariant().Contains("title");
        if (bgm == null)
        {
            GameObject go = new GameObject("[BGM]");
            Undo.RegisterCreatedObjectUndo(go, "BGM");
            bgm = go.AddComponent<BGMPlayer>();
            log.Add("BGM: 作りました");
        }
        else log.Add("BGM: もうあります");
        SerializedObject bso = new SerializedObject(bgm);
        bso.FindProperty("mode").enumValueIndex = isTitle ? (int)BGMPlayer.Mode.Title : (int)BGMPlayer.Mode.Game;
        bso.ApplyModifiedProperties();
        log.Add("BGMの種類: " + (isTitle ? "タイトル" : "ゲーム"));

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        string msg = string.Join("\n", log);
        Debug.Log("[SE] シーンにSEを付けました\n" + msg);
        EditorUtility.DisplayDialog("SE", "シーンにSEを付けました。\n\n" + msg + "\n\nシーンを保存してください。", "OK");
    }

    private static T[] Find<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private static int Add<T>(GameObject go) where T : Component
    {
        if (go.GetComponent<T>() != null) return 0;
        Undo.AddComponent<T>(go);
        return 1;
    }

    private static AudioClip First(SoundGroup g)
    {
        if (g == null || g.clips == null) return null;
        foreach (AudioClip c in g.clips) if (c != null) return c;
        return null;
    }

    private static AudioSource Make3DSource(GameObject go, float maxDistance)
    {
        AudioSource s = Undo.AddComponent<AudioSource>(go);
        s.playOnAwake = false;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 1.5f;
        s.maxDistance = maxDistance;
        s.dopplerLevel = 0f;
        return s;
    }

    private static bool SetIfEmpty(SerializedObject so, string name, Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null || value == null || p.objectReferenceValue != null) return false;
        p.objectReferenceValue = value;
        return true;
    }

    private static int SetupDoor(KeyDoor door, SEBank bank)
    {
        SerializedObject so = new SerializedObject(door);
        bool changed = false;

        SerializedProperty src = so.FindProperty("audioSource");
        if (src != null && src.objectReferenceValue == null)
        {
            AudioSource s = door.GetComponent<AudioSource>();
            if (s == null) s = Make3DSource(door.gameObject, 20f);
            src.objectReferenceValue = s;
            changed = true;
        }
        if (src != null && src.objectReferenceValue is AudioSource ds)
        {
            Undo.RecordObject(ds, "SE");
            ds.volume = bank.doorOpen.volume * bank.masterVolume * bank.seVolume;
            ds.spatialBlend = 1f;
            ds.rolloffMode = AudioRolloffMode.Linear;
            ds.minDistance = 1.5f;
            ds.maxDistance = 20f;
        }

        changed |= SetIfEmpty(so, "openClip", First(bank.doorOpen));
        changed |= SetIfEmpty(so, "closeClip", First(bank.doorClose));
        changed |= SetIfEmpty(so, "lockedClip", First(bank.doorLocked));
        changed |= SetIfEmpty(so, "unlockClip", First(bank.doorUnlock));

        so.ApplyModifiedProperties();
        return changed ? 1 : 0;
    }
}
