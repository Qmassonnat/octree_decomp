using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.IO;
using UnityEngine.SceneManagement;

public class OctTreeFast : MonoBehaviour
{
    public float minSize;
    public GameObject node;
    public float bound;
    public float zBound;
    public float elongated_criteria; // 0 for no merging, high values for aggressive merging
    public bool load;
    [HideInInspector] public NodeData data;
    private List<GameObject> to_split;
    private string task;
    private float currentScale;
    private double t0;
    private string[] directions;

    // Start is called before the first frame update
    void Start()
    {
        data = ScriptableObject.CreateInstance<NodeData>();
        gameObject.GetComponent<AstarFast>().data = data;
        string path = "Assets/Data/" + SceneManager.GetActiveScene().name + minSize;
        if (load && AssetDatabase.IsValidFolder(path))
        {
            t0 = Time.realtimeSinceStartupAsDouble;
            data.LoadData(path);
            Debug.Log("Loading data from file " + path + " in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            task = "finished";
            gameObject.tag = "Finished";
        }
        else
        {
            t0 = Time.realtimeSinceStartupAsDouble;
            Vector3 center = new Vector3(0, zBound/2, 0);
            Vector3 scale = new Vector3(2 * bound, zBound, 2 * bound);
            to_split = new List<GameObject>();
            BuildOctree(center, scale, gameObject, "0", 
                new Dictionary<string, List<string>> {
                { "left", new List<string>{ } },
                { "right", new List<string>{ } },
                { "up", new List<string>{ } },
                { "down", new List<string>{ } },
                { "forward", new List<string>{ } },
                { "backward", new List<string>{ } }}
        );
        task = "build";
        currentScale = Mathf.Min(2 * bound, zBound);
        directions = new string[] { "up", "down", "left", "right", "forward", "backward" };
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (task == "build" || currentScale >= minSize)
        {
            currentScale /= 2;
            List<GameObject> to_split_copy = new List<GameObject>();
            foreach (GameObject node in to_split)
                to_split_copy.Add(node);
            to_split = new List<GameObject>();
            foreach (GameObject node in to_split_copy)
                SplitNode(node);
            task = "clean";
        }
        else
        {
            if (task == "clean")
            {
                Debug.Log("OctTree built in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
                t0 = Time.realtimeSinceStartupAsDouble;
                task = "prune";
                CleanOctTree();
            }
            else if (task == "prune")
            {
                Debug.Log("OctTree cleaned in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
                Debug.Log("Before pruning: " + data.validNodes.Count + " valid nodes " + data.invalidNodes.Count + " invalid nodes");
                if (elongated_criteria > 0)
                {
                    t0 = Time.realtimeSinceStartupAsDouble;
                    PruneOctTree();
                    Debug.Log("After pruning: " + data.validNodes.Count + " valid nodes " + data.invalidNodes.Count + " invalid nodes");
                    Debug.Log("OctTree pruned in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
                }
                task = "graph";
            }

            else if (task == "graph")
            {
                
                t0 = Time.realtimeSinceStartupAsDouble;
                task = "finished";
                gameObject.tag = "Finished";
                BuildGraph();
                Debug.Log("Graph built in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            }
        }
    }

    public void BuildOctree(Vector3 center, Vector3 scale, GameObject parent, string idx, Dictionary<string, List<string>> neigbors)
    {
        GameObject new_node;
        new_node = Instantiate(node,
            center,
            parent.transform.rotation) ;
        new_node.transform.localScale = scale;
        new_node.tag = "Valid";
        if (parent)
            new_node.transform.parent = parent.transform;

        CustomNode cn = new_node.GetComponent<CustomNode>();
        cn.idx = idx;
        new_node.name = "_"+idx;    
        cn.neighbors = neigbors;

        if (!GetComponent<CollisionCheck>().IsEmpty(center, scale))
        {
            new_node.tag = "Node";
            to_split.Add(new_node);
        }

    }


    public void SplitNode(GameObject node_)
    {
        // Make sure this node has not already been split
        if (node_.transform.childCount == 0)
        {
            Vector3 current_scale = node_.transform.lossyScale;
            CustomNode cn = node_.GetComponent<CustomNode>();
            if (current_scale.x > minSize)
            {
                Vector3[] new_centers = new Vector3[8];
                Vector3 center = node_.transform.position;

                Vector3 new_scale = current_scale / 2;
                Vector3 corner = Center2corner(center, current_scale);
                new_centers[0] = Corner2center(corner, new_scale);
                new_centers[1] = Corner2center(corner + Vector3.right * new_scale.x, new_scale);
                new_centers[2] = Corner2center(corner + Vector3.forward * new_scale.z, new_scale);
                new_centers[3] = Corner2center(corner + Vector3.right * new_scale.x + Vector3.forward * new_scale.z, new_scale);
                new_centers[4] = Corner2center(corner + Vector3.up * new_scale.y, new_scale);
                new_centers[5] = Corner2center(corner + Vector3.right * new_scale.x + Vector3.up * new_scale.y, new_scale);
                new_centers[6] = Corner2center(corner + Vector3.up * new_scale.y + Vector3.forward * new_scale.z, new_scale);
                new_centers[7] = Corner2center(corner + Vector3.right * new_scale.x + Vector3.up * new_scale.y + Vector3.forward * new_scale.z, new_scale);

                cn.UpdateNeighborsOnSplit();
                string idx = node_.GetComponent<CustomNode>().idx;
                for (int i = 0; i < 8; i++)
                {
                    Dictionary<string, List<string>> new_neigbors = cn.ComputeNeighbors(idx, i);         
                    BuildOctree(new_centers[i], new_scale, node_, idx+i.ToString(), new_neigbors);
                }
            }
            // if the leaf has not already been added
            else if (!data.invalidNodes.ContainsKey(node_.name))
            {
                CustomNodeScriptable cvi = ScriptableObject.CreateInstance<CustomNodeScriptable>();
                cvi.position = node_.transform.position;
                cvi.scale = current_scale;
                cvi.name = node_.name;
                cvi.idx = cn.idx;
                cvi.neighbors = cn.neighbors;
                cvi.tag = "Invalid";
                data.invalidNodes[cvi.name] = cvi;
                Destroy(node_);
            }
        }
    }

    Vector3 Center2corner(Vector3 center, Vector3 scale)
    {
        return center - scale/2;
    }

    Vector3 Corner2center(Vector3 corner, Vector3 scale)
    {
        return corner + scale / 2;
    }
    public void CleanOctTree() {
        GameObject[] nodes;
        nodes = GameObject.FindGameObjectsWithTag("Valid");
        foreach (GameObject node_ in nodes)
        {
            CustomNode cn = node_.GetComponent<CustomNode>();
            CustomNodeScriptable cvv = ScriptableObject.CreateInstance<CustomNodeScriptable>();
            cvv.name = node_.name;
            cvv.position = node_.transform.position;
            cvv.scale = node_.transform.lossyScale;
            cvv.idx = cn.idx;
            cvv.neighbors = cn.neighbors;
            cvv.tag = "Valid";
            data.validNodes[cvv.name] = cvv;
            Destroy(node_);
        }
    }

    public void PruneOctTree()
    {
        Stack validStack = new Stack(data.validNodes.Values);
        Stack invalidStack = new Stack(data.invalidNodes.Values);
        while (validStack.Count > 0)
        {
            CustomNodeScriptable n1 = (CustomNodeScriptable)validStack.Pop();
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2 and n1 has not already been merged
                if (n1.neighbors[key].Count == 1 && !data.deletedNodes.ContainsKey(n1.name))
                {
                    CustomNodeScriptable n2 = data.FindNode("_" + n1.neighbors[key][0]);
                    // if n2 is valid and only has n1 as neighbor on the opposite direction merge them
                    string opposite = n1.GetOppositeDirection(key);
                    bool elongated = false;
                    // don't merge if the resulting node would be too elongated
                    if (key == "up" || key == "down")
                        elongated = n1.scale.y + n2.scale.y > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.z);
                    if (key == "left" || key == "right")
                        elongated = n1.scale.x + n2.scale.x > elongated_criteria * Mathf.Min(n1.scale.y, n1.scale.z);
                    if (key == "forward" || key == "backward")
                        elongated = n1.scale.z + n2.scale.z > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.y);
                    if (n2.tag == "Valid" && n2.neighbors[opposite].Count == 1 && !elongated)
                    {
                        MergeNeighbors(n1, n2, key);
                        data.deletedNodes[n2.name] = n1.name;
                        // add n1 again to check if this merge enabled further merges
                        validStack.Push(n1);
                        break;
                    }
                }
            }
        }
        while (invalidStack.Count > 0)
        {
            CustomNodeScriptable n1 = (CustomNodeScriptable)invalidStack.Pop();
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2 and n1 has not already been merged
                if (n1.neighbors[key].Count == 1 && !data.deletedNodes.ContainsKey(n1.name))
                {
                    CustomNodeScriptable n2 = data.FindNode("_" + n1.neighbors[key][0]);
                    // if n2 is valid and only has n1 as neighbor on the opposite direction merge them
                    string opposite = n1.GetOppositeDirection(key);
                    bool elongated = false;
                    // don't merge if the resulting node would be too elongated
                    if (key == "up" || key == "down")
                        elongated = n1.scale.y + n2.scale.y > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.z);
                    if (key == "left" || key == "right")
                        elongated = n1.scale.x + n2.scale.x > elongated_criteria * Mathf.Min(n1.scale.y, n1.scale.z);
                    if (key == "forward" || key == "backward")
                        elongated = n1.scale.z + n2.scale.z > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.y);
                    if (n2.tag == "Invalid" && n2.neighbors[opposite].Count == 1 && !elongated)
                    {
                        MergeNeighbors(n1, n2, key);
                        data.deletedNodes[n2.name] = n1.name;
                        // add n1 again to check if this merge enabled further merges
                        invalidStack.Push(n1);
                        break;
                    }
                }
            }
        } 
    }



    public void MergeNeighbors(CustomNodeScriptable n1, CustomNodeScriptable n2, string direction)
    {
        foreach (string key in directions) {
            // add the neighbors of n2 to those of n1
            n1.neighbors[key] = n1.neighbors[key].Union(n2.neighbors[key]).ToList();
            // remove n1 and n2 from the neighbor list
            n1.neighbors[key].RemoveAll(s => s == n1.idx);
            n1.neighbors[key].RemoveAll(s => s == n2.idx);

            // update the neighbors by adding n1 and removing n2 (set operations)
            string opposite = n1.GetOppositeDirection(key);
            foreach (string idx in n1.neighbors[key])
            {
                CustomNodeScriptable neighbor = data.FindNode("_" +idx);
                neighbor.neighbors[opposite].Remove(n2.idx);
                neighbor.neighbors[opposite] = neighbor.neighbors[opposite].Union(new List<string> { n1.idx }).ToList();
            }
        }

        // update the position and size of n1
        // add the scales on the neighboring direction and keep the other 2, handling the difference in relative scales
        if (direction == "up" || direction == "down")
        {
            n1.position = (n1.scale.y * n1.position + n2.scale.y * n2.position) / (n1.scale.y + n2.scale.y);
            n1.scale += n2.scale.y * Vector3.up;
        }
        if (direction == "right" || direction == "left")
        {
            n1.position = (n1.scale.x * n1.position + n2.scale.x * n2.position) / (n1.scale.x + n2.scale.x);
            n1.scale += n2.scale.x * Vector3.right;
        }
        if (direction == "forward" || direction == "backward")
        {
            n1.position = (n1.scale.z * n1.position + n2.scale.z * n2.position) / (n1.scale.z + n2.scale.z);
            n1.scale += n2.scale.z * Vector3.forward;
        }
        // update the valid or invalid stack and destroy n2
        if (n1.tag == "Valid")
            data.validNodes.Remove(n2.name);
        else
            data.invalidNodes.Remove(n2.name);
    }


    public void BuildGraph()
    {
        foreach (CustomNodeScriptable cn in data.validNodes.Values)
        {
            Dictionary<(string, string), CustomNodeScriptable> new_transitions = new Dictionary<(string, string), CustomNodeScriptable>();
            Dictionary<string, List<string>> neighbors = cn.neighbors;
            foreach (string key in new string[] { "up", "down", "left", "right", "forward", "backward" })
            {
                foreach (string neigh_idx in neighbors[key])
                {
                    // if the neighbor is valid
                    if (data.validNodes.ContainsKey("_"+neigh_idx))
                    {
                        CustomNodeScriptable neigh_ = data.validNodes["_" + neigh_idx];
                        // add a transition node at the center of the connecting surface
                        CustomNodeScriptable transition = ScriptableObject.CreateInstance<CustomNodeScriptable>();
                        string name = neigh_.name + "&" + cn.name;
                        if (data.nodes.ContainsKey(name))
                        {
                            transition = data.nodes[name];
                            new_transitions[(neigh_.name, cn.name)] = transition;
                        }

                        else
                        {
                            transition.name = cn.name + "&" + neigh_.name;
                            transition.idx = cn.name + "&" + neigh_.name;
                            Vector3 cn_pos = cn.position;
                            Vector3 neigh_pos = neigh_.position;
                            transition.position = new Vector3(
                                    (Mathf.Max(cn.position.x - cn.scale.x / 2, neigh_.position.x - neigh_.scale.x / 2) + Mathf.Min(cn_pos.x + cn.scale.x / 2, neigh_.position.x + neigh_.scale.x / 2)) / 2,
                                    (Mathf.Max(cn.position.y - cn.scale.y / 2, neigh_.position.y - neigh_.scale.y / 2) + Mathf.Min(cn_pos.y + cn.scale.y / 2, neigh_.position.y + neigh_.scale.y / 2)) / 2,
                                    (Mathf.Max(cn.position.z - cn.scale.z / 2, neigh_.position.z - neigh_.scale.z / 2) + Mathf.Min(cn_pos.z + cn.scale.z / 2, neigh_.position.z + neigh_.scale.z / 2)) / 2);
                            // we use scale to store the size of the connecting surface
                            transition.scale = new Vector3(
                                Mathf.Min(cn.position.x + cn.scale.x / 2, neigh_.position.x + neigh_.scale.x / 2) - Mathf.Max(cn.position.x - cn.scale.x / 2, neigh_.position.x - neigh_.scale.x / 2),
                                Mathf.Min(cn.position.y + cn.scale.y / 2, neigh_.position.y + neigh_.scale.y / 2) - Mathf.Max(cn.position.y - cn.scale.y / 2, neigh_.position.y - neigh_.scale.y / 2),
                                Mathf.Min(cn.position.z + cn.scale.z / 2, neigh_.position.z + neigh_.scale.z / 2) - Mathf.Max(cn.position.z - cn.scale.z / 2, neigh_.position.z - neigh_.scale.z / 2));

                            new_transitions[(cn.name, neigh_.name)] = transition;
                            data.nodes[transition.name] = transition;
                        }
                    }
                }
            }
            foreach (var key1 in new_transitions.Keys)
            {
                string name = key1.Item1 + "&" + key1.Item2;
                // make sure to not add the same transition point twice
                if (!data.nodes.ContainsKey(name))
                {
                    name = key1.Item2 + "&" + key1.Item1;
                }
                CustomNodeScriptable t1 = data.nodes[name];
                foreach (var key2 in new_transitions.Keys)
                {
                    if (key1 != key2)
                    {
                        CustomNodeScriptable t2 = new_transitions[key2];
                        t1.edges.Add((t2.idx, Vector3.Distance(t1.position, t2.position)));
                    }
                }
            }
        }
    }

    // returns the index of the node obtained after merging
    public string FindMergedNode(string idx)
    {
        if (!data.deletedNodes.ContainsKey(idx))
            return idx;
        while (data.deletedNodes.ContainsKey(idx))
            idx = data.deletedNodes[idx];
        return idx;
    }

    public bool Contains(string idx, List<CustomNodeScriptable> nodeList)
    {
        foreach (CustomNodeScriptable cn in nodeList)
        {
            if (cn.name == idx)
                return true;
        }
        return false;
    }

}

