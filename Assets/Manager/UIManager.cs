using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public MatchManager match;

    [Header("Score UI")]
    public TMP_Text scoreText;

    [Header("Time UI")]
    public TMP_Text timeText;
    public Button quitButton;
    void Start()
    {
        // Setup Quit button
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }
    void Update()
    {
        if (match == null || match.bb == null) return;

        // Update Score
        if (scoreText != null)
        {
            scoreText.text = $"{match.bb.scoreA}  -  {match.bb.scoreB}";
        }

        // Update Time
        if (timeText != null)
        {
            int minutes = Mathf.FloorToInt(match.bb.timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(match.bb.timeRemaining % 60f);
            timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }
    public void UpdateScoreAndTimeImmediately()
    {
        if (scoreText != null && match != null && match.bb != null)
        {
            scoreText.text = $"{match.bb.scoreA} - {match.bb.scoreB}";
        }

        if (timeText != null && match != null && match.bb != null)
        {
            int minutes = Mathf.FloorToInt(match.bb.timeRemaining / 60f);
            int seconds = Mathf.FloorToInt(match.bb.timeRemaining % 60f);
            timeText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    public void QuitGame()
    {
        Application.Quit();                                
    }
}