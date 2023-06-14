using System.IO;
using UnityEngine;

public class WarframeMap : MonoBehaviour
{
    public GameObject obstacle;
    public string map_name;
    Vector3 min_coord = Vector3.positiveInfinity;
    Vector3 max_coord = Vector3.negativeInfinity;
    Vector3 map_size;
    GameObject Obstacles;
    // Start is called before the first frame update
    void Start()
    {
        Obstacles = new GameObject();
        Obstacles.name = "Obstacles";
        string map_path = Application.dataPath + "/Warframe/" + map_name;
        StreamReader sr = new StreamReader(map_path);
        string s = sr.ReadLine();
        string[] l = s.Split(" ");
        map_size = new Vector3(float.Parse(l[0]), float.Parse(l[1]), float.Parse(l[2]));
        s = sr.ReadLine();
        while (s != null)
        {
            string[] li = s.Split(" ");
            Vector3 obstacle_coord = new Vector3(float.Parse(li[0]), float.Parse(li[1]), float.Parse(li[2]));
            GameObject g = Instantiate(obstacle);
            g.transform.position = obstacle_coord - new Vector3(200,50,300);
            g.transform.parent = Obstacles.transform;
            max_coord = Vector3.Max(max_coord, obstacle_coord);
            min_coord = Vector3.Min(min_coord, obstacle_coord);
            s = sr.ReadLine();
        }
        //Debug.Log(map_size + " " + min_coord + " " + max_coord);
    }

}
