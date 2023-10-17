using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

public class WarframeMap : MonoBehaviour
{
    public GameObject obstacle;
    public string map_name;
    public bool draw;
    public int i = 10;
    private Vector3 offset;
    Vector3 map_size;
    GameObject Obstacles;
    // Start is called before the first frame update
    void Start()
    {
        Obstacles = new GameObject();
        Obstacles.name = "Obstacles";
        string map_path = Application.dataPath + "/Warframe/" + map_name + ".3dmap";
        CollisionCheck cc = null;
        if (!draw)
            cc = GameObject.Find("PathFinding").GetComponent<CollisionCheck>();
        StreamReader sr = new StreamReader(map_path);
        OctTreeMerged oc = GameObject.Find("PathFinding").GetComponent<OctTreeMerged>();
        string s = sr.ReadLine();
        string[] l = s.Split(" ");
        map_size = new Vector3(float.Parse(l[1]), float.Parse(l[2]), float.Parse(l[3]));
        float max_size = Mathf.Max(map_size.x, Mathf.Max(map_size.y, map_size.z));
        if (max_size > 512) {
            offset = new Vector3(512, 0.5f, 512);
            oc.bound = 512;
            oc.zBound = 1024;
        }
        else if (max_size > 256) {
            offset = new Vector3(256, 0.5f, 256);
            oc.bound = 256;
            oc.zBound = 512;
        }
        else {
            offset = new Vector3(128, 0.5f, 128);
            oc.bound = 128;
            oc.zBound = 256;
        }
        s = sr.ReadLine();
        while (s != null)
        {
            string[] li = s.Split(" ");
            Vector3 obstacle_coord = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2])) - offset;
            if (draw)
            {
                GameObject g = Instantiate(obstacle);
                g.transform.position = obstacle_coord;
                g.transform.parent = Obstacles.transform;
            }
            else
                cc.obstacleList.Add((obstacle_coord, Vector3.one));

            s = sr.ReadLine();
        }
    }

    public void TestScenario()
    {
        string test_path = Application.dataPath + "/Warframe/" + map_name + ".3dmap.3dscen";
        AstarFast pf = GameObject.Find("PathFinding").GetComponent<AstarFast>();
        AstarMerged pfm = GameObject.Find("PathFinding").GetComponent<AstarMerged>();
        if (!Directory.Exists(Application.dataPath + "/Results/Warframe"))
            Directory.CreateDirectory(Application.dataPath + "/Results/Warframe");
        string filename = Application.dataPath + "/Results/Warframe/" + map_name + ".txt";
        StreamWriter sw = new StreamWriter(filename);
        List<double> nodes_searched = new List<double>();
        List<double> length = new List<double>();
        List<double> time = new List<double>();
        StreamReader sr = new StreamReader(test_path);
        // skip the first 2 lines
        string s = sr.ReadLine();
        s = sr.ReadLine();
        s = sr.ReadLine();
        while (i>0 && s != null)
        {
            i--;
            string[] li = s.Split(" ");
            Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2])) - offset;
            Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5])) - offset;
            int searched;
            float l;
            double dt;
            try
            {
                if (pf.isActiveAndEnabled)  
                    (searched, l, dt) = pf.A_star_path(start, target);
                else
                    (searched, l, dt) = pfm.A_star_path(start, target);
                nodes_searched.Add(searched);
                length.Add(l);
                time.Add(dt);
            }
            catch
            {
                Debug.Log("Test failed for " + start + " -> " + target);
            }
            s = sr.ReadLine();
        }
        // remove the first 5 entries as Unity is slow at startup
        for (int j=0; j<5; j++) { 
            nodes_searched.Remove(nodes_searched[0]);
            time.Remove(time[0]);
            length.Remove(length[0]);
        }

        int n = nodes_searched.Count;
        nodes_searched.Sort();
        time.Sort();
        length.Sort();
        double avg_nodes;
        double std_nodes;
        double avg_time;
        double std_time;
        double avg_length;
        double std_length;
        if (pf.isActiveAndEnabled)
        {
            (avg_nodes, std_nodes) = pf.STD(nodes_searched);
            (avg_length, std_length) = pf.STD(length);
            (avg_time, std_time) = pf.STD(time);
        }
        else
        {
            (avg_nodes, std_nodes) = pf.STD(nodes_searched);
            (avg_length, std_length) = pf.STD(length);
            (avg_time, std_time) = pf.STD(time);
        }
        sw.WriteLine(filename + "success rate" + (float)n / 10000);
        sw.WriteLine("avg," + avg_length.ToString() + "," + avg_nodes.ToString() + "," + decimal.Round(((decimal)(avg_time)) * 1000m, 3).ToString());
        sw.WriteLine("std," + std_length.ToString() + "," + std_nodes.ToString() + "," + decimal.Round(((decimal)(std_time)) * 1000m, 3).ToString());
        sw.WriteLine("min," + length[0].ToString() + "," + nodes_searched[0].ToString() + "," + decimal.Round(((decimal)(time[0])) * 1000m, 3).ToString());
        sw.WriteLine("Q1," + length[(int)n / 4].ToString() + "," + nodes_searched[(int)n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 4])) * 1000m, 3).ToString());
        sw.WriteLine("median," + length[(int)n / 2].ToString() + "," + nodes_searched[(int)n / 2].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 2])) * 1000m, 3).ToString());
        sw.WriteLine("Q3," + length[(int)3 * n / 4].ToString() + "," + nodes_searched[(int)3 * n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)3 * n / 4])) * 1000m, 3).ToString());
        sw.WriteLine("max," + length[n - 1].ToString() + "," + nodes_searched[n - 1].ToString() + "," + decimal.Round(((decimal)(time[n - 1])) * 1000m, 3).ToString());
        sw.Close();
    }

    public void TestScenarioBuckets(int n_buckets=10)
    {
        string test_path = Application.dataPath + "/Warframe/" + map_name + ".3dmap.3dscen";
        AstarFast pf = GameObject.Find("PathFinding").GetComponent<AstarFast>();
        AstarMerged pfm = GameObject.Find("PathFinding").GetComponent<AstarMerged>();
        if (!Directory.Exists(Application.dataPath + "/Results/Warframe"))
            Directory.CreateDirectory(Application.dataPath + "/Results/Warframe");
        string filename = Application.dataPath + "/Results/Warframe/" + map_name + ".csv";
        StreamWriter sw = new StreamWriter(filename);
        double global_avg_nodes = 0;
        double global_avg_time = 0;
        double global_avg_length = 0;
        double global_median_nodes = 0;
        double global_median_time = 0;
        double global_median_length = 0;
        var (buckets, buckets_limit) = ComputeLengthBuckets(test_path, n_buckets);
        for (int j=0; j<buckets.Count(); j++)
        {
            var scenarios = buckets[j];
            List<double> nodes_searched = new List<double>();
            List<double> length = new List<double>();
            List<double> time = new List<double>();
            foreach (string s in scenarios)
            {
                string[] li = s.Split(" ");
                Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2])) - offset;
                Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5])) - offset;
                int searched;
                float l;
                double dt;
                try
                {
                    if (pf.isActiveAndEnabled)
                        (searched, l, dt) = pf.A_star_path(start, target);
                    else
                        (searched, l, dt) = pfm.A_star_path(start, target);
                    nodes_searched.Add(searched);
                    length.Add(l);
                    time.Add(dt);
                }
                catch
                {
                    Debug.Log("Test failed for " + start + " -> " + target);
                }
            }
            int n = nodes_searched.Count;
            nodes_searched.Sort();
            time.Sort();
            length.Sort();
            double avg_nodes;
            double std_nodes;
            double avg_time;
            double std_time;
            double avg_length;
            double std_length;
            if (pf.isActiveAndEnabled)
            {
                (avg_nodes, std_nodes) = pf.STD(nodes_searched);
                (avg_length, std_length) = pf.STD(length);
                (avg_time, std_time) = pf.STD(time);
            }
            else
            {
                (avg_nodes, std_nodes) = pfm.STD(nodes_searched);
                (avg_length, std_length) = pfm.STD(length);
                (avg_time, std_time) = pfm.STD(time);
            }
            global_avg_length += avg_length / n_buckets;
            global_avg_nodes += avg_nodes / n_buckets;
            global_avg_time += avg_time / n_buckets;
            if (j == 5)
            {
                global_median_length = length[0];
                global_median_nodes = nodes_searched[0];
                global_median_time = time[0];
            }
            sw.WriteLine("filename,bucket_low,bucket_high,success rate");
            sw.WriteLine(filename + "," + buckets_limit[j] + "," + buckets_limit[j + 1] + "," + (float)n / scenarios.Count);
            sw.WriteLine("quantile,length,nodes_searched,time");
            sw.WriteLine("avg," + avg_length.ToString() + "," + avg_nodes.ToString() + "," + decimal.Round(((decimal)(avg_time)) * 1000m, 3).ToString());
            sw.WriteLine("std," + std_length.ToString() + "," + std_nodes.ToString() + "," + decimal.Round(((decimal)(std_time)) * 1000m, 3).ToString());
            sw.WriteLine("min," + length[0].ToString() + "," + nodes_searched[0].ToString() + "," + decimal.Round(((decimal)(time[0])) * 1000m, 3).ToString());
            sw.WriteLine("Q1," + length[(int)n / 4].ToString() + "," + nodes_searched[(int)n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 4])) * 1000m, 3).ToString());
            sw.WriteLine("median," + length[(int)n / 2].ToString() + "," + nodes_searched[(int)n / 2].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 2])) * 1000m, 3).ToString());
            sw.WriteLine("Q3," + length[(int)3 * n / 4].ToString() + "," + nodes_searched[(int)3 * n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)3 * n / 4])) * 1000m, 3).ToString());
            sw.WriteLine("max," + length[n - 1].ToString() + "," + nodes_searched[n - 1].ToString() + "," + decimal.Round(((decimal)(time[n - 1])) * 1000m, 3).ToString());
            sw.WriteLine();
        }
        sw.WriteLine("global");
        sw.WriteLine("avg," + global_avg_length.ToString() + "," + global_avg_nodes.ToString() + "," + decimal.Round(((decimal)(global_avg_time)) * 1000m, 3).ToString());
        sw.WriteLine("median," + global_median_length.ToString() + "," + global_median_nodes.ToString() + "," + decimal.Round(((decimal)(global_median_time)) * 1000m, 3).ToString());
        sw.Close(); 
    }

    (List<string>[], float[]) ComputeLengthBuckets(string path, int n_buckets = 10)
    {
        // returns the test scenarios in path ordered in buckets of equal size
        List<string>[] buckets = new List<string>[n_buckets];
        StreamReader sr = new StreamReader(path);
        // skip the first 2 lines
        string s = sr.ReadLine();
        s = sr.ReadLine();
        s = sr.ReadLine();
        // compute the n_tiles 
        List<float> length_list = new List<float>();
        while (s!=null)
        {
            string[] li = s.Split(" ");
            float length = float.Parse(li[6]);
            length_list.Add(length);
            s = sr.ReadLine();
        }
        length_list = length_list.OrderBy(x=>x).ToList();
        float[] bucket_limit = new float[n_buckets + 1];
        for (int i = 0; i < n_buckets; i++)
        {
            bucket_limit[i] = length_list[(i*length_list.Count) / n_buckets];
            buckets[i] = new List<string>();
        }
        bucket_limit[0] = 0;
        bucket_limit[n_buckets] = length_list.Last();
        // then add the scenarios in the correct buckets
        sr = new StreamReader(path);
        // skip the first 2 lines
        s = sr.ReadLine();
        s = sr.ReadLine();
        s = sr.ReadLine();
        // compute the n_tiles 
        while (s != null)
        {
            string[] li = s.Split(" ");
            float length = float.Parse(li[6]);
            for (int j=0; j<n_buckets; j++)
            {
                if (length > bucket_limit[j] && length <= bucket_limit[j + 1])
                    buckets[j].Add(s);
            }
            s = sr.ReadLine();
        }
        return (buckets, bucket_limit);
    }
}
