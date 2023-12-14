using UnityEngine;

public class Sim2D : MonoBehaviour
{
    public Vector3 boxSize;
    public int numParticles;
    [SerializeField] GameObject particle;

    //creating the box
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(0, 0, 0), boxSize);
    }

    private void Start()
    {
        for (int i = 0; i <= numParticles; i++)
        {
            Instantiate(particle);
        }
    }
}
