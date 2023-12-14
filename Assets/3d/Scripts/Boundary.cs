using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boundary : MonoBehaviour
{
    public Vector3 boxSize;
    [HideInInspector] public Vector3 boxSpawn;

    //creating the box
    private void OnDrawGizmos()
    {
        boxSpawn = transform.position;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(boxSpawn, boxSize);
    }

}
