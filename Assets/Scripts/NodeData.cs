using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

public class NodeData : ScriptableObject
{
    public Dictionary<string, CustomNodeScriptable> cells = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> validNodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> invalidNodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> nodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, string> deletedNodes = new Dictionary<string, string>();
    public List<string> path_cells = new List<string>();
    public DeletedNodes deleted;
    private string[] directions = new string[] { "up", "down", "left", "right", "forward", "backward" };

    public CustomNodeScriptable FindNode(string idx)
    {
        if (cells.ContainsKey(idx))
            return cells[idx];
        else if (validNodes.ContainsKey(idx))
            return validNodes[idx];
        else if (invalidNodes.ContainsKey(idx))
            return invalidNodes[idx];
        return null;
    }

    public (Dictionary<string, List<string>>, Dictionary<string, List<string>>) ComputeNeighbors(CustomNodeScriptable cn, int i)
    {
        // from the neighbors of a node about to be split, compute the correct neighbors for a child node
        Dictionary<string, List<string>> new_valid_neighbors = new Dictionary<string, List<string>>(cn.valid_neighbors.Count,
                                                            cn.valid_neighbors.Comparer);
        // copy the old dictionnary
        foreach (KeyValuePair<string, List<string>> entry in cn.valid_neighbors)
        {
            new_valid_neighbors.Add(entry.Key, new List<string>(entry.Value));
        }
        // Remove the neighbors no longer connected to the child node
        foreach (string direction in directions)
        {
            if (TestDirection(direction, i))
            {
                new_valid_neighbors[direction] = new List<string> { };
                // to avoid giving a children-sized neighbor to all children nodes
                List<string> to_remove = new List<string>();
                foreach (string neighbor in new_valid_neighbors[GetOppositeDirection(direction)])
                {
                    if (neighbor.Length > cn.idx.Length && ! (neighbor[cn.idx.Length].ToString() == ((i + Direction2Int(direction)).ToString())))
                        to_remove.Add(neighbor);
                }
                foreach (string neighbor in to_remove)
                {
                    new_valid_neighbors[GetOppositeDirection(direction)].Remove(neighbor);
                }
            }
        }

        // Add the correct children as new neighbors
        foreach (string direction in directions)
        {
            if (TestDirection(direction, i))
                new_valid_neighbors[direction].Add(cn.idx + (i + Direction2Int(direction)).ToString());
        }

        Dictionary<string, List<string>> new_invalid_neighbors = new Dictionary<string, List<string>>(cn.invalid_neighbors.Count,
                                                            cn.invalid_neighbors.Comparer);
        // copy the old dictionnary
        foreach (KeyValuePair<string, List<string>> entry in cn.invalid_neighbors)
        {
            new_invalid_neighbors.Add(entry.Key, new List<string>(entry.Value));
        }
        // Remove the neighbors no longer connected to the child node
        foreach (string direction in directions)
        {
            if (TestDirection(direction, i))
            {
                new_invalid_neighbors[direction] = new List<string> { };
                // to avoid giving a children-sized neighbor to all children nodes
                List<string> to_remove = new List<string>();
                foreach (string neighbor in new_invalid_neighbors[GetOppositeDirection(direction)])
                {
                    if (neighbor.Length > cn.idx.Length && !(neighbor[cn.idx.Length].ToString() == ((i + Direction2Int(direction)).ToString())))
                        to_remove.Add(neighbor);
                }
                foreach (string neighbor in to_remove)
                {
                    new_invalid_neighbors[GetOppositeDirection(direction)].Remove(neighbor);
                }
            }
        }
        return (new_valid_neighbors, new_invalid_neighbors);
    }

    public void UpdateNeighborsOnSplit(CustomNodeScriptable cn)
    {
        // when splitting a node, update the adjacent nodes' neighbors to have the relevant smaller nodes
        foreach (KeyValuePair<string, List<string>> entry in cn.valid_neighbors)
        {
            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNodeScriptable neigh = FindNode(neighbor_);
                // remove the parent entry and add the correct children entries
                if (neigh == null)
                    Debug.Log("spliterror");
                List<string> n_list = neigh.valid_neighbors[GetOppositeDirection(entry.Key)];
                n_list.Remove(cn.idx);
                // if the node is larger or has not been split yet
                if (neigh.idx.Length <= cn.idx.Length)
                {
                    neigh.AddValidNeighbors(GetOppositeDirection(entry.Key), GetNeighborsSplit(cn.idx, entry.Key));
                }
                // if the node has already been split
                else
                {
                    int last = int.Parse(neigh.idx[cn.idx.Length].ToString()); ;
                    neigh.AddValidNeighbors(GetOppositeDirection(entry.Key), new List<string> { cn.idx + (last + Direction2Int(entry.Key)).ToString() });
                }
            }
        }

