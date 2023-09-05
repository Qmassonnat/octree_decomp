using System.IO;
using UnityEngine;
using System.Collections.Generic;

public class WarframeMap : MonoBehaviour
{
    public GameObject obstacle;
    public string map_name;
    public bool draw;
    // TODO compute that offset automatically
    public Vector3 offset;
    Vector3 map_size;
    GameObject Obstacles;
    // Start is called before the first frame update
    void Start()
    {
        Obstacles = new GameObject();
        Obstacles.name = "Obstacles";
        string map_path = Application.dataPath + "/Warframe/" + map_name + ".txt";
        CollisionCheck cc = null;
        if (!draw)
            cc = GameObject.Find("PathFinding").GetComponent<CollisionCheck>();
        StreamReader sr = new StreamReader(map_path);
        string s = sr.ReadLine();
        string[] l = s.Split(" ");
        map_size = new Vector3(float.Parse(l[1]), float.Parse(l[2]), float.Parse(l[3]));
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
        string test_path = Application.dataPath + "/Warframe/" + map_name + "_paths.txt";
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
        string s = sr.ReadLine();
        int i = 10;
        while (i>0 && s != null)
        {
            i--;
            string[] li = s.Split(" ");
            Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2])) - offset;
            Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5])) - offset;
            int searched;
            float l;
            double dt;
            if (pf.isActiveAndEnabled)  
                (searched, l, dt) = pf.A_star_path(start, target);
            else
                (searched, l, dt) = pfm.A_star_path(start, target);
            nodes_searched.Add(searched);
            length.Add(l);
            time.Add(dt);
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
        sw.WriteLine(filename + "success rate" + (float)n / 1000);
        sw.WriteLine("avg," + avg_length.ToString() + "," + avg_nodes.ToString() + "," + decimal.Round(((decimal)(avg_time)) * 1000m, 3).ToString());
        sw.WriteLine("std," + std_length.ToString() + "," + std_nodes.ToString() + "," + decimal.Round(((decimal)(std_time)) * 1000m, 3).ToString());
        sw.WriteLine("min," + length[0].ToString() + "," + nodes_searched[0].ToString() + "," + decimal.Round(((decimal)(time[0])) * 1000m, 3).ToString());
        sw.WriteLine("Q1," + length[(int)n / 4].ToString() + "," + nodes_searched[(int)n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 4])) * 1000m, 3).ToString());
        sw.WriteLine("median," + length[(int)n / 2].ToString() + "," + nodes_searched[(int)n / 2].ToString() + "," + decimal.Round(((decimal)(time[(int)n / 2])) * 1000m, 3).ToString());
        sw.WriteLine("Q3," + length[(int)3 * n / 4].ToString() + "," + nodes_searched[(int)3 * n / 4].ToString() + "," + decimal.Round(((decimal)(time[(int)3 * n / 4])) * 1000m, 3).ToString());
        sw.WriteLine("max," + length[n - 1].ToString() + "," + nodes_searched[n - 1].ToString() + "," + decimal.Round(((decimal)(time[n - 1])) * 1000m, 3).ToString());
        sw.Close();
    }

}
