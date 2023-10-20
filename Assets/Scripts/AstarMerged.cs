using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using UnityEngine.SceneManagement;

public class AstarMerged : MonoBehaviour
{
    [HideInInspector] public Vector3 start;
    [HideInInspector] public Vector3 target;
    public bool read_from_file;
    public bool draw;
    public bool prune;
    public bool funnel;
    public bool move;
    public float collDetectRange = 5;
    [HideInInspector] public NodeDataMerged data;
    private Dictionary<string, List<(string, float)>> temp_edges;
    private CustomNodeScriptable startNode;
    private CustomNodeScriptable targetNode;
    private string start_idx;
    private string target_idx;
    [HideInInspector] public bool done;
    private int nb_visited;
    private float distAlongPath = 0;
    private List<(Vector3, string, float)> path_idx = new List<(Vector3, string, float)>();
    private GameObject drawPath;

    // Start is called before the first frame update
    void Start()
    {
        done = false;
        startNode = ScriptableObject.CreateInstance<CustomNodeScriptable>();
        targetNode = ScriptableObject.CreateInstance<CustomNodeScriptable>();
        start = GameObject.Find("Start").transform.position;
        target = GameObject.Find("Target").transform.position;
        drawPath = new GameObject();
        drawPath.name = "DrawPath";
    }

    // Update is called once per frame
    void Update()
    {
        if (!done && gameObject.CompareTag("Finished"))
        {
            done = true;

            // Create 1000 pair of test points for each bucket of paths lengths ([0,5], [5,10]...)
            string folder = Application.dataPath + "/TestPoints/" + SceneManager.GetActiveScene().name;
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                CreateRandomPositions(1000, folder);
            }

            // execute A* on all the random positions
            if (read_from_file)
                TestRandomPositions(folder);
            else if (GameObject.Find("MapGenerator") == null)
                A_star_path(start, target);
            string path = SceneManager.GetActiveScene().name + gameObject.GetComponent<OctTreeMerged>().minSize;
            if (!GetComponent<OctTreeMerged>().load || !AssetDatabase.IsValidFolder("Assets/Data/" + path))
            {
               // Debug.Log("Saving data to file Assets/Data/" + path + "...");
                double t0 = Time.realtimeSinceStartupAsDouble;
                data.SaveData(path);
               // Debug.Log("Saved data to file Assets/Data/" + path + " in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            }
        }
        else if (done)
        {
            if (move && distAlongPath < path_idx.Last().Item3 + 1)
            {
                if (distAlongPath == 0)
                {
                    GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    g.transform.position = start;
                    g.transform.localScale = 0.5f * Vector3.one;
                    g.name = "Player";
                }
                MoveAlongPath();
                distAlongPath += 2 * Time.deltaTime;
            }
        }
    }

    public string Pos2Idx(Vector3 v)
    {
        // iterate breadth first over the nodes containing v, stop when a valid cell is found
        List<CustomNodeScriptable> candidate_nodes = new List<CustomNodeScriptable> { data.FindNode("0")};
        while (candidate_nodes.Count != 0 )
        {
            CustomNodeScriptable cn = candidate_nodes[0];
            candidate_nodes.Remove(candidate_nodes.First());
            if (cn.tag == "Valid")
                return cn.idx;
            // if cn is a node, add its children that contain v
            foreach (string child_idx in cn.children)
            {
                CustomNodeScriptable child = data.FindNode(child_idx);
                if (child == null)
                    continue;
                if ((child.position.x - child.scale.x/2 <= v.x && v.x <= child.position.x + child.scale.x/2) &&
                    (child.position.y - child.scale.y/2 <= v.y && v.y <= child.position.y + child.scale.y/2) &&
                    (child.position.z - child.scale.z/2 <= v.z && v.z <= child.position.z + child.scale.z/2))
                {
                    candidate_nodes.Add(child);
                }
            }
        }
        return null;
    }

    void InsertTempNode(CustomNodeScriptable temp_node)
    {
        data.nodes[temp_node.idx] = temp_node;
        temp_node.edges = new List<(string, float)>();
        string idx = Pos2Idx(temp_node.position);
        if (temp_node.idx == "start")
            start_idx = idx;
        if (temp_node.idx == "target")
            target_idx = idx;

        // add edges between the temp node and neighbor transitions
        CustomNodeScriptable cn = data.FindNode(idx);
        foreach (string key in cn.valid_neighbors.Keys)
        {
            foreach (string neigh_idx in cn.valid_neighbors[key])
            {
                string trans_idx = "";
                if (data.nodes.ContainsKey(cn.idx + "&" + neigh_idx))
                    trans_idx = cn.idx + "&" + neigh_idx;
                else if (data.nodes.ContainsKey(neigh_idx + "&" + cn.idx))
                    trans_idx = neigh_idx + "&" + cn.idx;
                if (trans_idx != "")
                {
                    data.nodes[trans_idx].edges.Add((temp_node.idx, Vector3.Distance(data.nodes[trans_idx].position, temp_node.position)));
                    if (!temp_edges.ContainsKey(trans_idx))
                        temp_edges[trans_idx] = new List<(string, float)>();
                    temp_edges[trans_idx].Add((temp_node.idx, Vector3.Distance(data.nodes[trans_idx].position, temp_node.position)));
                    temp_node.edges.Add((trans_idx, Vector3.Distance(data.nodes[trans_idx].position, temp_node.position)));
                }
            }
        }
    }


    private float Heuristic(CustomNodeScriptable node)
    {
        return node.cost_to_start + node.dist_to_goal;
    }

