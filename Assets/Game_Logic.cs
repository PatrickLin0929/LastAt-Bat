using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;

public class Game_Logic : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text scenerioInfo;
    public TMP_Text outcomeInfo;
    public TMP_Dropdown dropdownStrategy;
    public Button confirmButton;
    public Button newGameButton;

    [Header("Base Icons")]
    public GameObject base1B;
    public GameObject base2B;
    public GameObject base3B;

    private string[] pitchTypes = { "Fastball", "Slider", "Changeup", "Curve" };
    private string[] outcomes = {
        "Hit : The ball gets through the infield ! The runner from first is rounding third, heading for home !",
        "Homerun : This ball is carrying out to right field, and it's a walk off homer !",
        "Strikeout : The batter swing and a miss, and the game is over......"
    };

    private bool isConfirmed = false;

    void Start()
    {
        if (!ValidateUI()) return;

        confirmButton.onClick.AddListener(HandleConfirm);
        newGameButton.onClick.AddListener(GenerateScenario);

        GenerateScenario();
    }

    bool ValidateUI()
    {
        bool allGood = true;

        if (scenerioInfo == null) { Debug.LogError("❌ Scenerio Info 尚未綁定！"); allGood = false; }
        if (outcomeInfo == null) { Debug.LogError("❌ Outcome Info 尚未綁定！"); allGood = false; }
        if (dropdownStrategy == null) { Debug.LogError("❌ Dropdown Strategy 尚未綁定！"); allGood = false; }
        if (confirmButton == null) { Debug.LogError("❌ Confirm Button 尚未綁定！"); allGood = false; }
        if (newGameButton == null) { Debug.LogError("❌ New Game Button 尚未綁定！"); allGood = false; }

        if (base1B == null || base2B == null || base3B == null)
        {
            Debug.LogError("❌ Base 壘包 GameObject 尚未綁定！");
            allGood = false;
        }

        return allGood;
    }

    void GenerateScenario()
    {
        isConfirmed = false;
        confirmButton.interactable = true;

        // 清空結果
        outcomeInfo.text = "";

        // 落後分數
        int trailingRuns = Random.Range(1, 3);
        int teamScore = Random.Range(3, 6);
        int opponentScore = teamScore + trailingRuns;

        // 隨機選一種投球類型
        string selectedPitch = pitchTypes[Random.Range(0, pitchTypes.Length)];

        // 隨機生成跑者
        bool runnerOn1B = Random.value > 0.5f;
        bool runnerOn2B = Random.value > 0.5f;
        bool runnerOn3B = Random.value > 0.5f;

        // 更新顯示
        string runnerText = "Runners on ";
        runnerText += runnerOn1B ? "1st " : "";
        runnerText += runnerOn2B ? "2nd " : "";
        runnerText += runnerOn3B ? "3rd " : "";
        if (!runnerOn1B && !runnerOn2B && !runnerOn3B)
            runnerText = "No runners on base";

        scenerioInfo.text =
            $"{runnerText}\n\n" +
            $"You trailed by {trailingRuns} runs ({teamScore}:{opponentScore})\n\n" +
            $"outs : 2\n\n" +
            $"The pitcher's pitching type : {selectedPitch}";

        UpdateBaseColors(runnerOn1B, runnerOn2B, runnerOn3B);
    }

    void HandleConfirm()
    {
        if (isConfirmed) return;

        string selectedOption = dropdownStrategy.options[dropdownStrategy.value].text;

        // 根據策略微調機率
        float rand = Random.value;
        string result;

        if (selectedOption == "Full Swing")
        {
            if (rand < 0.4f) result = outcomes[1]; // Homerun
            else if (rand < 0.75f) result = outcomes[0]; // Hit
            else result = outcomes[2]; // Strikeout
        }
        else if (selectedOption == "Normal Swing")
        {
            if (rand < 0.25f) result = outcomes[1];
            else if (rand < 0.8f) result = outcomes[0];
            else result = outcomes[2];
        }
        else // Defensive Swing
        {
            if (rand < 0.1f) result = outcomes[1];
            else if (rand < 0.7f) result = outcomes[0];
            else result = outcomes[2];
        }

        outcomeInfo.text = result;

        // 按一次後鎖住
        isConfirmed = true;
        confirmButton.interactable = false;
    }

    void UpdateBaseColors(bool on1B, bool on2B, bool on3B)
    {
        base1B.GetComponent<Image>().color = on1B ? Color.red : Color.white;
        base2B.GetComponent<Image>().color = on2B ? Color.red : Color.white;
        base3B.GetComponent<Image>().color = on3B ? Color.red : Color.white;
    }
}






