using UnityEngine;
using TMPro;

/// <summary>
/// Universal hazard zone. Stops the player, shows a custom message, 
/// and activates the Game Over panel.
/// </summary>
public class HazardArea : MonoBehaviour
{
    [Header("=== Hazard Settings ===")]
    [Tooltip("The exact message to show when hitting this specific hazard")]
    [TextArea]
    public string hazardMessage = "Warning";

    [Header("=== UI References ===")]
    [Tooltip("Drag the Warning Text UI here")]
    public TMP_Text warningTextUI;
    
    [Tooltip("Drag the Game Over Panel you copied here")]
    public GameObject hazardPanel; 

    // Static variable to ensure Game Over only triggers once per level load
    private static bool isGameOver = false;

    private void Start()
    {
        // Reset the variable every time the level starts
        isGameOver = false; 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isGameOver) return;

        if (other.CompareTag("Player"))
        {
            isGameOver = true;

            // 1. Completely stop the wheelchair's movement script
            Movement movementScript = other.GetComponent<Movement>();
            if (movementScript != null)
            {
                movementScript.enabled = false; 
            }

            // 2. Stop the physical wheels
            WheelController wheels = other.GetComponent<WheelController>();
            if (wheels != null)
            {
                wheels.StopWheels();
            }

            // 3. Show the message and activate the dark panel
            if (warningTextUI != null)
            {
                warningTextUI.text = hazardMessage;
            }
            
            if (hazardPanel != null)
            {
                hazardPanel.SetActive(true);
            }

            // 4. Force the mouse cursor to appear so the player can click "TENTAR NOVAMENTE"
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}