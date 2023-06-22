using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectCollision : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            gameObject.tag = "Invalid";
            GameObject octTreeGenerator = GameObject.Find("PathFinding");
            if (octTreeGenerator.GetComponent<OctTree>().enabled)
                octTreeGenerator.GetComponent<OctTree>().SplitNode(gameObject);
            else
                Debug.LogError("Please activate the script OctTree or OctTreeFast");
        }
    }
}
