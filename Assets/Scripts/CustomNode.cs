using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomNode : MonoBehaviour
{
    public string idx;
    public Dictionary<string, List<string>> valid_neighbors;
    public Dictionary<string, List<string>> invalid_neighbors;
    public float dist_to_goal;
    public float cost_to_start;
    public CustomNode nearest_to_start;
    public bool visited;
    public Vector3 position;
    public Vector3 scale;
    private string[] directions = new string[] { "up", "down", "left", "right", "forward", "backward" };


    public void ResetNode(Vector3 target)
    {
        dist_to_goal = Vector3.Distance(target, position);
        cost_to_start = -1;
        visited = false;
        nearest_to_start = null;
    }

    public void AddValidNeighbors (string key, List<string> new_neighbors)
    {
        foreach (string new_neighbor in new_neighbors)
            valid_neighbors[key].Add(new_neighbor);
    }

    public void AddInvalidNeighbors(string key, List<string> new_neighbors)
    {
        foreach (string new_neighbor in new_neighbors)
            invalid_neighbors[key].Add(new_neighbor);
    }

    public (Dictionary<string, List<string>>, Dictionary<string, List<string>>) ComputeNeighbors(string idx, int i)
    {
        // from the neighbors of a node about to be split, compute the correct neighbors for a child node
        Dictionary<string, List<string>> new_valid_neighbors = new Dictionary<string, List<string>>(valid_neighbors.Count,
                                                            valid_neighbors.Comparer);
        // copy the old dictionnary
        foreach (KeyValuePair<string, List<string>> entry in valid_neighbors)
        {
            new_valid_neighbors.Add(entry.Key, new List<string> (entry.Value));
        }
        // Remove the neighbors no longer connected to the child node
        foreach (string direction in directions)
        {
            if (TestDirection(direction, i))
            {
                new_valid_neighbors[direction] = new List<string> { };
                // to avoid giving a children-sized neighbor to all children nodes
                List<string> to_remove = new List<string>();
                foreach (string neighbor in new_valid_neighbors[GetOppositeDirection(direction)])
                {
                    if (neighbor.Length == idx.Length + 1 && !neighbor.EndsWith((i + Direction2Int(direction)).ToString()))
                        to_remove.Add(neighbor);
                }
                foreach (string neighbor in to_remove) {
                    new_valid_neighbors[GetOppositeDirection(direction)].Remove(neighbor);
                }
            }
        }

        // Add the correct children as new neighbors
        foreach (string direction in directions)
        {
            if (TestDirection(direction, i))
                new_valid_neighbors[direction].Add(idx + (i + Direction2Int(direction)).ToString());
        }

        Dictionary<string, List<string>> new_invalid_neighbors = new Dictionary<string, List<string>>(invalid_neighbors.Count,
                                                            invalid_neighbors.Comparer);
        // copy the old dictionnary
        foreach (KeyValuePair<string, List<string>> entry in invalid_neighbors)
        {
            new_invalid_neighbors.Add(entry.Key, new List<string>(entry.Value));
        }
        // Remove the neighbors no longer connected to the child node
        foreach (string direction in directions)
        {
            if (TestDirection(direction, i))
            {
                new_invalid_neighbors[direction] = new List<string> { };
                // to avoid giving a children-sized neighbor to all children nodes
                List<string> to_remove = new List<string>();
                foreach (string neighbor in new_invalid_neighbors[GetOppositeDirection(direction)])
                {
                    if (neighbor.Length == idx.Length + 1 && !neighbor.EndsWith((i + Direction2Int(direction)).ToString()))
                        to_remove.Add(neighbor);
                }
                foreach (string neighbor in to_remove)
                {
                    new_invalid_neighbors[GetOppositeDirection(direction)].Remove(neighbor);
                }
            }
        }
        return (new_valid_neighbors, new_invalid_neighbors);
    }

    public void UpdateNeighborsOnSplit()
    {
        // when splitting a node, update the adjacent nodes' neighbors to have the relevant smaller nodes
        foreach (KeyValuePair<string, List<string>> entry in valid_neighbors)
        {
            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNode cn = GameObject.Find("_"+neighbor_).GetComponent<CustomNode>();
                // remove the parent entry and add the correct children entries
                List<string> n_list = cn.valid_neighbors[GetOppositeDirection(entry.Key)];
                
                n_list.Remove(idx);

                string other_idx = cn.idx;
                // if the node is larger or has not been split yet
                if (other_idx.Length <= idx.Length)
                {
                    cn.AddValidNeighbors(GetOppositeDirection(entry.Key), GetNeighborsSplit(idx, entry.Key));
                }
                // if the node has already been split
                else
                {

                    int last = int.Parse(other_idx[other_idx.Length - 1].ToString());

                    cn.AddValidNeighbors(GetOppositeDirection(entry.Key), new List<string> { idx + (last + Direction2Int(entry.Key)).ToString() });
                }
            }
        }

        foreach (KeyValuePair<string, List<string>> entry in invalid_neighbors)
        {

            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNode cn = GameObject.Find("_" + neighbor_).GetComponent<CustomNode>();
                // remove the parent entry and add the correct children entries
                List<string> n_list = cn.invalid_neighbors[GetOppositeDirection(entry.Key)];

                n_list.Remove(idx);

                string other_idx = cn.idx;
                // if the node is larger or has not been split yet
                if (other_idx.Length <= idx.Length)
                {
                    cn.AddInvalidNeighbors(GetOppositeDirection(entry.Key), GetNeighborsSplit(idx, entry.Key));
                }
                // if the node has already been split
                else
                {

                    int last = int.Parse(other_idx[other_idx.Length - 1].ToString());

                    cn.AddInvalidNeighbors(GetOppositeDirection(entry.Key), new List<string> { idx + (last + Direction2Int(entry.Key)).ToString() });
                }
            }
        }
    }

    public void UpdateNeighborsOnInvalid()
    {
        // udpate the neighbors when a node becomes invalid
        foreach (KeyValuePair<string, List<string>> entry in valid_neighbors)
        {

            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNode cn = GameObject.Find("_" + neighbor_).GetComponent<CustomNode>();
                // remove the node from valid neighbors and it to invalid neighbors
                cn.valid_neighbors[GetOppositeDirection(entry.Key)].Remove(idx);
                cn.invalid_neighbors[GetOppositeDirection(entry.Key)].Add(idx);
            }
        }
        foreach (KeyValuePair<string, List<string>> entry in invalid_neighbors)
        {

            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNode cn = GameObject.Find("_" + neighbor_).GetComponent<CustomNode>();
                // remove the node from valid neighbors and it to invalid neighbors
                cn.valid_neighbors[GetOppositeDirection(entry.Key)].Remove(idx);
                cn.invalid_neighbors[GetOppositeDirection(entry.Key)].Add(idx);
            }
        }

    }

    public void UpdateNeighborsOnValid()
    {
        // udpate the neighbors when a node becomes valid
        foreach (KeyValuePair<string, List<string>> entry in valid_neighbors)
        {

            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNode cn = GameObject.Find("_" + neighbor_).GetComponent<CustomNode>();
                // remove the node from valid neighbors and add it to invalid neighbors
                cn.invalid_neighbors[GetOppositeDirection(entry.Key)].Remove(idx);
                cn.valid_neighbors[GetOppositeDirection(entry.Key)].Add(idx);
            }
        }
        foreach (KeyValuePair<string, List<string>> entry in invalid_neighbors)
        {

            // for each neighbor, update their list with the correct children nodes
            foreach (string neighbor_ in entry.Value)
            {
                // get the CustomNode associated with that neighbor
                CustomNode cn = GameObject.Find("_" + neighbor_).GetComponent<CustomNode>();
                // remove the node from valid neighbors and add it to invalid neighbors
                cn.invalid_neighbors[GetOppositeDirection(entry.Key)].Remove(idx);
                cn.valid_neighbors[GetOppositeDirection(entry.Key)].Add(idx);
            }
        }
    }

    public void UpdateNeighborsOnMerge(GameObject parent)
    {
        // compute the neighbors of the parent from the children's neighbors
        CustomNode cn_parent = parent.GetComponent<CustomNode>();
        // discard the old neighbor information of the parent
        foreach (string direction in directions)
        {
            cn_parent.valid_neighbors[direction] = new List<string>();
            cn_parent.invalid_neighbors[direction] = new List<string>();
        }
        foreach (Transform t in parent.transform)
        {
            CustomNode cn = t.gameObject.GetComponent<CustomNode>();
            int i = int.Parse(t.name[t.name.Length - 1].ToString());
            foreach (var direction in directions)
            {
                // only add the non-children neighbors
                if (!TestDirection(direction, i)) {
                    foreach (var neighbor in cn.valid_neighbors[direction])
                    {
                        // make sure the neighbor has not already been added
                        if (!cn_parent.valid_neighbors[direction].Contains(neighbor))
                            cn_parent.valid_neighbors[direction].Add(neighbor);
                        // remove the child from the adjacent node's neighbor list
                        CustomNode cn_neigh = GameObject.Find("_" + neighbor).GetComponent<CustomNode>();
                        cn_neigh.valid_neighbors[GetOppositeDirection(direction)].Remove(cn.idx);
                    }
                    foreach (var neighbor in cn.invalid_neighbors[direction])
                    {
                        if (!cn_parent.invalid_neighbors[direction].Contains(neighbor))
                            cn_parent.invalid_neighbors[direction].Add(neighbor);
                        // remove the child from the adjacent node's neighbor list
                        CustomNode cn_neigh = GameObject.Find("_" + neighbor).GetComponent<CustomNode>();
                        cn_neigh.valid_neighbors[GetOppositeDirection(direction)].Remove(cn.idx);
                    }
                }
            }
        }
        // update the adjacent's neighbors to reference the parent and not the children
        foreach (string direction in directions)
        {
            foreach (var neighbor in cn_parent.valid_neighbors[direction])
            {
                // add the parent to the adjacent node's neighbor list
                CustomNode cn_neigh = GameObject.Find("_" + neighbor).GetComponent<CustomNode>();
                cn_neigh.valid_neighbors[GetOppositeDirection(direction)].Add(cn_parent.idx);
            }
        }
        foreach (string direction in directions)
        {
            foreach (var neighbor in cn_parent.invalid_neighbors[direction])
            {
                // add the parent to the adjacent node's neighbor list
                CustomNode cn_neigh = GameObject.Find("_" + neighbor).GetComponent<CustomNode>();
                cn_neigh.valid_neighbors[GetOppositeDirection(direction)].Add(cn_parent.idx);
            }
        }
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

    public List<string> GetNeighborsSplit(string idx, string direction)
    {
        List<string> res = new List<string> {};
        if (direction.Equals("up"))
            res = new List<string> { idx + "4", idx + "5", idx + "6", idx + "7" };
        if (direction.Equals("down"))
            res = new List<string> { idx + "0", idx + "1", idx + "2", idx + "3" };
        if (direction.Equals("left"))
            res = new List<string> { idx + "0", idx + "2", idx + "4", idx + "6" };
        if (direction.Equals("right"))
            res = new List<string> { idx + "1", idx + "3", idx + "5", idx + "7" };
        if (direction.Equals("forward"))
            res = new List<string> { idx + "2", idx + "3", idx + "6", idx + "7" };
        if (direction.Equals("backward"))
            res = new List<string> { idx + "0", idx + "1", idx + "4", idx + "5" };
        return res;
    }

    public int Direction2Int(string direction)
    {

        if (direction.Equals("up"))
            return 4;
        if (direction.Equals("down"))
            return -4;
        if (direction.Equals("left"))
            return -1;
        if (direction.Equals("right"))
            return 1;
        if (direction.Equals("forward"))
            return 2;
        if (direction.Equals("backward"))
            return -2;
        return 0;
    }

    public bool TestDirection(string direction, int i)
    {
        if (direction.Equals("up"))
            return i<4;
        if (direction.Equals("down"))
            return i>=4;
        if (direction.Equals("left"))
            return i%2==1;
        if (direction.Equals("right"))
            return i%2==0;
        if (direction.Equals("forward"))
            return i%4<2;
        if (direction.Equals("backward"))
            return i%4>=2;
        return false;
    }
}
