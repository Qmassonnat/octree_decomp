using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DeletedNodes : ScriptableObject
{
    public Dictionary<string, string> deleted = new Dictionary<string, string>();
    public List<string> deleted_idx = new List<string>();
    public List<string> deleted_values = new List<string>();

    public void Save(Dictionary<string,string> deleted)
    {
        foreach (var (idx, value) in deleted)
        {
            deleted_idx.Add(idx);
            deleted_values.Add(value);
        }
    }

    public void Load()
    {
        for (int i = 0; i < deleted_idx.Count; i++)
            deleted[deleted_idx[i]] = deleted_values[i];
    }
}
