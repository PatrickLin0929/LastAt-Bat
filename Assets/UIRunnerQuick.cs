using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIRunnerQuick : MonoBehaviour
{
    [Header("Bases (RectTransform)")]
    public RectTransform baseHome;   // Base_H
    public RectTransform base1;      // Base_1
    public RectTransform base2;      // Base_2
    public RectTransform base3;      // Base_3

    [Header("Runner (RectTransform)")]
    public RectTransform runner;     // Runner 圓點／人像

    [Header("Optional: 閃爍提示用")]
    public Image baseHomeImg;
    public Image base1Img;
    public Image base2Img;
    public Image base3Img;
    public float flashDuration = 0.5f;
    public int   flashCount    = 2;

    // 0=本壘, 1=一壘, 2=二壘, 3=三壘
    RectTransform GetBaseByIndex(int idx)
    {
        return idx switch
        {
            0 => baseHome,
            1 => base1,
            2 => base2,
            3 => base3,
            _ => null
        };
    }

    Image GetBaseImgByIndex(int idx)
    {
        return idx switch
        {
            0 => baseHomeImg,
            1 => base1Img,
            2 => base2Img,
            3 => base3Img,
            _ => null
        };
    }

    void Awake()
    {
        // 如果忘了指定 Runner，就嘗試抓同物件下的 Image
        if (runner == null)
        {
            var img = GetComponentInChildren<Image>();
            if (img != null) runner = img.rectTransform;
        }
    }

    /// <summary>
    /// 直接把 Runner 瞬移到 fromBase 往前 steps 的位置。
    /// 0=本壘、1=一壘、2=二壘、3=三壘、>=4 代表回到本壘得分（隱藏跑者）
    /// 例：
    ///  安打一壘：TeleportAdvance(0, 1)
    ///  全壘打   ：TeleportAdvance(0, 4) -> 隱藏
    ///  保送     ：TeleportAdvance(0, 1)
    /// </summary>
    public void TeleportAdvance(int fromBaseIndex, int steps)
    {
        int dest = Mathf.Clamp(fromBaseIndex + steps, 0, 4);

        // 得分（>=4）：收起跑者
        if (dest >= 4)
        {
            HideRunner();
            return;
        }

        var target = GetBaseByIndex(dest);
        if (runner == null || target == null) return;

        // 用 UI 的世界座標對齊即可（兩者都在同一個 Canvas）
        runner.position = target.position;
        if (!runner.gameObject.activeSelf) runner.gameObject.SetActive(true);

        // 閃爍提示（可選）
        var img = GetBaseImgByIndex(dest);
        if (img != null) StartCoroutine(CoFlash(img));
    }

    /// <summary>顯示跑者在指定壘包（0~3）。</summary>
    public void ShowRunnerAtBase(int baseIndex)
    {
        var target = GetBaseByIndex(baseIndex);
        if (runner == null || target == null) return;

        runner.position = target.position;
        runner.gameObject.SetActive(true);
    }

    /// <summary>隱藏跑者。</summary>
    public void HideRunner()
    {
        if (runner != null) runner.gameObject.SetActive(false);
    }

    IEnumerator CoFlash(Image img)
    {
        Color orig = img.color;
        for (int i = 0; i < flashCount; i++)
        {
            img.color = new Color(orig.r, orig.g, orig.b, 0.25f);
            yield return new WaitForSeconds(flashDuration * 0.5f);
            img.color = orig;
            yield return new WaitForSeconds(flashDuration * 0.5f);
        }
        img.color = orig;
    }
}

