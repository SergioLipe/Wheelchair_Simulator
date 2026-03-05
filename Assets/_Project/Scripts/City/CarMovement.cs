using UnityEngine;

/// <summary>
/// Controls the forward movement of a car.
/// The car stops at specific Stop Zones during red lights, 
/// and uses multiple raycast sensors (forward and oblique) to avoid rear-ending other cars.
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
    [Tooltip("How far the main forward sensor looks ahead (in meters).")]
    public float frontSensorLength = 8f; // Aumentei um pouco o default porque agora começa no meio do carro

    [Tooltip("How far the oblique (angled) sensors look ahead.")]
    public float obliqueSensorLength = 5f;

    [Tooltip("The angle (in degrees) for the oblique sensors to point left and right.")]
    public float obliqueSensorAngle = 25f;

    // Internal variable to track if the car is currently inside its specific stop zone
    private bool isInStopZone = false;

    void Update()
    {
        // 1. SAFETY SENSOR: Check if there is another car directly in front or slightly to the sides
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
    /// Shoots multiple invisible raycasts forward and diagonally to detect other cars.
    /// </summary>
    private bool IsCarAhead()
    {
        // The sensor now starts exactly in the middle of the car (slightly above the ground)
        Vector3 sensorStartPos = transform.position + new Vector3(0, 0.5f, 0);

        // Calculate the directions for the three raycasts
        Vector3 forwardDirection = transform.forward;
        Vector3 leftObliqueDirection = Quaternion.AngleAxis(-obliqueSensorAngle, Vector3.up) * transform.forward;
        Vector3 rightObliqueDirection = Quaternion.AngleAxis(obliqueSensorAngle, Vector3.up) * transform.forward;

        // Check Center Ray
        if (CheckSingleRay(sensorStartPos, forwardDirection, frontSensorLength)) return true;
        
        // Check Left Oblique Ray
        if (CheckSingleRay(sensorStartPos, leftObliqueDirection, obliqueSensorLength)) return true;
        
        // Check Right Oblique Ray
        if (CheckSingleRay(sensorStartPos, rightObliqueDirection, obliqueSensorLength)) return true;

        // If no rays hit another car, the path is clear
        return false;
    }

    /// <summary>
    /// Helper method to fire a single raycast and check for another car.
    /// </summary>
    private bool CheckSingleRay(Vector3 startPos, Vector3 direction, float length)
    {
        // RAYCAST ALL: This shoots a laser that passes through everything, collecting all hits!
        RaycastHit[] hits = Physics.RaycastAll(startPos, direction, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        // Loop through everything the laser touched
        foreach (RaycastHit hit in hits)
        {
            // Check if the object we hit has a CarMovement script anywhere in its hierarchy
            CarMovement otherCar = hit.collider.GetComponentInParent<CarMovement>();

            // IMPORTANT: If we found a car, AND it is NOT our own car!
            if (otherCar != null && otherCar.gameObject != this.gameObject)
            {
                // Draw a RED line for debugging
                Debug.DrawRay(startPos, direction * hit.distance, Color.red);
                return true;
            }
        }

        // Draw a GREEN line if the path is clear
        Debug.DrawRay(startPos, direction * length, Color.green);
        return false;
    }

    /// <summary>
    /// Detects when the car enters an invisible trigger zone.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(targetStopZoneTag))
        {
            isInStopZone = true;
        }
    }

    /// <summary>
    /// Failsafe: Detects if the car spawns or is already resting inside the trigger zone.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
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
        if (other.CompareTag(targetStopZoneTag))
        {
            isInStopZone = false;
        }
    }
}