using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class OctTree : MonoBehaviour
{
    public float minSize;
    public GameObject voxelValid;
    public GameObject voxelInvalid;
    public GameObject node;
    public float bound;
    public float zBound;
    public float elongated_criteria; // 0 for no merging, high values for aggressive merging
    private string task;
    private List<GameObject> to_split;
    private List<CustomNode> validNodes = new List<CustomNode> { };
    private List<CustomNode> invalidNodes = new List<CustomNode> { };
    Dictionary<string, string> deletedNodes = new Dictionary<string, string>();
    private double t0;
    string[] directions;

    // Start is called before the first frame update
    void Start()
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
            { "backward", new List<string>{ } }},
            new Dictionary<string, List<string>> {
            { "left", new List<string>{ } },
            { "right", new List<string>{ } },
            { "up", new List<string>{ } },
            { "down", new List<string>{ } },
            { "forward", new List<string>{ } },
            { "backward", new List<string>{ } }}
        );
        task = "build";
        directions = new string[] { "up", "down", "left", "right", "forward", "backward" };
    }

    // Update is called once per frame
    void Update()
    {
        if (task == "build")
        {
            while (to_split.Count > 0)
            {
                var node = to_split[0];
                to_split.Remove(to_split.First());
                SplitNode(node);
            }
            task = "prune";
        }
        else if (task == "prune")
        {
            Debug.Log("OctTree built in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            t0 = Time.realtimeSinceStartupAsDouble;
            Debug.Log("Before pruning: " + validNodes.Count + " valid nodes " + invalidNodes.Count + " invalid nodes");
            if (elongated_criteria > 0)
            {
                PruneOctTree(validNodes, invalidNodes);
                Debug.Log("After pruning: " + validNodes.Count + " valid nodes " + invalidNodes.Count + " invalid nodes");
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
        else
        {
            //t0 = Time.realtimeSinceStartupAsDouble;
            UpdateOctTree();
            //Debug.Log("OctTree updated in  " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
        }
    }

    public void BuildOctree(Vector3 center, Vector3 scale, GameObject parent, string idx, Dictionary<string, List<string>> valid_neigbors, Dictionary<string, List<string>> invalid_neighbors)
    {
        GameObject new_node;
        new_node = Instantiate(node,
            center,
            parent.transform.rotation) ;
        new_node.transform.localScale = scale;
        new_node.tag = "Node";
        if (parent)
            new_node.transform.parent = parent.transform;

        CustomNode cn = new_node.GetComponent<CustomNode>();
        cn.idx = idx;
        new_node.name = "_"+idx;
        cn.valid_neighbors = valid_neigbors;
        cn.invalid_neighbors = invalid_neighbors;
        cn.position = center;
        cn.scale = scale;
        // if there is an obstacle in the node split it
        if (!GetComponent<CollisionCheck>().IsEmpty(center, scale))
        {
            to_split.Add(new_node);
        }
        // if the node is empty create a valid leaf
        else
        {
            new_node = ChangeType(new_node, "Node", "Valid");
        }
    }


    public void SplitNode(GameObject node_)
    {
        Vector3 scale = node_.transform.lossyScale;
        // if this was a valid leaf turn it into a node
        if (node_.CompareTag("Valid"))
        {
            node_ = ChangeType(node_, "Valid", "Node");
        }
        CustomNode cn = node_.GetComponent<CustomNode>();
        if (scale.x > minSize)
        {
            Vector3[] new_centers = new Vector3[8];
            Vector3 center = node_.transform.position;
            Vector3 new_scale = scale / 2;
            Vector3 corner = Center2corner(center, scale);
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
                var (valid_neighbors, invalid_neighbors) = cn.ComputeNeighbors(idx, i);         
                BuildOctree(new_centers[i], new_scale, node_, idx+i.ToString(), valid_neighbors, invalid_neighbors);
            }
        }
        else {
            cn.UpdateNeighborsOnInvalid();
            node_ = ChangeType(node_, "Node", "Invalid");
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

    public void PruneOctTree(List<CustomNode> valid_nodes, List<CustomNode> invalid_nodes)
    {
        // start a greedy merging from the input valid and invalid nodes
        Stack validStack = new Stack(valid_nodes);
        Stack invalidStack = new Stack(invalid_nodes);
        deletedNodes = new Dictionary<string, string>();
        while (validStack.Count > 0)
        {
            CustomNode n1 = (CustomNode)validStack.Pop();
            if (n1 == null)
                continue;
            foreach (string key in directions)
            {
                // if n1 only has 1 valid neighbor n2 and n1 has not already been merged
                if (n1.valid_neighbors[key].Count == 1 && n1.invalid_neighbors[key].Count == 0 && !deletedNodes.ContainsKey(n1.name))
                {
                    CustomNode n2 = GameObject.Find("_" + n1.valid_neighbors[key][0]).GetComponent<CustomNode>();
                    // if n2 is valid and only has n1 as neighbor on the opposite direction merge them
                    string opposite = n1.GetOppositeDirection(key);
                    bool elongated = false;
                    // don't merge if the resulting node would be too elongated
                    if (key == "up" || key == "down")
                        elongated = n1.transform.lossyScale.y + n2.transform.lossyScale.y > elongated_criteria * Mathf.Min(n1.transform.lossyScale.x, n1.transform.lossyScale.z);
                    if (key == "left" || key == "right")
                        elongated = n1.transform.lossyScale.x + n2.transform.lossyScale.x > elongated_criteria * Mathf.Min(n1.transform.lossyScale.y, n1.transform.lossyScale.z);
                    if (key == "forward" || key == "backward")
                        elongated = n1.transform.lossyScale.z + n2.transform.lossyScale.z > elongated_criteria * Mathf.Min(n1.transform.lossyScale.x, n1.transform.lossyScale.y);
                    if (n2.gameObject.CompareTag("Valid") && n2.valid_neighbors[opposite].Count == 1 && n2.invalid_neighbors[opposite].Count == 0 && !elongated)
                    {
                        deletedNodes[n2.name] = n1.name;
                        MergeNeighbors(n1, n2, key);
                        // add n1 again to check if this merge enabled further merges
                        validStack.Push(n1);
                        break;
                    }
                }
            }
        }
        while (invalidStack.Count > 0)
        {
            CustomNode n1 = (CustomNode)invalidStack.Pop();
            if (n1 == null)
                continue;
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2 and n1 has not already been merged
                if (n1.invalid_neighbors[key].Count == 1 && n1.valid_neighbors[key].Count == 0 && !deletedNodes.ContainsKey(n1.name))
                {
                    CustomNode n2 = GameObject.Find("_" + n1.invalid_neighbors[key][0]).GetComponent<CustomNode>();
                    // if n2 is invalid and only has n1 as neighbor on the opposite direction merge them
                    string opposite = n1.GetOppositeDirection(key);
                    bool elongated = false;
                    // don't merge if the resulting node would be too elongated
                    if (key == "up" || key == "down")
                        elongated = n1.transform.lossyScale.y + n2.transform.lossyScale.y > elongated_criteria * Mathf.Min(n1.transform.lossyScale.x, n1.transform.lossyScale.z);
                    if (key == "left" || key == "right")
                        elongated = n1.transform.lossyScale.x + n2.transform.lossyScale.x > elongated_criteria * Mathf.Min(n1.transform.lossyScale.y, n1.transform.lossyScale.z);
                    if (key == "forward" || key == "backward")
                        elongated = n1.transform.lossyScale.z + n2.transform.lossyScale.z > elongated_criteria * Mathf.Min(n1.transform.lossyScale.x, n1.transform.lossyScale.y);
                    if (n2.gameObject.CompareTag("Invalid") && n2.invalid_neighbors[opposite].Count == 1 && n2.valid_neighbors[opposite].Count == 0 && !elongated)
                    {
                        deletedNodes[n2.name] = n1.name;
                        MergeNeighbors(n1, n2, key);
                        invalidStack.Push(n1);
                        break;
                    }
                }
            }
        }
    }



    public void MergeNeighbors(CustomNode n1, CustomNode n2, string direction)
    {
        foreach (string key in directions) {
            // add the neighbors of n2 to those of n1
            n1.valid_neighbors[key] = n1.valid_neighbors[key].Union(n2.valid_neighbors[key]).ToList();
            // remove n1 and n2 from the neighbor list
            n1.valid_neighbors[key].RemoveAll(s => s == n1.idx);
            n1.valid_neighbors[key].RemoveAll(s => s == n2.idx);

            // update the valid neighbors by adding n1 and removing n2 (set operations)
            string opposite = n1.GetOppositeDirection(key);
            foreach (string idx in n1.valid_neighbors[key])
            {
                CustomNode neighbor = GameObject.Find("_" + idx).GetComponent<CustomNode>();
                if (n1.tag == "Valid")
                {
                    neighbor.valid_neighbors[opposite].Remove(n2.idx);
                    neighbor.valid_neighbors[opposite] = neighbor.valid_neighbors[opposite].Union(new List<string> { n1.idx }).ToList();
                }
                else if (n1.tag == "Invalid")
                {
                    neighbor.invalid_neighbors[opposite].Remove(n2.idx);
                    neighbor.invalid_neighbors[opposite] = neighbor.invalid_neighbors[opposite].Union(new List<string> { n1.idx }).ToList();
                }
                else
                    Debug.LogError(n1.tag);
            }
        }
        foreach (string key in directions)
        {
            // add the neighbors of n2 to those of n1
            n1.invalid_neighbors[key] = n1.invalid_neighbors[key].Union(n2.invalid_neighbors[key]).ToList();
            // remove n1 and n2 from the neighbor list
            n1.invalid_neighbors[key].RemoveAll(s => s == n1.idx);
            n1.invalid_neighbors[key].RemoveAll(s => s == n2.idx);

            // update the neighbors by adding n1 and removing n2 (set operations)
            string opposite = n1.GetOppositeDirection(key);
            foreach (string idx in n1.invalid_neighbors[key])
            {
                CustomNode neighbor = GameObject.Find("_" + idx).GetComponent<CustomNode>();
                if (n1.tag == "Valid")
                {
                    neighbor.valid_neighbors[opposite].Remove(n2.idx);
                    neighbor.valid_neighbors[opposite] = neighbor.valid_neighbors[opposite].Union(new List<string> { n1.idx }).ToList();
                }
                else if (n1.tag == "Invalid")
                {
                    neighbor.invalid_neighbors[opposite].Remove(n2.idx);
                    neighbor.invalid_neighbors[opposite] = neighbor.invalid_neighbors[opposite].Union(new List<string> { n1.idx }).ToList();
                }
            }
        }

        // update the position and size of n1
        GameObject g1 = n1.gameObject;
        GameObject g2 = n2.gameObject;
        // add the scales on the neighboring direction and keep the other 2, handling the difference in relative scales
        if (direction == "up" || direction == "down")
        {
            g1.transform.position = (g1.transform.lossyScale.y * g1.transform.position + g2.transform.lossyScale.y * g2.transform.position) / (g1.transform.lossyScale.y + g2.transform.lossyScale.y);
            g1.transform.localScale += g2.transform.localScale.y * Mathf.Pow(0.5f, g2.name.Length - g1.name.Length) * Vector3.up;
        }
        if (direction == "right" || direction == "left")
        {
            g1.transform.position = (g1.transform.lossyScale.x * g1.transform.position + g2.transform.lossyScale.x * g2.transform.position) / (g1.transform.lossyScale.x + g2.transform.lossyScale.x);
            g1.transform.localScale += g2.transform.localScale.x * Mathf.Pow(0.5f, g2.name.Length - g1.name.Length) * Vector3.right;
        }
        if (direction == "forward" || direction == "backward")
        {
            g1.transform.position = (g1.transform.lossyScale.z * g1.transform.position + g2.transform.lossyScale.z * g2.transform.position) / (g1.transform.lossyScale.z + g2.transform.lossyScale.z);
            g1.transform.localScale += g2.transform.localScale.z * Mathf.Pow(0.5f, g2.name.Length - g1.name.Length) * Vector3.forward;
        }
        // update the valid or invalid stack and destroy n2
        if (g1.CompareTag("Valid"))
            validNodes.Remove(n2);
        else
            invalidNodes.Remove(n2);
        DestroyImmediate(g2);
    }


    public void BuildGraph()
    {
        if (GameObject.Find("transitions") != null)
            DestroyImmediate(GameObject.Find("transitions"));
        GameObject empty = new GameObject();
        empty.name = "transitions";
        empty.transform.parent = gameObject.transform;
        Astar script = gameObject.GetComponent<Astar>();
        script.nodes = new Dictionary<string, CustomNode>();
        script.edges = new Dictionary<string, List<(string, float)>>();
        Dictionary<(string, string), CustomNode> transitions = new Dictionary<(string, string), CustomNode>();
        foreach (CustomNode cn in validNodes)
        {
            Dictionary<(string, string), CustomNode> new_transitions = new Dictionary<(string, string), CustomNode>();
            Dictionary<string, List<string>> neighbors = cn.valid_neighbors;
            foreach (string key in new string[] { "up", "down", "left", "right", "forward", "backward" })
            {
                foreach (string neigh_idx in neighbors[key])
                {
                    GameObject neigh_ = GameObject.Find("_" + neigh_idx);
                    // if the neighbor is valid
                    if (neigh_.CompareTag("Valid"))
                    {
                        // add a transition node at the center of the connecting surface
                        GameObject g = new GameObject();
                        g.name = cn.name + "&" + neigh_.name;
                        g.AddComponent<CustomNode>();
                        g.transform.parent = empty.transform;
                        CustomNode transition = g.GetComponent<CustomNode>();
                        transition.idx = cn.name + "&" + neigh_.name;
                        Vector3 cn_pos = cn.transform.position;
                        Vector3 neigh_pos = neigh_.transform.position;
                        transition.position = new Vector3(
                                (Mathf.Max(cn_pos.x - cn.transform.lossyScale.x / 2, neigh_pos.x - neigh_.transform.lossyScale.x / 2) + Mathf.Min(cn_pos.x + cn.transform.lossyScale.x / 2, neigh_pos.x + neigh_.transform.lossyScale.x / 2)) / 2,
                                (Mathf.Max(cn_pos.y - cn.transform.lossyScale.y / 2, neigh_pos.y - neigh_.transform.lossyScale.y / 2) + Mathf.Min(cn_pos.y + cn.transform.lossyScale.y / 2, neigh_pos.y + neigh_.transform.lossyScale.y / 2)) / 2,
                                (Mathf.Max(cn_pos.z - cn.transform.lossyScale.z / 2, neigh_pos.z - neigh_.transform.lossyScale.z / 2) + Mathf.Min(cn_pos.z + cn.transform.lossyScale.z / 2, neigh_pos.z + neigh_.transform.lossyScale.z / 2)) / 2);
                        new_transitions[(cn.name, neigh_.name)] = transition;
                        
                    }
                }
            }
            foreach (var key1 in new_transitions.Keys)
            {
                CustomNode t1 = new_transitions[key1];
                // make sure to not add the same transition point twice
                if (!transitions.ContainsKey(key1) && !transitions.ContainsKey((key1.Item2, key1.Item1)))
                {
                    transitions[key1] = t1;
                    script.nodes[t1.idx] = t1;
                    script.edges[t1.idx] = new List<(string, float)>();
                }
                else
                    t1.idx = t1.idx.Split('&')[1] + "&" + t1.idx.Split('&')[0];
                foreach (var key2 in new_transitions.Keys)
                {
                    if (key1 != key2)
                    {
                        CustomNode t2 = new_transitions[key2];
                        script.edges[t1.idx].Add((t2.idx, Vector3.Distance(t1.position, t2.position)));
                        //Debug.Log("edge" + t1.name + " " + t2.name);
                    }
                }

            }
        }

    }

    // returns the index of the node obtained after merging
    public string FindMergedNode(string idx)
    {
        while (deletedNodes.ContainsKey(idx))
            idx = deletedNodes[idx];
        return idx;
    }

    public bool Contains(string idx, List<CustomNode> nodeList)
    {
        foreach (CustomNode cn in nodeList)
        {
            if (cn.name == idx)
                return true;
        }
        return false;
    }

    public void UpdateOctTree()
    {
        t0 = Time.realtimeSinceStartupAsDouble;
        // iterate over all nodes
        // if valid -> invalid, split + update neighbors
        CollisionCheck coll = GetComponent<CollisionCheck>();
        bool changed = false;
        foreach (GameObject gv in GameObject.FindGameObjectsWithTag("Valid"))
        {
            CustomNode cv = gv.GetComponent<CustomNode>();
            if (!coll.IsEmpty(cv.position, cv.scale))
            {
                changed = true;
                SplitNode(gv);
            }
        }
        while (to_split.Count > 0)
        {
            var node = to_split[0];
            to_split.Remove(to_split.First());
            SplitNode(node);
        }
        // if invalid -> valid, try to restore the parent node
        foreach (GameObject gi in GameObject.FindGameObjectsWithTag("Invalid"))
        {
            CustomNode ci = gi.GetComponent<CustomNode>();
            if (coll.IsEmpty(ci.position, ci.scale))
            {
                changed = true;
                RepairNode(gi);
            }
        }
        // if the octtree has been updated, update the graph and recompute the path
        if (changed)
        {
            // update the graph: recompute it all or find a local update method
            BuildGraph();
            Astar path_finder = GetComponent<Astar>();
            if (!path_finder.read_from_file)
            {
                path_finder.RecomputePath();
            }
            Debug.Log("OctTree updated in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");

        }
    }

    public void RepairNode(GameObject node)
    {
        // transform the invalid node into a valid node, updating its neighbors
        node = ChangeType(node, "Invalid", "Valid");
        CustomNode cn = node.GetComponent<CustomNode>();
        cn.UpdateNeighborsOnValid();
        // try merging it with its neighbors if all children nodes are valid
        GameObject parent = GameObject.Find(node.name.Substring(0, node.name.Length - 1));
        // if the parent was already repaired, quit
        if (parent.CompareTag("Valid"))
            return;
        elongated_criteria = 99;
        PruneOctTree(new List<CustomNode> { cn }, new List<CustomNode>());
        /*bool merge = true;
        while (merge)
        {
            Transform tr = parent.transform;
            foreach (Transform t in parent.transform)
            {
                GameObject child = t.gameObject;
                if (child.CompareTag("Invalid") || child.CompareTag("Node"))
                    merge = false;
            }
            if (merge)
            {
                cn.UpdateNeighborsOnMerge(parent);
                while (parent.transform.childCount > 0) {
                    validNodes.Remove(parent.transform.GetChild(0).gameObject.GetComponent<CustomNode>());
                    DestroyImmediate(parent.transform.GetChild(0).gameObject); 
                }
                parent = ChangeType(parent, "Node", "Valid");
            }
            node = parent;
            if (node.name.Length > 2)
                parent = GameObject.Find(node.name.Substring(0, node.name.Length - 1));
            else
                // we reached the root node
                merge = false;
        }
        */

        // make sure neighbors are updated correctly in all scenarios, including when merging children
    }

    public GameObject ChangeType(GameObject g, string old_type, string new_type)
    {
        // change the type of an octTree cell between "Node", "Valid" and "Invalid"
        GameObject new_g;
        CustomNode new_cn;
        CustomNode cn = g.GetComponent<CustomNode>();
        if (new_type == "Valid")
        {
            new_g = Instantiate(voxelValid, cn.position, g.transform.rotation);
            new_g.tag = "Valid";
            new_cn = new_g.GetComponent<CustomNode>();
            validNodes.Add(new_cn);
            if (old_type == "Invalid")
                invalidNodes.Remove(cn);
        }
        else if (new_type == "Invalid")
        {
            new_g = Instantiate(voxelInvalid, cn.position, g.transform.rotation);
            new_g.tag = "Invalid";
            new_cn = new_g.GetComponent<CustomNode>();
            invalidNodes.Add(new_cn);
            if (old_type == "Valid")
                validNodes.Remove(cn);
        }
        else
        {
            // a node does not have neighbors
            new_g = Instantiate(node, cn.position, g.transform.rotation);
            new_g.tag = "Node";
            new_cn = new_g.GetComponent<CustomNode>();
            if (old_type == "Valid")
                validNodes.Remove(cn);
            if (old_type == "Invalid")
                invalidNodes.Remove(cn);
        }
        new_g.transform.localScale = cn.scale;
        new_g.transform.parent = g.transform.parent.transform;
        new_cn.idx = cn.idx;
        new_cn.position = cn.position;
        new_cn.scale = cn.scale;
        new_g.name = "_" + cn.idx;
        new_cn.valid_neighbors = cn.valid_neighbors;
        new_cn.invalid_neighbors = cn.invalid_neighbors;
        DestroyImmediate(g);
        return new_g;

    }

}

