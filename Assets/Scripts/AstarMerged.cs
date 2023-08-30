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
                Debug.Log("Saving data to file Assets/Data/" + path + "...");
                double t0 = Time.realtimeSinceStartupAsDouble;
                data.SaveData(path);
                Debug.Log("Saved data to file Assets/Data/" + path + " in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
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
        OctTreeMerged octTreeGenerator = gameObject.GetComponent<OctTreeMerged>();
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
                if ((child.position.x - child.scale.x <= v.x && v.x <= child.position.x + child.scale.x) &&
                    (child.position.y - child.scale.y <= v.y && v.y <= child.position.y + child.scale.y) &&
                    (child.position.z - child.scale.z <= v.z && v.z <= child.position.z + child.scale.z))
                {
                    candidate_nodes.Add(child);
                }
            }
        }
        Debug.Log("Error in Pos2Idx");
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
        startNode.name = "start";
        startNode.idx = "start";
        startNode.position = start;
        InsertTempNode(startNode);
        targetNode.name = "target";
        targetNode.idx = "target";
        targetNode.position = target;
        InsertTempNode(targetNode);
        var shortestPath = new List<CustomNodeScriptable>();
        List<Vector3> path_positions = new List<Vector3>();
        float path_length = 0;
        if (start_idx != target_idx)
        {
            //double t1 = Time.realtimeSinceStartupAsDouble;
            foreach (var node in data.nodes.Values)
                node.ResetNode(target);
            //Debug.Log("reset " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t1)) * 1000m, 3) + " ms");
            double t1 = Time.realtimeSinceStartup;
            AstarSearch();
            Debug.Log("A* " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t1)) * 1000m, 3) + " ms");
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
        Debug.Log("TOTAL " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
        return (nb_visited, path_length, dt);
    }


    private void AstarSearch()
    {
        startNode.cost_to_start = 0;
        var prioQueue = new List<CustomNodeScriptable>();
        prioQueue.Add(startNode);
        do
        {
            // prioQueue can get very long, instead of sorting everything insert the few values we added
            // maybe try with only string in the queue and then data.FindNode?
            var node = prioQueue.First();
            prioQueue.Remove(node);
            foreach (var (idx, cost) in node.edges.OrderBy(x => x.Item2))
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
                    if (!prioQueue.Contains(childNode))
                        InsertInQueue(prioQueue, childNode);
                }
            }
            node.visited = true;
            nb_visited += 1;
            if (node.idx == targetNode.idx)
                return;
        } while (prioQueue.Any());
    }

    public List<Vector3> ComputePath(CustomNodeScriptable s, CustomNodeScriptable t)
    {
        List<Vector3> path = new List<Vector3>();
        if (s != t)
        {
            foreach (var node in data.nodes.Values)
                node.ResetNode(target);
            s.cost_to_start = 0;
            var prioQueue = new List<CustomNodeScriptable>();
            prioQueue.Add(s);
            do
            {
                // prioQueue can get very long, instead of sorting everything insert the few values we added
                // maybe try with only string in the queue and then data.FindNode?
                var node = prioQueue.First();
                prioQueue.Remove(node);
                foreach (var (idx, cost) in node.edges.OrderBy(x => x.Item2))
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
                        if (!prioQueue.Contains(childNode))
                            InsertInQueue(prioQueue, childNode);
                    }
                }
                node.visited = true;
                if (node.idx == t.idx)
                    break;
            } while (prioQueue.Any());
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

    public void InsertInQueue(List<CustomNodeScriptable> queue, CustomNodeScriptable cn)
    {
        int cur = 0;
        float heuristic = Heuristic(cn);
        while (cur < queue.Count && Heuristic(queue[cur]) < heuristic)
            cur++;
        queue.Insert(cur, cn);
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
        old_dist += Vector3.Distance(first, path[1]);
        new_dist += Vector3.Distance(first, last);
        new_path.Reverse();
        //Debug.Log("Before pruning path " + path.Count + " nodes, length " + old_dist + " after pruning " + new_path.Count + " nodes, length " + new_dist);
        return new_path;
    }

    public bool IsVisible(Vector3 x, Vector3 y)
    {
        // returns true iff the straight line between x and y does not intersect an invalid octtree cell
        //bool visible = !Physics.Raycast(x, y - x, Vector3.Distance(y, x));
        bool visible = true;
        float step = gameObject.GetComponent<OctTreeMerged>().minSize / 2;
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
            while (drawPath.transform.childCount > 0)
                DestroyImmediate(drawPath.transform.GetChild(0).gameObject);
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
