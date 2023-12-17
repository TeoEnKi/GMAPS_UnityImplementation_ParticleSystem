using System;
using UnityEngine;

public class SPH : MonoBehaviour
{
    [SerializeField] float gravity = 10;

    ParticleSpawner particleSpawner;
    [SerializeField] int numParticles;

    public float mass;
    public float smoothKerRadius;

    public Particle[] particles;
    [SerializeField] LayerMask particleLayer;

    //can be replaced by any array of values (e.g. density and pressure)
    public float[] particleProperties;

    //what the density should be throughout the fluid body
    public float targetDensity;
    //how much should particles be pushed depending on density
    public float pressureMultiplier;

    //for spatial partitioning for performance
    Entry[] spatialLookup;
    int[] startIndices;

    public Vector3[] positions;

    private void Start()
    {
        particleSpawner = GetComponent<ParticleSpawner>();
        numParticles = particleSpawner.numParticles;

        startIndices = new int[numParticles];
        spatialLookup = new Entry[numParticles];
    }

    private void Update()
    {
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

        //// for the sampleParticle to consider itself in the density
        //float dist = 0;
        //float influence = SmoothingKernel(dist, smoothKerRadius);
        ////mass scales that influence
        //density += mass * influence;

        //Collider[] neighbourParticles = GetNeighbours(particles[samplePointID].position);

        for (int i = 0; i < numParticles; i++)
        {
            //if (neighbourParticles[i].gameObject == particles[samplePointID].gameObject)continue;

            float dist = (particles[i].transform.position - particles[samplePointID].position).magnitude;
            float influence = SmoothingKernel(dist, smoothKerRadius);
            //mass scales that influence
            density += mass * influence;
        }
        return density;
    }

    void UpdateDensities()
    {
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].density = CalculateDensity(i);
        }
    }

    //get the density gradient at a particle in the fluid to determine the amount of pressure force to act on the particle
    Vector3 CalculatePressureForce(int samplePointID)
    {
        Vector3 propertyGradient = Vector3.zero;

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
            float sharedPressure = CalculateSharedPressure(density, particles[samplePointID].density);
            propertyGradient += -sharedPressure * (mass / density) * dir * slope;
        }
        return propertyGradient;
    }
    //get the density gradient at a particle in the fluid to determine the amount of pressure force to act on the particle
    //Vector3 CalculatePressureForce(int samplePointID)
    //{
    //    Vector3 propertyGradient = Vector2.zero;

    //    Collider[] neighbourParticles = GetNeighbours(particles[samplePointID].position);

    //    for (int i = 0; i < neighbourParticles.Length; i++)
    //    {
    //        //so that the particles don't cancel themselves
    //        if (particles[samplePointID].gameObject == neighbourParticles[i].gameObject) continue;

    //        Vector3 offset = neighbourParticles[i].transform.position - particles[samplePointID].transform.position;
    //        float dist = offset.magnitude;
    //        float slope = SmoothingKernelDerivative(dist, smoothKerRadius);
    //        //give random direction so that particle moves somewhere away from the other particle that may have the same position as it
    //        Vector3 dir = /*dist == 0 ? particles[samplePointID].RandomDir() :*/ offset / dist;
    //        //Debug.Log("dir" + dir + "dist" + dist);
    //        float density = neighbourParticles[i].GetComponent<Particle>().density;
    //        float sharedPressure = CalculateSharedPressure(density, particles[samplePointID].density);
    //        propertyGradient += -sharedPressure * (mass / density) * dir * slope;
    //    }
    //    return propertyGradient;
    //}
    private float CalculateSharedPressure(float densityA, float densityB)
    {
        float pressureA = DensityToPressure(densityA);
        float pressureB = DensityToPressure(densityB);
        return (pressureA + pressureB) / 2;
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
    //separate for loops as some functions need data from neightbouring particles
    public void SimulationSteps()
    {
        if (numParticles == 0) return;

        //Apply gravity and calculate the density 
        for (int i = 0; i < numParticles; i++)
        {

            particles[i].velocity += Vector3.down * gravity * Time.deltaTime;
            Debug.Log("particles[i].velocity" + particles[i].velocity);

            particles[i].density = CalculateDensity(i);
        }

        // Calculate and apply pressure forces
        for (int i = 0; i < numParticles; i++)
        {
            Vector3 pressureForce = CalculatePressureForce(i);
            //moving small parts of the fluid and not the entire fluid moving together at the same acceleration --> density instead of mass
            Vector3 pressureAcceleration = pressureForce / particles[i].density;
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

    ////optimise by getting list of neighbouring particles of sample point
    //Collider[] GetNeighbours(Vector3 samplePoint)
    //{
    //    Collider[] hitColliders = Physics.OverlapSphere(samplePoint, smoothKerRadius, particleLayer);
    //    return hitColliders;
    //}

    //run for everytime the particles move
    void UpdateSpatialLookup()
    {
        // Create (unordered) spatial lookup
        //hash the position of the particles and put into array
        for (int i = 0; i < numParticles; i++)
        {
            //convert position of particle to the position of the cell that the particle is in
            (int cellX, int cellY, int cellZ) = PositionToCellCoord(particles[i].transform.position, smoothKerRadius);
            //a cellkey that will only be positive or 0, thus the uint
            uint cellKey = GetKeyFromHash(HashCell(cellX, cellY, cellZ));
            //array of array/list of list?
            spatialLookup[i] = new Entry(i, cellKey);
            //reset start index values
            startIndices[i] = int.MaxValue;
        }

        //Sort by cell key
        Array.Sort(spatialLookup);

        //Calculate start indices of each unique cell key in the spatial lookup
        for (int i = 0; i < positions.Length; i++)
        {
            uint key = spatialLookup[i].cellKey;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i - 1].cellKey;
            if (key != keyPrev)
            {
                startIndices[key] = i;
            }
        }
    }
    private (int cellX, int cellY, int cellZ) PositionToCellCoord(Vector3 position, float radius)
    {
        int cellX = (int)(position.x / radius);
        int cellY = (int)(position.y / radius);
        int cellZ = (int)(position.z / radius);
        return (cellX, cellY, cellZ);
    }
    private uint HashCell(int cellX, int cellY, int cellZ)
    {
        uint a = (uint)cellX * 15823;
        uint b = (uint)cellY * 9737333;
        uint c = (uint)cellZ * 10061;
        return a + b + c;
    }

    private uint GetKeyFromHash(uint hash)
    {
        return hash % (uint)spatialLookup.Length;
    }
    //find particles within the radius of the sample point
    public void ForeachPointWithinRadius(Vector3 samplePoint)
    {
        //find the cell that the sample point is in (this cell is the center of the 3x3x3 block of cells)
        (int centreX, int centreY, int centreZ) = PositionToCellCoord(samplePoint, smoothKerRadius);
        float sqrRadius = Mathf.Pow(smoothKerRadius, 2);

        //loop over all cells of the of the 3x3x3
        foreach ((int offsetX, int offsetY, int offsetZ) in cellOffsets)
        {
            uint key = GetKeyFromHash(HashCell(centreX + offsetX, centreY + offsetY, centreZ + offsetZ))
            int cellsStartID = startIndices[key];

            for (int i = cellsStartID; i < spatialLookup.Length; i++)
            {
                if (spatialLookup[i].cellKey != key) break;

                int particleID = spatialLookup[i].particleID;
                float sqrDst = (particles[particleID].transform.position - samplePoint).sqrMagnitude;

                if (sqrDst <= sqrRadius)
                {
                    //do something
                }
            }
        }
    }
}
