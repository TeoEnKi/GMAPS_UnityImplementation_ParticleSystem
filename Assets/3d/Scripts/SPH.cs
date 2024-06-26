using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SPH : MonoBehaviour
{
    [SerializeField] float gravity = 10;

    ParticleSpawner particleSpawner;
    [SerializeField] int numParticles;

    const float mass = 10;
    public float smoothKerRadius;

    public Particle[] particles;
    [SerializeField] LayerMask particleLayer;

    //what the density should be throughout the fluid body
    [SerializeField] float targetDensity;
    //how much should particles be pushed depending on density
    [SerializeField] float pressureMultiplier;
    [SerializeField] float viscosityStrength = 10;


    //for spatial partitioning for performance
    Entry[] spatialLookup;
    int[] startIndices;
    List<(int, int, int)> cellOffsets;

    public void EditSPHProperties()
    {
        Slider sphPropertyInput = EventSystem.current.currentSelectedGameObject.GetComponent<Slider>();

        switch (sphPropertyInput.gameObject.name.Replace(" Slider", ""))
        {
            case ("Density"):
                targetDensity = sphPropertyInput.value;
                break;
            case ("Pressure"):
                pressureMultiplier = sphPropertyInput.value;
                break;
        }
    }

    private void Start()
    {
        particleSpawner = GetComponent<ParticleSpawner>();
        numParticles = particleSpawner.numParticles;

        startIndices = new int[numParticles];
        spatialLookup = new Entry[numParticles];
        cellOffsets = new List<(int, int, int)>
        {
            (-1, -1, -1), (-1, -1, 0), (-1, -1, 1),
            (-1, 0, -1), (-1, 0, 0), (-1, 0, 1),
            (-1, 1, -1), (-1, 1, 0), (-1, 1, 1),
            (0, -1, -1), (0, -1, 0), (0, -1, 1),
            (0, 0, -1), (0, 0, 0), (0, 0, 1),
            (0, 1, -1), (0, 1, 0), (0, 1, 1),
            (1, -1, -1), (1, -1, 0), (1, -1, 1),
            (1, 0, -1), (1, 0, 0), (1, 0, 1),
            (1, 1, -1), (1, 1, 0), (1, 1, 1)
        };
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(particles[87].transform.position, smoothKerRadius);
    }
    private void Update()
    {
        ////update spatial lookup with predicted positions
        //UpdateSpatialLookup();
        ////Calulate densities based on predicted positions
        //for (int i = 0; i < numParticles; i++)
        //{
        //    particles[i].density = CalculateDensity(i);
        //}
        SimulationSteps();
    }

    //measure of influence of a particle on the updated particle 
    //measured by the radius of the smoothing kernel - the distance between the 2 particles
    //a very important concept used in most SPH formulas
    float SmoothKernelPow2(float dist, float radius)
    {
        if (dist >= radius) return 0;

        float volume = (2 * Mathf.PI * Mathf.Pow(radius, 5)) / 15;
        float v = radius - dist;
        return v * v / volume;
    }

    //Getting the rate of change at the point of the influence graph
    float DeriveSmoothKernelPow2(float dist, float radius)
    {
        //constant
        if (dist >= radius) return 0;

        //partially derive the formula used for the smoothing kernel
        float scale = 15 / (Mathf.Pow(radius, 5) * Mathf.PI);
        float v = radius - dist;
        return -v * scale;
    }
    //based on cubic exponential graph
    //sharper increase/decrease than pow 2
    //to get the nearest density to get nearest pressure to apply a small force to the particle to move it away from the clusters that it is in
    float SmoothKernelPow3(float dist, float radius)
    {
        if (dist >= radius) return 0;

        float volume = (Mathf.PI * Mathf.Pow(radius, 6)) / 15;
        float v = radius - dist;
        return v * v * v / volume;
    }

    //Getting the rate of change at the point of the influence graph
    float DeriveSmoothKernelPow3(float dist, float radius)
    {
        if (dist >= radius) return 0;

        float scale = 45 / (Mathf.Pow(radius, 6) * Mathf.PI);
        float v = radius - dist;
        return -v * v * scale;
    }

    //getting the density of the fluid at that particle/ point of the fluid body
    float CalculateDensity(int samplePointID)
    {
        float density = 0;

        //for the sampleParticle to consider itself in the density
        float dist = 0;
        float influence = SmoothKernelPow2(dist, smoothKerRadius);
        density += mass * influence;

        List<Particle> neighbourParts = ForeachPointWithinRadius(particles[samplePointID].transform.position);

        for (int i = 0; i < neighbourParts.Count; i++)
        {
            if (neighbourParts[i].gameObject == particles[samplePointID].gameObject) continue;

            dist = (neighbourParts[i].transform.position - particles[samplePointID].predictedPosition).magnitude;
            influence = SmoothKernelPow2(dist, smoothKerRadius);

            //mass scales that influence
            density += mass * influence;
        }
        return density;
    }

    //get the density gradient at a particle in the fluid to determine the amount of pressure force to act on the particle
    Vector3 CalculatePressureForce(int samplePointID)
    {
        Vector3 propertyGradient = Vector3.zero;
        List<Particle> neighbourParts = ForeachPointWithinRadius(particles[samplePointID].transform.position);

        for (int i = 0; i < neighbourParts.Count; i++)
        {
            //so that the particles don't cancel themselves
            if (neighbourParts[i] == particles[samplePointID]) continue;

            Vector3 offset = neighbourParts[i].transform.position - particles[samplePointID].transform.position;
            float dist = offset.magnitude;
            float slope = DeriveSmoothKernelPow2(dist, smoothKerRadius);
            //give up direction so that particle moves somewhere away from the other particle that may have the same position as it
            Vector3 dir = dist > 0 ? -offset / dist : Vector3.up;
            float density = neighbourParts[i].density;
            float nearestDensity = neighbourParts[i].nearestDensity;
            (float sharedPressure, float nearestSharedPressure) = CalculateSharedPressure(density, particles[samplePointID].density, nearestDensity, particles[samplePointID].nearestDensity);
            //interpolation equation which can be used to find the pressure values at the empty spaces where here are no particles
            propertyGradient += sharedPressure * (mass / density) * dir * slope;
        }
        return propertyGradient;
    }
    private (float, float) CalculateSharedPressure(float densityA, float densityB, float nearestDensityA, float nearestDensityB)
    {
        (float pressureA, float nearestPressureA) = DensityToPressure(densityA, nearestDensityA);
        (float pressureB, float nearestPressureB) = DensityToPressure(densityB, nearestDensityB);

        float sharedPressure = (pressureA + pressureB) / 2;
        float nearestSharedPressure = (nearestPressureA + nearestPressureB) / 2;
        return (sharedPressure, nearestSharedPressure);
    }

    (float, float) DensityToPressure(float density, float nearestDensity)
    {
        //how far off the actual density is from the target density
        float densityError = density - targetDensity;
        //how much particle should move due to pressure force
        float pressure = densityError * pressureMultiplier;
        float nearestPressure = nearestDensity * pressureMultiplier;

        return (pressure, nearestPressure);
    }
    //using the graph with the slow increase when x is near 0
    //function with a polynomial equation
    //!!code should be put in SmoothKernelPoly
    float ViscositySmoothingKernel(float dist, float radius)
    {
        if (dist >= radius) return 0;

        float vol = (float)((64 * Mathf.PI * Mathf.Pow(Mathf.Abs(radius), 9)) / 315);
        float v = radius * radius - dist * dist;
        return v * v * v / vol;
    }

    Vector3 CalculateViscosityForce(int samplePointID)
    {
        Vector3 viscosityForce = Vector3.zero;
        Vector3 position = particles[samplePointID].transform.position;

        List<Particle> neighbourParts = ForeachPointWithinRadius(particles[samplePointID].transform.position);
        for (int otherIndex = 0; otherIndex < neighbourParts.Count; otherIndex++)
        {
            float dist = (position - particles[otherIndex].transform.position).magnitude;
            float influence = ViscositySmoothingKernel(dist, smoothKerRadius);
            viscosityForce += (particles[otherIndex].velocity - particles[samplePointID].velocity) * influence;
        }

        return viscosityForce * viscosityStrength;
    }

    //determines how the particles behave based on the given SPH values
    //separate for loops as some functions need data from neightbouring particles
    public void SimulationSteps()
    {
        const float deltaTime = 1 / 120f;
        if (numParticles == 0) return;

        //Apply gravity and predict next positions (to better react to upcoming situations) (so instead of step one move here, step 2 move there, take that position and calculate everything before deciding where the particles should move)
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].velocity += new Vector3(0, -0.5f, 0) * gravity * deltaTime;
            particles[i].predictedPosition = particles[i].transform.position + particles[i].velocity;
        }

        //update spatial lookup with predicted positions
        UpdateSpatialLookup();

        //Calulate densities based on predicted positions
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].density = CalculateDensity(i);
        }

        // Calculate and apply pressure forces and viscosity
        for (int i = 0; i < numParticles; i++)
        {
            Vector3 pressureForce = CalculatePressureForce(i);
            //moving small parts of the fluid and not the entire fluid moving together at the same acceleration --> density instead of mass
            Vector3 pressureAcceleration = pressureForce / particles[i].density;
            //newton's second law: law of momentum.
            //The acceleration of the body is directly proportional to the net force acting on the body
            //and inversely proportional to the mass of the body. 
            particles[i].velocity += pressureAcceleration * deltaTime;

            Vector3 viscosityForce = CalculateViscosityForce(i);
            particles[i].velocity += viscosityForce * deltaTime;

        }

        //Apply and add velocities to positions values
        for (int i = 0; i < numParticles; i++)
        {
            particles[i].transform.position += particles[i].velocity * deltaTime;
            particles[i].ResolveCollisions();
        }
    }
    void UpdateSpatialLookup()
    {
        // Create (unordered) spatial lookup
        //hash the position of the particles and put into array
        for (int i = 0; i < numParticles; i++)
        {
            //convert position of particle to the position of the cell that the particle is in
            (int cellX, int cellY, int cellZ) = PositionToCellCoord(particles[i].predictedPosition, smoothKerRadius);
            //a cellkey that will only be positive or 0, thus the uint
            uint cellKey = GetKeyFromHash(HashCell(cellX, cellY, cellZ));
            //array of array/list of list?
            spatialLookup[i] = new Entry(i, cellKey);
            //reset start index values
            startIndices[i] = int.MaxValue;

        }

        //Sort by cell key
        Array.Sort(spatialLookup, (x, y) => x.cellKey.CompareTo(y.cellKey));


        //Calculate start indices of each unique cell key in the spatial lookup
        for (int i = 0; i < particles.Length; i++)
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
    List<Particle> ForeachPointWithinRadius(Vector3 samplePoint)
    {
        List<Particle> neighbourParts = new List<Particle>();

        //find the cell that the sample point is in (this cell is the center of the 3x3x3 block of cells)
        (int centreX, int centreY, int centreZ) = PositionToCellCoord(samplePoint, smoothKerRadius);
        float sqrRadius = Mathf.Pow(smoothKerRadius, 2);

        //loop over all cells of the of the 3x3x3
        foreach ((int offsetX, int offsetY, int offsetZ) in cellOffsets)
        {
            // get the key of the cell
            uint key = GetKeyFromHash(HashCell(centreX + offsetX, centreY + offsetY, centreZ + offsetZ));
            //get the spatial lookup index of where the list of particles with that cell key starts
            int cellsStartID = startIndices[key];

            //starting from where the list starts, loop through the particles
            for (int i = cellsStartID; i < spatialLookup.Length; i++)
            {
                //when the spacial lookup key is different from the wanted key, list ends and the for loop breaks
                if (spatialLookup[i].cellKey != key) break;

                //get the particle ID and check if the particle is within the radius of the sample point
                int particleID = spatialLookup[i].particleID;
                float sqrDst = (particles[particleID].transform.position - samplePoint).sqrMagnitude;

                if (sqrDst <= sqrRadius)
                {
                    neighbourParts.Add(particles[particleID]);
                }
            }
        }
        return neighbourParts;
    }
}