    public (int, float, double) A_star_path(Vector3 start, Vector3 target)
    {
        nb_visited = 0;
        temp_edges = new Dictionary<string, List<(string, float)>>();
        double t0 = Time.realtimeSinceStartupAsDouble;
        startNode = ScriptableObject.CreateInstance<CustomNodeScriptable>();
        startNode.name = "start";
        startNode.idx = "start";
        startNode.position = start;
        InsertTempNode(startNode);
        targetNode = ScriptableObject.CreateInstance<CustomNodeScriptable>();
        targetNode.name = "target";
        targetNode.idx = "target";
        targetNode.position = target;
        InsertTempNode(targetNode);
        var shortestPath = new List<CustomNodeScriptable>();
        List<Vector3> path_positions = new List<Vector3>();
        float path_length = 0;
        if (start_idx != target_idx)
        {
            foreach (var node in data.nodes.Values)
                node.ResetNode(target);
            double t1 = Time.realtimeSinceStartup;
            AstarSearch();
            Debug.Log("A* " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t1)) * 1000m, 3) + " ms" + nb_visited);
            CustomNodeScriptable cn = targetNode;
            while (cn.nearest_to_start != startNode)
            {
                shortestPath.Add(cn);
                cn = cn.nearest_to_start;
            }
            shortestPath.Add(cn);
            shortestPath.Add(startNode);
            shortestPath.Reverse();

            // path refining
            data.path_cells = new List<string>();
            foreach (CustomNodeScriptable n in shortestPath)
            {
                path_positions.Add(n.position);
                if (n.idx == "start" || n.idx == "target")
                    continue;
                string[] t = n.idx.Split("&");
                if (!data.path_cells.Contains(t[0]))
                    data.path_cells.Add(t[0]);
                if (!data.path_cells.Contains(t[1]))
                    data.path_cells.Add(t[1]);
            }
            if (draw)
                DrawPath(path_positions, Color.red);
            if (funnel)
                path_positions = gameObject.GetComponent<Funnel>().Funnel3D(shortestPath);
            if (draw)
            {
                DrawPath(path_positions, Color.yellow);
            }
            if (prune)
                path_positions = PrunePath(path_positions);
            if (draw)
            {
                DrawPath(path_positions, Color.green);
            }
            // keep track of the nodes we cross and their distance along the path for the movement model and recomputing the path
            if (move)
                ComputePathIdx2(path_positions);
            path_length = PathLength(path_positions);
        }
        else
        {
            shortestPath = new List<CustomNodeScriptable> { startNode, targetNode };
            data.path_cells.Add(start_idx);
            path_length = Vector3.Distance(startNode.position, targetNode.position);
        }

        // remove the temporary edges
        foreach (string key in temp_edges.Keys)
        {
            foreach (var value in temp_edges[key])
                data.nodes[key].edges.Remove(value);
        }
        temp_edges = new Dictionary<string, List<(string, float)>>();
        double dt = (Time.realtimeSinceStartupAsDouble - t0);
        //Debug.Log("TOTAL " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
        return (nb_visited, path_length, dt);
    }


    private void AstarSearch()
    {
        startNode.cost_to_start = 0;
        var prioQueue = new Utils.PriorityQueue<CustomNodeScriptable, float>();
        prioQueue.Enqueue(startNode, Heuristic(startNode)); 
        do
        {
            var node = prioQueue.Dequeue();
            if (node.visited)
                continue;
            foreach (var (idx, cost) in node.edges)
            {
                CustomNodeScriptable childNode;
                if (idx != "start" && idx != "target" && !data.nodes.ContainsKey(idx))
                {
                    string inv_idx = idx.Split('&')[1] + "&" + idx.Split('&')[0];
                    childNode = data.nodes[inv_idx];
                }
                else
                    childNode = data.nodes[idx];
                if (childNode.visited)
                    continue;
                if (childNode.cost_to_start == -1 ||
                    node.cost_to_start + cost < childNode.cost_to_start)
                {
                    childNode.cost_to_start = node.cost_to_start + cost;
                    childNode.nearest_to_start = node;
                    prioQueue.Enqueue(childNode, Heuristic(node));
                }
            }
            node.visited = true;
            nb_visited += 1;
            if (node.idx == targetNode.idx)
                return;
        } while (prioQueue.Count != 0);
    }

    public List<Vector3> ComputePath(CustomNodeScriptable s, CustomNodeScriptable t)
    {
        List<Vector3> path = new List<Vector3>();
        if (s != t)
        {
            foreach (var node in data.nodes.Values)
                node.ResetNode(target);
            s.cost_to_start = 0;
            var prioQueue = new Utils.PriorityQueue<CustomNodeScriptable, float>();
            prioQueue.Enqueue(s, -Heuristic(s));
            do
            {
                var node = prioQueue.Dequeue();
                if (node.visited)
                continue;
                foreach (var (idx, cost) in node.edges)
                {
                    CustomNodeScriptable childNode;
                    if (idx != s.idx && idx != t.idx && !data.nodes.ContainsKey(idx))
                    {
                        string inv_idx = idx.Split('&')[1] + "&" + idx.Split('&')[0];
                        childNode = data.nodes[inv_idx];
                    }
                    else
                        childNode = data.nodes[idx];
                    if (childNode.visited)
                        continue;
                    if (childNode.cost_to_start == -1 ||
                        node.cost_to_start + cost < childNode.cost_to_start)
                    {
                        childNode.cost_to_start = node.cost_to_start + cost;
                        childNode.nearest_to_start = node;
                        prioQueue.Enqueue(childNode, Heuristic(childNode));
                    }
                }
                node.visited = true;
                if (node.idx == t.idx)
                    break;
            } while (prioQueue.Count != 0);
            CustomNodeScriptable cn = t;
            while (cn.nearest_to_start != s)
            {
                path.Add(cn.position);
                cn = cn.nearest_to_start;
            }
            path.Add(cn.position);
            path.Add(s.position);
            path.Reverse();
            return path;
        }
        else
            return new List<Vector3> { s.position };
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


    public List<Vector3> PrunePath(List<Vector3> path)
    {
        float old_dist = 0;
        float new_dist = 0;
        Vector3 last = path.Last();
        List<Vector3> new_path = new List<Vector3> { last };
        for (int i = path.Count - 2; i >= 0; i--)
        {
            old_dist += Vector3.Distance(path[i], path[i + 1]);
            Vector3 pos = path[i];
            bool visible = IsVisible(last, pos);
            if (!visible)
            {
                new_path.Add(path[i + 1]);
                new_dist += Vector3.Distance(path[i + 1], last);
                last = path[i + 1];
            }
        }
        Vector3 first = path.First();
        new_path.Add(first);
        new_path.Reverse();
        return new_path;
    }

    public bool IsVisible(Vector3 x, Vector3 y)
    {
        // returns true iff the straight line between x and y does not intersect an invalid octtree cell
        //bool visible = !Physics.Raycast(x, y - x, Vector3.Distance(y, x));
        bool visible = true;
        float step = gameObject.GetComponent<OctTreeMerged>().minSize / 5;
        float n = Mathf.Floor(Vector3.Distance(x, y) / step);
        OctTreeMerged octTreeGenerator = gameObject.GetComponent<OctTreeMerged>();
        for (int j = 0; j < n; j++)
        {
            Vector3 pos = x + j / n * (y - x);
            string idx = Pos2Idx(pos);
            if (idx == null || !data.validNodes.ContainsKey(idx))
            {
                visible = false;
                break;
            }
        }
        return visible;
    }

    public float PathLength(List<Vector3> path)
    {
        float path_length = 0;
        for (int i = 1; i < path.Count; i++)
            path_length += Vector3.Distance(path[i], path[i - 1]);
        return path_length;
    }

    public void CreateRandomPositions(int n, string folder)
    {
        float t = Time.realtimeSinceStartup;
        // preparing 6 buckets of size bound/2
        var octTree = GetComponent<OctTreeMerged>();
        List<int> count = new List<int> { 0, 0, 0, 0, 0, 0 };
        StreamWriter[] sw = new StreamWriter[6];
        for (int idx = 0; idx < count.Count; idx++)
            sw[idx] = new StreamWriter(folder + "/" + idx * octTree.bound / 2 + "-" + (idx + 1) * octTree.bound / 2 + ".txt");

        while (count.Any(i => i < n))
        {
            Vector3 start = new Vector3(UnityEngine.Random.Range(-octTree.bound, octTree.bound), UnityEngine.Random.Range(0, octTree.zBound), UnityEngine.Random.Range(-octTree.bound, octTree.bound));
            Vector3 target = new Vector3(UnityEngine.Random.Range(-octTree.bound, octTree.bound), UnityEngine.Random.Range(0, octTree.zBound), UnityEngine.Random.Range(-octTree.bound, octTree.bound));

            try
            {
                var (nb_visited, length, dt) = A_star_path(start, target);
                int idx = (int)Mathf.Floor(2 * length / octTree.bound);
                if (idx >= count.Count)
                    Debug.Log("path too long: " + length);
                if (count[idx] < n)
                {
                    count[idx]++;
                    string s = start.x.ToString() + "_" + start.y.ToString() + "_" + start.z.ToString() + "_" + target.x.ToString() + "_" + target.y.ToString() + "_" + target.z.ToString();
                    sw[idx].WriteLine(s);
                }
            }
            catch
            {
                Debug.Log("random point creation failed");
            }
        }
        for (int idx = 0; idx < count.Count; idx++)
            sw[idx].Close();
        Debug.Log("Found " + n + "pair of points for each bucket in " + decimal.Round(((decimal)(Time.realtimeSinceStartup - t)) * 1000m, 3) + " ms");
    }

    public void TestRandomPositions(string folder)
    {
        if (!Directory.Exists(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name))
            Directory.CreateDirectory(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name);
        StreamWriter sw = new StreamWriter(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name + "/octTree_m" + GetComponent<OctTreeMerged>().elongated_criteria + "_p" + prune + "f_" + funnel + ".txt");
        StreamWriter log = new StreamWriter(Application.dataPath + "/Results/log.txt");
        int n_iter = 0;
        foreach (string filename in Directory.EnumerateFiles(folder))
        {
            var split_filename = filename.Split(".");
            if (split_filename[split_filename.Length - 1] != "txt")
                continue;
            List<double> nodes_searched = new List<double>();
            List<double> length = new List<double>();
            List<double> time = new List<double>();
            StreamReader sr = new StreamReader(filename);
            string s = sr.ReadLine();
            while (s != null)
            {
                log.WriteLine(n_iter);
                n_iter++;
                string[] li = s.Split("_");
                Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2]));
                Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5]));
                try
                {
                    var (searched, l, dt) = A_star_path(start, target);
                    nodes_searched.Add(searched);
                    length.Add(l);
                    time.Add(dt);
                }
                catch
                {
                    Debug.Log("random test failed for " + start + " " + target);
                }
                s = sr.ReadLine();
            }
            int n = nodes_searched.Count;
            nodes_searched.Sort();
            time.Sort();
            length.Sort();
            var (avg_nodes, std_nodes) = STD(nodes_searched);
            var (avg_length, std_length) = STD(length);
            var (avg_time, std_time) = STD(time);
            sw.WriteLine(filename + "success rate" + (float)n / 1000);
            sw.WriteLine("avg," + avg_length.ToString() + "," + avg_nodes.ToString() + "," + decimal.Round(((decimal)(avg_time)) * 1000m, 3).ToString());
            sw.WriteLine("std," + std_length.ToString() + "," + std_nodes.ToString() + "," + decimal.Round(((decimal)(std_time)) * 1000m, 3).ToString());
            sw.WriteLine("min," + length[0].ToString() + "," + nodes_searched[0].ToString() + "," + decimal.Round(((decimal)(time[0])) * 1000m, 3).ToString());
            sw.WriteLine("Q1," + length[(int)n / 4].ToString() + "," + nodes_searched[(int)n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 4])) * 1000m, 3).ToString());
            sw.WriteLine("median," + length[(int)n / 2].ToString() + "," + nodes_searched[(int)n / 2].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 2])) * 1000m, 3).ToString());
            sw.WriteLine("Q3," + length[(int)3 * n / 4].ToString() + "," + nodes_searched[(int)3 * n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)3 * n / 4])) * 1000m, 3).ToString());
            sw.WriteLine("max," + length[n - 1].ToString() + "," + nodes_searched[n - 1].ToString() + "," + decimal.Round(((decimal)(time[n - 1])) * 1000m, 3).ToString());
        }
        sw.Close();
    }

    public (double, double) STD(List<double> li)
    {
        double sum = 0;
        foreach (double x in li)
            sum += x;
        double avg = sum / li.Count;
        double sumOfSquares = 0.0;
        foreach (double x in li)
            sumOfSquares += Math.Pow((x - avg), 2.0);
        return (avg, sumOfSquares / (double)(li.Count - 1));
    }

    public void RecomputePath()
    {
        if (draw)
        {
            for (int i=0; i<drawPath.transform.childCount; i++)
                Destroy(drawPath.transform.GetChild(i).gameObject);
        }
        if (move)
        {
            start = GameObject.Find("Player").transform.position;
            distAlongPath = 0;
        }
        var (nb_visited, path_length, dt) = A_star_path(start, target);
    }

    public void MoveAlongPath()
    {
        GameObject g = GameObject.Find("Player");
        Vector3 previous = start;
        var (next, _, d) = path_idx[0];
        float old_d = 0;
        int cur = 1;
        while (next != target && distAlongPath > d)
        {
            old_d = d;
            previous = next;
            (next, _, d) = path_idx[cur];
            cur++;
        }
        g.transform.position = next + Math.Max(0, (d - distAlongPath) / (d - old_d)) * (previous - next);
    }

    public void ComputePathIdx2(List<Vector3> path)
    {
        float dist_along_path = 0;
        string idx;
        Vector3 previous = start;
        Vector3 next;
        path_idx = new List<(Vector3, string, float)>();
        int i = 1;
        while (i < path.Count)
        {
            next = path[i];
            idx = Pos2Idx(previous + 1e-3f * (next - previous).normalized);
            CustomNodeScriptable cn = data.FindNode(idx);
            // compute the exit point of the node
            Vector3 exit = ComputeExit(previous, next, cn);
            // if it is not the next point in the path, get the pos2idx of a point just after the exit
            if (Vector3.Distance(exit, next) > 1e-3)
            {
                idx = Pos2Idx(exit + 1e-3f * (next - previous).normalized);
                // add the exit point to path_idx, with old_idx and the correct distance
                dist_along_path += Vector3.Distance(previous, exit);
                path_idx.Add((exit, idx, dist_along_path));
                previous = exit;
            }
            // if it is the next point in the path, add it and restart with anchor = next and next = path[i+1]
            else
            {
                dist_along_path += Vector3.Distance(previous, next);
                path_idx.Add((exit, idx, dist_along_path));
                previous = next;
                i++;
            }

        }
    }

    public Vector3 ComputeExit(Vector3 u, Vector3 v, CustomNodeScriptable cn)
    {
        // compute the exit point of cn along [u, v] assuming u is in cn and v is outside
        // if v is inside cn simply return it
        if (
                (v.x >= cn.position.x - cn.scale.x / 2 && v.x <= cn.position.x + cn.scale.x / 2) &&
                (v.y >= cn.position.y - cn.scale.y / 2 && v.y <= cn.position.y + cn.scale.y / 2) &&
                (v.z >= cn.position.z - cn.scale.z / 2 && v.z <= cn.position.z + cn.scale.z / 2)
                )
            return v;
        // this is the first point on the cube we encounter when starting from target
        float[] candidate_t = new float[]
    {
            (Mathf.Abs(v.x - (cn.position.x + cn.scale.x / 2))/(Mathf.Abs(v.x - u.x))),
            (Mathf.Abs(v.x - (cn.position.x - cn.scale.x / 2))/(Mathf.Abs(v.x - u.x))),
            (Mathf.Abs(v.y - (cn.position.y + cn.scale.y / 2))/(Mathf.Abs(v.y - u.y))),
            (Mathf.Abs(v.y - (cn.position.y - cn.scale.y / 2))/(Mathf.Abs(v.y - u.y))),
            (Mathf.Abs(v.z - (cn.position.z + cn.scale.z / 2))/(Mathf.Abs(v.z - u.z))),
            (Mathf.Abs(v.z - (cn.position.z - cn.scale.z / 2))/(Mathf.Abs(v.z - u.z))),
    };
        // one of these 6 points along [v, u] is the correct one, start from the closest to v
        foreach (float t in candidate_t.OrderBy(x => x))
        {
            Vector3 w = (1 - t) * v + t * u;
            // if w is inside the cube return it
            if (
                (w.x >= cn.position.x - cn.scale.x / 2 - 1e-3 && w.x <= cn.position.x + cn.scale.x / 2 + 1e-3) &&
                (w.y >= cn.position.y - cn.scale.y / 2 - 1e-3 && w.y <= cn.position.y + cn.scale.y / 2 + 1e-3) &&
                (w.z >= cn.position.z - cn.scale.z / 2 - 1e-3 && w.z <= cn.position.z + cn.scale.z / 2 + 1e-3)
                )
                return w;
        }
        Debug.LogError("exit not found!");
        return new Vector3();
    }

    public List<string> CellsAhead()
    {
        // returns the list of cells crossed between distAlongPath and distAlongPath + collDetectRange
        List<string> cellsAhead = new List<string>();
        float old_d = 0;
        foreach (var (_, idx, d) in path_idx)
        {
            if (distAlongPath < d && distAlongPath + collDetectRange > old_d)
            {
                cellsAhead.Add(idx);
                old_d = d;
            }
        }
        return cellsAhead;
    }

}

