using UnityEngine;

/// <summary>
/// Controls the forward movement of a car.
/// The car stops at specific Stop Zones during red lights, 
/// and uses a raycast sensor to avoid rear-ending other cars.
/// </summary>
public class CarMovement : MonoBehaviour
{
    [Header("=== Movement Settings ===")]
    [Tooltip("How fast the car moves forward.")]
    public float speed = 10f;
    
    [Tooltip("Controlled by the Traffic Light. True = Green Light (Go), False = Red Light (Stop).")]
    public bool canMove = true;

    [Header("=== Zone Settings ===")]
    [Tooltip("The specific Tag this car will look for to stop. (e.g., 'StopZone_Street1')")]
    public string targetStopZoneTag = "StopZone";

    [Header("=== Collision Sensor Settings ===")]
    [Tooltip("How far the car looks ahead (in meters) to detect other vehicles.")]
    public float sensorLength = 5f;
    
    [Tooltip("The tag used to identify other cars on the road.")]
    public string carTag = "Car";

    // Internal variable to track if the car is currently inside its specific stop zone
    private bool isInStopZone = false;

    void Update()
    {
        // 1. SAFETY SENSOR: Check if there is another car directly in front of us
        if (IsCarAhead())
        {
            // If there is a car ahead, stop moving immediately to prevent a crash
            return; 
        }

        // 2. NORMAL MOVEMENT: Move forward if the light is green (canMove is true) 
        // OR if the car is NOT inside its specific stop zone (!isInStopZone)
        if (canMove || !isInStopZone)
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
    }

    /// <summary>
    /// Shoots an invisible raycast forward to detect other cars.
    /// </summary>
    private bool IsCarAhead()
    {
        // Start the sensor slightly above the ground (bumper height) so it doesn't hit the road
        Vector3 sensorStartPos = transform.position + new Vector3(0, 0.5f, 0);
        RaycastHit hit;
        
        // Shoot the invisible raycast forward
        if (Physics.Raycast(sensorStartPos, transform.forward, out hit, sensorLength))
        {
            // Check if the raycast hit an object with the "Car" tag (and isn't hitting itself)
            if (hit.collider.CompareTag(carTag) && hit.collider.gameObject != gameObject)
            {
                // Draw a RED line in the Scene view for debugging (visible only in the Unity Editor)
                Debug.DrawRay(sensorStartPos, transform.forward * hit.distance, Color.red);
                return true;
            }
        }

        // Draw a GREEN line in the Scene view if the path is clear
        Debug.DrawRay(sensorStartPos, transform.forward * sensorLength, Color.green);
        return false;
    }

    /// <summary>
    /// Detects when the car enters an invisible trigger zone.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Check if the zone the car just entered matches its specific target tag
        if (other.CompareTag(targetStopZoneTag))
        {
            isInStopZone = true;
        }
    }

    /// <summary>
    /// Detects when the car completely leaves the invisible trigger zone.
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // Check if the zone the car just left matches its specific target tag
        if (other.CompareTag(targetStopZoneTag))
        {
            isInStopZone = false;
        }
    }
}