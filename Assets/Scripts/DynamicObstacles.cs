using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DynamicObstacles : MonoBehaviour
{
    public GameObject obstacle;
    public int obs_number = 4;
    public float obs_size = 3;
    public float obs_speed = 5;
    private List<GameObject> obs_list = new List<GameObject>();
    private List<Vector3> obs_vel = new List<Vector3>();
    // moves the obstacles toward a position, make them randomly orbit around said position then assign a new position
    private List<(Vector3, float)> erratic_mvt = new List<(Vector3, float)>();
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
            Vector3 pos = new Vector3(Random.Range(-bound, bound), Random.Range(0, zBound), Random.Range(-bound, bound));
            Vector3 vel = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized;
            GameObject obs = GameObject.Instantiate(obstacle);
            obs.transform.localScale = obs_size * Vector3.one;
            obs.transform.position = pos;
            obs_list.Add(obs);
            obs_vel.Add(vel);
            erratic_mvt.Add((obs.transform.position, Time.realtimeSinceStartup + Random.Range(3f,5f)));
        }
    }

    public void MoveAsteroids()
    {
        for (int i = 0; i<obs_list.Count; i++)
        {
            
            //Vector3 new_pos = new Vector3(UnityEngine.Random.Range(-bound, bound), UnityEngine.Random.Range(0, zBound), UnityEngine.Random.Range(-bound, bound));
            Vector3 new_pos = obs_list[i].transform.position + obs_speed*(obs_vel[i] * Time.deltaTime);
            // make the obstacles warp around the level at (bound+obs_size/2)
            new_pos.x = (new_pos.x + 3*(bound + obs_size/2)) % (2*bound + obs_size) - (bound + obs_size/2);
            new_pos.y = (new_pos.y + +zBound + obs_size + obs_size / 2) % (zBound + obs_size) - obs_size/2;
            new_pos.z = (new_pos.z + 3*(bound + obs_size/2)) % (2*bound + obs_size) - (bound + obs_size/2);
            obs_list[i].transform.position = new_pos;
        }
    }

    public void MoveErratic()
    {
        for (int i = 0; i<obs_list.Count; i++)
        {
            GameObject obs = obs_list[i];
            Vector3 pos = obs.transform.position;
            var (target, t) = erratic_mvt[i];
            if (t == 0)
            {
                // if the obstacle has reached the target position make it orbit around it for 5-10 second
                if (Vector3.Distance(pos, target) < 0.1f)
                {
                    erratic_mvt[i] = (target, Time.realtimeSinceStartup + Random.Range(3f, 5f));
                }
                // if not move the obstacle towards target
                else
                {
                    obs.transform.position = pos + obs_speed * (obs_vel[i] * Time.deltaTime);
                }
            }
            else if (Time.realtimeSinceStartup < t)
            {
                if (Vector3.Distance(pos, target) < 0.1f)
                {
                    obs_vel[i] = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f)).normalized;
                }
                Vector3 vel = obs_vel[i] * ((t - Time.realtimeSinceStartup) % 2 - 1);
                obs_list[i].transform.position = obs_list[i].transform.position + obs_speed * (vel * Time.deltaTime);
            }
            else if (Time.realtimeSinceStartup >= t)
            {
                // assign a new random target to the obstacle
                erratic_mvt[i] = (new Vector3(Random.Range(-bound, bound), Random.Range(0, zBound), Random.Range(-bound, bound)), 0);
                obs_vel[i] = (erratic_mvt[i].Item1 - pos).normalized;
            }
            

        }
    }

    // Update is called once per frame
    void Update()
    {
        MoveErratic();
        //MoveAsteroids();
    }
}
