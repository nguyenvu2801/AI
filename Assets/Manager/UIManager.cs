//using System.Collections.Generic;
//using TMPro;
//using UnityEngine;
//using UnityEngine.UI;

//public class UIManager : MonoBehaviour
//{
//    public MatchManager match;
//    public TMP_Dropdown tacticADropdown;
//    public TMP_Dropdown tacticBDropdown;
//    public Button changeTacticAButton;   
//    public Button changeTacticBButton;
//    public TeamTactic aggressiveTactic;   // Drag Aggressive.asset here in Inspector
//    public TeamTactic balancedTactic;     // Drag Balanced.asset
//    public TeamTactic defensiveTactic;    // Drag Defensive.asset
//    void Start()
//    {
//        // Populate dropdowns
//        tacticADropdown.options.Clear();
//        tacticADropdown.options.Add(new TMP_Dropdown.OptionData("Aggressive"));
//        tacticADropdown.options.Add(new TMP_Dropdown.OptionData("Balanced"));
//        tacticADropdown.options.Add(new TMP_Dropdown.OptionData("Defensive"));

//        tacticBDropdown.options = new List<TMP_Dropdown.OptionData>(tacticADropdown.options);

//        // Add listeners
//        changeTacticAButton.onClick.AddListener(ChangeTacticA);
//        changeTacticBButton.onClick.AddListener(ChangeTacticB);
//    }

//    private void ChangeTacticA()
//    {
//        TeamTactic newTactic = GetTacticFromDropdown(tacticADropdown.value);
//        match.teamA.ChangeTactic(newTactic);
//    }

//    private void ChangeTacticB()
//    {
//        TeamTactic newTactic = GetTacticFromDropdown(tacticBDropdown.value);
//        match.teamB.ChangeTactic(newTactic);
//    }

//    private TeamTactic GetTacticFromDropdown(int value)
//    {
//        return value switch
//        {
//            0 => aggressiveTactic,
//            1 => balancedTactic,
//            _ => defensiveTactic
//        };
//    }
//}