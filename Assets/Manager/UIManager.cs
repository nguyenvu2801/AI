using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public MatchManager match;

    [Header("Score UI")]
    public TMP_Text scoreText;

    [Header("Time UI")]
    public TMP_Text timeText;

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

    // Optional: Call this when match ends to show final result
    public void ShowMatchEnd()
    {
        if (timeText != null)
            timeText.text = "00:00";

        if (scoreText != null)
            scoreText.text = $"{match.bb.scoreA}  -  {match.bb.scoreB}  (Match Ended)";
    }
}