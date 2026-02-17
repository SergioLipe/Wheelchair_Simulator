using UnityEngine;
using TMPro;                // Required for TextMeshPro elements
using UnityEngine.SceneManagement; // Required for reloading scenes or loading the Menu

/// <summary>
/// Manages the game state, timer, scoring system, saving progress, and UI updates.
/// Handles the logic for calculating stars based on Time, Collisions, and Wall Slides.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // Singleton instance to allow easy access from other scripts (like CollisionSystem)
    public static LevelManager Instance { get; private set; }

    [Header("--- Level Configuration ---")]
    [Tooltip("Unique ID for this level (1, 2, 3...). Crucial for the Save System.")]
    public int levelID = 1; 

    [Header("--- Star Criteria (Time) ---")]
    [Tooltip("Maximum time in seconds to achieve 3 stars")]
    public float timeFor3Stars = 60f;

    [Tooltip("Maximum time in seconds to achieve 2 stars")]
    public float timeFor2Stars = 90f;

    [Header("--- Star Criteria (Collisions) ---")]
    [Tooltip("Maximum number of collisions allowed to achieve 3 stars")]
    public int maxCollisionsFor3Stars = 0;

    [Tooltip("Maximum number of collisions allowed to achieve 2 stars")]
    public int maxCollisionsFor2Stars = 2;

    [Header("--- Star Criteria (Slides) ---")]
    [Tooltip("Maximum number of slides allowed to achieve 3 stars")]
    public int maxSlidesFor3Stars = 2;

    [Tooltip("Maximum number of slides allowed to achieve 2 stars")]
    public int maxSlidesFor2Stars = 5;

    [Header("--- Current State (Read Only) ---")]
    public float elapsedTime = 0f;
    public int collisionCount = 0;
    public int slideCount = 0;
    public bool isLevelActive = true;

    [Header("--- UI References (Drag and Drop) ---")]
    [Tooltip("Reference to the Timer Text (TextMeshPro)")]
    public TMP_Text timeText;

    [Tooltip("Reference to the Collision Counter Text (TextMeshPro)")]
    public TMP_Text collisionText;

    [Tooltip("Reference to the Slide Counter Text (TextMeshPro)")]
    public TMP_Text slideText;

    [Tooltip("Reference to the End Game Panel (Must be disabled at start)")]
    public GameObject endGamePanel;

    [Tooltip("Reference to the Star Rating Text inside the End Game Panel")]
    public TMP_Text finalStarsText;

    private void Awake()
    {
        // Singleton Pattern: Ensures only one instance exists
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Only update the timer if the level is active
        if (isLevelActive)
        {
            elapsedTime += Time.deltaTime;
            UpdateUI();
        }
    }

    /// <summary>
    /// Updates the timer text in the UI (Format: MM:SS)
    /// </summary>
    private void UpdateUI()
    {
        if (timeText != null)
        {
            string minutes = Mathf.Floor(elapsedTime / 60).ToString("00");
            string seconds = (elapsedTime % 60).ToString("00");
            
            // UI output in Portuguese
            timeText.text = $"Tempo: {minutes}:{seconds}";
        }
    }

    /// <summary>
    /// Called by CollisionSystem when a hard impact occurs
    /// </summary>
    public void RegisterStrongCollision(string objectHit)
    {
        if (!isLevelActive) return;

        collisionCount++;
        
        // UI output in Portuguese
        if (collisionText != null)
        {
            collisionText.text = $"Colisões: {collisionCount}";
        }
    }

    /// <summary>
    /// Called by CollisionSystem when the wheelchair scrapes a wall
    /// </summary>
    public void RegisterSlide()
    {
        if (!isLevelActive) return;

        slideCount++;
        
        // UI output in Portuguese
        if (slideText != null)
        {
            slideText.text = $"Deslizes: {slideCount}";
        }
    }

    /// <summary>
    /// Called by GoalDetector when the player reaches the goal
    /// </summary>
    public void FinishLevel()
    {
        isLevelActive = false;
        CalculateResults();
    }

    /// <summary>
    /// Calculates stars based on Time, Collisions AND Slides.
    /// Saves the result if it's a new high score.
    /// </summary>
    private void CalculateResults()
    {
        int stars = 1; // Minimum 1 star for completing the level

        // 3 STARS CRITERIA (GOLD)
        // Must pass ALL checks: Fast Time AND Low Collisions AND Low Slides
        if (elapsedTime <= timeFor3Stars && 
            collisionCount <= maxCollisionsFor3Stars && 
            slideCount <= maxSlidesFor3Stars)
        {
            stars = 3;
        }
        // 2 STARS CRITERIA (SILVER)
        // If failed Gold, check if it meets Silver criteria
        else if (elapsedTime <= timeFor2Stars && 
                 collisionCount <= maxCollisionsFor2Stars && 
                 slideCount <= maxSlidesFor2Stars)
        {
            stars = 2;
        }
        
        // If neither criteria is met, the player keeps the default 1 Star (Bronze)

        // --- SAVE SYSTEM ---
        string saveKey = "Level_" + levelID + "_Stars";

        // Get the previous best score (default is 0)
        int currentBest = PlayerPrefs.GetInt(saveKey, 0);

        // If the new score is better, save it to disk
        if (stars > currentBest)
        {
            PlayerPrefs.SetInt(saveKey, stars);
            PlayerPrefs.Save();
            Debug.Log($"Game Saved! Level {levelID} completed with {stars} stars.");
        }

        ShowEndScreen(stars);
    }

    /// <summary>
    /// Displays the victory panel and the star rating
    /// </summary>
    private void ShowEndScreen(int starCount)
    {
        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);

            if (finalStarsText != null)
            {
                // Build star string (e.g., "★★★")
                string starString = "";
                for (int i = 0; i < starCount; i++)
                {
                    starString += "★";
                }

                // UI output in Portuguese
                finalStarsText.text = $"NÍVEL CONCLUÍDO!\nClassificação: {starString}";
            }
        }
    }

    /// <summary>
    /// Helper function to load the Main Menu (to be used by UI Buttons)
    /// </summary>
    public void GoToMenu()
    {
        // Ensure time scale is normal before leaving the scene
        Time.timeScale = 1f; 
        SceneManager.LoadScene("MainMenu");
    }
}