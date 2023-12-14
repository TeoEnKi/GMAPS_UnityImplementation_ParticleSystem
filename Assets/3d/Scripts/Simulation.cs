using UnityEngine;

public class Simulation : MonoBehaviour
{
    // TODO
    // - Setup the drawing lines for the cube ends
    // - spawn particles
    [SerializeField] GameObject particleGO;
    [SerializeField] int numParticles;

    private void Start()
    {
        for (int i = 0; i < numParticles; i++)
        {
            Instantiate(particleGO);
        }
    }
}
