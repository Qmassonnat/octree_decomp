using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CollisionCheck : MonoBehaviour
{
    private List<(Vector3, Vector3)> obstacleList;

    // Start is called before the first frame update
    void Start()
    {
        UpdateObstacles();
    }

    // Update is called once per frame
    void Update()
    {
        // TODO: update obstacles, maybe once every 5 or 10 frames?
    }

    void UpdateObstacles()
    {
        obstacleList = new List<(Vector3, Vector3)>();
        foreach (GameObject g in FindObjectsOfType(typeof (GameObject)))
        {
            if (g.CompareTag("Obstacle"))
            {
                var col = g.GetComponent<BoxCollider>();
                var trans = col.transform;
                var min = col.center - col.size * 0.5f;
                var max = col.center + col.size * 0.5f;
                var P000 = trans.TransformPoint(new Vector3(min.x, min.y, min.z));
                var P111 = trans.TransformPoint(new Vector3(max.x, max.y, max.z));
                Vector3 center = (P000 + P111) / 2;
                Vector3 scale = (P111 - P000);
                obstacleList.Add((center, scale));

            }
        }
    }

    bool CheckCollisions(Vector3 center1, Vector3 scale1, Vector3 center2, Vector3 scale2)
    {
        // returns true iff the two cubes intersect
        Vector3 min1 = center1 - scale1 / 2;
        Vector3 max1 = center1 + scale1 / 2;
        Vector3 min2 = center2 - scale2 / 2;
        Vector3 max2 = center2 + scale2 / 2;
        if (((max1.x > min2.x && max1.x < max2.x) || (min1.x > min2.x && min1.x < max2.x) || (max2.x > min1.x && max2.x < max1.x) || (min2.x > min1.x && min2.x < max1.x)) &&
            ((max1.y > min2.y && max1.y < max2.y) || (min1.y > min2.y && min1.y < max2.y) || (max2.y > min1.y && max2.y < max1.y) || (min2.y > min1.y && min2.y < max1.y)) &&
            ((max1.z > min2.z && max1.z < max2.z) || (min1.z > min2.z && min1.z < max2.z) || (max2.z > min1.z && max2.z < max1.z) || (min2.z > min1.z && min2.z < max1.z)))
            return true;
        return false;
    }

    public bool IsEmpty(Vector3 center, Vector3 scale)
    {
        // returns false iff the octtree cell with center and scale intersect an obstacle
        foreach (var (obs_center, obs_scale) in obstacleList)
        {
            if (CheckCollisions(center, scale, obs_center, obs_scale))
                return false;
        }
        return true;
    }

    public List<CustomNodeScriptable> ComputeIntersectionNode(Vector3 obsCenter, Vector3 obsScale)
    {
        // returns the list of nodes intersection the input obstacle
        return new List<CustomNodeScriptable>();
    }
}
