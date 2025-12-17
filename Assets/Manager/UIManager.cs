using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour {
    public MatchManager match;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    public TMP_Dropdown tacticADropdown;  
    public TMP_Dropdown tacticBDropdown;
    public Button startButton;
    void Start(){
        startButton.onClick.AddListener(OnStartClick);
        // populate dropdowns if you have concrete assets; for now we set text options
        tacticADropdown.options.Clear();
        tacticADropdown.options.Add(new TMP_Dropdown.OptionData("Aggressive"));  
        tacticADropdown.options.Add(new TMP_Dropdown.OptionData("Balanced"));    
        tacticADropdown.options.Add(new TMP_Dropdown.OptionData("Defensive"));   
        tacticBDropdown.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>(tacticADropdown.options);
    }

    void Update(){
        timerText.text = Mathf.Ceil(match.bb.timeRemaining).ToString();
        scoreText.text = $"{match.bb.scoreA} - {match.bb.scoreB}";
    }

    void OnStartClick(){
        // map dropdown selection to tactics (simplified)
        match.tacticA = (tacticADropdown.value==0) ? TeamTactic.Aggressive : (tacticADropdown.value==1) ? TeamTactic.Balanced : TeamTactic.Defensive;
        match.tacticB = (tacticBDropdown.value==0) ? TeamTactic.Aggressive : (tacticBDropdown.value==1) ? TeamTactic.Balanced : TeamTactic.Defensive;
    }
}
