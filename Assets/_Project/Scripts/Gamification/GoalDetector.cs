using UnityEngine;

public class GoalDetector : MonoBehaviour
{
    // Detects when another collider enters this object's trigger zone
    private void OnTriggerEnter(Collider other)
    {
        // Check if the object is the Player (Wheelchair)
        if (other.CompareTag("Player") || other.gameObject.name.Contains("Wheelchair"))
        {
            // Call the LevelManager to stop the timer and calculate stars
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.FinishLevel();
            }
        }
    }
}