#nullable enable

// namespace System.Collections.Generic {
namespace Utils
{

    // Licensed to the .NET Foundation under one or more agreements.
    // The .NET Foundation licenses this file to you under the MIT license.

    // ported from:
    // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/src/System/Collections/Generic/PriorityQueue.cs

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.CompilerServices;

    internal sealed class PriorityQueueDebugView<TElement, TPriority>
    {
        private readonly PriorityQueue<TElement, TPriority> _queue;
        private readonly bool _sort;

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public (TElement Element, TPriority Priority)[] Items
        {
            get
            {
                List<(TElement Element, TPriority Priority)> list = new(_queue.UnorderedItems);
                if (_sort)
                {
                    list.Sort((i1, i2) => _queue.Comparer.Compare(i1.Priority, i2.Priority));
                }

                return list.ToArray();
            }
        }

        public PriorityQueueDebugView(PriorityQueue<TElement, TPriority> queue)
        {
            ArgumentNullException.ThrowIfNull(queue);

            _queue = queue;
            _sort = true;
        }

        public PriorityQueueDebugView(PriorityQueue<TElement, TPriority>.UnorderedItemsCollection collection) =>
            _queue = collection?._queue ?? throw new System.ArgumentNullException(nameof(collection));
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class SR
    {
        internal const string ArgumentOutOfRange_NeedNonNegNum = "Non-negative number required.";
        internal const string ArgumentOutOfRange_IndexMustBeLessOrEqual = "Index must be less or equal";
        internal const string InvalidOperation_EmptyQueue = "The queue is empty.";
        internal const string InvalidOperation_EnumFailedVersion = "Collection modified while iterating over it.";
        internal const string Arg_NonZeroLowerBound = "Non-zero lower bound required.";
        internal const string Arg_RankMultiDimNotSupported = "Multi-dimensional arrays not supported.";
        internal const string Argument_InvalidArrayType = "Invalid array type.";
        internal const string Argument_InvalidOffLen = "Invalid offset or length.";
    }

