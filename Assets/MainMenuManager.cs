using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;   // ★ 新增：因為要控制 Text (TMP)

public class MainMenuManager : MonoBehaviour
{
    [Header("UI")]
    public Button startButton;         // 原本的「遊戲開始！」按鈕

    [Header("Credits UI")]
    public Button creditButton;        // 「Credits」按鈕
    public GameObject creditsPanel;    // 整個 Credits Panel（含 ScrollView）
    public TMP_Text creditsText;       // ScrollView -> Viewport -> Content -> Text (TMP)
    public Button closeCreditsButton;  // 關閉 Credits 的按鈕

    [Header("Audio")]
    public AudioSource audioSource;   // 綁定你場景裡的 Audio Source
    public AudioClip openingMusic;    // 綁定 opening 音樂

    // ★ 可選：如果你想在 Inspector 直接輸入 credits 文字
    [TextArea(3, 10)]
    public string creditsContent = "";

    void Start()
    {
        // ===== Start Button =====
        if (startButton != null)
        {
            startButton.onClick.AddListener(LoadMainGame);
        }
        else
        {
            Debug.LogError("❌ Start Button 尚未綁定！");
        }

        // ===== Credits Button (打開面板) =====
        if (creditButton != null)
        {
            creditButton.onClick.AddListener(OpenCredits);
        }
        else
        {
            Debug.LogWarning("⚠️ Credit Button 尚未綁定");
        }

        // ===== Close Credits Button (關閉面板) =====
        if (closeCreditsButton != null)
        {
            closeCreditsButton.onClick.AddListener(CloseCredits);
        }
        else
        {
            Debug.LogWarning("⚠️ Close Credits Button 尚未綁定");
        }

        // 一開始先把 Credits Panel 關掉
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }

        // 如果有在 Inspector 填 creditsContent，就寫進 Text(TMP)
        if (creditsText != null && !string.IsNullOrEmpty(creditsContent))
        {
            creditsText.text = creditsContent;
        }

        // 播放開場音樂
        if (audioSource != null && openingMusic != null)
        {
            audioSource.clip = openingMusic;
            audioSource.loop = true;   // ✅ 讓音樂循環播放
            audioSource.Play();
        }
    }

    void LoadMainGame()
    {
        SceneManager.LoadScene("Main Game"); // ✅ 確保你的 scene 名稱正確
    }

    // ===== Credits 開關 =====
    void OpenCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(true);
        }
    }

    void CloseCredits()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }
}
