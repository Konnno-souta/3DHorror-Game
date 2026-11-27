using UnityEditor;
using UnityEngine;

/// <summary>
/// 懐中電灯と電池の説明文を、いい感じの文に変えるツール。
/// 全部の ItemDatabase の中から探して書き換える。
///  懐中電灯: Tag が Flashlight (または名前に「懐中電灯」)
///  電池    : 種類が Battery。回復するマス数が2以上なら(大)の文、1なら(小)の文
/// メニュー: Tools/Item/説明文 A, B, C (懐中電灯と電池を同じ雰囲気でそろえる)
/// </summary>
public static class ItemDescriptionTool
{
    private const string KeyF = "<color=#E8C36A>[ F ]</color>";
    private const string Gray = "<color=#9A9A9A>";
    private const string End = "</color>";

    private static string Cells(int n)
    {
        return "<color=#8FD16A>" + n + "マス</color>";
    }

    private class Style
    {
        public string label;
        public string flashlight;
        public string batterySmall;   // {N} は回復するマス数
        public string batteryBig;
    }

    private static readonly Style A = new Style
    {
        label = "A (標準)",
        flashlight =
            "古びた懐中電灯。この暗闇で、唯一たよれる明かり。\n" +
            KeyF + " で点ける / 消す\n" +
            "電池は少しずつ減っていく。\n" +
            Gray + "切れる前に、電池を探しておこう。" + End,
        batterySmall =
            "小さな乾電池。まだ少しだけ力が残っている。\n" +
            "使うと、懐中電灯の電池が {N} 回復する。\n" +
            Gray + "満タンのときは使えない。" + End,
        batteryBig =
            "大きめのバッテリー。ずっしりと重い。\n" +
            "使うと、懐中電灯の電池が {N} 回復する。\n" +
            Gray + "いざという時のために、取っておくのもいい。" + End,
    };

    private static readonly Style B = new Style
    {
        label = "B (短め)",
        flashlight =
            "少し頼りない、古い懐中電灯。\n" +
            KeyF + " で点ける / 消す\n" +
            Gray + "電池が尽きれば、闇に飲まれる。" + End,
        batterySmall =
            "小さな乾電池。\n" +
            "懐中電灯の電池が {N} 回復する。",
        batteryBig =
            "大きなバッテリー。\n" +
            "懐中電灯の電池が {N} 回復する。",
    };

    private static readonly Style C = new Style
    {
        label = "C (不気味)",
        flashlight =
            "床に転がっていた懐中電灯。\n" +
            "グリップに、乾いた血がこびりついている。\n" +
            KeyF + " で点ける / 消す\n" +
            Gray + "前の持ち主は、電池が切れたあと どうなったのだろう。" + End,
        batterySmall =
            "誰かのポケットに入っていた乾電池。少し湿っている。\n" +
            "懐中電灯の電池が {N} 回復する。\n" +
            Gray + "……まだ、使えるはずだ。" + End,
        batteryBig =
            "非常用ロッカーに残されていたバッテリー。\n" +
            "ラベルに、震える字で「にげて」と書かれている。\n" +
            "懐中電灯の電池が {N} 回復する。",
    };

    [MenuItem("Tools/Item/説明文 A (標準)")]
    private static void ApplyA() { Apply(A); }

    [MenuItem("Tools/Item/説明文 B (短め)")]
    private static void ApplyB() { Apply(B); }

    [MenuItem("Tools/Item/説明文 C (不気味)")]
    private static void ApplyC() { Apply(C); }

    private static void Apply(Style st)
    {
        int lights = 0, smalls = 0, bigs = 0;
        int batteryType = (int)ItemType.Battery;

        foreach (string guid in AssetDatabase.FindAssets("t:ItemDatabase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object db = AssetDatabase.LoadMainAssetAtPath(path);
            if (db == null) continue;

            SerializedObject so = new SerializedObject(db);
            SerializedProperty list = so.FindProperty("allItems");
            if (list == null || !list.isArray) continue;

            bool any = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                string tag = item.FindPropertyRelative("itemTag").stringValue;
                string itemName = item.FindPropertyRelative("itemName").stringValue ?? "";
                SerializedProperty typeProp = item.FindPropertyRelative("itemType");
                SerializedProperty desc = item.FindPropertyRelative("description");

                if (tag == "Flashlight" || itemName.Contains("懐中電灯"))
                {
                    desc.stringValue = st.flashlight;
                    lights++;
                    any = true;
                }
                else if (typeProp != null && typeProp.enumValueIndex == batteryType)
                {
                    SerializedProperty cellsProp = item.FindPropertyRelative("batteryRechargeCells");
                    int n = cellsProp != null ? Mathf.Max(1, cellsProp.intValue) : 1;
                    bool big = n >= 2;
                    desc.stringValue = (big ? st.batteryBig : st.batterySmall).Replace("{N}", Cells(n));
                    if (big) bigs++; else smalls++;
                    any = true;
                }
            }
            if (any)
            {
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(db);
            }
        }
        AssetDatabase.SaveAssets();

        string msg = "説明文を " + st.label + " にしました。\n\n" +
                     "懐中電灯: " + lights + "か所\n" +
                     "電池(小): " + smalls + "か所\n" +
                     "電池(大): " + bigs + "か所\n\n" +
                     "プレイして、インベントリで確認してください。";
        Debug.Log("[Item] " + msg);
        EditorUtility.DisplayDialog("Item", msg, "OK");
    }
}
