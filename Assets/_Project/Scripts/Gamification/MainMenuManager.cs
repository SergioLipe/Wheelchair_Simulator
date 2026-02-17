using UnityEngine;
using UnityEngine.SceneManagement; 
using UnityEngine.UI;              
using TMPro;                       
using System.Collections.Generic; // Required for Lists

/// <summary>
/// Manages the Main Menu dynamically.
/// Supports ANY number of levels using Arrays and Loops.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("--- Configuration ---")]
    [Tooltip("Drag ALL your level buttons here in order (Level 1, Level 2, etc.)")]
    public Button[] levelButtons;

    [Tooltip("Drag ALL your star texts here in order (matching the buttons)")]
    public TMP_Text[] starTexts;

    private void Start()
    {
        // This function runs once when the menu opens
        InitializeAllLevels();
    }

    /// <summary>
    /// Loops through all buttons and configures them automatically
    /// </summary>
    private void InitializeAllLevels()
    {
        // "For Loop": Repeats this code for every button in the list
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int levelID = i + 1; // Array index starts at 0, but Levels start at 1
            
            // 1. Get saved data for THIS level
            string saveKey = "Level_" + levelID + "_Stars";
            int currentStars = PlayerPrefs.GetInt(saveKey, 0);

            // 2. Determine if UNLOCKED
            bool isUnlocked = false;

            if (i == 0)
            {
                // Level 1 (Index 0) is ALWAYS unlocked
                isUnlocked = true;
            }
            else
            {
                // For other levels, check if the PREVIOUS level was completed
                int previousLevelID = levelID - 1;
                string prevKey = "Level_" + previousLevelID + "_Stars";
                int prevStars = PlayerPrefs.GetInt(prevKey, 0);

                // Unlock condition: At least 1 star in previous level
                if (prevStars >= 1)
                {
                    isUnlocked = true;
                }
            }

            // 3. Apply settings to the Button
            if (levelButtons[i] != null)
            {
                levelButtons[i].interactable = isUnlocked;

                // MAGIC: Automatically link the click to the correct level!
                // You don't need to configure OnClick in Inspector anymore.
                int captureID = levelID; // Necessary for the button to remember its ID
                levelButtons[i].onClick.RemoveAllListeners();
                levelButtons[i].onClick.AddListener(() => LoadGameLevel(captureID));
            }

            // 4. Apply settings to the Text (Portuguese)
            if (starTexts.Length > i && starTexts[i] != null)
            {
                if (isUnlocked)
                {
                    starTexts[i].text = $"{currentStars}/3 ★";
                }
                else
                {
                    starTexts[i].text = "Bloqueado";
                }
            }
        }
    }

    /// <summary>
    /// Loads the scene. Ensure scenes are named "Level1", "Level2", etc.
    /// </summary>
    public void LoadGameLevel(int levelNumber)
    {
        string sceneName = "Level" + levelNumber;
        Debug.Log($"Loading Scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void ResetProgress()
    {
        PlayerPrefs.DeleteAll();
        InitializeAllLevels(); // Refresh immediately
        Debug.Log("Progress Reset!");
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}