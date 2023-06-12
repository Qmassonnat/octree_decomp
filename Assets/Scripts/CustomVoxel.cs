using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomVoxel: MonoBehaviour
{
    public (int,int,int) idx;
    public Vector3 position;
    public float dist_to_goal;
    public float cost_to_start;
    public CustomVoxel nearest_to_start;
    public bool visited = false;

    public void ResetVoxel(Vector3 target)
    {
        dist_to_goal = Vector3.Distance(target, position);
        cost_to_start = -1;
        visited = false;
        nearest_to_start = null;
    }
}
