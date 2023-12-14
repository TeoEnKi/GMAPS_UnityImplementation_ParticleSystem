using UnityEngine;
using UnityEngine.UIElements;

public class Particle : MonoBehaviour
{
    // TODO
    // - mesh component
    // - material shader

    //functions:
    //collision resolve
    //gravity
    //density
    //pressure
    //visocity

    public float gravity = 10;
    [SerializeField][Range(0,1)] float collisionDamping = 0.7f;
    Boundary boundary;
    public Vector3 velocity;

    public float density;
    public float pressure;
    public float viscosity;

    private void Start()
    {
        boundary = FindAnyObjectByType<Boundary>();
    }
    private void Update()
    {
        velocity += Vector3.down * gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        ResolveCollisions();
    }

    void ResolveCollisions()
    {
        Vector3 halfBoundsSize = boundary.boxSize / 2 - transform.localScale / 2;
        
        if (Mathf.Abs(transform.position.x - boundary.boxSpawn.x) > halfBoundsSize.x)
        {
            //move particle back in before it "moves" back in
            transform.position = new Vector3(boundary.boxSpawn.x + halfBoundsSize.x * Mathf.Sign(transform.position.x - boundary.boxSpawn.x), transform.position.y, transform.position.z);
            velocity.x *= -1 * collisionDamping;
        }
        if (Mathf.Abs(transform.position.y - boundary.boxSpawn.y) > halfBoundsSize.y)
        {
            //move particle back in before it "bounces" back in
            transform.position = new Vector3(transform.position.x, boundary.boxSpawn.y + halfBoundsSize.y * Mathf.Sign(transform.position.y - boundary.boxSpawn.y), transform.position.z);
            velocity.y *= -1 * collisionDamping;
        }
        if (Mathf.Abs(transform.position.z - boundary.boxSpawn.z) > halfBoundsSize.z)
        {
            //move particle back in before it "moves" back in
            transform.position = new Vector3(transform.position.x, transform.position.y, boundary.boxSpawn.z + halfBoundsSize.z * Mathf.Sign(transform.position.z - boundary.boxSpawn.z));
            velocity.z *= -1 * collisionDamping;
        }
    }

}
