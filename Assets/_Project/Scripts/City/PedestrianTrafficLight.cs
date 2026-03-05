using UnityEngine;

/// <summary>
/// Controls a pedestrian traffic light and toggles a hazard zone.
/// Includes an option to choose whether the light starts Green or Red.
/// </summary>
public class PedestrianTrafficLight : MonoBehaviour
{
    [Header("=== Light Objects ===")]
    [Tooltip("Drag the Red Glow object here.")]
    public GameObject redLightObject;
    
    [Tooltip("Drag the Green Glow object here.")]
    public GameObject greenLightObject;

    [Header("=== Crosswalk Hazard ===")]
    [Tooltip("Drag the invisible Box Collider (the hazard zone) here.")]
    public Collider crosswalkHazard;

    [Header("=== Timers & Settings ===")]
    [Tooltip("How many seconds the Red light stays on.")]
    public float redDuration = 5.0f;
    
    [Tooltip("How many seconds the Green light stays on.")]
    public float greenDuration = 5.0f;

    [Tooltip("Check this box if you want the light to start Green. Leave unchecked to start Red.")]
    public bool startGreen = false;

    // Internal variables to track state and time
    private float timer;
    private bool isRed;

    void Start()
    {
        // Check the setting chosen in the Inspector to decide how to start
        if (startGreen)
        {
            SetGreenLight();
        }
        else
        {
            SetRedLight();
        }
    }

    void Update()
    {
        // Countdown the timer
        timer -= Time.deltaTime;

        // When the timer reaches zero, switch the lights
        if (timer <= 0f)
        {
            if (isRed)
            {
                SetGreenLight();
            }
            else
            {
                SetRedLight();
            }
        }
    }

    private void SetRedLight()
    {
        isRed = true;
        
        // Turn ON the red light, turn OFF the green light
        redLightObject.SetActive(true);
        greenLightObject.SetActive(false);
        
        // Turn ON the hazard zone (Danger!)
        if (crosswalkHazard != null)
        {
            crosswalkHazard.enabled = true;
        }

        // Reset the timer
        timer = redDuration;
    }

    private void SetGreenLight()
    {
        isRed = false;
        
        // Turn OFF the red light, turn ON the green light
        redLightObject.SetActive(false);
        greenLightObject.SetActive(true);
        
        // Turn OFF the hazard zone (Safe!)
        if (crosswalkHazard != null)
        {
            crosswalkHazard.enabled = false;
        }

        // Reset the timer
        timer = greenDuration;
    }
}