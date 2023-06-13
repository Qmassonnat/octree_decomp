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
    private float currentScale;
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
            { "backward", new List<string>{ } }}
        );
        task = "build";
        currentScale = Mathf.Min(2 * bound, zBound);
        directions = new string[] { "up", "down", "left", "right", "forward", "backward" };
    }

    // Update is called once per frame
    void Update()
    {
        if (task == "build" || currentScale >= minSize/2)
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
                Debug.Log("Before pruning: " + validNodes.Count + " valid nodes " + invalidNodes.Count + " invalid nodes");
                if (elongated_criteria > 0)
                {
                    t0 = Time.realtimeSinceStartupAsDouble;
                    PruneOctTree();
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
            Vector3 scale = node_.transform.lossyScale;
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
                    Dictionary<string, List<string>> new_neigbors = cn.ComputeNeighbors(idx, i);         
                    BuildOctree(new_centers[i], new_scale, node_, idx+i.ToString(), new_neigbors);
                }
            }
            // if the leaf has not already been added
            else if (!Contains(node_.name, invalidNodes))
            {
                GameObject vi = Instantiate(voxelInvalid, node_.transform.position, node_.transform.rotation);
                vi.transform.localScale = scale;
                vi.transform.parent = node_.transform.parent.transform;
                vi.name = node_.name;

                CustomNode cvi = vi.GetComponent<CustomNode>();
                cvi.idx = cn.idx;
                cvi.neighbors = cn.neighbors;
                cvi.tag = "Invalid";
                invalidNodes.Add(cvi);
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
            GameObject vv = Instantiate(voxelValid, node_.transform.position, node_.transform.rotation);
            vv.transform.localScale = node_.transform.lossyScale;
            vv.transform.parent = node_.transform.parent.transform;
            CustomNode cn = node_.GetComponent<CustomNode>();
            CustomNode cvv = vv.GetComponent<CustomNode>();
            cvv.idx = cn.idx;
            vv.name = "_" + cn.idx;
            cvv.neighbors = cn.neighbors;
            cvv.tag = "Valid";
            validNodes.Add(cvv);
            Destroy(node_);
        }
    }

    public void PruneOctTree()
    {
        Stack validStack = new Stack(validNodes);
        Stack invalidStack = new Stack(invalidNodes);
        deletedNodes = new Dictionary<string, string>();
        while (validStack.Count > 0)
        {
            CustomNode n1 = (CustomNode)validStack.Pop();
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2 and n1 has not already been merged
                if (n1.neighbors[key].Count == 1 && !deletedNodes.ContainsKey(n1.name))
                {
                    CustomNode n2 = GameObject.Find("_" + n1.neighbors[key][0]).GetComponent<CustomNode>();
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
                    if (n2.gameObject.CompareTag("Valid") && n2.neighbors[opposite].Count == 1 && !elongated)
                    {
                        MergeNeighbors(n1, n2, key);
                        deletedNodes[n2.name] = n1.name;
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
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2 and n1 has not already been merged
                if (n1.neighbors[key].Count == 1 && !deletedNodes.ContainsKey(n1.name))
                {
                    CustomNode n2 = GameObject.Find("_" + n1.neighbors[key][0]).GetComponent<CustomNode>();
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
                    if (n2.gameObject.CompareTag("Invalid") && n2.neighbors[opposite].Count == 1 && !elongated)
                    {
                        MergeNeighbors(n1, n2, key);
                        deletedNodes[n2.name] = n1.name;
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
            n1.neighbors[key] = n1.neighbors[key].Union(n2.neighbors[key]).ToList();
            // remove n1 and n2 from the neighbor list
            n1.neighbors[key].RemoveAll(s => s == n1.idx);
            n1.neighbors[key].RemoveAll(s => s == n2.idx);

            // update the neighbors by adding n1 and removing n2 (set operations)
            string opposite = n1.GetOppositeDirection(key);
            foreach (string idx in n1.neighbors[key])
            {
                CustomNode neighbor = GameObject.Find("_" + idx).GetComponent<CustomNode>();
                neighbor.neighbors[opposite].Remove(n2.idx);
                neighbor.neighbors[opposite] = neighbor.neighbors[opposite].Union(new List<string> { n1.idx }).ToList();
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
        Destroy(g2);
    }


    public void BuildGraph()
    {
        GameObject empty = new GameObject();
        empty.name = "transitions";
        empty.transform.parent = gameObject.transform;
        Astar script = gameObject.GetComponent<Astar>();
        Dictionary<(string, string), CustomNode> transitions = new Dictionary<(string, string), CustomNode>();
        foreach (CustomNode cn in validNodes)
        {
            Dictionary<(string, string), CustomNode> new_transitions = new Dictionary<(string, string), CustomNode>();
            Dictionary<string, List<string>> neighbors = cn.neighbors;
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

}

