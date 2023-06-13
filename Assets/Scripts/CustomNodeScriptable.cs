using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomNodeScriptable : ScriptableObject
{
    public string idx;
    public Dictionary<string, List<string>> valid_neighbors = new Dictionary<string, List<string>>();
    public Dictionary<string, List<string>> invalid_neighbors = new Dictionary<string, List<string>>();
    [HideInInspector] public List<string> valid_neigh_up = new List<string>();
    [HideInInspector] public List<string> valid_neigh_down = new List<string>();
    [HideInInspector] public List<string> valid_neigh_left = new List<string>();
    [HideInInspector] public List<string> valid_neigh_right = new List<string>();
    [HideInInspector] public List<string> valid_neigh_back = new List<string>();
    [HideInInspector] public List<string> valid_neigh_forth = new List<string>();
    [HideInInspector] public List<string> invalid_neigh_up = new List<string>();
    [HideInInspector] public List<string> invalid_neigh_down = new List<string>();
    [HideInInspector] public List<string> invalid_neigh_left = new List<string>();
    [HideInInspector] public List<string> invalid_neigh_right = new List<string>();
    [HideInInspector] public List<string> invalid_neigh_back = new List<string>();
    [HideInInspector] public List<string> invalid_neigh_forth = new List<string>();
    public float dist_to_goal;
    public float cost_to_start;
    public CustomNodeScriptable nearest_to_start;
    public bool visited;
    public Vector3 position;
    public Vector3 scale;
    public string tag;
    public List<(string, float)> edges = new List<(string, float)>();
    [HideInInspector] public List<string> edge_idx = new List<string>();
    [HideInInspector] public List<float> edge_dist = new List<float>();
    private string[] directions = new string[] { "up", "down", "left", "right", "forward", "backward" };


    public void ResetNode(Vector3 target)
    {
        dist_to_goal = Vector3.Distance(target, position);
        cost_to_start = -1;
        visited = false;
        nearest_to_start = null;
    }

   

    public string GetOppositeDirection(string direction)
    {
        string res = "";
        if (direction.Equals("up"))
            res = "down";
        if (direction.Equals("down"))
            res = "up";
        if (direction.Equals("left"))
            res = "right";
        if (direction.Equals("right"))
            res = "left";
        if (direction.Equals("forward"))
            res = "backward";
        if (direction.Equals("backward"))
            res = "forward";
        return res;
    }




    public void SaveNeighbors()
    {
        valid_neigh_up = valid_neighbors["up"];
        valid_neigh_down = valid_neighbors["down"];
        valid_neigh_left = valid_neighbors["left"];
        valid_neigh_right = valid_neighbors["right"];
        valid_neigh_back = valid_neighbors["backward"];
        valid_neigh_forth = valid_neighbors["forward"];
        invalid_neigh_up = invalid_neighbors["up"];
        invalid_neigh_down = invalid_neighbors["down"];
        invalid_neigh_left = invalid_neighbors["left"];
        invalid_neigh_right = invalid_neighbors["right"];
        invalid_neigh_back = invalid_neighbors["backward"];
        invalid_neigh_forth = invalid_neighbors["forward"];
    }

    public void LoadNeighbors()
    {
        valid_neighbors["up"] = valid_neigh_up;
        valid_neighbors["down"] = valid_neigh_down;
        valid_neighbors["left"] = valid_neigh_left;
        valid_neighbors["right"] = valid_neigh_right;
        valid_neighbors["backward"] = valid_neigh_back;
        valid_neighbors["forward"] = valid_neigh_forth;
        invalid_neighbors["up"] = invalid_neigh_up;
        invalid_neighbors["down"] = invalid_neigh_down;
        invalid_neighbors["left"] = invalid_neigh_left;
        invalid_neighbors["right"] = invalid_neigh_right;
        invalid_neighbors["backward"] = invalid_neigh_back;
        invalid_neighbors["forward"] = invalid_neigh_forth;
    }

    public void SaveEdges()
    {
        edge_dist = new List<float>();
        edge_idx = new List<string>();
        foreach (var (idx, dist) in edges)
        {
            edge_idx.Add(idx);
            edge_dist.Add(dist);
        }
    }

    public void LoadEdges()
    {
        edges = new List<(string, float)>();
        for (int i = 0; i < edge_idx.Count; i++)
            edges.Add((edge_idx[i], edge_dist[i]));
    }
}
