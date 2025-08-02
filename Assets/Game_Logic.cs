using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class Game_Logic : MonoBehaviour
{
    public TMP_Text scenerioInfo;
    public TMP_Text outcomeInfo;
    public TMP_Dropdown dropdownStrategy;
    public Button confirmButton;
    public Button newGameButton; // ⭐️ 新增這行！

    private string[] pitchTypes = { "Fastball", "Slider", "Changeup", "Curve" };
    private string[] outcomes = {
        "Hit : The ball gets through the infield ! The runner from first is rounding third, heading for home !",
        "Homerun : This ball is carrying out to right field, and it's a walk off homer !",
        "Strikeout : The batter swing and a miss, and the game is over......"
    };

    void Start()
    {
        if (!ValidateUI()) return;

        GenerateScenario();
        confirmButton.onClick.AddListener(HandleConfirm);
        newGameButton.onClick.AddListener(GenerateScenario); // ⭐️ 綁定 New Game 按鈕
    }

    bool ValidateUI()
    {
        bool allGood = true;

        if (scenerioInfo == null)
        {
            Debug.LogError("❌ Scenerio Info 尚未綁定！");
            allGood = false;
        }

        if (outcomeInfo == null)
        {
            Debug.LogError("❌ Outcome Info 尚未綁定！");
            allGood = false;
        }

        if (dropdownStrategy == null)
        {
            Debug.LogError("❌ Dropdown_strategy 尚未綁定！");
            allGood = false;
        }

        if (confirmButton == null)
        {
            Debug.LogError("❌ Confirm Button 尚未綁定！");
            allGood = false;
        }

        if (newGameButton == null)
        {
            Debug.LogError("❌ New Game Button 尚未綁定！");
            allGood = false;
        }

        return allGood;
    }

    void GenerateScenario()
    {
        int trailingRuns = Random.Range(1, 3); // 落後 1 或 2 分
        int teamScore = Random.Range(3, 6);
        int opponentScore = teamScore + trailingRuns;

        string[] shuffled = pitchTypes.OrderBy(x => Random.value).ToArray();
        string selectedPitches = $"{shuffled[0]}, {shuffled[1]}, {shuffled[2]}, {shuffled[3]}";

        scenerioInfo.text =
            $"Runners on 1st and 2nd base\n\n" +
            $"You trailed by {trailingRuns} runs ({teamScore}:{opponentScore})\n\n" +
            $"outs : 2\n\n" +
            $"The pitcher's pitching type : {selectedPitches}";

        outcomeInfo.text = "";

        // ⭐️ 若要重設選項，請取消註解：
        // dropdownStrategy.value = 0;
    }

    void HandleConfirm()
    {
        string selectedOption = dropdownStrategy.options[dropdownStrategy.value].text;
        string randomOutcome = outcomes[Random.Range(0, outcomes.Length)];
        outcomeInfo.text = randomOutcome;
    }
}



