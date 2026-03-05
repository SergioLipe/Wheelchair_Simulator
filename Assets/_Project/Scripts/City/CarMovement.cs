using UnityEngine;

/// <summary>
/// Controls the forward movement of a car.
/// The movement is toggled by the 'canMove' variable, usually controlled by a Traffic Light script.
/// </summary>
public class CarMovement : MonoBehaviour
{
    [Header("=== Movement Settings ===")]
    [Tooltip("How fast the car moves forward.")]
    public float speed = 10f;
    
    [Tooltip("Controls if the car is currently allowed to move. The traffic light will turn this on and off.")]
    public bool canMove = false;

    void Update()
    {
        // Check if the car is allowed to move
        if (canMove)
        {
            // Move the car forward continuously while canMove is true
            transform.Translate(Vector3.forward * speed * Time.deltaTime);
        }
    }
}