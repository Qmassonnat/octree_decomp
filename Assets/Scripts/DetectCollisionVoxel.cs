using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectCollisionVoxel : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {

    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            gameObject.tag = "Invalid";
            GameObject voxelizer = GameObject.Find("PathFinding");
            voxelizer.GetComponent<Voxel>().MakeInvalid(gameObject);
        }
    }
}
