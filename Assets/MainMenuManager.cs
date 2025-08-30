using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI")]
    public Button startButton;

    [Header("Audio")]
    public AudioSource audioSource;   // 綁定你場景裡的 Audio Source
    public AudioClip openingMusic;    // 綁定 opening 音樂

    void Start()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(LoadMainGame);
        }
        else
        {
            Debug.LogError("❌ Start Button 尚未綁定！");
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
}
