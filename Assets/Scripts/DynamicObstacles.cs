using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class DynamicObstacles : MonoBehaviour
{
    public GameObject obstacle;
    public int obs_number = 4;
    public float obs_size = 3;
    public float obs_speed = 5;
    private List<GameObject> obs_list = new List<GameObject>();
    // for linear mvt, store a random velocity
    private List<Vector3> obs_vel = new List<Vector3>();
    // for mvt in octtree, store a target and the list of cell the obstacle will go through to reach it
    private List<CustomNodeScriptable> obs_target = new List<CustomNodeScriptable>();
    private List<List<Vector3>> intermediate_target = new List<List<Vector3>>();

    // for erratic mvt, moves the obstacles toward a position, make them randomly orbit around said position then assign a new position
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
        //AddAsteroids(obs_number);
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
        //MoveErratic();
        //MoveAsteroids();
        if (gameObject.CompareTag("Finished") && gameObject.GetComponent<AstarFast>().done)
        {
            if (obs_list.Count == 0)
                AddObstaclesInOctTree();
            else
                MoveInOctTree();
        }
    }

    public void AddObstaclesInOctTree()
    {
        // add  obs_number obstacles at the center of random valid octtree cells
        OctTreeFast oc = GetComponent<OctTreeFast>();
        int n = oc.data.nodes.Count;
        for (int i = 0; i<obs_number; i++)
        {
            int j = Random.Range(0, n);
            var cn = oc.data.nodes.ElementAt(j).Value;
            if (cn.idx == "start" || cn.idx == "target")
                cn = oc.data.nodes.ElementAt((j+2)%n).Value;
            GameObject obs = GameObject.Instantiate(obstacle);
            obs.transform.localScale = obs_size * Vector3.one;
            obs.transform.position = cn.position;
            obs_list.Add(obs);
            obs_target.Add(cn);
            intermediate_target.Add(new List<Vector3>());
        }
    }

    public void MoveInOctTree()
    {
        AstarFast pf = GetComponent<AstarFast>();
        int n = pf.data.validNodes.Count;
        for (int i = 0; i < obs_list.Count; i++)
        {
            GameObject obs = obs_list[i];
            // move every obstacle towards the center of another random valid octtree cell, store the list of intermediate cells that it needs to traverse
            if (Vector3.Distance(obs.transform.position, obs_target[i].position) < 0.01f)
            {
                // when it has reached its target, randomly select another cell and repeat
                int j = Random.Range(0, n);
                var new_target = pf.data.nodes.ElementAt(j).Value;
                if (new_target.idx == "start" || new_target.idx == "target")
                    new_target = pf.data.nodes.ElementAt((j + 2) % n).Value;
                // if the octtree was updated and the node no longer exists, restart from another point
                if (!pf.data.nodes.ContainsKey(obs_target[i].idx))
                {
                    intermediate_target[i] = new List<Vector3> { new_target.position };
                }
                else
                {
                    // store the path the obstacle will take
                    try
                    {
                        intermediate_target[i] = pf.ComputePath(obs_target[i], new_target);
                    }
                    catch
                    {
                        intermediate_target[i] = new List<Vector3> { new_target.position };
                    }
                }
                obs_target[i] = new_target;
            }
            else
            {
                Vector3 intermediate = intermediate_target[i].First();
                Vector3 pos = obs_list[i].transform.position;
                if (Vector3.Distance(pos, intermediate) < 0.1)
                {
                    obs_list[i].transform.position = intermediate;
                    intermediate_target[i].Remove(intermediate_target[i].First());
                }
                else
                {
                    // move along the computed path stored in intermediate_target
                    obs_list[i].transform.position = pos + obs_speed * Mathf.Min(1, Vector3.Distance(intermediate, pos)) * (intermediate - pos).normalized * Time.deltaTime;
                }
            }
        }

    }
}
