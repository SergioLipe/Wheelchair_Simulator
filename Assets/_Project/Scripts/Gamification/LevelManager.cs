using UnityEngine;
using TMPro;                       // Required for TextMeshPro elements
using UnityEngine.SceneManagement; // Required for reloading scenes or loading the Menu

/// <summary>
/// Manages the game state, timer, scoring system, saving progress, pause system, and UI updates.
/// </summary>
public class LevelManager : MonoBehaviour
{
    // Singleton instance to allow easy access from other scripts
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
    private bool isPaused = false;

    [Header("--- UI References (Drag and Drop) ---")]
    [Tooltip("Reference to the Timer Text")]
    public TMP_Text timeText;

    [Tooltip("Reference to the Collision Counter Text")]
    public TMP_Text collisionText;

    [Tooltip("Reference to the Slide Counter Text")]
    public TMP_Text slideText;

    [Tooltip("Reference to the End Game Panel (Must be disabled at start)")]
    public GameObject endGamePanel;

    [Tooltip("Reference to the Pause Menu Panel (Must be disabled at start)")]
    public GameObject pauseMenuPanel;

    [Tooltip("Reference to the Star Rating Text inside the End Game Panel")]
    public TMP_Text finalStarsText;

    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Ensure the game starts in a playable state
        ResumeGame();
    }

    private void Update()
    {
        // Check for Pause Input (ESC)
        if (Input.GetKeyDown(KeyCode.Escape) && isLevelActive)
        {
            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }

        // Only update the timer if the level is active and NOT paused
        if (isLevelActive && !isPaused)
        {
            elapsedTime += Time.deltaTime;
            UpdateUI();
        }
    }

    /// <summary>
    /// Pauses the game, shows the cursor, and opens the pause menu
    /// </summary>
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Freezes physics and movement

        // Unlock and show cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Resumes the game, hides the cursor, and closes the pause menu
    /// </summary>
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Unfreezes the game

        // Lock and hide cursor for gameplay
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
    }

    private void UpdateUI()
    {
        if (timeText != null)
        {
            string minutes = Mathf.Floor(elapsedTime / 60).ToString("00");
            string seconds = (elapsedTime % 60).ToString("00");
            
            // UI output in Portuguese
            timeText.text = $"{minutes}:{seconds}";
        }
    }

    public void RegisterStrongCollision(string objectHit)
    {
        if (!isLevelActive || isPaused) return;

        collisionCount++;
        
        if (collisionText != null)
        {
            collisionText.text = $"Colisões: {collisionCount}";
        }
    }

    public void RegisterSlide()
    {
        if (!isLevelActive || isPaused) return;

        slideCount++;
        
        if (slideText != null)
        {
            slideText.text = $"Deslizes: {slideCount}";
        }
    }

    public void FinishLevel()
    {
        isLevelActive = false;
        
        // Ensure pause menu is closed
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        
        CalculateResults();
    }

    private void CalculateResults()
    {
        int stars = 1; // Minimum 1 star

        // 3 STARS CRITERIA (GOLD)
        if (elapsedTime <= timeFor3Stars && 
            collisionCount <= maxCollisionsFor3Stars && 
            slideCount <= maxSlidesFor3Stars)
        {
            stars = 3;
        }
        // 2 STARS CRITERIA (SILVER)
        else if (elapsedTime <= timeFor2Stars && 
                 collisionCount <= maxCollisionsFor2Stars && 
                 slideCount <= maxSlidesFor2Stars)
        {
            stars = 2;
        }

        // --- SAVE SYSTEM ---
        string saveKey = "Level_" + levelID + "_Stars";
        int currentBest = PlayerPrefs.GetInt(saveKey, 0);

        if (stars > currentBest)
        {
            PlayerPrefs.SetInt(saveKey, stars);
            PlayerPrefs.Save();
            Debug.Log($"Game Saved! Level {levelID} completed with {stars} stars.");
        }

        ShowEndScreen(stars);
    }

    private void ShowEndScreen(int starCount)
    {
        // Freeze game and show cursor for the menu
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (endGamePanel != null)
        {
            endGamePanel.SetActive(true);

            if (finalStarsText != null)
            {
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
    /// Loads the Main Menu scene
    /// </summary>
    public void GoToMenu()
    {
        // Important: Unfreeze time before leaving
        Time.timeScale = 1f; 
        SceneManager.LoadScene("MainMenu");
    }
}