        foreach (KeyValuePair<string, List<string>> entry in cn.invalid_neighbors)
        {

            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNodeScriptable neigh = FindNode(neighbor_);
                // remove the parent entry and add the correct children entries
                List<string> n_list = neigh.invalid_neighbors[GetOppositeDirection(entry.Key)];
                n_list.Remove(cn.idx);
                // if the node is larger or has not been split yet
                if (neigh.idx.Length <= cn.idx.Length)
                {
                    neigh.AddValidNeighbors(GetOppositeDirection(entry.Key), GetNeighborsSplit(cn.idx, entry.Key));
                }
                // if the node has already been split
                else
                {
                    int last = int.Parse(neigh.idx[cn.idx.Length].ToString());
                    neigh.AddValidNeighbors(GetOppositeDirection(entry.Key), new List<string> { cn.idx + (last + Direction2Int(entry.Key)).ToString() });
                }
            }
        }
    }

    public void UpdateNeighborsOnInvalid(CustomNodeScriptable cn)
    {
        // udpate the neighbors when a node becomes invalid
        foreach (KeyValuePair<string, List<string>> entry in cn.valid_neighbors)
        {
            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNodeScriptable neigh = FindNode(neighbor_);
                // remove the node from valid neighbors and it to invalid neighbors
                neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Remove(cn.idx);
                neigh.invalid_neighbors[GetOppositeDirection(entry.Key)].Add(cn.idx);
            }
        }
        foreach (KeyValuePair<string, List<string>> entry in cn.invalid_neighbors)
        {
            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNodeScriptable neigh = FindNode(neighbor_);
                // remove the node from valid neighbors and it to invalid neighbors
                neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Remove(cn.idx);
                neigh.invalid_neighbors[GetOppositeDirection(entry.Key)].Add(cn.idx);
            }
        }

    }

    public void UpdateNeighborsOnValid(CustomNodeScriptable cn)
    {
        // udpate the neighbors when a node becomes valid
        foreach (KeyValuePair<string, List<string>> entry in cn.valid_neighbors)
        {
            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNodeScriptable neigh = FindNode(neighbor_);
                // remove the node from valid neighbors and add it to invalid neighbors
                neigh.invalid_neighbors[GetOppositeDirection(entry.Key)].Remove(cn.idx);
                neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Add(cn.idx);
            }
        }
        foreach (KeyValuePair<string, List<string>> entry in cn.invalid_neighbors)
        {
            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNodeScriptable neigh = FindNode(neighbor_);
                // remove the node from valid neighbors and add it to invalid neighbors
                    neigh.invalid_neighbors[GetOppositeDirection(entry.Key)].Remove(cn.idx);
                    neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Add(cn.idx);
            }
        }
    }

    public void UpdateNeighborsOnMerge(CustomNodeScriptable parent)
    {
        // compute the neighbors of the parent from the children's neighbors
        // discard the old neighbor information of the parent
        foreach (string direction in directions)
        {
            parent.valid_neighbors[direction] = new List<string>();
            parent.invalid_neighbors[direction] = new List<string>();
        }
        foreach (string child_idx in parent.children)
        {
            CustomNodeScriptable child = FindNode(child_idx);
            int i = int.Parse(child.idx[child.idx.Length - 1].ToString());
            foreach (var direction in directions)
            {
                // only add the non-children neighbors
                if (!TestDirection(direction, i))
                {
                    foreach (var neigh_idx in child.valid_neighbors[direction])
                    {                                           
                        // make sure the neighbor has not already been added
                        if (!parent.valid_neighbors[direction].Contains(neigh_idx))
                            parent.valid_neighbors[direction].Add(neigh_idx);
                        // remove the child from the adjacent node's neighbor list
                        CustomNodeScriptable neigh = FindNode(neigh_idx);
                        neigh.valid_neighbors[GetOppositeDirection(direction)].Remove(child.idx);
                    }
                    foreach (var neigh_idx in child.invalid_neighbors[direction])
                    {
                        if (!parent.invalid_neighbors[direction].Contains(neigh_idx))
                            parent.invalid_neighbors[direction].Add(neigh_idx);
                        // remove the child from the adjacent node's neighbor list
                        CustomNodeScriptable neigh = FindNode(neigh_idx);
                        neigh.valid_neighbors[GetOppositeDirection(direction)].Remove(child.idx);
                    }
                }
            }
        }
        // update the adjacent's neighbors to reference the parent and not the children
        foreach (string direction in directions)
        {
            foreach (var neigh_idx in parent.valid_neighbors[direction])
            {
                // add the parent to the adjacent node's neighbor list
                CustomNodeScriptable neigh = FindNode(neigh_idx);
                if (!neigh.valid_neighbors[GetOppositeDirection(direction)].Contains(parent.idx))
                    neigh.valid_neighbors[GetOppositeDirection(direction)].Add(parent.idx);
            }
        }
        foreach (string direction in directions)
        {
            foreach (var neigh_idx in parent.invalid_neighbors[direction])
            {
                // add the parent to the adjacent node's neighbor list
                CustomNodeScriptable neigh = FindNode(neigh_idx);
                if (!neigh.valid_neighbors[GetOppositeDirection(direction)].Contains(parent.idx))
                    neigh.valid_neighbors[GetOppositeDirection(direction)].Add(parent.idx);
            }
        }
    }

    public string GetOppositeDirection(string direction)
    {
        string res = "";
        if (direction.Equals("up"))
            res = "down";
        if (direction.Equals("down"))
            res = "up";
        if (direction.Equals("left"))
            res = "right";
        if (direction.Equals("right"))
            res = "left";
        if (direction.Equals("forward"))
            res = "backward";
        if (direction.Equals("backward"))
            res = "forward";
        return res;
    }

    public List<string> GetNeighborsSplit(string idx, string direction)
    {
        List<string> res = new List<string> { };
        if (direction.Equals("up"))
            res = new List<string> { idx + "4", idx + "5", idx + "6", idx + "7" };
        if (direction.Equals("down"))
            res = new List<string> { idx + "0", idx + "1", idx + "2", idx + "3" };
        if (direction.Equals("left"))
            res = new List<string> { idx + "0", idx + "2", idx + "4", idx + "6" };
        if (direction.Equals("right"))
            res = new List<string> { idx + "1", idx + "3", idx + "5", idx + "7" };
        if (direction.Equals("forward"))
            res = new List<string> { idx + "2", idx + "3", idx + "6", idx + "7" };
        if (direction.Equals("backward"))
            res = new List<string> { idx + "0", idx + "1", idx + "4", idx + "5" };
        return res;
    }

    public int Direction2Int(string direction)
    {

        if (direction.Equals("up"))
            return 4;
        if (direction.Equals("down"))
            return -4;
        if (direction.Equals("left"))
            return -1;
        if (direction.Equals("right"))
            return 1;
        if (direction.Equals("forward"))
            return 2;
        if (direction.Equals("backward"))
            return -2;
        return 0;
    }

    public bool TestDirection(string direction, int i)
    {
        if (direction.Equals("up"))
            return i < 4;
        if (direction.Equals("down"))
            return i >= 4;
        if (direction.Equals("left"))
            return i % 2 == 1;
        if (direction.Equals("right"))
            return i % 2 == 0;
        if (direction.Equals("forward"))
            return i % 4 < 2;
        if (direction.Equals("backward"))
            return i % 4 >= 2;
        return false;
    }


    public void SaveData(string filename) 
    {
        Debug.Log(nodes.Count + " nodes " + validNodes.Count + " valid cells");
        string path = "Assets/Data/" + filename;
        if (AssetDatabase.IsValidFolder(path))
            Directory.Delete(path, true);
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

        CustomNodeScriptable cell = CreateInstance<CustomNodeScriptable>();
        AssetDatabase.CreateAsset(cell, path + "/cells.asset");
        foreach (string key in cells.Keys)
        {
            CustomNodeScriptable cn = cells[key];
            cn.SaveNeighbors();
            AssetDatabase.AddObjectToAsset(cn, invalid);
        }
        AssetDatabase.ImportAsset(path + "/cells.asset");

        deleted = CreateInstance<DeletedNodes>();
        deleted.Save(deletedNodes);
        AssetDatabase.CreateAsset(deleted, path + "/deleted.asset");
    }

    public void LoadData(string path)
    {
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/valid.asset"))
        {
            if (cn.name != "valid")
            {
                validNodes[cn.idx] = cn;
                cn.LoadNeighbors();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/invalid.asset"))
        {
            if (cn.name != "invalid")
            {
                //GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                //g.transform.position = cn.position;
                //g.transform.localScale = cn.scale;
                invalidNodes[cn.idx] = cn;
                cn.LoadNeighbors();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/nodes.asset"))
        {
            if (cn.name != "nodes")
            {
                nodes[cn.idx] = cn;
                cn.LoadEdges();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/cells.asset"))
        {
            if (cn.name != "cell")
            {
                cells[cn.idx] = cn;
                cn.LoadNeighbors();
            }
        }

        deleted = (DeletedNodes)AssetDatabase.LoadAssetAtPath(path +"/deleted.asset", typeof(DeletedNodes));
        deleted.Load();
        deletedNodes = deleted.deleted;
    }


}
