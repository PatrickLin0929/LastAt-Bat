using UnityEngine;
using UnityEngine.UI;

public class BaseLight : MonoBehaviour
{
    public Image baseImage;
    public Color activeColor = new Color(1f, 0.84f, 0.2f); // 黃色
    public Color idleColor   = Color.white;
    [Range(0.1f, 8f)] public float blinkSpeed = 1.2f;

    bool isActive = false;
    float timer = 0f;

    void Start()
    {
        if (baseImage == null) baseImage = GetComponent<Image>();
        if (baseImage != null) baseImage.color = idleColor;
    }

    void Update()
    {
        if (baseImage == null) return;

        if (isActive)
        {
            timer += Time.deltaTime * blinkSpeed;
            float t = (Mathf.Sin(timer * Mathf.PI * 2f) + 1f) * 0.5f;
            baseImage.color = Color.Lerp(idleColor, activeColor, t);
        }
        else
        {
            baseImage.color = idleColor;
        }
    }

    public void SetActive(bool active)
    {
        isActive = active;
        if (!active)
        {
            timer = 0f;
            if (baseImage != null) baseImage.color = idleColor;
        }
    }
}
