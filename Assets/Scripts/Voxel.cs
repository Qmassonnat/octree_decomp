using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.SceneManagement;

public class Voxel : MonoBehaviour
{
    public float minSize;
    public GameObject voxelValid;
    public GameObject voxelInvalid;
    public float bound;
    public float zBound;
    public Dictionary<(int, int, int), List<(int, int, int)>> edges;
    public bool read_from_file;
    [HideInInspector] public Vector3 start;
    [HideInInspector] public Vector3 target;
    private List<CustomVoxel> nodes;
    private bool done;
    // Start is called before the first frame update
    void Start()
    {
        done = false;
        nodes = new List<CustomVoxel>();
        edges = new Dictionary<(int, int, int), List<(int, int, int)>>();
        float t0 = Time.realtimeSinceStartup;
        start = GameObject.Find("Start").transform.position;
        target = GameObject.Find("Target").transform.position;
        Voxelize();
        Debug.Log("Voxelization time " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");
    }

    // Update is called once per frame
    void Update()
    {
        if (!done)
        {
            Debug.Log("Number of valid voxels: " + nodes.Count + " invalid voxels " + ((int)(2 * bound / minSize) * (int)(2 * bound / minSize) * (int)(zBound / minSize) - nodes.Count));
            done = true;
            float t0 = Time.realtimeSinceStartup;
            BuildGraph();
            Debug.Log("Graph building time " + decimal.Round(((decimal)(Time.realtimeSinceStartupAsDouble - t0)) * 1000m, 3) + " ms");

            string folder = Application.dataPath + "/TestPoints/" + SceneManager.GetActiveScene().name;

            // execute A* on all the random positions
            if (read_from_file)
                TestRandomPositions(folder);
            else
                GetComponent<AstarVoxel>(). A_star_path(start, target);

        }
    }
    void Voxelize() {
        for (int i = 0; i < (int) (2*bound/minSize); i++)
        {
            for (int j = 0; j < (int) ((zBound)/ minSize); j++)
            {
                for (int k = 0; k < (int)(2 * bound / minSize); k++)
                {
                    Vector3 corner = new Vector3(-bound + minSize * i, minSize * j, -bound + minSize * k);
                    GameObject vv = Instantiate(voxelValid,
                        Corner2center(corner, minSize),
                        voxelValid.transform.rotation);
                    vv.name = Idx2Name((i, j, k));
                    vv.transform.localScale = new Vector3(minSize, minSize, minSize);
                    vv.transform.parent = gameObject.transform;
                    vv.tag = "Valid";
                    // add node and edges
                    vv.GetComponent<CustomVoxel>().idx = (i, j, k);
                    nodes.Add(vv.GetComponent<CustomVoxel>());
                    edges[(i, j, k)] = new List<(int, int, int)>();
                    if (i >= 1)
                        edges[(i,j,k)].Add((i - 1, j, k));
                    if (i < (int)(2 * bound / minSize) - 1)
                        edges[(i, j, k)].Add((i + 1, j, k));
                    if (j >= 1)
                        edges[(i, j, k)].Add((i, j - 1, k));
                    if (j < (int)(zBound / minSize) - 1)
                        edges[(i, j, k)].Add((i, j + 1, k));
                    if (k >= 1)
                        edges[(i, j, k)].Add((i, j, k - 1));
                    if (k < (int)(2 * bound / minSize) - 1)
                        edges[(i, j, k)].Add((i, j, k + 1));
                }

            }

        }
    }
    
    public void MakeInvalid(GameObject vv)
    {
        GameObject vi = Instantiate(voxelInvalid, vv.transform.position, vv.transform.rotation);
        vi.transform.localScale = vv.transform.lossyScale;
        vi.transform.parent = gameObject.transform;
        vi.name = vv.name;
        vi.tag = "Invalid";
        (int i, int j, int k) = Coord2idx(vv.transform.position);
        vi.GetComponent<CustomVoxel>().idx = (i, j, k);
        // remove edges going to and from that node
        nodes.Remove(vv.GetComponent<CustomVoxel>());
        if (i >= 1)
        {
            edges[(i, j, k)].Remove((i - 1, j, k));
            edges[(i - 1, j, k)].Remove((i, j, k));
        }
        if (i < (int)(2 * bound / minSize) - 1)
        {
            edges[(i, j, k)].Remove((i + 1, j, k));
            edges[(i + 1, j, k)].Remove((i, j, k));

        }
        if (j >= 1)
        {
            edges[(i, j, k)].Remove((i, j - 1, k));
            edges[(i, j - 1, k)].Remove((i, j, k));

        }
        if (j < (int)(zBound / minSize) - 1)
        {
            edges[(i, j, k)].Remove((i, j + 1, k));
            edges[(i, j + 1, k)].Remove((i, j, k));

        }
        if (k >= 1)
        {
            edges[(i, j, k)].Remove((i, j, k - 1));
            edges[(i, j, k - 1)].Remove((i, j, k));
        }
        if (k < (int)(2 * bound / minSize) - 1)
        {
            edges[(i, j, k)].Remove((i, j, k + 1));
            edges[(i, j, k + 1)].Remove((i, j, k));
        }
        Destroy(vv);
    }

    public (int,int,int) Coord2idx(Vector3 position)
    {
        return (
            (int)Mathf.Floor((position.x + bound) / minSize),
            (int)Mathf.Floor(position.y / minSize),
            (int)Mathf.Floor((position.z + bound) / minSize)
            );
    }

    public string Idx2Name ((int, int, int) idx)
    {
        return "_" + idx.Item1.ToString() + "_" + idx.Item2.ToString() + "_" + idx.Item3.ToString();
    }

    Vector3 Corner2center(Vector3 corner, float minSize)
    {
        return corner + minSize * new Vector3(1, 1, 1) / 2;
    }

    public void TestRandomPositions(string folder)
    {
        if (!Directory.Exists(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name))
            Directory.CreateDirectory(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name);
        StreamWriter sw = new StreamWriter(Application.dataPath + "/Results/" + SceneManager.GetActiveScene().name + "/voxel" + minSize + ".txt");

        foreach (string filename in Directory.EnumerateFiles(folder))
        {
            var split_filename = filename.Split(".");
            if (split_filename[split_filename.Length - 1] != "txt")
                continue;
            List<float> nodes_searched = new List<float>();
            List<float> length = new List<float>();
            List<float> time = new List<float>();
            StreamReader sr = new StreamReader(filename);
            string s = sr.ReadLine();
            int i = 0;
            while (s != null && i<100)
            {
                string[] li = s.Split("_");
                Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2]));
                Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5]));
                try
                {
                    var (searched, l, dt) = GetComponent<AstarVoxel>().A_star_path(start, target);
                    nodes_searched.Add(searched);
                    length.Add(l);
                    time.Add(dt);
                    i++;
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
            // very high variance + !gaussian distrib (uniform sampling of points in valid regions) makes STD meaningless --> quantiles
            Debug.Log("Found " + length.Count + " paths");
            Debug.Log("Q1  length " + length[(int)n / 4] + " nodes " + nodes_searched[(int)n / 4] + " time " + decimal.Round(((decimal)(time[(int)n / 4])) * 1000m, 3));
            Debug.Log("median  length " + length[(int)n / 2] + " nodes " + nodes_searched[(int)n / 2] + " time " + decimal.Round(((decimal)(time[(int)n / 2])) * 1000m, 3));
            Debug.Log("Q3  length " + length[(int)3 * n / 4] + " nodes " + nodes_searched[(int)3 * n / 4] + " time " + decimal.Round(((decimal)(time[(int)3 * n / 4])) * 1000m, 3));
            Debug.Log("max  length " + length[n - 1] + " nodes " + nodes_searched[n - 1] + " time " + decimal.Round(((decimal)(time[n - 1])) * 1000m, 3));
            sw.WriteLine(filename + "success rate" + (float)n / 100);
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

    public (float, float) STD(List<float> li)
    {
        float sum = 0;
        foreach (float x in li)
            sum += x;
        float avg = sum / li.Count;
        float sumOfSquares = 0.0f;
        foreach (float x in li)
            sumOfSquares += Mathf.Pow((x - avg), 2.0f);
        return (avg, sumOfSquares / (float)(li.Count - 1));
    }

    public void BuildGraph()
    {
        GameObject empty = new GameObject();
        empty.name = "transitions";
        empty.transform.parent = gameObject.transform;
        AstarVoxel script = gameObject.GetComponent<AstarVoxel>();
        Dictionary < ((int, int, int), (int,int,int)), CustomVoxel> transitions = new Dictionary<((int, int, int), (int, int, int)), CustomVoxel>();
        foreach (CustomVoxel cv in nodes)
        {
            Dictionary<((int, int, int), (int, int, int)), CustomVoxel> new_transitions = new Dictionary<((int, int, int), (int, int, int)), CustomVoxel>();
            var neighbors = edges[cv.idx];
            foreach (var neigh_idx in neighbors)
            {
                CustomVoxel neigh_ = GameObject.Find(Idx2Name(neigh_idx)).GetComponent<CustomVoxel>();
                // if the neighbor is valid
                if (neigh_.CompareTag("Valid"))
                {
                    // add a transition node at the center of the connecting surface
                    GameObject g = new GameObject();
                    g.transform.parent = empty.transform;
                    g.AddComponent<CustomVoxel>();
                    CustomVoxel transition = g.GetComponent<CustomVoxel>();
                    transition.name = cv.name + "&" + neigh_.name;
                    // the transition is at the center of the side of the smallest node
                    transition.position = (cv.transform.position + neigh_.transform.position) / 2;
                    new_transitions[(cv.idx, neigh_.idx)] = transition;
                }
            }
            foreach (var key1 in new_transitions.Keys)
            {
                CustomVoxel t1 = new_transitions[key1];
                // make sure to not add the same transition point twice
                if (!transitions.ContainsKey(key1) && !transitions.ContainsKey((key1.Item2, key1.Item1)))
                {
                    transitions[key1] = t1;
                    script.nodes[t1.name] = t1;
                    script.edges[t1.name] = new List<(string, float)>();
                }
                else
                    t1.name = t1.name.Split('&')[1] + "&" + t1.name.Split('&')[0];
                foreach (var key2 in new_transitions.Keys)
                {
                    if (key1 != key2)
                    {
                    CustomVoxel t2 = new_transitions[key2];
                    script.edges[t1.name].Add((t2.name, Vector3.Distance(t1.position, t2.position)));        
                    }
                }

            }
        }

    }

}
