using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System;
using UnityEngine.SceneManagement;

public class AstarFast : MonoBehaviour
{
    [HideInInspector] public Vector3 start;
    [HideInInspector] public Vector3 target;
    public bool read_from_file;
    public bool draw;
    public bool prune;
    public bool funnel;
    [HideInInspector] public NodeData data;
    private Dictionary<string, List<(string, float)>> temp_edges;
    private CustomNodeScriptable startNode;
    private CustomNodeScriptable targetNode;
    private string start_idx;
    private string target_idx;
    private bool done;
    private int nb_visited;
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
            else
                A_star_path(start, target);
            string path = SceneManager.GetActiveScene().name + gameObject.GetComponent<OctTreeFast>().minSize;
            if (!GetComponent<OctTreeFast>().load || !AssetDatabase.IsValidFolder("Assets/Data/"+path))
            {
                Debug.Log("Saving data to file Assets/Data/" + path+"...");
                double t0 = Time.realtimeSinceStartupAsDouble;
                data.SaveData(path);
                Debug.Log("Saved data to file Assets/Data/" + path + " in " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
            }
        }   
    }

    void InsertTempNode(CustomNodeScriptable temp_node)
    {
        data.nodes[temp_node.idx] = temp_node;
        temp_node.edges = new List<(string, float)>();
        OctTreeFast octTreeGenerator = gameObject.GetComponent<OctTreeFast>();
        Vector3 position = new Vector3(0, octTreeGenerator.zBound / 2, 0);
        Vector3 scale = new Vector3(octTreeGenerator.bound, octTreeGenerator.zBound/2, octTreeGenerator.bound);
        string idx = "_0";
        string merged_idx = "";
        int n = 100;
        while (n>0 && !data.validNodes.ContainsKey(merged_idx))
        {
            n--;
            int i = 1 * (temp_node.position.x > position.x ? 1 : 0) + 2 * (temp_node.position.z > position.z ? 1 : 0) + 4 * (temp_node.position.y > position.y ? 1 : 0);
            // Find in which children the argument position is
            idx = idx + i.ToString();
            merged_idx = gameObject.GetComponent<OctTreeFast>().FindMergedNode(idx);
            scale /= 2;
            // update the position of the candidate node
            if (temp_node.position.x > position.x)
                position.x += scale.x;
            else
                position.x -= scale.x;
            if (temp_node.position.y > position.y)
                position.y += scale.y;
            else
                position.y -= scale.y;
            if (temp_node.position.z > position.z)
                position.z += scale.z;
            else
                position.z -= scale.z;
        }
        if (n == 0) 
            Debug.LogError("Error in InsertTempNode");
        if (temp_node.idx == "start")
            start_idx = idx;
        if (temp_node.idx == "target")
            target_idx = idx;

        // add edges between the temp node and neighbor transitions
        CustomNodeScriptable cn = data.FindNode(merged_idx);
        foreach (string key in cn.valid_neighbors.Keys)
        {
            foreach (string neigh_idx in cn.valid_neighbors[key])
            {
                string trans_idx = "";
                if (data.nodes.ContainsKey("_" + cn.idx + "&" +"_"+ neigh_idx))
                    trans_idx = "_" + cn.idx + "&" + "_" + neigh_idx;
                else if (data.nodes.ContainsKey("_" + neigh_idx + "&" + "_" + cn.idx))
                    trans_idx = "_" + neigh_idx + "&" + "_" + cn.idx;
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

    (int, float, double) A_star_path(Vector3 start, Vector3 target)
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
        float path_length=0;
        if (start_idx != target_idx)
        {
            foreach (var node in data.nodes.Values)
                node.ResetNode(target);
            AstarSearch();

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
            foreach (CustomNodeScriptable n in shortestPath)
                path_positions.Add(n.position);
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

            path_length = PathLength(path_positions);
        }
        else
        {
            shortestPath = new List<CustomNodeScriptable> { startNode, targetNode };
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
        return (nb_visited, path_length, dt);
    }


    private void AstarSearch()
    {
        startNode.cost_to_start = 0;
        var prioQueue = new List<CustomNodeScriptable>();
        prioQueue.Add(startNode);
        do
        {
            prioQueue = prioQueue.OrderBy(Heuristic).ToList();
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
                        prioQueue.Add(childNode);
                }
            }
            node.visited = true;
            nb_visited += 1;
            if (node.name == targetNode.name)
                return;
        } while (prioQueue.Any());
    }

    public void DrawPath(List<Vector3> path, Color color)
    {
        for (int i = 0; i<path.Count - 1; i++)
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
        for (int i = path.Count -2; i>=0; i--)
        {
            old_dist += Vector3.Distance(path[i], path[i + 1]);
            Vector3 pos = path[i];
            bool inter = Physics.Raycast(last, pos - last, Vector3.Distance(pos, last));
            if (inter)
            {
                new_path.Add(path[i+1]);
                new_dist += Vector3.Distance(path[i+1], last);
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

    public float PathLength(List<Vector3> path)
    {
        float path_length = 0;
        for (int i=1; i<path.Count; i++)
            path_length += Vector3.Distance(path[i], path[i-1]);
        return path_length;
    }

    public void CreateRandomPositions(int n, string folder)
    {
        float t = Time.realtimeSinceStartup;
        // preparing 6 buckets of size bound/2
        var octTree = GetComponent<OctTreeFast>();
        List<int> count = new List<int> { 0, 0, 0, 0, 0, 0 };
        StreamWriter[] sw = new StreamWriter[6];
        for (int idx = 0; idx<count.Count; idx++)
            sw[idx] = new StreamWriter(folder + "/" + idx * octTree.bound / 2 + "-" + (idx + 1) * octTree.bound / 2 + ".txt");

        while (count.Any( i => i<n))
        {
            Vector3 start = new Vector3(UnityEngine.Random.Range(-octTree.bound, octTree.bound), UnityEngine.Random.Range(0, octTree.zBound), UnityEngine.Random.Range(-octTree.bound, octTree.bound));
            Vector3 target = new Vector3(UnityEngine.Random.Range(-octTree.bound, octTree.bound), UnityEngine.Random.Range(0, octTree.zBound), UnityEngine.Random.Range(-octTree.bound, octTree.bound));

            try
            {
                var (nb_visited, length, dt) = A_star_path(start, target);
                int idx = (int) Mathf.Floor(2*length / octTree.bound);
                if (idx >= count.Count)
                    Debug.Log("path too long: " + length);
                if (count[idx] < n) {
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
        Debug.Log("Found "+n+"pair of points for each bucket in "+ decimal.Round(((decimal)(Time.realtimeSinceStartup - t)) * 1000m, 3) + " ms");
    }

    public void TestRandomPositions(string folder)
    {
        if (!Directory.Exists(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name))
            Directory.CreateDirectory(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name);
        StreamWriter sw = new StreamWriter(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name + "/octTree_m" + GetComponent<OctTreeFast>().elongated_criteria + "_p" + prune + "f_" + funnel + ".txt");
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
            sw.WriteLine(filename + "success rate" + (float)n/1000);
            sw.WriteLine("avg," + avg_length.ToString() + "," + avg_nodes.ToString() + "," + decimal.Round(((decimal)(avg_time)) * 1000m, 3).ToString());
            sw.WriteLine("std," + std_length.ToString() + "," + std_nodes.ToString() + "," + decimal.Round(((decimal)(std_time)) * 1000m, 3).ToString());
            sw.WriteLine("min," + length[0].ToString() + "," + nodes_searched[0].ToString() + "," + decimal.Round(((decimal)(time[0])) * 1000m, 3).ToString());
            sw.WriteLine("Q1,"+length[(int)n / 4].ToString() + "," + nodes_searched[(int)n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 4])) * 1000m, 3).ToString());
            sw.WriteLine("median,"+length[(int)n / 2].ToString() + "," + nodes_searched[(int)n / 2].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 2])) * 1000m, 3).ToString());
            sw.WriteLine("Q3,"+length[(int)3 * n / 4].ToString() + "," + nodes_searched[(int)3 * n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)3 * n / 4])) * 1000m, 3).ToString());
            sw.WriteLine("max,"+length[n - 1].ToString() + "," + nodes_searched[n - 1].ToString() + "," + decimal.Round(((decimal)(time[n - 1])) * 1000m, 3).ToString());
        }
        sw.Close();
    }

    public (double, double) STD(List<double> li)
    {
        double sum = 0;
        foreach (double x in li)
            sum += x;
        double avg = sum/li.Count;
        double sumOfSquares = 0.0;
        foreach (double x in li)
            sumOfSquares += Math.Pow((x - avg), 2.0);
        return (avg, sumOfSquares / (double)(li.Count - 1));
    }

}
