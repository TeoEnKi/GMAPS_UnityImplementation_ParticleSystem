using UnityEngine;
using UnityEngine.UIElements;

public class Simulation : MonoBehaviour
{
    // TODO
    // - spawn particles
    [SerializeField] GameObject particleGO;
    [SerializeField] int numParticles;
    Boundary boundary;
    [SerializeField] float spacing = 0.2f;
    Particle[] particles;

    SPH sph;
    private void Start()
    {
        particles = new Particle[numParticles];

        sph = GetComponent<SPH>();
        sph.positions = new Vector3[numParticles];
        sph.velocities = new Vector3[numParticles];

        boundary = GetComponent<Boundary>();

        int particlesPerRow = (int) Mathf.Pow(numParticles, 1f / 3f);
        int particlesPerCol = particlesPerRow;
        int numLayers = (int)Mathf.Round(numParticles/ (particlesPerCol * particlesPerRow));
        //get diameter of the particle
        spacing += particleGO.transform.localScale.x;

        //spawning particles
        for (int particleID = 0; particleID < numParticles; particleID++)
        {
            //get particle's row in its layer
            float x = particleID % particlesPerRow * spacing;
            //get the id of the particle of the layer to accurately get the column that the particle is in
            //(e.g. if particle is id 5 and the layer has only 4 balls, the id of that ball in the layer is 1)
            //Divide by the number of particles per row to get its col
            float z = (particleID % (particlesPerCol * particlesPerRow)) / particlesPerRow * spacing;
            //get the particle's number layer
            float y = Mathf.Round(particleID / (particlesPerCol * particlesPerRow)) * spacing ;

            GameObject tempParticle = Instantiate(particleGO);
            tempParticle.transform.position = new Vector3(x, y, z) + (boundary.boxSpawn
                - ((numLayers -2) * spacing)/ 2 * Vector3.up 
                - ((particlesPerRow -1) * spacing)/ 2 * Vector3.right 
                - ((particlesPerCol - 1) * spacing)/ 2 * Vector3.forward);
            particles[particleID] = tempParticle.GetComponent<Particle>();
        }
    }
    private void Update()
    {
        if (particles.Length == 0) return;
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].ResolveCollisions(ref sph.velocities[i], ref sph.positions[i]);
        }

    }
}