    internal static class ArgumentNullException
    {
        public static void ThrowIfNull(object o)
        {
            if (o == null)
            {
                throw new System.ArgumentNullException(); // hard to do it differently without C# 10's features
            }
        }
    }

    internal static class ArrayEx
    {
        internal const int MaxLength = int.MaxValue;
    }

    /// <summary>
    ///     Internal helper functions for working with enumerables.
    /// </summary>
    internal static class EnumerableHelpers
    {
        /// <summary>Converts an enumerable to an array using the same logic as List{T}.</summary>
        /// <param name="source">The enumerable to convert.</param>
        /// <param name="length">The number of items stored in the resulting array, 0-indexed.</param>
        /// <returns>
        ///     The resulting array.  The length of the array may be greater than <paramref name="length" />,
        ///     which is the actual number of elements in the array.
        /// </returns>
        internal static T[] ToArray<T>(IEnumerable<T> source, out int length)
        {
            if (source is ICollection<T> ic)
            {
                int count = ic.Count;
                if (count != 0)
                {
                    // Allocate an array of the desired size, then copy the elements into it. Note that this has the same
                    // issue regarding concurrency as other existing collections like List<T>. If the collection size
                    // concurrently changes between the array allocation and the CopyTo, we could end up either getting an
                    // exception from overrunning the array (if the size went up) or we could end up not filling as many
                    // items as 'count' suggests (if the size went down).  This is only an issue for concurrent collections
                    // that implement ICollection<T>, which as of .NET 4.6 is just ConcurrentDictionary<TKey, TValue>.
                    T[] arr = new T[count];
                    ic.CopyTo(arr, 0);
                    length = count;
                    return arr;
                }
            }
            else
            {
                using (IEnumerator<T> en = source.GetEnumerator())
                {
                    if (en.MoveNext())
                    {
                        const int DefaultCapacity = 4;
                        T[] arr = new T[DefaultCapacity];
                        arr[0] = en.Current;
                        int count = 1;

                        while (en.MoveNext())
                        {
                            if (count == arr.Length)
                            {
                                // This is the same growth logic as in List<T>:
                                // If the array is currently empty, we make it a default size.  Otherwise, we attempt to
                                // double the size of the array.  Doubling will overflow once the size of the array reaches
                                // 2^30, since doubling to 2^31 is 1 larger than Int32.MaxValue.  In that case, we instead
                                // constrain the length to be Array.MaxLength (this overflow check works because of the
                                // cast to uint).
                                int newLength = count << 1;
                                if ((uint)newLength > ArrayEx.MaxLength)
                                {
                                    newLength = ArrayEx.MaxLength <= count ? count + 1 : ArrayEx.MaxLength;
                                }

                                Array.Resize(ref arr, newLength);
                            }

                            arr[count++] = en.Current;
                        }

                        length = count;
                        return arr;
                    }
                }
            }

            length = 0;
            return Array.Empty<T>();
        }
    }

