using System.Reflection;
using UnityEngine;

/// <summary>
/// 他のスクリプトの private な変数を、変更せずに読むための小さな道具。
/// (元のスクリプトを書き換えなくても、SEを付けられるようにするため)
/// </summary>
public class SEField
{
    private readonly FieldInfo field;
    private readonly object owner;

    public bool IsValid => field != null && owner != null;

    public SEField(Component owner, string fieldName)
    {
        this.owner = owner;
        if (owner == null) return;
        System.Type t = owner.GetType();
        while (t != null && field == null)
        {
            field = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            t = t.BaseType;
        }
        if (field == null)
        {
            Debug.LogWarning("[SE] " + owner.GetType().Name + " に " + fieldName + " が見つかりません");
        }
    }

    public object Get()
    {
        return IsValid ? field.GetValue(owner) : null;
    }

    public bool GetBool(bool fallback = false)
    {
        object v = Get();
        return v is bool b ? b : fallback;
    }

    public int GetInt(int fallback = 0)
    {
        object v = Get();
        if (v is int i) return i;
        if (v != null && v.GetType().IsEnum) return System.Convert.ToInt32(v);
        return fallback;
    }

    public string GetName()
    {
        object v = Get();
        return v != null ? v.ToString() : "";
    }

    public int GetCount(int fallback = 0)
    {
        object v = Get();
        return v is System.Collections.ICollection c ? c.Count : fallback;
    }
}
