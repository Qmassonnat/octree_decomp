using System.IO;
using UnityEngine;

public class WarframeMap : MonoBehaviour
{
    public GameObject obstacle;
    public string map_name;
    public bool draw;
    Vector3 map_size;
    Vector3 min_coord;
    Vector3 max_coord;
    // TODO compute that offset automatically
    Vector3 offset = new Vector3(200,50,300);
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
        map_size = new Vector3(float.Parse(l[0]), float.Parse(l[1]), float.Parse(l[2]));
        s = sr.ReadLine();
        while (s != null)
        {
            string[] li = s.Split(" ");
            Vector3 obstacle_coord = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2])) - offset;
            max_coord = Vector3.Max(max_coord, obstacle_coord);
            min_coord = Vector3.Min(min_coord, obstacle_coord);
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
       // Debug.Log(map_size + " " + min_coord + " " + max_coord);
    }

    public void TestScenario()
    {
        string test_path = Application.dataPath + "/Warframe/" + map_name + "_paths.txt";
        StreamReader sr = new StreamReader(test_path);
        string s = sr.ReadLine();
        int cont = 10;
        AstarFast pf = GameObject.Find("PathFinding").GetComponent<AstarFast>();
        float min = 1000;
        float max = 0;
        while (s != null && cont>0)
        {
            string[] li = s.Split(" ");
            Vector3 start = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2])) - offset;
            Vector3 target = new Vector3(float.Parse(li[3]), float.Parse(li[4]), float.Parse(li[5])) - offset;
            GameObject.Find("Start").transform.position = start;
            GameObject.Find("Target").transform.position = target;
            try
            {
                var (nb, l, dt) = pf.A_star_path(start, target); 
                float opt_length = float.Parse(li[6]);
                float opt_ratio = float.Parse(li[7]);
                min = Mathf.Min(min, opt_length);
                max = Mathf.Max(max, opt_length);
                Debug.Log("OPT"+opt_length + " " + opt_ratio);
                Debug.Log("Octtree"+nb + " " + l + " " + dt);
            }
            catch
            {
                Debug.Log(start + " " + target);
            }
            cont--;
            s = sr.ReadLine();
        }
        Debug.Log(min + " " + max);

    }

}
