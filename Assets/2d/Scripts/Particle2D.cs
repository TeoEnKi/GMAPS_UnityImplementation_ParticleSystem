using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Particle2D : MonoBehaviour
{
    //particles that contain data at certain points of the fluid
    public float velocity;
    public float density;
    public float pressure;
    public float viscosity;

    public void CreateParticle(GameObject particle, Color particleColor)
    {
        Instantiate(particle);
    }
}
