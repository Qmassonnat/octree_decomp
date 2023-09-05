using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class NodeDataMerged : ScriptableObject
{
    public Dictionary<string, CustomNodeScriptable> cells = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> validNodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> invalidNodes = new Dictionary<string, CustomNodeScriptable>();
    public Dictionary<string, CustomNodeScriptable> nodes = new Dictionary<string, CustomNodeScriptable>();
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

    public string IsNeighbor(CustomNodeScriptable c1, CustomNodeScriptable c2)
    {
        string res;
        // c1 and c2 are neighbors iff they share a side, i.e. if there is a strict overlap for 2 coordinates and an equality for the third
        if ((c1.position.x + c1.scale.x/2 == c2.position.x - c2.scale.x/2 || c1.position.x - c1.scale.x/2 == c2.position.x + c2.scale.x/2)
        && (c1.position.y + c1.scale.y/2 > c2.position.y - c2.scale.y/2) && (c1.position.y - c1.scale.y/2 < c2.position.y + c2.scale.y/2) 
        && (c1.position.z + c1.scale.z/2 > c2.position.z - c2.scale.z/2) && (c1.position.z - c1.scale.z/2 < c2.position.z + c2.scale.z/2))
        {
            if (c1.position.x < c2.position.x)
                res = "right";
            else
                res = "left";
        }
        else if ((c1.position.y + c1.scale.y/2 == c2.position.y - c2.scale.y/2 || c1.position.y - c1.scale.y/2 == c2.position.y + c2.scale.y/2)
        && (c1.position.x + c1.scale.x/2 > c2.position.x - c2.scale.x/2) && (c1.position.x - c1.scale.x/2 < c2.position.x + c2.scale.x/2) 
        && (c1.position.z + c1.scale.z/2 > c2.position.z - c2.scale.z/2) && (c1.position.z - c1.scale.z/2 < c2.position.z + c2.scale.z/2))
        {
            if (c1.position.y < c2.position.y)
                res = "up";
            else
                res = "down";
        }
        
        else if ((c1.position.z + c1.scale.z/2 == c2.position.z - c2.scale.z/2 || c1.position.z - c1.scale.z/2 == c2.position.z + c2.scale.z/2)
            && (c1.position.x + c1.scale.x/2 > c2.position.x - c2.scale.x/2) && (c1.position.x - c1.scale.x/2 < c2.position.x + c2.scale.x/2) 
            && (c1.position.y + c1.scale.y/2 > c2.position.y - c2.scale.y/2) && (c1.position.y - c1.scale.y/2 < c2.position.y + c2.scale.y/2)) {
            if (c1.position.z < c2.position.z)
                res = "forward";
            else
                res = "backward";
        }
        else
            res = null;
        // if they are neighbors, return the direction from c1 to c2
        return res;
    }

    public (Dictionary<string, List<string>>, Dictionary<string, List<string>>) ComputeChildNeighbors(CustomNodeScriptable parent, CustomNodeScriptable child)
    {
        // from the neighbors of a node about to be split, compute the correct external neighbors for a child node
        Dictionary<string, List<string>> new_valid_neighbors = new Dictionary<string, List<string>>(parent.valid_neighbors.Count,
                                                            parent.valid_neighbors.Comparer);
        // copy the old dictionnary
        foreach (KeyValuePair<string, List<string>> entry in parent.valid_neighbors)
        {
            new_valid_neighbors.Add(entry.Key, new List<string>(entry.Value));
        }
        // Remove the neighbors no longer connected to the child node
        foreach (string direction in directions)
        {
            List<string> to_remove = new List<string>();
            foreach (string neigh_idx in new_valid_neighbors[direction])
            {
                CustomNodeScriptable neigh = FindNode(neigh_idx);
                if (IsNeighbor(neigh, child) == null)
                    to_remove.Add(neigh_idx);
            }
            foreach (string neighbor in to_remove)
                new_valid_neighbors[direction].Remove(neighbor);
        }

        Dictionary<string, List<string>> new_invalid_neighbors = new Dictionary<string, List<string>>(parent.invalid_neighbors.Count,
                                                            parent.invalid_neighbors.Comparer);
        // copy the old dictionnary
        foreach (KeyValuePair<string, List<string>> entry in parent.invalid_neighbors)
        {
            new_invalid_neighbors.Add(entry.Key, new List<string>(entry.Value));
        }
        // Remove the neighbors no longer connected to the child node
        foreach (string direction in directions)
        {
            List<string> to_remove = new List<string>();
            foreach (string neigh_idx in new_invalid_neighbors[direction])
            {
                CustomNodeScriptable neigh = FindNode(neigh_idx);
                if (IsNeighbor(neigh, child) == null)
                    to_remove.Add(neigh_idx);
            }
            foreach (string neighbor in to_remove)
                new_invalid_neighbors[direction].Remove(neighbor);
        }
        return (new_valid_neighbors, new_invalid_neighbors);
    }

    public void UpdateNeighborsOnSplit(CustomNodeScriptable cn, List<CustomNodeScriptable> children)
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
                neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Remove(cn.idx);
                foreach (var child in children) {
                    if (IsNeighbor(neigh, child) != null)
                        neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Add(child.idx);
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
                neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Remove(cn.idx);
                foreach (var child in children)
                {
                    if (IsNeighbor(neigh, child) != null)
                        neigh.valid_neighbors[GetOppositeDirection(entry.Key)].Add(child.idx);
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
            foreach (var direction in directions)
            {
                // only add the non-children neighbors
                foreach (var neigh_idx in child.valid_neighbors[direction])
                {
                    CustomNodeScriptable neigh = FindNode(neigh_idx);
                    if (child.parent == neigh.parent)
                        continue;
                    // make sure the neighbor has not already been added
                    if (!parent.valid_neighbors[direction].Contains(neigh_idx))
                        parent.valid_neighbors[direction].Add(neigh_idx);
                    // remove the child from the adjacent node's neighbor list
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

    public void SaveData(string filename)
    {
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
            AssetDatabase.AddObjectToAsset(cn.Clone(), valid);
        }
        AssetDatabase.ImportAsset(path + "/valid.asset");

        CustomNodeScriptable invalid = CreateInstance<CustomNodeScriptable>();
        AssetDatabase.CreateAsset(invalid, path + "/invalid.asset");
        foreach (string key in invalidNodes.Keys)
        {
            CustomNodeScriptable cn = invalidNodes[key];
            cn.SaveNeighbors();
            AssetDatabase.AddObjectToAsset(cn.Clone(), invalid);
        }
        AssetDatabase.ImportAsset(path + "/invalid.asset");

        CustomNodeScriptable nodeList = CreateInstance<CustomNodeScriptable>();
        AssetDatabase.CreateAsset(nodeList, path + "/nodes.asset");
        foreach (string key in nodes.Keys)
        {
            CustomNodeScriptable cn = nodes[key];
            cn.SaveEdges();
            AssetDatabase.AddObjectToAsset(cn.Clone(), nodeList);
        }
        AssetDatabase.ImportAsset(path + "/nodes.asset");

        CustomNodeScriptable cell = CreateInstance<CustomNodeScriptable>();
        AssetDatabase.CreateAsset(cell, path + "/cells.asset");
        foreach (string key in cells.Keys)
        {
            CustomNodeScriptable cn = cells[key];
            cn.SaveNeighbors();
            AssetDatabase.AddObjectToAsset(cn.Clone(), cell);
        }
        AssetDatabase.ImportAsset(path + "/cells.asset");

        // store the global id for merged octtree
        using (StreamWriter writetext = new StreamWriter(path + "/id.txt"))
        {
            writetext.WriteLine(GameObject.Find("PathFinding").GetComponent<OctTreeMerged>().global_id);
        }
    }

    public int LoadData(string path)
    {
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/valid.asset"))
        {
            var cn_copy = cn.Clone();
            if (cn_copy.name != "valid")
            {
                validNodes[cn_copy.idx] = cn_copy;
                cn_copy.LoadNeighbors();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/invalid.asset"))
        {
            var cn_copy = cn.Clone();
            if (cn_copy.name != "invalid")
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.transform.position = cn_copy.position;
                g.transform.localScale = cn_copy.scale;
                invalidNodes[cn_copy.idx] = cn_copy;
                cn_copy.LoadNeighbors();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/nodes.asset"))
        {
            var cn_copy = cn.Clone();
            if (cn_copy.name != "nodes")
            {
                nodes[cn_copy.idx] = cn_copy;
                cn_copy.LoadEdges();
            }
        }
        foreach (CustomNodeScriptable cn in AssetDatabase.LoadAllAssetsAtPath(path + "/cells.asset"))
        {
            var cn_copy = cn.Clone();
            if (cn_copy.name != "cells")
            {
                cells[cn_copy.idx] = cn_copy;
                cn_copy.LoadNeighbors();
            }
        }
        using (StreamReader readtext = new StreamReader(path + "/id.txt"))
        {
            int global_id = int.Parse(readtext.ReadLine());
            return global_id;
        }

    }

}