    /// <summary>
    ///     Represents a min priority queue.
    /// </summary>
    /// <typeparam name="TElement">Specifies the type of elements in the queue.</typeparam>
    /// <typeparam name="TPriority">Specifies the type of priority associated with enqueued elements.</typeparam>
    /// <remarks>
    ///     Implements an array-backed quaternary min-heap. Each element is enqueued with an associated priority
    ///     that determines the dequeue order: elements with the lowest priority get dequeued first.
    /// </remarks>
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(PriorityQueueDebugView<,>))]
    public class PriorityQueue<TElement, TPriority>
    {

        /// <summary>
        ///     Specifies the arity of the d-ary heap, which here is quaternary.
        ///     It is assumed that this value is a power of 2.
        /// </summary>
        private const int Arity = 4;

        /// <summary>
        ///     The binary logarithm of <see cref="Arity" />.
        /// </summary>
        private const int Log2Arity = 2;

        /// <summary>
        ///     Represents an implicit heap-ordered complete d-ary tree, stored as an array.
        /// </summary>
        private (TElement Element, TPriority Priority)[] _nodes;

        /// <summary>
        ///     Custom comparer used to order the heap.
        /// </summary>
        private readonly IComparer<TPriority>? _comparer;

        /// <summary>
        ///     Lazily-initialized collection used to expose the contents of the queue.
        /// </summary>
        private UnorderedItemsCollection? _unorderedItems;

        /// <summary>
        ///     The number of nodes in the heap.
        /// </summary>
        private int _size;

        /// <summary>
        ///     Version updated on mutation to help validate enumerators operate on a consistent state.
        /// </summary>
        private int _version;

        /// <summary>
        ///     Gets the number of elements contained in the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        public int Count => _size;

        /// <summary>
        ///     Gets the priority comparer used by the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        public IComparer<TPriority> Comparer => _comparer ?? Comparer<TPriority>.Default;

        /// <summary>
        ///     Gets a collection that enumerates the elements of the queue in an unordered manner.
        /// </summary>
        /// <remarks>
        ///     The enumeration does not order items by priority, since that would require N * log(N) time and N space.
        ///     Items are instead enumerated following the internal array heap layout.
        /// </remarks>
        public UnorderedItemsCollection UnorderedItems => _unorderedItems ??= new(this);

#if DEBUG
        static PriorityQueue()
        {
            Debug.Assert(Log2Arity > 0 && Math.Pow(2, Log2Arity) == Arity);
        }
#endif

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}" /> class.
        /// </summary>
        public PriorityQueue()
        {
            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = InitializeComparer(null);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}" /> class
        ///     with the specified initial capacity.
        /// </summary>
        /// <param name="initialCapacity">Initial capacity to allocate in the underlying heap array.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The specified <paramref name="initialCapacity" /> was negative.
        /// </exception>
        public PriorityQueue(int initialCapacity)
            : this(initialCapacity, comparer: null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}" /> class
        ///     with the specified custom priority comparer.
        /// </summary>
        /// <param name="comparer">
        ///     Custom comparer dictating the ordering of elements.
        ///     Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null" />.
        /// </param>
        public PriorityQueue(IComparer<TPriority>? comparer)
        {
            _nodes = Array.Empty<(TElement, TPriority)>();
            _comparer = InitializeComparer(comparer);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}" /> class
        ///     with the specified initial capacity and custom priority comparer.
        /// </summary>
        /// <param name="initialCapacity">Initial capacity to allocate in the underlying heap array.</param>
        /// <param name="comparer">
        ///     Custom comparer dictating the ordering of elements.
        ///     Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null" />.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The specified <paramref name="initialCapacity" /> was negative.
        /// </exception>
        public PriorityQueue(int initialCapacity, IComparer<TPriority>? comparer)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(initialCapacity), initialCapacity, SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            _nodes = new (TElement, TPriority)[initialCapacity];
            _comparer = InitializeComparer(comparer);
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}" /> class
        ///     that is populated with the specified elements and priorities.
        /// </summary>
        /// <param name="items">The pairs of elements and priorities with which to populate the queue.</param>
        /// <exception cref="ArgumentNullException">
        ///     The specified <paramref name="items" /> argument was <see langword="null" />.
        /// </exception>
        /// <remarks>
        ///     Constructs the heap using a heapify operation,
        ///     which is generally faster than enqueuing individual elements sequentially.
        /// </remarks>
        public PriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items)
            : this(items, comparer: null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="PriorityQueue{TElement, TPriority}" /> class
        ///     that is populated with the specified elements and priorities,
        ///     and with the specified custom priority comparer.
        /// </summary>
        /// <param name="items">The pairs of elements and priorities with which to populate the queue.</param>
        /// <param name="comparer">
        ///     Custom comparer dictating the ordering of elements.
        ///     Uses <see cref="Comparer{T}.Default" /> if the argument is <see langword="null" />.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     The specified <paramref name="items" /> argument was <see langword="null" />.
        /// </exception>
        /// <remarks>
        ///     Constructs the heap using a heapify operation,
        ///     which is generally faster than enqueuing individual elements sequentially.
        /// </remarks>
        public PriorityQueue(IEnumerable<(TElement Element, TPriority Priority)> items, IComparer<TPriority>? comparer)
        {
            ArgumentNullException.ThrowIfNull(items);

            _nodes = EnumerableHelpers.ToArray(items, out _size);
            _comparer = InitializeComparer(comparer);

            if (_size > 1)
            {
                Heapify();
            }
        }

        /// <summary>
        ///     Adds the specified element with associated priority to the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        /// <param name="element">The element to add to the <see cref="PriorityQueue{TElement, TPriority}" />.</param>
        /// <param name="priority">The priority with which to associate the new element.</param>
        public void Enqueue(TElement element, TPriority priority)
        {
            // Virtually add the node at the end of the underlying array.
            // Note that the node being enqueued does not need to be physically placed
            // there at this point, as such an assignment would be redundant.

            int currentSize = _size++;
            _version++;

            if (_nodes.Length == currentSize)
            {
                Grow(currentSize + 1);
            }

            if (_comparer == null)
            {
                MoveUpDefaultComparer((element, priority), currentSize);
            }
            else
            {
                MoveUpCustomComparer((element, priority), currentSize);
            }
        }

        /// <summary>
        ///     Returns the minimal element from the <see cref="PriorityQueue{TElement, TPriority}" /> without removing it.
        /// </summary>
        /// <exception cref="InvalidOperationException">The <see cref="PriorityQueue{TElement, TPriority}" /> is empty.</exception>
        /// <returns>The minimal element of the <see cref="PriorityQueue{TElement, TPriority}" />.</returns>
        public TElement Peek()
        {
            if (_size == 0)
            {
                throw new InvalidOperationException(SR.InvalidOperation_EmptyQueue);
            }

            return _nodes[0].Element;
        }

        /// <summary>
        ///     Removes and returns the minimal element from the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        /// <exception cref="InvalidOperationException">The queue is empty.</exception>
        /// <returns>The minimal element of the <see cref="PriorityQueue{TElement, TPriority}" />.</returns>
        public TElement Dequeue()
        {
            if (_size == 0)
            {
                throw new InvalidOperationException(SR.InvalidOperation_EmptyQueue);
            }

            TElement element = _nodes[0].Element;
            RemoveRootNode();
            return element;
        }

        /// <summary>
        ///     Removes the minimal element from the <see cref="PriorityQueue{TElement, TPriority}" />,
        ///     and copies it to the <paramref name="element" /> parameter,
        ///     and its associated priority to the <paramref name="priority" /> parameter.
        /// </summary>
        /// <param name="element">The removed element.</param>
        /// <param name="priority">The priority associated with the removed element.</param>
        /// <returns>
        ///     <see langword="true" /> if the element is successfully removed;
        ///     <see langword="false" /> if the <see cref="PriorityQueue{TElement, TPriority}" /> is empty.
        /// </returns>
        public bool TryDequeue([MaybeNullWhen(false)] out TElement element,
                                [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_size != 0)
            {
                (element, priority) = _nodes[0];
                RemoveRootNode();
                return true;
            }

            element = default;
            priority = default;
            return false;
        }

        /// <summary>
        ///     Returns a value that indicates whether there is a minimal element in the
        ///     <see cref="PriorityQueue{TElement, TPriority}" />,
        ///     and if one is present, copies it to the <paramref name="element" /> parameter,
        ///     and its associated priority to the <paramref name="priority" /> parameter.
        ///     The element is not removed from the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        /// <param name="element">The minimal element in the queue.</param>
        /// <param name="priority">The priority associated with the minimal element.</param>
        /// <returns>
        ///     <see langword="true" /> if there is a minimal element;
        ///     <see langword="false" /> if the <see cref="PriorityQueue{TElement, TPriority}" /> is empty.
        /// </returns>
        public bool TryPeek([MaybeNullWhen(false)] out TElement element,
                             [MaybeNullWhen(false)] out TPriority priority)
        {
            if (_size != 0)
            {
                (element, priority) = _nodes[0];
                return true;
            }

            element = default;
            priority = default;
            return false;
        }

        /// <summary>
        ///     Adds the specified element with associated priority to the <see cref="PriorityQueue{TElement, TPriority}" />,
        ///     and immediately removes the minimal element, returning the result.
        /// </summary>
        /// <param name="element">The element to add to the <see cref="PriorityQueue{TElement, TPriority}" />.</param>
        /// <param name="priority">The priority with which to associate the new element.</param>
        /// <returns>The minimal element removed after the enqueue operation.</returns>
        /// <remarks>
        ///     Implements an insert-then-extract heap operation that is generally more efficient
        ///     than sequencing Enqueue and Dequeue operations: in the worst case scenario only one
        ///     shift-down operation is required.
        /// </remarks>
        public TElement EnqueueDequeue(TElement element, TPriority priority)
        {
            if (_size != 0)
            {
                (TElement Element, TPriority Priority) root = _nodes[0];

                if (_comparer == null)
                {
                    if (Comparer<TPriority>.Default.Compare(priority, root.Priority) > 0)
                    {
                        MoveDownDefaultComparer((element, priority), 0);
                        _version++;
                        return root.Element;
                    }
                }
                else
                {
                    if (_comparer.Compare(priority, root.Priority) > 0)
                    {
                        MoveDownCustomComparer((element, priority), 0);
                        _version++;
                        return root.Element;
                    }
                }
            }

            return element;
        }

        /// <summary>
        ///     Enqueues a sequence of element/priority pairs to the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        /// <param name="items">The pairs of elements and priorities to add to the queue.</param>
        /// <exception cref="ArgumentNullException">
        ///     The specified <paramref name="items" /> argument was <see langword="null" />.
        /// </exception>
        public void EnqueueRange(IEnumerable<(TElement Element, TPriority Priority)> items)
        {
            ArgumentNullException.ThrowIfNull(items);

            int count = 0;
            ICollection<(TElement Element, TPriority Priority)>? collection =
                items as ICollection<(TElement Element, TPriority Priority)>;
            if (collection is not null && (count = collection.Count) > _nodes.Length - _size)
            {
                Grow(_size + count);
            }

            if (_size == 0)
            {
                // build using Heapify() if the queue is empty.

                if (collection is not null)
                {
                    collection.CopyTo(_nodes, 0);
                    _size = count;
                }
                else
                {
                    int i = 0;
                    (TElement, TPriority)[] nodes = _nodes;
                    foreach ((TElement element, TPriority priority) in items)
                    {
                        if (nodes.Length == i)
                        {
                            Grow(i + 1);
                            nodes = _nodes;
                        }

                        nodes[i++] = (element, priority);
                    }

                    _size = i;
                }

                _version++;

                if (_size > 1)
                {
                    Heapify();
                }
            }
            else
            {
                foreach ((TElement element, TPriority priority) in items)
                {
                    Enqueue(element, priority);
                }
            }
        }

        /// <summary>
        ///     Enqueues a sequence of elements pairs to the <see cref="PriorityQueue{TElement, TPriority}" />,
        ///     all associated with the specified priority.
        /// </summary>
        /// <param name="elements">The elements to add to the queue.</param>
        /// <param name="priority">The priority to associate with the new elements.</param>
        /// <exception cref="ArgumentNullException">
        ///     The specified <paramref name="elements" /> argument was <see langword="null" />.
        /// </exception>
        public void EnqueueRange(IEnumerable<TElement> elements, TPriority priority)
        {
            ArgumentNullException.ThrowIfNull(elements);

            int count;
            if (elements is ICollection<(TElement Element, TPriority Priority)> collection &&
                (count = collection.Count) > _nodes.Length - _size)
            {
                Grow(_size + count);
            }

            if (_size == 0)
            {
                // build using Heapify() if the queue is empty.

                int i = 0;
                (TElement, TPriority)[] nodes = _nodes;
                foreach (TElement element in elements)
                {
                    if (nodes.Length == i)
                    {
                        Grow(i + 1);
                        nodes = _nodes;
                    }

                    nodes[i++] = (element, priority);
                }

                _size = i;
                _version++;

                if (i > 1)
                {
                    Heapify();
                }
            }
            else
            {
                foreach (TElement element in elements)
                {
                    Enqueue(element, priority);
                }
            }
        }

        /// <summary>
        ///     Removes all items from the <see cref="PriorityQueue{TElement, TPriority}" />.
        /// </summary>
        public void Clear()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>())
            {
                // Clear the elements so that the gc can reclaim the references
                Array.Clear(_nodes, 0, _size);
            }

            _size = 0;
            _version++;
        }

        /// <summary>
        ///     Ensures that the <see cref="PriorityQueue{TElement, TPriority}" /> can hold up to
        ///     <paramref name="capacity" /> items without further expansion of its backing storage.
        /// </summary>
        /// <param name="capacity">The minimum capacity to be used.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     The specified <paramref name="capacity" /> is negative.
        /// </exception>
        /// <returns>The current capacity of the <see cref="PriorityQueue{TElement, TPriority}" />.</returns>
        public int EnsureCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, SR.ArgumentOutOfRange_NeedNonNegNum);
            }

            if (_nodes.Length < capacity)
            {
                Grow(capacity);
                _version++;
            }

            return _nodes.Length;
        }

        /// <summary>
        ///     Sets the capacity to the actual number of items in the <see cref="PriorityQueue{TElement, TPriority}" />,
        ///     if that is less than 90 percent of current capacity.
        /// </summary>
        /// <remarks>
        ///     This method can be used to minimize a collection's memory overhead
        ///     if no new elements will be added to the collection.
        /// </remarks>
        public void TrimExcess()
        {
            int threshold = (int)(_nodes.Length * 0.9);
            if (_size < threshold)
            {
                Array.Resize(ref _nodes, _size);
                _version++;
            }
        }

        /// <summary>
        ///     Grows the priority queue to match the specified min capacity.
        /// </summary>
        private void Grow(int minCapacity)
        {
            Debug.Assert(_nodes.Length < minCapacity);

            const int GrowFactor = 2;
            const int MinimumGrow = 4;

            int newcapacity = GrowFactor * _nodes.Length;

            // Allow the queue to grow to maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when _nodes.Length overflowed thanks to the (uint) cast
            if ((uint)newcapacity > ArrayEx.MaxLength)
            {
                newcapacity = ArrayEx.MaxLength;
            }

            // Ensure minimum growth is respected.
            newcapacity = Math.Max(newcapacity, _nodes.Length + MinimumGrow);

            // If the computed capacity is still less than specified, set to the original argument.
            // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
            if (newcapacity < minCapacity)
            {
                newcapacity = minCapacity;
            }

            Array.Resize(ref _nodes, newcapacity);
        }

        /// <summary>
        ///     Removes the node from the root of the heap
        /// </summary>
        private void RemoveRootNode()
        {
            int lastNodeIndex = --_size;
            _version++;

            if (lastNodeIndex > 0)
            {
                (TElement Element, TPriority Priority) lastNode = _nodes[lastNodeIndex];
                if (_comparer == null)
                {
                    MoveDownDefaultComparer(lastNode, 0);
                }
                else
                {
                    MoveDownCustomComparer(lastNode, 0);
                }
            }

            if (RuntimeHelpers.IsReferenceOrContainsReferences<(TElement, TPriority)>())
            {
                _nodes[lastNodeIndex] = default;
            }
        }

        /// <summary>
        ///     Gets the index of an element's parent.
        /// </summary>
        private static int GetParentIndex(int index) => (index - 1) >> Log2Arity;

        /// <summary>
        ///     Gets the index of the first child of an element.
        /// </summary>
        private static int GetFirstChildIndex(int index) => (index << Log2Arity) + 1;

        /// <summary>
        ///     Converts an unordered list into a heap.
        /// </summary>
        private void Heapify()
        {
            // Leaves of the tree are in fact 1-element heaps, for which there
            // is no need to correct them. The heap property needs to be restored
            // only for higher nodes, starting from the first node that has children.
            // It is the parent of the very last element in the array.

            (TElement Element, TPriority Priority)[] nodes = _nodes;
            int lastParentWithChildren = GetParentIndex(_size - 1);

            if (_comparer == null)
            {
                for (int index = lastParentWithChildren; index >= 0; --index)
                {
                    MoveDownDefaultComparer(nodes[index], index);
                }
            }
            else
            {
                for (int index = lastParentWithChildren; index >= 0; --index)
                {
                    MoveDownCustomComparer(nodes[index], index);
                }
            }
        }

        /// <summary>
        ///     Moves a node up in the tree to restore heap order.
        /// </summary>
        private void MoveUpDefaultComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            // Instead of swapping items all the way to the root, we will perform
            // a similar optimization as in the insertion sort.

            Debug.Assert(_comparer is null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            (TElement Element, TPriority Priority)[] nodes = _nodes;

            while (nodeIndex > 0)
            {
                int parentIndex = GetParentIndex(nodeIndex);
                (TElement Element, TPriority Priority) parent = nodes[parentIndex];

                if (Comparer<TPriority>.Default.Compare(node.Priority, parent.Priority) < 0)
                {
                    nodes[nodeIndex] = parent;
                    nodeIndex = parentIndex;
                }
                else
                {
                    break;
                }
            }

            nodes[nodeIndex] = node;
        }

        /// <summary>
        ///     Moves a node up in the tree to restore heap order.
        /// </summary>
        private void MoveUpCustomComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            // Instead of swapping items all the way to the root, we will perform
            // a similar optimization as in the insertion sort.

            Debug.Assert(_comparer is not null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            IComparer<TPriority> comparer = _comparer;
            (TElement Element, TPriority Priority)[] nodes = _nodes;

            while (nodeIndex > 0)
            {
                int parentIndex = GetParentIndex(nodeIndex);
                (TElement Element, TPriority Priority) parent = nodes[parentIndex];

                if (comparer.Compare(node.Priority, parent.Priority) < 0)
                {
                    nodes[nodeIndex] = parent;
                    nodeIndex = parentIndex;
                }
                else
                {
                    break;
                }
            }

            nodes[nodeIndex] = node;
        }

        /// <summary>
        ///     Moves a node down in the tree to restore heap order.
        /// </summary>
        private void MoveDownDefaultComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            // The node to move down will not actually be swapped every time.
            // Rather, values on the affected path will be moved up, thus leaving a free spot
            // for this value to drop in. Similar optimization as in the insertion sort.

            Debug.Assert(_comparer is null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            (TElement Element, TPriority Priority)[] nodes = _nodes;
            int size = _size;

            int i;
            while ((i = GetFirstChildIndex(nodeIndex)) < size)
            {
                // Find the child node with the minimal priority
                (TElement Element, TPriority Priority) minChild = nodes[i];
                int minChildIndex = i;

                int childIndexUpperBound = Math.Min(i + Arity, size);
                while (++i < childIndexUpperBound)
                {
                    (TElement Element, TPriority Priority) nextChild = nodes[i];
                    if (Comparer<TPriority>.Default.Compare(nextChild.Priority, minChild.Priority) < 0)
                    {
                        minChild = nextChild;
                        minChildIndex = i;
                    }
                }

                // Heap property is satisfied; insert node in this location.
                if (Comparer<TPriority>.Default.Compare(node.Priority, minChild.Priority) <= 0)
                {
                    break;
                }

                // Move the minimal child up by one node and
                // continue recursively from its location.
                nodes[nodeIndex] = minChild;
                nodeIndex = minChildIndex;
            }

            nodes[nodeIndex] = node;
        }

        /// <summary>
        ///     Moves a node down in the tree to restore heap order.
        /// </summary>
        private void MoveDownCustomComparer((TElement Element, TPriority Priority) node, int nodeIndex)
        {
            // The node to move down will not actually be swapped every time.
            // Rather, values on the affected path will be moved up, thus leaving a free spot
            // for this value to drop in. Similar optimization as in the insertion sort.

            Debug.Assert(_comparer is not null);
            Debug.Assert(0 <= nodeIndex && nodeIndex < _size);

            IComparer<TPriority> comparer = _comparer;
            (TElement Element, TPriority Priority)[] nodes = _nodes;
            int size = _size;

            int i;
            while ((i = GetFirstChildIndex(nodeIndex)) < size)
            {
                // Find the child node with the minimal priority
                (TElement Element, TPriority Priority) minChild = nodes[i];
                int minChildIndex = i;

                int childIndexUpperBound = Math.Min(i + Arity, size);
                while (++i < childIndexUpperBound)
                {
                    (TElement Element, TPriority Priority) nextChild = nodes[i];
                    if (comparer.Compare(nextChild.Priority, minChild.Priority) < 0)
                    {
                        minChild = nextChild;
                        minChildIndex = i;
                    }
                }

                // Heap property is satisfied; insert node in this location.
                if (comparer.Compare(node.Priority, minChild.Priority) <= 0)
                {
                    break;
                }

                // Move the minimal child up by one node and continue recursively from its location.
                nodes[nodeIndex] = minChild;
                nodeIndex = minChildIndex;
            }

            nodes[nodeIndex] = node;
        }

        /// <summary>
        ///     Initializes the custom comparer to be used internally by the heap.
        /// </summary>
        private static IComparer<TPriority>? InitializeComparer(IComparer<TPriority>? comparer)
        {
            if (typeof(TPriority).IsValueType)
            {
                if (comparer == Comparer<TPriority>.Default)
                {
                    // if the user manually specifies the default comparer,
                    // revert to using the optimized path.
                    return null;
                }

                return comparer;
            }
            else
            {
                // Currently the JIT doesn't optimize direct Comparer<T>.Default.Compare
                // calls for reference types, so we want to cache the comparer instance instead.
                // TODO https://github.com/dotnet/runtime/issues/10050: Update if this changes in the future.
                return comparer ?? Comparer<TPriority>.Default;
            }
        }

        /// <summary>
        ///     Enumerates the contents of a <see cref="PriorityQueue{TElement, TPriority}" />, without any ordering guarantees.
        /// </summary>
        [DebuggerDisplay("Count = {Count}")]
        [DebuggerTypeProxy(typeof(PriorityQueueDebugView<,>))]
        public sealed class UnorderedItemsCollection : IReadOnlyCollection<(TElement Element, TPriority Priority)>,
                                                       ICollection
        {
            internal readonly PriorityQueue<TElement, TPriority> _queue;

            internal UnorderedItemsCollection(PriorityQueue<TElement, TPriority> queue) => _queue = queue;

            /// <summary>
            ///     Returns an enumerator that iterates through the <see cref="UnorderedItems" />.
            /// </summary>
            /// <returns>An <see cref="Enumerator" /> for the <see cref="UnorderedItems" />.</returns>
            public Enumerator GetEnumerator() => new(_queue);

            object ICollection.SyncRoot => this;
            bool ICollection.IsSynchronized => false;

            void ICollection.CopyTo(Array array, int index)
            {
                ArgumentNullException.ThrowIfNull(array);

                if (array.Rank != 1)
                {
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported, nameof(array));
                }

                if (array.GetLowerBound(0) != 0)
                {
                    throw new ArgumentException(SR.Arg_NonZeroLowerBound, nameof(array));
                }

                if (index < 0 || index > array.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), index,
                                                           SR.ArgumentOutOfRange_IndexMustBeLessOrEqual);
                }

                if (array.Length - index < _queue._size)
                {
                    throw new ArgumentException(SR.Argument_InvalidOffLen);
                }

                try
                {
                    Array.Copy(_queue._nodes, 0, array, index, _queue._size);
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(SR.Argument_InvalidArrayType, nameof(array));
                }
            }

            public int Count => _queue._size;

            IEnumerator<(TElement Element, TPriority Priority)> IEnumerable<(TElement Element, TPriority Priority)>.
                GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            /// <summary>
            ///     Enumerates the element and priority pairs of a <see cref="PriorityQueue{TElement, TPriority}" />,
            ///     without any ordering guarantees.
            /// </summary>
            public struct Enumerator : IEnumerator<(TElement Element, TPriority Priority)>
            {
                private readonly PriorityQueue<TElement, TPriority> _queue;
                private readonly int _version;
                private int _index;

                internal Enumerator(PriorityQueue<TElement, TPriority> queue)
                {
                    _queue = queue;
                    _index = 0;
                    _version = queue._version;
                    Current = default;
                }

                /// <summary>
                ///     Releases all resources used by the <see cref="Enumerator" />.
                /// </summary>
                public void Dispose()
                {
                }

                /// <summary>
                ///     Advances the enumerator to the next element of the <see cref="UnorderedItems" />.
                /// </summary>
                /// <returns>
                ///     <see langword="true" /> if the enumerator was successfully advanced to the next element;
                ///     <see langword="false" /> if the enumerator has passed the end of the collection.
                /// </returns>
                public bool MoveNext()
                {
                    PriorityQueue<TElement, TPriority> localQueue = _queue;

                    if (_version == localQueue._version && ((uint)_index < (uint)localQueue._size))
                    {
                        Current = localQueue._nodes[_index];
                        _index++;
                        return true;
                    }

                    return MoveNextRare();
                }

                private bool MoveNextRare()
                {
                    if (_version != _queue._version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }

                    _index = _queue._size + 1;
                    Current = default;
                    return false;
                }

                /// <summary>
                ///     Gets the element at the current position of the enumerator.
                /// </summary>
                public (TElement Element, TPriority Priority) Current { get; private set; }

                object IEnumerator.Current => Current;

                void IEnumerator.Reset()
                {
                    if (_version != _queue._version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }

                    _index = 0;
                    Current = default;
                }
            }
        }
    }

}