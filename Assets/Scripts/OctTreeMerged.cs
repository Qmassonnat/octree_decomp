using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
using System.IO;
using UnityEngine.SceneManagement;

public class OctTreeMerged : MonoBehaviour
{
    public GameObject vv;
    public GameObject vi;
    public float minSize;
    public float bound;
    public float zBound;
    public float elongated_criteria; // 0 for no merging, high values for aggressive merging
    public bool load;
    [HideInInspector] public NodeDataMerged data;
    private List<CustomNodeScriptable> to_split = new List<CustomNodeScriptable>();
    private List<CustomNodeScriptable> to_repair = new List<CustomNodeScriptable>();
    private List<(string, string)> transitions_add = new List<(string, string)>();
    private List<(string, string)> transitions_remove = new List<(string, string)>();
    private string task;
    private double t0;
    private string[] directions;
    private bool tested = false;
    private int global_id = 1;

    // Start is called before the first frame update
    void Start()
    {
        data = ScriptableObject.CreateInstance<NodeDataMerged>();
        gameObject.GetComponent<AstarMerged>().data = data;
        string path = "Assets/Data/" + SceneManager.GetActiveScene().name + minSize;
        if (load && AssetDatabase.IsValidFolder(path))
        {
            t0 = Time.realtimeSinceStartupAsDouble;
            data.LoadData(path);
            Debug.Log("Loading data from file " + path + " in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            task = "finished";
            gameObject.tag = "Finished";
            Debug.Log(data.nodes.Count + " nodes " + data.validNodes.Count + " valid cells");
        }
        else
        {
            t0 = Time.realtimeSinceStartupAsDouble;
            to_split = new List<CustomNodeScriptable>();
            CustomNodeScriptable root = ScriptableObject.CreateInstance<CustomNodeScriptable>();
            root.position = new Vector3(0, zBound / 2, 0); 
            root.scale = new Vector3(2 * bound, zBound, 2 * bound);
            root.tag = "Valid";
            root.idx = "0";
            root.name = "0";
            root.valid_neighbors = new Dictionary<string, List<string>> {
                { "left", new List<string>{ } },
                { "right", new List<string>{ } },
                { "up", new List<string>{ } },
                { "down", new List<string>{ } },
                { "forward", new List<string>{ } },
                { "backward", new List<string>{ } }};
            root.invalid_neighbors = new Dictionary<string, List<string>> {
                { "left", new List<string>{ } },
                { "right", new List<string>{ } },
                { "up", new List<string>{ } },
                { "down", new List<string>{ } },
                { "forward", new List<string>{ } },
                { "backward", new List<string>{ } }};
            BuildOctree(root);
            task = "build";
            directions = new string[] { "up", "down", "left", "right", "forward", "backward" };
        }
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
            //Debug.Log("OctTree built in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            Debug.Log("Before pruning: " + data.validNodes.Count + " valid nodes " + data.invalidNodes.Count + " invalid nodes");
            if (elongated_criteria > 0)
            {
                t0 = Time.realtimeSinceStartupAsDouble;
                PruneOctTree(data.validNodes.Values.ToList(), null);
                Debug.Log("After pruning: " + data.validNodes.Count + " valid nodes " + data.invalidNodes.Count + " invalid nodes");
                Debug.Log("OctTree pruned in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            }
            task = "graph";
        }

        else if (task == "graph")
        {
            foreach ( var cn in data.validNodes.Values)
            {
                GameObject g = Instantiate(vv);
                g.transform.localScale = cn.scale;
                g.transform.position = cn.position;
            }
            foreach (var cn in data.invalidNodes.Values)
            {
                GameObject g = Instantiate(vi);
                g.transform.localScale = cn.scale;
                g.transform.position = cn.position;
            }
            t0 = Time.realtimeSinceStartupAsDouble;
            BuildGraph();
            task = "finished";
            gameObject.tag = "Finished";
            Debug.Log("Graph built in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
        }
        else
        {
            if (GameObject.Find("MapGenerator") == null)
                UpdateOctTree();
            else if (!tested)
            {
                tested = true;
                GameObject.Find("MapGenerator").GetComponent<WarframeMap>().TestScenario();
            }
        }
    }

    public void BuildOctree(CustomNodeScriptable new_node)
    {
        //Debug.Log("building" + new_node.idx);
        data.validNodes[new_node.idx] = new_node;

        // if there is an obstacle in the node split it
        if (!GetComponent<CollisionCheck>().IsEmpty(new_node.position, new_node.scale))
            to_split.Add(new_node);
        else
        {
            // if the node is valid, update the graph
            foreach (KeyValuePair<string, List<string>> entry in new_node.valid_neighbors)
                // add the transitions between the new node and its neighbors
                foreach (string neigh in entry.Value)
                    transitions_add.Add((new_node.idx, neigh));
        }
    }


    public void SplitNode(CustomNodeScriptable node_)
    {
        // if this was a valid leaf turn it into a node
        if (node_.tag == "Valid")
            ChangeType(node_, "Valid", "Node");
        Vector3 current_scale = node_.scale;
        if (current_scale.x > minSize || current_scale.y > minSize || current_scale.z > minSize)
        {
            foreach (KeyValuePair<string, List<string>> entry in node_.valid_neighbors)
            {
                // remove the transitions between the split node and its neighbors
                foreach (string neigh in entry.Value)
                    transitions_remove.Add((node_.idx, neigh));
            }
            var (new_scale, new_centers) = GetNewCenters(node_);
            List<CustomNodeScriptable> children = new List<CustomNodeScriptable>();
            // update the neighbors' list to reflect the split
            foreach (Vector3 position in new_centers)
            {
                CustomNodeScriptable child = ScriptableObject.CreateInstance<CustomNodeScriptable>();
                child.position = position;
                child.scale = new_scale;
                child.idx = global_id.ToString();
                child.name = global_id.ToString();
                child.tag = "Valid";
                child.parent = node_.idx;
                if (!node_.children.Contains(child.idx))
                    node_.children.Add(child.idx);
                child.valid_neighbors = new Dictionary<string, List<string>> {
                { "left", new List<string>{ } },
                { "right", new List<string>{ } },
                { "up", new List<string>{ } },
                { "down", new List<string>{ } },
                { "forward", new List<string>{ } },
                { "backward", new List<string>{ } }};
                child.invalid_neighbors = new Dictionary<string, List<string>> {
                { "left", new List<string>{ } },
                { "right", new List<string>{ } },
                { "up", new List<string>{ } },
                { "down", new List<string>{ } },
                { "forward", new List<string>{ } },
                { "backward", new List<string>{ } }};
                global_id++;
                children.Add(child);
            }
            data.UpdateNeighborsOnSplit(node_, children);
            //Debug.Log(children.Count);
            //Debug.Log("split" + node_.idx + " into " + children[0].idx + " to " + children[children.Count-1].idx);
            foreach (CustomNodeScriptable child in children)
            {
                // compute the exterior neighbors of the "child" node based off the parent's neighbors
                var (new_valid_neighbors, new_invalid_neighbors) = data.ComputeChildNeighbors(node_, child);
                child.valid_neighbors = new_valid_neighbors;
                child.invalid_neighbors = new_invalid_neighbors;
                // add the inner neighbors with a proximity check
                foreach (CustomNodeScriptable other_child in children)
                { 
                    if (other_child.idx != child.idx)
                    {
                        string direction = data.IsNeighbor(child, other_child);
                        if (direction != null)
                            child.valid_neighbors[direction].Add(other_child.idx);
                    }
                }
                BuildOctree(child);
            }
        }
        // if  we reached the minimum size, make the node invalid
        else
        {
            data.UpdateNeighborsOnInvalid(node_);
            ChangeType(node_, "Node", "Invalid");
            foreach (KeyValuePair<string, List<string>> entry in node_.valid_neighbors)
            {
                // remove the transitions between the invalid node and its neighbors
                foreach (string neigh in entry.Value)
                    transitions_remove.Add((node_.idx, neigh));
            }
        }
    }

    (Vector3, List<Vector3>) GetNewCenters(CustomNodeScriptable cn)
    {
        List<Vector3> new_centers = new List<Vector3>();
        Vector3 new_scale;
        // returns the list of the position of the children of cn
        if (cn.scale.x == cn.scale.y && cn.scale.x == cn.scale.z)
        {
            Vector3 center = cn.position;
            new_scale = cn.scale / 2;
            Vector3 corner = Center2corner(center, cn.scale);
            new_centers.Add(Corner2center(corner, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.right * new_scale.x, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.forward * new_scale.z, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.right * new_scale.x + Vector3.forward * new_scale.z, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.up * new_scale.y, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.right * new_scale.x + Vector3.up * new_scale.y, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.up * new_scale.y + Vector3.forward * new_scale.z, new_scale));
            new_centers.Add(Corner2center(corner + Vector3.right * new_scale.x + Vector3.up * new_scale.y + Vector3.forward * new_scale.z, new_scale));
        }
        else
        {
            // if cn is not a cube, instead of splitting it in 8 split it in cubes the same size as the shortest side of cn
            float min_scale = new List<float> { cn.scale.x, cn.scale.y, cn.scale.z }.Min();
            Vector3 center = cn.position;
            Vector3 corner = Center2corner(center, cn.scale);
            new_scale = min_scale * Vector3.one;
            for (int i = 0; i < cn.scale.x / min_scale; i++)
            {
                for (int j = 0; j < cn.scale.y / min_scale; j++)
                {
                    for (int k = 0; k < cn.scale.z / min_scale; k++)
                    {
                        new_centers.Add(Corner2center(corner + Vector3.Scale(new Vector3(i, j, k), new_scale), new_scale));
                    }
                }
            }
        }
        return (new_scale, new_centers);
    }


    Vector3 Center2corner(Vector3 center, Vector3 scale)
    {
        return center - scale / 2;
    }

    Vector3 Corner2center(Vector3 corner, Vector3 scale)
    {
        return corner + scale / 2;
    }

    public void PruneOctTree(List<CustomNodeScriptable> validNodes, List<CustomNodeScriptable> invalidNodes)
    {
        Stack validStack = new Stack();
        Stack invalidStack = new Stack();
        if (validNodes == null && invalidNodes == null)
        {
            validStack = new Stack(data.validNodes.Values);
            invalidStack = new Stack(data.invalidNodes.Values);
        }
        else
        {
            if (validNodes != null)
                validStack = new Stack(validNodes);
            if (invalidNodes != null)
                invalidStack = new Stack(invalidNodes);
        }
        while (validStack.Count > 0)
        {
            CustomNodeScriptable n1 = (CustomNodeScriptable)validStack.Pop();
            if (n1 == null || !data.validNodes.ContainsKey(n1.idx))
                continue;
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2
                if (n1.valid_neighbors[key].Count == 1 && n1.invalid_neighbors[key].Count == 0)
                {
                    CustomNodeScriptable n2 = data.FindNode(n1.valid_neighbors[key][0]);
                    // if n2 is valid and only has n1 as neighbor on the opposite direction merge them
                    string opposite = data.GetOppositeDirection(key);
                    bool elongated = false;
                    // don't merge if the resulting node would be too elongated
                    if (key == "up" || key == "down")
                        elongated = n1.scale.y + n2.scale.y > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.z);
                    if (key == "left" || key == "right")
                        elongated = n1.scale.x + n2.scale.x > elongated_criteria * Mathf.Min(n1.scale.y, n1.scale.z);
                    if (key == "forward" || key == "backward")
                        elongated = n1.scale.z + n2.scale.z > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.y);
                    if (n2.tag == "Valid" && n2.valid_neighbors[opposite].Count == 1 && n2.invalid_neighbors[opposite].Count == 0 && !elongated)
                    {
                        MergeNeighbors(n1, n2, key);
                        //Debug.Log("merging" + n1.idx + " " + n2.idx);
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
            if (n1 == null || !data.invalidNodes.ContainsKey(n1.idx))
                continue;
            foreach (string key in directions)
            {
                // if n1 only has 1 neighbor n2
                if (n1.invalid_neighbors[key].Count == 1 && n1.valid_neighbors[key].Count == 0)
                {
                    CustomNodeScriptable n2 = data.FindNode(n1.invalid_neighbors[key][0]);
                    // if n2 is valid and only has n1 as neighbor on the opposite direction merge them
                    string opposite = data.GetOppositeDirection(key);
                    bool elongated = false;
                    // don't merge if the resulting node would be too elongated
                    if (key == "up" || key == "down")
                        elongated = n1.scale.y + n2.scale.y > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.z);
                    if (key == "left" || key == "right")
                        elongated = n1.scale.x + n2.scale.x > elongated_criteria * Mathf.Min(n1.scale.y, n1.scale.z);
                    if (key == "forward" || key == "backward")
                        elongated = n1.scale.z + n2.scale.z > elongated_criteria * Mathf.Min(n1.scale.x, n1.scale.y);
                    if (n2.tag == "Invalid" && n2.invalid_neighbors[opposite].Count == 1 && n2.valid_neighbors[opposite].Count == 0 && !elongated)
                    {
                        MergeNeighbors(n1, n2, key);
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
        // if the graph is already created, update it by deleting the transitions pointing to and from n1 and n2
        if (task == "finished")
        {
            foreach (KeyValuePair<string, List<string>> entry in n1.valid_neighbors)
            {
                foreach (string neigh in entry.Value)
                    transitions_remove.Add((n1.idx, neigh));
            }
            foreach (KeyValuePair<string, List<string>> entry in n2.valid_neighbors)
            {
                foreach (string neigh in entry.Value)
                    transitions_remove.Add((n2.idx, neigh));
            }
        }

        foreach (string key in directions)
        {
            // add the neighbors of n2 to those of n1
            n1.valid_neighbors[key] = n1.valid_neighbors[key].Union(n2.valid_neighbors[key]).ToList();
            // remove n1 and n2 from the neighbor list
            n1.valid_neighbors[key].RemoveAll(s => s == n1.idx);
            n1.valid_neighbors[key].RemoveAll(s => s == n2.idx);

            // update the neighbors by adding n1 and removing n2 (set operations)
            string opposite = data.GetOppositeDirection(key);
            foreach (string idx in n1.valid_neighbors[key])
            {
                CustomNodeScriptable neighbor = data.FindNode(idx);
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
            string opposite = data.GetOppositeDirection(key);
            foreach (string idx in n1.invalid_neighbors[key])
            {
                CustomNodeScriptable neighbor = data.FindNode(idx);
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
        // add the scales on the neighboring direction and keep the other 2
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

        // if the graph is already created, update it by inserting the merged n1+n2
        if (task == "finished")
        {
            foreach (KeyValuePair<string, List<string>> entry in n1.valid_neighbors)
            {
                // add a transition between the now valid node and all of its valid neighbors
                foreach (string neigh in entry.Value)
                    transitions_add.Add((n1.idx, neigh));
            }
        }
        // remove n2 from its parent's children list
        if (n2.parent != null)
        {
            CustomNodeScriptable parent = data.FindNode(n2.parent);
            parent.children.Remove(n2.idx);
        }

        // update the valid or invalid stack and destroy n2
        if (n1.tag == "Valid")
            data.validNodes.Remove(n2.idx);
        else
            data.invalidNodes.Remove(n2.idx);
    }

    public void BuildGraph()
    {
        data.nodes = new Dictionary<string, CustomNodeScriptable>();
        foreach (CustomNodeScriptable cn in data.validNodes.Values)
        {
            // create all transitions between cn and its valid neighbors
            Dictionary<(string, string), CustomNodeScriptable> new_transitions = new Dictionary<(string, string), CustomNodeScriptable>();
            Dictionary<string, List<string>> neighbors = cn.valid_neighbors;
            foreach (string key in directions)
            {
                foreach (string neigh_idx in neighbors[key])
                {
                    // if the neighbor is valid
                    if (data.validNodes.ContainsKey(neigh_idx))
                    {
                        CustomNodeScriptable neigh_ = data.validNodes[neigh_idx];
                        // add a transition node at the center of the connecting surface
                        var (transition, trans_idx) = CreateTransition(cn, neigh_);
                        new_transitions[trans_idx] = transition;
                        data.nodes[transition.idx] = transition;
                    }
                }
            }
            foreach (var key1 in new_transitions.Keys)
            {
                string idx = key1.Item1 + "&" + key1.Item2;
                // make sure to not add the same transition point twice
                if (!data.nodes.ContainsKey(idx))
                {
                    idx = key1.Item2 + "&" + key1.Item1;
                }
                CustomNodeScriptable t1 = data.nodes[idx];
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

    public (CustomNodeScriptable, (string, string)) CreateTransition(CustomNodeScriptable n1, CustomNodeScriptable n2)
    {
        CustomNodeScriptable transition = ScriptableObject.CreateInstance<CustomNodeScriptable>();
        (string, string) trans_idx;
        string idx = n2.idx + "&" + n1.idx;
        if (data.nodes.ContainsKey(idx))
        {
            transition = data.nodes[idx];
            trans_idx = (n2.idx, n1.idx);
        }

        else
        {
            trans_idx = (n1.idx, n2.idx);
            transition.name = n1.idx + "&" + n2.idx;
            transition.idx = n1.idx + "&" + n2.idx;
            transition.position = new Vector3(
                    (Mathf.Max(n1.position.x - n1.scale.x / 2, n2.position.x - n2.scale.x / 2) + Mathf.Min(n1.position.x + n1.scale.x / 2, n2.position.x + n2.scale.x / 2)) / 2,
                    (Mathf.Max(n1.position.y - n1.scale.y / 2, n2.position.y - n2.scale.y / 2) + Mathf.Min(n1.position.y + n1.scale.y / 2, n2.position.y + n2.scale.y / 2)) / 2,
                    (Mathf.Max(n1.position.z - n1.scale.z / 2, n2.position.z - n2.scale.z / 2) + Mathf.Min(n1.position.z + n1.scale.z / 2, n2.position.z + n2.scale.z / 2)) / 2);
            //GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //g.transform.position = transition.position;
            // we use scale to store the size of the connecting surface
            transition.scale = new Vector3(
                Mathf.Min(n1.position.x + n1.scale.x / 2, n2.position.x + n2.scale.x / 2) - Mathf.Max(n1.position.x - n1.scale.x / 2, n2.position.x - n2.scale.x / 2),
                Mathf.Min(n1.position.y + n1.scale.y / 2, n2.position.y + n2.scale.y / 2) - Mathf.Max(n1.position.y - n1.scale.y / 2, n2.position.y - n2.scale.y / 2),
                Mathf.Min(n1.position.z + n1.scale.z / 2, n2.position.z + n2.scale.z / 2) - Mathf.Max(n1.position.z - n1.scale.z / 2, n2.position.z - n2.scale.z / 2));
        }
        // add the edges from and to that transition
        if (task == "finished" && !data.nodes.ContainsKey(transition.idx))
        {
            // the transitions are with the valid neighbors of n1 and n2
            List<string> edges_to_add = new List<string>();
            foreach (KeyValuePair<string, List<string>> entry in n1.valid_neighbors)
            {
                foreach (string neigh_idx in entry.Value)
                {
                    if (data.nodes.ContainsKey(n1.idx + "&" + neigh_idx))
                        edges_to_add.Add(n1.idx + "&" + neigh_idx);
                    else if (data.nodes.ContainsKey(neigh_idx + "&" + n1.idx))
                        edges_to_add.Add(neigh_idx + "&" + n1.idx);
                }
            }
            foreach (KeyValuePair<string, List<string>> entry in n2.valid_neighbors)
            {
                foreach (string neigh_idx in entry.Value)
                {
                    if (data.nodes.ContainsKey(n2.idx + "&" + neigh_idx))
                        edges_to_add.Add(n2.idx + "&" + neigh_idx);
                    else if (data.nodes.ContainsKey(neigh_idx + "&" + n2.idx))
                        edges_to_add.Add(neigh_idx + "&" + n2.idx);
                }
            }
            foreach (string other_idx in edges_to_add)
            {
                CustomNodeScriptable other_tr = data.nodes[other_idx];
                if (!other_tr.edges.Contains((transition.idx, Vector3.Distance(transition.position, other_tr.position))))
                {
                    transition.edges.Add((other_tr.idx, Vector3.Distance(transition.position, other_tr.position)));
                    other_tr.edges.Add((transition.idx, Vector3.Distance(transition.position, other_tr.position)));
                }
            }
        }
        return (transition, trans_idx);
    }


    public void RemoveTransition(string cn1_idx, string cn2_idx)
    {
        // delete the transition and the edges pointing to it
        string tr_idx = cn1_idx + "&" + cn2_idx;
        if (!data.nodes.ContainsKey(tr_idx))
            // if the transition is stored as c2_idx&c1_idx
            tr_idx = cn2_idx + "&" + cn1_idx;
        if (data.nodes.ContainsKey(tr_idx))
        {
            CustomNodeScriptable transition = data.nodes[tr_idx];
            foreach (var edge in transition.edges)
            {
                CustomNodeScriptable neigh = data.nodes[edge.Item1];
                var back_edge = (tr_idx, edge.Item2);
                neigh.edges.Remove(back_edge);
            }
            data.nodes.Remove(tr_idx);
        }
    }

    public void UpdateGraph()
    {
        // add or remove the appropriate transitions to reflect the change in the octtree
        foreach (var (cn1_idx, cn2_idx) in transitions_add)
        {
            CustomNodeScriptable cn1 = data.FindNode(cn1_idx);
            CustomNodeScriptable cn2 = data.FindNode(cn2_idx);
            if (cn1 != null && cn2 != null && cn1.tag == "Valid" && cn2.tag == "Valid")
            {
                var (transition, trans_idx) = CreateTransition(cn1, cn2);
                data.nodes[transition.idx] = transition;
            }
        }
        foreach (var (cn1_idx, cn2_idx) in transitions_remove)
        {
            RemoveTransition(cn1_idx, cn2_idx);
        }

    }

    public void UpdateOctTree()
    {
        transitions_add = new List<(string, string)>();
        transitions_remove = new List<(string, string)>();
        t0 = Time.realtimeSinceStartupAsDouble;
        // iterate over all nodes
        // if valid -> invalid, split + update neighbors
        CollisionCheck coll = GetComponent<CollisionCheck>();
        foreach (CustomNodeScriptable cv in data.validNodes.Values)
        {
            // check that this node is not empty anymore and has not been staged to be split already
            if (!to_split.Contains(cv) && !coll.IsEmpty(cv.position, cv.scale))
            {
                to_split.Add(cv);
                to_repair.Remove(cv);
            }
        }

        // if invalid -> valid, try to restore the parent node
        foreach (CustomNodeScriptable ci in data.invalidNodes.Values)
        {
            // check that this node is now empty and has not been staged to be repaired already
            if (!to_repair.Contains(ci) && coll.IsEmpty(ci.position, ci.scale))
            {
                to_repair.Add(ci);
                to_split.Remove(ci);
            }
        }
        // if there is an obstacle along the path, update the graph and recompute the path
        bool path_blocked = false;
        if (GetComponent<AstarFast>().move)
        {
            foreach (var idx in GetComponent<AstarFast>().CellsAhead())
            {
                CustomNodeScriptable cn = data.FindNode(idx);
                if (to_split.Contains(cn))
                    path_blocked = true;
            }
        }
        else
        {
            foreach (var idx in data.path_cells)
            {
                CustomNodeScriptable cn = data.FindNode(idx);
                if (to_split.Contains(cn))
                    path_blocked = true;
            }
        }
        // if we want to update as soon as we can (not using the movement system) just use path_blocked = to_repair.Count > 0 || to_split.Count > 0
        //path_blocked = to_repair.Count > 0 || to_split.Count > 0;
        if (path_blocked)
        {
            while (to_split.Count > 0)
            {
                var node = to_split[0];
                to_split.Remove(to_split.First());
                SplitNode(node);
            }
            foreach (CustomNodeScriptable ci in to_repair)
                RepairNode(ci);
            to_split = new List<CustomNodeScriptable>();
            to_repair = new List<CustomNodeScriptable>();

            Debug.Log("OctTree updated in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            t0 = Time.realtimeSinceStartupAsDouble;
            // update the graph with a local update method
            UpdateGraph();
            AstarFast path_finder = GetComponent<AstarFast>();
            if (!path_finder.read_from_file)
            {
                try
                {
                    path_finder.RecomputePath();
                }
                catch
                {
                    Debug.Log("start or target inside of obstacle");
                }
            }
            Debug.Log("Graph updated in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
        }


    }

    
    public void RepairNode(CustomNodeScriptable node)
    {
        // transform the invalid node into a valid node, updating its neighbors
        ChangeType(node, "Invalid", "Valid");
        foreach (KeyValuePair<string, List<string>> entry in node.valid_neighbors)
        {
            // add a transition between the now valid node and all of its valid neighbors
            foreach (string neigh in entry.Value)
                transitions_add.Add((node.idx, neigh));
        }
        data.UpdateNeighborsOnValid(node);

        // start a greedy merge from the repaired node
        // when we merge 2 nodes, remove their transitions and add new ones for the merged cell
        PruneOctTree(new List<CustomNodeScriptable> { node }, null);
    }

    public void ChangeType(CustomNodeScriptable cn, string old_type, string new_type)
    {
        // change the type of an octTree cell between "Node", "Valid" and "Invalid"
        if (new_type == "Valid")
        {
            cn.tag = "Valid";
            data.validNodes[cn.idx] = cn;
            if (old_type == "Invalid")
                data.invalidNodes.Remove(cn.idx);
            else if (old_type == "Node")
                data.cells.Remove(cn.idx);
        }
        else if (new_type == "Invalid")
        {
            cn.tag = "Invalid";
            data.invalidNodes[cn.idx] = cn;
            if (old_type == "Valid")
                data.validNodes.Remove(cn.idx);
            else if (old_type == "Node")
                data.cells.Remove(cn.idx);
        }
        else
        {
            cn.tag = "Node";
            data.cells[cn.idx] = cn;
            if (old_type == "Valid")
                data.validNodes.Remove(cn.idx);
            else if (old_type == "Invalid")
                data.invalidNodes.Remove(cn.idx);
        }
    }
}
