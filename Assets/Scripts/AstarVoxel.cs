using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AstarVoxel : MonoBehaviour
{
    [HideInInspector] public Vector3 start;
    [HideInInspector] public Vector3 target;
    public Dictionary<string, CustomVoxel> nodes;
    public Dictionary<string, List<(string, float)>> edges;
    public bool draw;
    private Dictionary<string, List<(string, float)>> temp_edges;
    private Voxel voxelizer;
    private CustomVoxel startVoxel;
    private CustomVoxel targetVoxel;
    private string start_idx;
    private string target_idx;
    private int nb_visited;
    private GameObject drawPath;

    // Start is called before the first frame update
    void Start()
    {
        drawPath = new GameObject();
        nodes = new Dictionary<string, CustomVoxel>();
        edges = new Dictionary<string, List<(string, float)>>();
        start = GameObject.Find("Start").transform.position;
        target = GameObject.Find("Target").transform.position;
        voxelizer = gameObject.GetComponent<Voxel>();

    }

    private float heuristic(CustomVoxel node)
    {
        return node.cost_to_start + node.dist_to_goal;
    }

    void InsertTempNode(CustomVoxel temp_node)
    {
        nodes[temp_node.name] = temp_node;
        edges[temp_node.name] = new List<(string, float)>();
        // reach the voxel containing the node
        var idx = voxelizer.Idx2Name(voxelizer.Coord2idx(temp_node.position));

        if (temp_node.name == "start")
            start_idx = idx;
        if (temp_node.name == "target")
            target_idx = idx;

        // add edges between the temp node and neighbor transitions
        CustomVoxel cv = GameObject.Find(idx).GetComponent<CustomVoxel>();
        foreach (var n_idx in voxelizer.edges[cv.idx])
        {
            string neigh_idx = voxelizer.Idx2Name(n_idx);
            CustomVoxel neigh_ = GameObject.Find(neigh_idx).GetComponent<CustomVoxel>();
            if (neigh_.CompareTag("Valid"))
            {
                string trans_idx = "";
                if (nodes.ContainsKey(cv.name + "&" + neigh_idx))
                    trans_idx = cv.name + "&" + neigh_idx;
                else if (nodes.ContainsKey(neigh_idx + "&" + cv.name))
                    trans_idx = neigh_idx + "&" + cv.name;
                if (trans_idx != "")
                {
                    edges[trans_idx].Add((temp_node.name, Vector3.Distance(nodes[trans_idx].position, temp_node.position)));
                    if (!temp_edges.ContainsKey(trans_idx))
                        temp_edges[trans_idx] = new List<(string, float)>();
                    temp_edges[trans_idx].Add((temp_node.name, Vector3.Distance(nodes[trans_idx].position, temp_node.position)));
                    edges[temp_node.name].Add((trans_idx, Vector3.Distance(nodes[trans_idx].position, temp_node.position)));
                }
            }
        }
    }

    public (int, float, float) A_star_path(Vector3 start, Vector3 target)
    {
        //drawPath = new GameObject();
        //drawPath.name = "DrawPath";
        double t0 = Time.realtimeSinceStartupAsDouble;
        nb_visited = 0;
        temp_edges = new Dictionary<string, List<(string, float)>>();
        GameObject gs = new GameObject();
        gs.AddComponent<CustomVoxel>();
        startVoxel = gs.GetComponent<CustomVoxel>();
        startVoxel.name = "start";
        startVoxel.position = start;
        InsertTempNode(startVoxel);
        GameObject gt = new GameObject();
        gt.AddComponent<CustomVoxel>();
        targetVoxel = gt.GetComponent<CustomVoxel>();
        targetVoxel.name = "target";
        targetVoxel.position = target;
        InsertTempNode(targetVoxel);
        foreach (var node in nodes.Values)
            node.ResetVoxel(target);
        var shortestPath = new List<CustomVoxel>();
        List<Vector3> path_positions = new List<Vector3>();
        if (start_idx != target_idx)
        {
            AstarSearch();
            CustomVoxel cv = targetVoxel;
            while (cv.nearest_to_start != startVoxel)
            {
                shortestPath.Add(cv);
                cv = cv.nearest_to_start;
            }
            shortestPath.Add(cv);
            shortestPath.Add(startVoxel);
            shortestPath.Reverse();
        }
        else
            shortestPath = new List<CustomVoxel> { startVoxel, targetVoxel };
        foreach (CustomVoxel v in shortestPath)
            path_positions.Add(v.position);
        // drawing the straight line path and the computed path
        CustomVoxel previous = startVoxel;
        float path_length = Vector3.Distance(start, startVoxel.position);
        foreach (var node in shortestPath) {
            path_length += Vector3.Distance(previous.position, node.position);
            previous = node;
        }
        path_length += Vector3.Distance(targetVoxel.position, target);
        if (draw)
            DrawPath(path_positions, Color.blue);

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
        startVoxel.cost_to_start = 0;
        var prioQueue = new List<CustomVoxel>();
        prioQueue.Add(startVoxel);
        do
        {
            prioQueue = prioQueue.OrderBy(heuristic).ToList();
            var node = prioQueue.First();
            prioQueue.Remove(node);
            foreach (var (idx, cost) in edges[node.name].OrderBy(x => x.Item2))
            {
                CustomVoxel cv;
                if (idx != "start" && idx != "target" && !nodes.ContainsKey(idx))
                {
                    string inv_idx = idx.Split('&')[1] + "&" + idx.Split('&')[0];
                    cv = nodes[inv_idx];
                }
                else
                    cv = nodes[idx];
                if (cv.visited)
                    continue;
                if (cv.cost_to_start == -1 ||
                    node.cost_to_start + 1 < cv.cost_to_start)
                {
                    cv.cost_to_start = node.cost_to_start + 1;
                    cv.nearest_to_start = node;
                    if (!prioQueue.Contains(cv))
                        prioQueue.Add(cv);
                }
            }
            node.visited = true;
            nb_visited += 1;
            if (node == targetVoxel)
                return;
        } while (prioQueue.Any());
    }

    public void DrawPath(List<Vector3> path, Color color)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector3 u = path[i];
            Vector3 v = path[i + 1];
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
            lr.SetPosition(0, u);
            lr.SetPosition(1, v);
            Destroy(myLine, 1000);
        }
    }
}
