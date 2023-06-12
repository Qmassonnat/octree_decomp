using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class NodeData : ScriptableObject
{
    public Dictionary<string, CustomNodeScriptable> validNodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> invalidNodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> nodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, string> deletedNodes = new Dictionary<string, string>();
    public DeletedNodes deleted;

    public CustomNodeScriptable FindNode(string idx)
    {
        if (validNodes.ContainsKey(idx))
            return validNodes[idx];
        else if (invalidNodes.ContainsKey(idx))
            return invalidNodes[idx];
        else
            Debug.Log("node not found: " + idx);
        return CreateInstance<CustomNodeScriptable>();
    }

    public void SaveData(string filename) 
    {
        string path = "Assets/Data/" + filename;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder("Assets/Data", filename);

            CustomNodeScriptable valid = CreateInstance<CustomNodeScriptable>();
            AssetDatabase.CreateAsset(valid, path + "/valid.asset");
            foreach (string key in validNodes.Keys)
            {
                CustomNodeScriptable cn = validNodes[key];
                cn.SaveNeighbors();
                AssetDatabase.AddObjectToAsset(cn, valid);
            }
            AssetDatabase.ImportAsset(path + "/valid.asset");

            CustomNodeScriptable invalid = CreateInstance<CustomNodeScriptable>();
            AssetDatabase.CreateAsset(invalid, path + "/invalid.asset");
            foreach (string key in invalidNodes.Keys)
            {
                CustomNodeScriptable cn = invalidNodes[key];
                cn.SaveNeighbors();
                AssetDatabase.AddObjectToAsset(cn, invalid);
            }
            AssetDatabase.ImportAsset(path + "/invalid.asset");

            CustomNodeScriptable nodeList = CreateInstance<CustomNodeScriptable>();
            AssetDatabase.CreateAsset(nodeList, path + "/nodes.asset");
            foreach (string key in nodes.Keys)
            {
                CustomNodeScriptable cn = nodes[key];
                cn.SaveEdges();
                AssetDatabase.AddObjectToAsset(cn, nodeList);
            }
            AssetDatabase.ImportAsset(path + "/nodes.asset");
            deleted = CreateInstance<DeletedNodes>();
            deleted.Save(deletedNodes);
            AssetDatabase.CreateAsset(deleted, path + "/deleted.asset");
        }
    }

    public void LoadData(string path)
    {
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/valid.asset"))
        {
            if (cn.name != "valid")
            {
                validNodes[cn.name] = cn;
                cn.LoadNeighbors();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/invalid.asset"))
        {
            if (cn.name != "invalid")
            {
                invalidNodes[cn.name] = cn;
                cn.LoadNeighbors();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/nodes.asset"))
        {
            if (cn.name != "nodes")
            {
                nodes[cn.name] = cn;
                cn.LoadEdges();
            }
        }
        deleted = (DeletedNodes)AssetDatabase.LoadAssetAtPath(path +"/deleted.asset", typeof(DeletedNodes));
        deleted.Load();
        deletedNodes = deleted.deleted;
    }
}
