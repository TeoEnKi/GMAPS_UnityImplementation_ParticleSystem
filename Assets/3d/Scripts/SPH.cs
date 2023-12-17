using System.Threading.Tasks;
using UnityEngine;

public class SPH : MonoBehaviour
{
    [SerializeField] float gravity = 10;

    ParticleSpawner particleSpawner;
    [SerializeField] int numParticles;

    public float mass;
    public float smoothKerRadius;

    public Particle[] particles;
    //public Vector3[] velocities;
    //public float[] densities;

    //can be replaced by any array of values (e.g. density and pressure)
    public float[] particleProperties;

    //what the density should be throughout the fluid body
    public float targetDensity;
    //how much should particles be pushed depending on density
    public float pressureMultiplier;

    private void Start()
    {
        particleSpawner = GetComponent<ParticleSpawner>();
        numParticles = particleSpawner.numParticles;


    }

    private void Update()
    {
        for (int i = 0; i < numParticles; i++)
        {
            Debug.Log(i + " i ");
            Debug.Log(particles[i].transform.position);
        }
        UpdateDensities();
        SimulationSteps();
    }

    //measure of influence of a particle on the updated particle 
    //measured by the radius of the smoothing kernel - the distance between the 2 particles
    //a very important concept used in most SPH formulas
    float SmoothingKernel(float dist, float radius)
    {
        if (dist >= radius) return 0;

        //based on formula of sphere
        //float volume = 4/ 3 * Mathf.PI * Mathf.Pow(radius, 3);
        //based on double intergral of line graph of smoothing kernel
        float volume = 2 * Mathf.PI * Mathf.Pow(radius, 5) / 5;

        //based on a exponential graph
        //faster increase/decrease in y value
        return Mathf.Pow(radius - dist, 2) / volume;
    }

    //Getting the rate of change at the point of the influence graph
    float SmoothingKernelDerivative(float dist, float radius)
    {
        //constant
        if (dist >= radius) return 0;

        //partially derive the formula used for the smoothing kernel
        float f = radius - dist;
        float scale = -5 / (Mathf.PI * Mathf.Pow(radius, 5));
        return scale * f;
    }

    //getting the density of the fluid at that particle/ point of the fluid body
    float CalculateDensity(int samplePointID)
    {
        float density = 0;

        for (int i = 0; i < numParticles; i++)
        {
            float dist = (particles[i].position - particles[samplePointID].position).magnitude;
            float influence = SmoothingKernel(dist, smoothKerRadius);
            Debug.Log("dist" + dist + "influence" + influence);
            //mass scales that influence
            density += mass * influence;
        }
        return density;
    }

    void UpdateDensities()
    {
        //to run the loops in separate different frames to improve performance
        //reason: many many particles
        Parallel.For(0, numParticles - 1, i =>
        {
            particles[i].density = CalculateDensity(i);
        });
    }

    //get the density gradient at a particle in the fluid to determine the amount of pressure force to act on the particle
    Vector3 CalculatePressureForce(int samplePointID)
    {
        Vector3 propertyGradient = Vector2.zero;

        for (int i = 0; i < numParticles; i++)
        {
            //so that the particles don't cancel themselves
            if (samplePointID == i) continue;

            Vector3 offset = particles[i].transform.position - particles[samplePointID].transform.position;
            float dist = offset.magnitude;
            float slope = SmoothingKernelDerivative(dist, smoothKerRadius);
            //give random direction so that particle moves somewhere away from the other particle that may have the same position as it
            Vector3 dir = dist == 0 ? particles[samplePointID].RandomDir() : offset / dist;
            //Debug.Log("dir" + dir + "dist" + dist);
            float density = particles[i].density;
            propertyGradient += -DensityToPressure(density) * (mass / density) * dir * slope;
        }
        return propertyGradient;
    }

    float DensityToPressure(float density)
    {
        //how far off the actual density is from the target density
        float densityError = density - targetDensity;
        //how much particle should move due to pressure force
        return densityError * pressureMultiplier;
    }

    //interpolation equation which can be used to find the pressure values at the empty spaces where here are no particles
    //no need to use for density as the particleProperty (density) will cancel out the density denominator
    float CalculateProperty(int samplePoint)
    {
        float property = 0;

        for (int i = 0; i < numParticles; i++)
        {
            if (samplePoint == i) continue;
            float dist = (particles[i].transform.position - particles[samplePoint].transform.position).magnitude;
            float influence = SmoothingKernel(dist, smoothKerRadius);
            float density = CalculateDensity(i);
            property += particleProperties[i] * (mass / density) * influence;
        }

        return property;
    }

    //determines how the particles behave based on the given SPH values
    public void SimulationSteps()
    {
        if (numParticles == 0) return;

        //Apply gravity and calculate the density 
        for (int i = 0; i < numParticles; i++)
        {

            particles[i].velocity += Vector3.down * gravity * Time.deltaTime;
            Debug.Log(particles[i].velocity);

            particles[i].density = CalculateDensity(i);
        }

        // Calculate and apply pressure forces
        for (int i = 0; i < numParticles; i++)
        {
            Vector3 pressureForce = CalculatePressureForce(i);
            //moving small parts of the fluid and not the entire fluid moving together at the same acceleration --> density instead of mass
            Vector3 pressureAcceleration = pressureForce / particles[i].density;
            Debug.Log("particles[i].density" + particles[i].density);
            //newton's second law: law of momentum.
            //The acceleration of the body is directly proportional to the net force acting on the body
            //and inversely proportional to the mass of the body. 
            particles[i].velocity += pressureAcceleration * Time.deltaTime;

        }

        //Apply and add velocities to positions values
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].transform.position += particles[i].velocity * Time.deltaTime;
            particles[i].ResolveCollisions();
        }
    }
}
