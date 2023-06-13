using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public class Astar : MonoBehaviour
{
    [HideInInspector] public Vector3 start;
    [HideInInspector] public Vector3 target;
    public bool read_from_file;
    public bool draw;
    public bool prune;
    [HideInInspector] public Dictionary<string, CustomNode> nodes;
    [HideInInspector] public Dictionary<string, List<(string, float)>> edges;
    private Dictionary<string, List<(string, float)>> temp_edges;
    private CustomNode start_node;
    private CustomNode target_node;
    private string start_idx;
    private string target_idx;
    private bool done;
    private int nb_visited;
    private GameObject drawPath;

    // Start is called before the first frame update
    void Start()
    {
        done = false;
        nodes = new Dictionary<string, CustomNode>();
        edges = new Dictionary<string, List<(string, float)>>();
        start = GameObject.Find("Start").transform.position;
        target = GameObject.Find("Target").transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        if (!done && gameObject.CompareTag("Finished"))
        {
            string filename = Application.dataPath + "/TestPoints/" + SceneManager.GetActiveScene().name + ".txt";
            if (read_from_file)
                TestRandomPositions(filename);
            else
                A_star_path(start, target);
            done = true;
        }   
    }

    void InsertTempNode(CustomNode temp_node)
    {
        nodes[temp_node.idx] = temp_node;
        edges[temp_node.idx] = new List<(string, float)>();
        string idx = "0";
        GameObject currentNode = GameObject.Find("_0");
        // reach the corresponding tree in the octree before pruning
        while (currentNode != null && currentNode.CompareTag("Node"))
        {
            Vector3 node_position = currentNode.transform.position;
            // Find in which children the argument position is
            int i = 1 * (temp_node.position.x > node_position.x ? 1 : 0) + 2 * (temp_node.position.z > node_position.z ? 1 : 0) + 4 * (temp_node.position.y > node_position.y ? 1 : 0);
            idx = idx + i.ToString();
            // find the new node associated with this one after merging
            currentNode = GameObject.Find("_"+idx);
        }
        idx = gameObject.GetComponent<OctTree>().FindMergedNode("_"+idx);

        if (temp_node.idx == "start")
            start_idx = idx;
        if (temp_node.idx == "target")
            target_idx = idx;

        // add edges between the temp node and neighbor transitions
        CustomNode cn = GameObject.Find(idx).GetComponent<CustomNode>();
        foreach (string key in cn.valid_neighbors.Keys)
        {
            foreach (string neigh_idx in cn.valid_neighbors[key])
            {
                string trans_idx = "";
                if (nodes.ContainsKey(cn.name + "&" +"_"+ neigh_idx))
                    trans_idx = cn.name + "&" + "_" + neigh_idx;
                else if (nodes.ContainsKey("_" + neigh_idx + "&" + cn.name))
                    trans_idx = "_" + neigh_idx + "&" + cn.name;
                if (trans_idx != "")
                {
                    edges[trans_idx].Add((temp_node.idx, Vector3.Distance(nodes[trans_idx].position, temp_node.position)));
                    if (!temp_edges.ContainsKey(trans_idx))
                        temp_edges[trans_idx] = new List<(string, float)>();
                    temp_edges[trans_idx].Add((temp_node.idx, Vector3.Distance(nodes[trans_idx].position, temp_node.position)));
                    edges[temp_node.idx].Add((trans_idx, Vector3.Distance(nodes[trans_idx].position, temp_node.position)));
                }
            }
        }
    }


    private float heuristic(CustomNode node)
    {
        return node.cost_to_start + node.dist_to_goal;
    }

    (int, float, float) A_star_path(Vector3 start, Vector3 target)
    {
        if (draw)
        {
            drawPath = new GameObject();
            drawPath.name = "DrawPath";
        }
        nb_visited = 0;
        temp_edges = new Dictionary<string, List<(string, float)>>();
        double t0 = Time.realtimeSinceStartupAsDouble;
        GameObject gs = new GameObject();
        gs.AddComponent<CustomNode>();
        start_node = gs.GetComponent<CustomNode>();
        start_node.idx = "start";
        start_node.position = start;
        InsertTempNode(start_node);
        GameObject gt = new GameObject();
        gt.AddComponent<CustomNode>();
        target_node = gt.GetComponent<CustomNode>();
        target_node.idx = "target";
        target_node.position = target;
        InsertTempNode(target_node);
        var shortestPath = new List<CustomNode>();
        if (start_idx != target_idx)
        {
            foreach (var node in nodes.Values)
                node.ResetNode(target);
            AstarSearch();

            CustomNode cn = target_node;
            while (cn.nearest_to_start != start_node)
            {
                shortestPath.Add(cn);
                cn = cn.nearest_to_start;
            }
            shortestPath.Add(cn);
            shortestPath.Add(start_node);
            shortestPath.Reverse();
            if (prune)
                shortestPath = prunePath(shortestPath);
        }
        else
            shortestPath = new List<CustomNode> { start_node, target_node };
        // drawing the straight line path and the computed path
        CustomNode previous = start_node;
        float path_length = Vector3.Distance(start, start_node.position);
        foreach (var node in shortestPath) {
            if (draw)
                DrawLine(previous.position, node.position, Color.green);
            path_length += Vector3.Distance(previous.position, node.position);
            previous = node;
        }
        path_length += Vector3.Distance(target_node.position, target);
        if (draw)
            DrawLine(target_node.position, target, Color.green);

        // remove the temporary edges
        foreach (string key in temp_edges.Keys)
        {
            foreach (var value in temp_edges[key])
                edges[key].Remove(value);
        }
        temp_edges = new Dictionary<string, List<(string, float)>>();
        float dt = (float)(Time.realtimeSinceStartupAsDouble - t0);
        Destroy(gs);
        Destroy(gt);
        return (nb_visited, path_length, dt);
    }


    private void AstarSearch()
    {
        start_node.cost_to_start = 0;
        var prioQueue = new List<CustomNode>();
        prioQueue.Add(start_node);
        do
        {
            prioQueue = prioQueue.OrderBy(heuristic).ToList();
            var node = prioQueue.First();
            prioQueue.Remove(node);
            foreach (var (idx, cost) in edges[node.idx].OrderBy(x => x.Item2))
            {
                CustomNode childNode;
                if (idx != "start" && idx != "target" && !nodes.ContainsKey(idx))
                {
                    string inv_idx = idx.Split('&')[1] + "&" + idx.Split('&')[0];
                    childNode = nodes[inv_idx];
                }
                else
                    childNode = nodes[idx];
                if (childNode.visited)
                    continue;
                if (childNode.cost_to_start == -1 ||
                    node.cost_to_start + cost < childNode.cost_to_start)
                {
                    childNode.cost_to_start = node.cost_to_start + cost;
                    childNode.nearest_to_start = node;
                    if (!prioQueue.Contains(childNode))
                        prioQueue.Add(childNode);
                }
            }
            node.visited = true;
            nb_visited += 1;
            if (node == target_node)
                return;
        } while (prioQueue.Any());
    }

    public void DrawLine(Vector3 start, Vector3 end, Color color)
    {
        GameObject myLine = new GameObject();
        myLine.transform.parent = drawPath.transform;
        myLine.transform.position = start;
        myLine.AddComponent<LineRenderer>();
        LineRenderer lr = myLine.GetComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.startWidth = 0.1f;
        lr.endWidth = 0.1f;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        GameObject.Destroy(myLine, 100);
    }

    public void TestRandomPositions(string filename)
    {
        int n = 0;
        float avg_nodes_searched = 0;
        float avg_length = 0;
        float avg_time = 0;
        StreamReader sr = new StreamReader(filename);
        string s = sr.ReadLine();
        while (s != null)
        {
            string[] li = s.Split("_");
            Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2]));
            Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5]));
            var (searched, length, dt) = A_star_path(start, target);
            avg_nodes_searched += searched;
            avg_length += length;
            avg_time += dt;
            n++;
            s = sr.ReadLine();
        }
        avg_nodes_searched /= n;
        avg_length /= n;
        avg_time /= n;
        Debug.Log("Found " + n + " paths of avg length " + avg_length + " by searching " + avg_nodes_searched + " nodes in " + decimal.Round(((decimal)(avg_time)) * 1000m, 3) + " ms");
    }

    public List<CustomNode> prunePath(List<CustomNode> path)
    {
        float old_dist = 0;
        float new_dist = 0;
        CustomNode last = path.Last();
        List<CustomNode> new_path = new List<CustomNode> { last };
        for (int i = path.Count -2; i>0; i--)
        {
            old_dist += Vector3.Distance(path[i].position, path[i + 1].position);
            CustomNode node = path[i];
            if (Physics.Raycast(last.position, node.position - last.position, Vector3.Distance(node.position, last.position)))
            {
                new_path.Add(path[i+1]);
                new_dist += Vector3.Distance(path[i+1].position, last.position);
                last = path[i + 1];
            }
        }
        CustomNode first = path.First();
        new_path.Add(first);
        old_dist += Vector3.Distance(first.position, path[1].position);
        new_dist += Vector3.Distance(first.position, last.position);
        new_path.Reverse();
        //Debug.Log("Before pruning path " + path.Count + " nodes, length " + old_dist + " after pruning " + new_path.Count + " nodes, length " + new_dist);
        return new_path;
    }
}
