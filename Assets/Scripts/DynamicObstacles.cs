using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicObstacles : MonoBehaviour
{
    public GameObject obstacle;
    public int obs_number = 4;
    public float obs_size = 3;
    private List<GameObject> obs_list = new List<GameObject>();
    private List<Vector3> obs_vel  = new List<Vector3>();
    private float bound;
    private float zBound;

    // Start is called before the first frame update
    void Start()
    {
        GameObject pf = GameObject.Find("PathFinding");
        if (pf.GetComponent<OctTree>().isActiveAndEnabled)
        {
            bound = pf.GetComponent<OctTree>().bound;
            zBound = pf.GetComponent<OctTree>().zBound;
        }
        else if (pf.GetComponent<OctTreeFast>().isActiveAndEnabled)
        {
            bound = pf.GetComponent<OctTreeFast>().bound;
            zBound = pf.GetComponent<OctTreeFast>().zBound;
        }
        else if (pf.GetComponent<Voxel>().isActiveAndEnabled)
        {
            bound = pf.GetComponent<Voxel>().bound;
            bound = pf.GetComponent<Voxel>().zBound;
        }
        else
        {
            bound = 10;
            zBound = 20;
            Debug.LogError("Pathfinding component inactive");
        }
        AddAsteroids(obs_number);
    }

    public void AddAsteroids(int n)
    {
        for (int i=0;i<n;i++)
        {
            Vector3 pos = new Vector3(UnityEngine.Random.Range(-bound, bound), UnityEngine.Random.Range(0, zBound), UnityEngine.Random.Range(-bound, bound));
            Vector3 vel = new Vector3(UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f), UnityEngine.Random.Range(-1.0f, 1.0f)).normalized;
            GameObject obs = GameObject.Instantiate(obstacle);
            obs.transform.localScale = obs_size * Vector3.one;
            obs.transform.position = pos;
            obs_list.Add(obs);
            obs_vel.Add(vel);
        }
    }

    public void MoveObstacles()
    {
        for (int i = 0; i<obs_list.Count; i++)
        {
            
            //Vector3 new_pos = new Vector3(UnityEngine.Random.Range(-bound, bound), UnityEngine.Random.Range(0, zBound), UnityEngine.Random.Range(-bound, bound));
            Vector3 new_pos = obs_list[i].transform.position + 5*(obs_vel[i] * Time.deltaTime);
            // make the obstacles warp around the level at (bound+obs_size/2)
            new_pos.x = (new_pos.x + 3*(bound + obs_size/2)) % (2*bound + obs_size) - (bound + obs_size/2);
            new_pos.y = (new_pos.y + +zBound + obs_size + obs_size / 2) % (zBound + obs_size) - obs_size/2;
            new_pos.z = (new_pos.z + 3*(bound + obs_size/2)) % (2*bound + obs_size) - (bound + obs_size/2);
            obs_list[i].transform.position = new_pos;
        }
    }

    // Update is called once per frame
    void Update()
    {
        MoveObstacles();
    }
}
