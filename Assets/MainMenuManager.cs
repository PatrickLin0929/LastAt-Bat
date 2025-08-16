using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // ✅ 加入這個

public class MainMenuManager : MonoBehaviour
{
    public Button startButton;

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
    }

    void LoadMainGame()
    {
        SceneManager.LoadScene("Main Game"); // ✅ 確保你的 scene 名稱為 "MainGame"
    }
}

