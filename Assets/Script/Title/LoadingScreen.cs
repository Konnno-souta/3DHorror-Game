using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトルからゲームへ行くあいだの、ロード画面。
/// シーンに置かなくてよい。ボタンなどから LoadingScreen.Load("MainScene") と呼ぶだけで、自分で画面を作る。
///
///   1. 画面が、ゆっくり黒くなる
///   2. 黒い画面に「LOADING」がちらつき、下に赤い進み具合の線と、ヒントの文が出る
///   3. 裏でゲームのシーンを読みこむ(少なくとも minShowSeconds 秒は見せる)
///   4. 読みこみが終わったら、ゲームの画面へ切りかえ、黒からゆっくり明るくなる
///
/// 文字のフォントは、今の画面で使っている日本語のフォントを、自動で借りる。
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    [Header("時間(秒)")]
    [SerializeField] private float fadeOutSeconds = 0.8f;
    [SerializeField] private float minShowSeconds = 2.5f;
    [SerializeField] private float fadeInSeconds = 1.2f;

    [Header("読みこみが終わったら、キーを押すまで待つ")]
    [SerializeField] private bool waitForKey = false;

    [Header("ヒント(ランダムに1つ出る)")]
    [SerializeField] private string[] tips =
    {
        "物音を立てると、気づかれる…",
        "懐中電灯の電池は、大切に使え",
        "E キーで拾う。TAB で持ち物を開く",
        "走りすぎると、息が切れる",
        "色のついたカードキーは、同じ色の扉を開ける",
        "暗い所で、立ち止まってはいけない",
        "あれに、見つかるな"
    };

    private static LoadingScreen current;

    private CanvasGroup group;
    private TMP_Text loadingText;
    private TMP_Text percentText;
    private TMP_Text tipText;
    private TMP_Text pressText;
    private RectTransform barFill;
    private float barWidth = 600f;

    /// <summary>
    /// ロード画面を出して、sceneName のシーンへ行く。
    /// </summary>
    public static void Load(string sceneName)
    {
        if (current != null) return;   // 2回押しても、1回だけ

        GameObject go = new GameObject("LoadingScreen");
        DontDestroyOnLoad(go);
        current = go.AddComponent<LoadingScreen>();
        current.Build();
        current.StartCoroutine(current.Run(sceneName));
    }

    // ---------------- 画面を作る ----------------

    private void Build()
    {
        TMP_FontAsset font = FindJapaneseFont();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();   // うしろのボタンを押せないようにする

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = true;

        // 黒い背景
        Image bg = NewImage("Background", transform, Color.black);
        Stretch(bg.rectTransform);

        // LOADING
        loadingText = NewText("Loading", transform, font, "LOADING", 64f, new Color(0.85f, 0.85f, 0.85f));
        loadingText.characterSpacing = 30f;
        Place(loadingText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1200f, 100f));

        // 進み具合の線
        Image barBack = NewImage("BarBack", transform, new Color(1f, 1f, 1f, 0.08f));
        Place(barBack.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(barWidth, 4f));

        Image fill = NewImage("BarFill", barBack.transform, new Color(0.75f, 0.05f, 0.05f));
        barFill = fill.rectTransform;
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.anchoredPosition = Vector2.zero;
        barFill.sizeDelta = new Vector2(0f, 0f);

        percentText = NewText("Percent", transform, font, "0%", 26f, new Color(0.6f, 0.6f, 0.6f));
        Place(percentText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(400f, 40f));

        // ヒント
        string tip = (tips != null && tips.Length > 0) ? tips[Random.Range(0, tips.Length)] : "";
        tipText = NewText("Tip", transform, font, tip, 30f, new Color(0.55f, 0.55f, 0.55f));
        Place(tipText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1600f, 60f));

        pressText = NewText("Press", transform, font, "クリックで はじめる", 30f, new Color(0.8f, 0.8f, 0.8f));
        Place(pressText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(800f, 50f));
        pressText.gameObject.SetActive(false);
    }

    // ---------------- 流れ ----------------

    private IEnumerator Run(string sceneName)
    {
        Time.timeScale = 1f;

        // 1. 黒くなる
        yield return Fade(0f, 1f, fadeOutSeconds);

        // 2. 読みこみ
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError("[LoadingScreen] シーン「" + sceneName + "」が見つかりません。Build Settings に入っているか確かめてください。");
            yield return Fade(1f, 0f, 0.3f);
            Finish();
            yield break;
        }
        op.allowSceneActivation = false;

        float shown = 0f;
        float display = 0f;

        while (true)
        {
            shown += Time.unscaledDeltaTime;

            // 本当の進み具合(0.9 で読みこみ完了)と、見せている時間の、遅いほうに合わせて、なめらかに進める
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float byTime = Mathf.Clamp01(shown / Mathf.Max(0.01f, minShowSeconds));
            float target = Mathf.Min(real, byTime);
            display = Mathf.MoveTowards(display, target, Time.unscaledDeltaTime * 0.8f);

            UpdateBar(display);
            FlickerLoading();

            if (op.progress >= 0.9f && display >= 0.999f) break;
            yield return null;
        }

        UpdateBar(1f);

        // (任意)キーを押すまで待つ
        if (waitForKey)
        {
            pressText.gameObject.SetActive(true);
            while (!Input.anyKeyDown)
            {
                FlickerLoading();
                float a = 0.5f + Mathf.Sin(Time.unscaledTime * 3f) * 0.5f;
                pressText.alpha = a;
                yield return null;
            }
        }

        // 3. ゲームの画面へ
        loadingText.alpha = 0f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        // 文字は消して、黒い画面だけにしてから、明るくする
        loadingText.gameObject.SetActive(false);
        percentText.gameObject.SetActive(false);
        tipText.gameObject.SetActive(false);
        pressText.gameObject.SetActive(false);
        barFill.parent.gameObject.SetActive(false);

        yield return null;
        yield return Fade(1f, 0f, fadeInSeconds);
        Finish();
    }

    private void Finish()
    {
        current = null;
        Destroy(gameObject);
    }

    private IEnumerator Fade(float from, float to, float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / Mathf.Max(0.01f, seconds)));
            yield return null;
        }
        group.alpha = to;
    }

    private void UpdateBar(float value)
    {
        barFill.sizeDelta = new Vector2(barWidth * value, 0f);
        percentText.text = Mathf.RoundToInt(value * 100f) + "%";
    }

    /// <summary>
    /// 「LOADING」を、こわれた電灯のように、ちらつかせる。
    /// </summary>
    private void FlickerLoading()
    {
        float a = 0.75f + Mathf.PerlinNoise(Time.unscaledTime * 6f, 0f) * 0.25f;
        if (Random.value < 0.03f) a = 0.1f;
        loadingText.alpha = a;
        // 点が1つずつ増える(文字の幅が変わらないよう、足りない点は透明にする)
        int count = 1 + (int)(Time.unscaledTime * 2f) % 3;
        loadingText.text = "LOADING" + new string('.', count) + "<alpha=#00>" + new string('.', 3 - count) + "</alpha>";
    }

    // ---------------- 部品を作る道具 ----------------

    /// <summary>
    /// 今の画面の文字が使っているフォント(日本語が出るもの)を借りる。なければ TMP の標準のフォント。
    /// </summary>
    private static TMP_FontAsset FindJapaneseFont()
    {
        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font != null && t.font != TMP_Settings.defaultFontAsset) return t.font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    private static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }
}
