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
                Vector3 position = (P000 + P111) / 2;
                Vector3 scale = (P111 - P000);
                obstacleList.Add((position, scale));

            }
        }
    }

    bool CheckCollisions(Vector3 center1, Vector3 scale1, Vector3 center2, Vector3 scale2)
    {
        Vector3 min1 = center1 - scale1 / 2;
        Vector3 max1 = center1 + scale1 / 2;
        Vector3 min2 = center2 - scale2 / 2;
        Vector3 max2 = center2 + scale2 / 2;
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.transform.position = min1;
        GameObject s2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s2.transform.position = max1;
        if (((max1.x >= min2.x && min1.x <= max2.x) || (max2.x <= min1.x && min2.x <= max1.x)) &&
            ((max1.y >= min2.y && min1.y <= max2.y) || (max2.y <= min1.y && min2.y <= max1.y)) &&
            ((max1.z >= min2.z && min1.z <= max2.z) || (max2.z <= min1.z && min2.z <= max1.z)))
            return true;
        return false;
    }

    bool CheckCollisions2(Vector3 center, Vector3 scale)
    {
        // returns true iff the octtree cell with center and scale intersect an obstacle
        return true;
    }

    List<CustomNodeScriptable> ComputeIntersection(Vector3 obsCenter, Vector3 obsScale)
    {
        // returns the list of nodes intersection the input obstacle
        return new List<CustomNodeScriptable>();
    }
}
