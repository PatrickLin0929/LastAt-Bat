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

    // ★ 新增：按鈕音效
    [Header("Button SFX")]
    public AudioClip buttonClickSFX;  // 按鈕點擊音效

    // ★ 可選：如果你想在 Inspector 直接輸入 credits 文字
    [TextArea(3, 10)]
    public string creditsContent = "";

    // ★ 狀態旗標：避免重複點擊
    bool isLoading = false;

    void Start()
    {
        // ===== Start Button =====
        if (startButton != null)
        {
            // ✔ 改成只綁一個函式，由它負責「先播聲音再換場景」
            startButton.onClick.AddListener(OnStartButtonClicked);
        }
        else
        {
            Debug.LogError("❌ Start Button 尚未綁定！");
        }

        // ===== Credits Button (打開面板) =====
        if (creditButton != null)
        {
            creditButton.onClick.AddListener(OpenCredits);
            creditButton.onClick.AddListener(PlayButtonSFX);   // 播放按鈕音效
        }
        else
        {
            Debug.LogWarning("⚠️ Credit Button 尚未綁定");
        }

        // ===== Close Credits Button (關閉面板) =====
        if (closeCreditsButton != null)
        {
            closeCreditsButton.onClick.AddListener(CloseCredits);
            closeCreditsButton.onClick.AddListener(PlayButtonSFX); // 播放按鈕音效
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

    // ==== Start 按鈕的入口 ====
    void OnStartButtonClicked()
    {
        if (isLoading) return;   // 已經在等就不要再觸發一次

        isLoading = true;

        // 可以順便暫時鎖按鈕避免連按
        if (startButton != null)
            startButton.interactable = false;

        StartCoroutine(CoPlayClickThenLoad());
    }

    // 先播按鈕聲 → 等播完 → 再 LoadScene
    System.Collections.IEnumerator CoPlayClickThenLoad()
    {
        // 1. 播按鈕音效
        PlayButtonSFX();

        // 2. 如果有設定音效，就等它播完
        float waitTime = 0f;
        if (buttonClickSFX != null)
            waitTime = buttonClickSFX.length;

        if (waitTime > 0f)
            yield return new WaitForSeconds(waitTime);

        // 3. 播完後才真正切場景
        LoadMainGame();
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

    // ★ 統一的按鈕音效播放函式
    void PlayButtonSFX()
    {
        if (audioSource != null && buttonClickSFX != null)
        {
            audioSource.PlayOneShot(buttonClickSFX);
        }
    }
}

