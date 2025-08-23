using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class Game_Logic : MonoBehaviour
{
    // ===================== Main UI =====================
    [Header("Main UI")]
    public TMP_Text scenerioInfo;               // "Scenerio Info"
    public TMP_Text outcomeInfo;                // "Outcome Info"
    public TMP_Dropdown swingTypeDropdown;      // "Dropdown_strategy" (Full / Normal)

    [Header("Buttons")]
    public Button confirmButton;                // "Confirm Button"  => Let's Go !
    public Button swingButton;                  // "SwingButton"
    public Button takeButton;                   // "TakeButton"
    public Button newGameButton;                // "New Game Button" / Play Again

    [Header("Base Icons (UI Image)")]
    public GameObject base1B;                   // "Base_1B"
    public GameObject base2B;                   // "Base_2B"
    public GameObject base3B;                   // "Base_3B"

    // ===================== Detail Panel =====================
    [Header("Detail Info UI")]
    public Button openDetailInfoButton;         // "Open Detail Info Button"
    public GameObject detailInfoCanvas;         // "Detail Info Canvas"
    public TMP_Dropdown detailSwingDropdown;    // detail panel Dropdown_strategy
    public Button closeDetailButton;            // "Close Button"
    public TMP_Text recordTable;                // "Record Table" (放在 Content 底下)

    [Header("Optional: ScrollView for Record Table")]
    public ScrollRect recordScrollView;         // 指到 Detail 面板的 Scroll View

    [Header("Scroll Content Root (Assign Content)")]
    public RectTransform recordContent;         // 指到 Scroll View/Viewport/Content

    [Header("Detail Layout Align")]
    public RectTransform titleArea;             // 指到 "Title Text" 的 RectTransform
    public float extraPadding = 0f;             // 對齊微調

    [Header("Optional")]
    public bool tryAutoWire = true;             // 自動從 ScrollRect 取得 content

    // ===================== Internal State =====================
    private enum Choice { None, Swing, Take }
    private Choice currentChoice = Choice.None;

    private int strikes = 0;
    private int balls = 0;
    private int outs = 0;

    private bool on1B = false;
    private bool on2B = false;
    private bool on3B = false;

    private int ourScore = 0;
    private int oppScore = 0;

    private bool inningOver = false;
    private int pitchNumber = 0;

    private string pitcherType = "Fastball";
    private readonly string[] pitchTypes = { "Fastball", "Slider", "Changeup", "Curve" };

    private enum AtBatState { Ready, InProgress, Complete }
    private AtBatState state = AtBatState.Ready;

    // pretty log columns
    private readonly List<string> records = new List<string>();
    private string headerText = "";             // 永久表頭
    const int COL_NO = 5;
    const int COL_PITCH = 8;
    const int COL_ACTION = 18;
    const int COL_RESULT = 10;

    void Start()
    {
        if (!ValidateBindings()) return;

        // 自動接 Content
        if (tryAutoWire)
        {
            if (recordScrollView != null && recordContent == null)
                recordContent = recordScrollView.content;
        }

        // 綁定按鈕
        swingButton.onClick.AddListener(() => SetChoice(Choice.Swing));
        takeButton.onClick.AddListener(() => SetChoice(Choice.Take));
        confirmButton.onClick.AddListener(ExecutePitch);
        newGameButton.onClick.AddListener(GenerateNewInning);

        // Detail 面板
        openDetailInfoButton.onClick.AddListener(ShowDetailCanvas);
        closeDetailButton.onClick.AddListener(HideDetailCanvas);

        // 兩個 dropdown 同步
        if (swingTypeDropdown != null)
            swingTypeDropdown.onValueChanged.AddListener(OnMainDropdownChanged);
        if (detailSwingDropdown != null)
            detailSwingDropdown.onValueChanged.AddListener(OnDetailDropdownChanged);

        if (detailInfoCanvas != null) detailInfoCanvas.SetActive(false);

        // RecordTable：固定字體大小 + 允許換行 + 頂左對齊
        if (recordTable != null)
        {
            recordTable.enableAutoSizing = false;               // 固定字體
            recordTable.fontSize = 20f;                         // 看得清楚的大小 (可在 Inspector 改)
            recordTable.textWrappingMode = TextWrappingModes.Normal; // 允許換行
            recordTable.overflowMode = TextOverflowModes.Overflow;
            recordTable.alignment = TextAlignmentOptions.TopLeft;
            recordTable.text = "";
        }

        GenerateNewInning();
        AlignRecordAreaToTitle();
    }

    bool ValidateBindings()
    {
        bool ok = true;
        if (scenerioInfo == null) { Debug.LogError("Scenerio Info not assigned."); ok = false; }
        if (outcomeInfo == null) { Debug.LogError("Outcome Info not assigned."); ok = false; }
        if (swingTypeDropdown == null) { Debug.LogError("Dropdown_strategy (main) not assigned."); ok = false; }

        if (confirmButton == null) { Debug.LogError("Confirm Button not assigned."); ok = false; }
        if (swingButton == null) { Debug.LogError("SwingButton not assigned."); ok = false; }
        if (takeButton == null) { Debug.LogError("TakeButton not assigned."); ok = false; }
        if (newGameButton == null) { Debug.LogError("New Game Button not assigned."); ok = false; }

        if (base1B == null || base2B == null || base3B == null) { Debug.LogError("Base icons not assigned."); ok = false; }

        if (openDetailInfoButton == null) { Debug.LogError("Open Detail Info Button not assigned."); ok = false; }
        if (detailInfoCanvas == null) { Debug.LogError("Detail Info Canvas not assigned."); ok = false; }
        if (detailSwingDropdown == null) { Debug.LogError("Dropdown_strategy (detail) not assigned."); ok = false; }
        if (closeDetailButton == null) { Debug.LogError("Close Button not assigned."); ok = false; }
        if (recordTable == null) { Debug.LogError("Record Table (TMP_Text) not assigned."); ok = false; }
        if (recordScrollView == null) { Debug.LogError("Record Scroll View not assigned."); ok = false; }
        // recordContent / titleArea 可稍後再接
        return ok;
    }

    // ===================== Inning lifecycle =====================
    void GenerateNewInning()
    {
        inningOver = false;

        // dramatic last at-bat setup
        strikes = 0;
        balls = 0;
        outs = 2; // already 2 outs
        pitchNumber = 0;

        ourScore = Random.Range(3, 6);
        oppScore = ourScore + Random.Range(1, 3);

        on1B = Random.value > 0.5f;
        on2B = Random.value > 0.5f;
        on3B = Random.value > 0.5f;

        pitcherType = pitchTypes[Random.Range(0, pitchTypes.Length)];

        currentChoice = Choice.None;
        outcomeInfo.text = "";
        ResetChoiceHighlight();

        // 清空紀錄 + 建立表頭
        records.Clear();
        BuildHeader();                  // 先建表頭
        SafeSetRecordText(headerText);  // 顯示表頭

        state = AtBatState.Ready;
        SetUIForState();
        UpdateScenarioText();
        UpdateBaseIcons();
        SyncDetailDropdownFromMain();

        AutoScrollToBottom();
        AlignRecordAreaToTitle();
    }

    void EndInning(string finalLine)
    {
        inningOver = true;
        state = AtBatState.Complete;
        SetUIForState();
        outcomeInfo.text = finalLine;

        if (detailInfoCanvas != null)
            detailInfoCanvas.SetActive(true);

        AutoScrollToBottom();
    }

    void SetUIForState()
    {
        switch (state)
        {
            case AtBatState.Ready:
            case AtBatState.InProgress:
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = true;
                swingButton.interactable = true;
                takeButton.interactable = true;
                newGameButton.gameObject.SetActive(false);
                break;

            case AtBatState.Complete:
                confirmButton.interactable = false;
                swingButton.interactable = false;
                takeButton.interactable = false;
                newGameButton.gameObject.SetActive(true);
                newGameButton.interactable = true;
                break;
        }
    }

    // ===================== Detail Canvas =====================
    void ShowDetailCanvas()
    {
        if (detailInfoCanvas == null) return;
        SyncDetailDropdownFromMain();
        detailInfoCanvas.SetActive(true);
        AlignRecordAreaToTitle();
        AutoScrollToBottom();
    }

    void HideDetailCanvas()
    {
        if (detailInfoCanvas != null)
            detailInfoCanvas.SetActive(false);
    }

    // keep two dropdowns in sync (without feedback loop)
    bool syncing = false;
    void OnMainDropdownChanged(int v)
    {
        if (syncing) return;
        syncing = true;
        if (detailSwingDropdown != null) detailSwingDropdown.value = v;
        syncing = false;
        UpdateScenarioText();
    }

    void OnDetailDropdownChanged(int v)
    {
        if (syncing) return;
        syncing = true;
        if (swingTypeDropdown != null) swingTypeDropdown.value = v;
        syncing = false;
        UpdateScenarioText();
    }

    void SyncDetailDropdownFromMain()
    {
        if (detailSwingDropdown != null && swingTypeDropdown != null)
            detailSwingDropdown.value = swingTypeDropdown.value;
    }

    // ===================== Choice & execution =====================
    void SetChoice(Choice choice)
    {
        if (inningOver) return;

        currentChoice = choice;

        // 高亮被選中的按鈕
        if (swingButton != null) swingButton.image.color = (choice == Choice.Swing) ? Color.white : new Color(1, 1, 1, 0.6f);
        if (takeButton  != null) takeButton.image.color  = (choice == Choice.Take)  ? Color.white : new Color(1, 1, 1, 0.6f);

        state = AtBatState.InProgress;
        SetUIForState();
        UpdateScenarioText();
    }

    void ResetChoiceHighlight()
    {
        if (swingButton != null) swingButton.image.color = Color.white;
        if (takeButton  != null) takeButton.image.color  = Color.white;
    }

    void ExecutePitch()
    {
        if (inningOver) return;

        if (currentChoice == Choice.None)
        {
            outcomeInfo.text = "Choose Swing or Take first, then press \"Let's Go !\"";
            return;
        }

        string thisPitch = pitchTypes[Random.Range(0, pitchTypes.Length)];
        string actionLabel = (currentChoice == Choice.Swing) ? $"Swing ({GetSwingTypeLabel()})" : "Take";
        string result = (currentChoice == Choice.Take) ? ExecuteTake() : ExecuteSwing();

        pitchNumber++;
        AppendRecordLine(pitchNumber, thisPitch, actionLabel, result);

        if (outs >= 3)
        {
            EndInning($"Inning over — 3 outs. Final: {ourScore}:{oppScore}");
            return;
        }
        if (ourScore > oppScore)
        {
            EndInning($"Walk-off! We lead {ourScore}:{oppScore}!");
            return;
        }

        UpdateScenarioText();
        UpdateBaseIcons();

        AutoScrollToBottom();
    }

    // ----- Take: 55% strike, 45% ball -----
    string ExecuteTake()
    {
        float r = Random.value;
        if (r < 0.55f)
        {
            strikes++;
            outcomeInfo.text = "Strike (Taken)";
            CheckStrikeout();
            return "Strike (Taken)";
        }
        else
        {
            balls++;
            if (balls >= 4)
            {
                outcomeInfo.text = "Walk (force advance)";
                WalkAdvance();
                ResetCountForNextBatter();
                return "Walk";
            }
            outcomeInfo.text = "Ball";
            return "Ball";
        }
    }

    // ----- Swing probabilities with Swing & Miss + Foul -----
    // Power:  Hit 20%, HR 4%, Out 40%, Miss 24%, Foul 12%
    // Normal: Hit 24%, HR 2%, Out 42%, Miss 17%, Foul 15%
    string ExecuteSwing()
    {
        int swingType = (swingTypeDropdown != null) ? swingTypeDropdown.value : 1; // default Normal
        float r = Random.value;

        if (swingType == 0) // Power
        {
            if (r < 0.20f)                 { SingleAdvance(); outcomeInfo.text = "Hit (Power)";      ResetCountForNextBatter(); return "Hit"; }
            else if (r < 0.24f)            { Homerun();       outcomeInfo.text = "Homerun (Power)";  ResetCountForNextBatter(); return "Homerun"; }
            else if (r < 0.64f)            { outs++;          outcomeInfo.text = "Out (Power)";       ResetCountForNextBatter(); return "Out"; }
            else if (r < 0.88f)            { strikes++;       outcomeInfo.text = "Swing & Miss";      CheckStrikeout();          return "Swing & Miss"; }
            else                           { if (strikes < 2) strikes++; outcomeInfo.text = "Foul";  return "Foul"; }
        }
        else // Normal
        {
            if (r < 0.24f)                 { SingleAdvance(); outcomeInfo.text = "Hit (Normal)";     ResetCountForNextBatter(); return "Hit"; }
            else if (r < 0.26f)            { Homerun();       outcomeInfo.text = "Homerun (Normal)"; ResetCountForNextBatter(); return "Homerun"; }
            else if (r < 0.68f)            { outs++;          outcomeInfo.text = "Out (Normal)";     ResetCountForNextBatter(); return "Out"; }
            else if (r < 0.85f)            { strikes++;       outcomeInfo.text = "Swing & Miss";      CheckStrikeout();          return "Swing & Miss"; }
            else                           { if (strikes < 2) strikes++; outcomeInfo.text = "Foul";  return "Foul"; }
        }
        return "—";
    }

    // ===================== Baserunning & scoring =====================
    void SingleAdvance()
    {
        if (on3B) { ourScore++; on3B = false; }
        if (on2B) { on3B = true; on2B = false; }
        if (on1B) { on2B = true; on1B = false; }
        on1B = true;
    }

    void Homerun()
    {
        int runs = 1;
        if (on1B) { runs++; on1B = false; }
        if (on2B) { runs++; on2B = false; }
        if (on3B) { runs++; on3B = false; }
        ourScore += runs;
    }

    // simple walk-force logic（簡化）
    void WalkAdvance()
    {
        if (on1B && on2B && on3B)
        {
            ourScore++;
        }
        else
        {
            if (on2B && on1B && !on3B) on3B = true;
            if (on1B && !on2B)        on2B = true;
            on1B = true;
        }
    }

    void ResetCountForNextBatter()
    {
        strikes = 0;
        balls = 0;
        currentChoice = Choice.None;
        ResetChoiceHighlight();
    }

    void CheckStrikeout()
    {
        if (strikes >= 3)
        {
            outs++;
            outcomeInfo.text = "Strikeout!";
            ResetCountForNextBatter();
        }
    }

    // ===================== UI Helpers =====================
    void UpdateScenarioText()
    {
        string runners = (on1B || on2B || on3B) ? "Runners on " : "No runners on base";
        if (on1B) runners += "1st ";
        if (on2B) runners += "2nd ";
        if (on3B) runners += "3rd ";

        int diff = Mathf.Max(0, oppScore - ourScore);
        string choiceText = currentChoice == Choice.None ? "Choose Swing or Take, then press \"Let's Go !\"" :
                            currentChoice == Choice.Swing ? $"Selected: Swing ({GetSwingTypeLabel()})" : "Selected: Take";

        scenerioInfo.text =
            $"{runners}\n" +
            $"You trailed by {diff} runs ({ourScore}:{oppScore})\n" +
            $"outs : {outs}\n" +
            $"The pitcher's pitching type : {pitcherType}\n" +
            $"Count: {balls} Balls - {strikes} Strikes\n" +
            $"{choiceText}";
    }

    void UpdateBaseIcons()
    {
        SetBaseColor(base1B, on1B ? Color.red : Color.white);
        SetBaseColor(base2B, on2B ? Color.red : Color.white);
        SetBaseColor(base3B, on3B ? Color.red : Color.white);
    }

    void SetBaseColor(GameObject go, Color c)
    {
        if (go == null) return;
        var img = go.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    string GetSwingTypeLabel()
    {
        if (swingTypeDropdown == null || swingTypeDropdown.options.Count == 0) return "-";
        return swingTypeDropdown.options[swingTypeDropdown.value].text;
    }

    // ===================== Record Table =====================
    void BuildHeader()
    {
        headerText =
            Col("No",   COL_NO)    + " " +
            Col("Pitch",COL_PITCH) + " " +
            Col("Action",COL_ACTION) + " " +
            Col("Result",COL_RESULT) + "\n" +
            new string('-', COL_NO + 1 + COL_PITCH + 1 + COL_ACTION + 1 + COL_RESULT) +
            "\n\n";
    }

    void AppendRecordLine(int no, string pitch, string action, string result)
    {
        string line =
            Col("#" + no, COL_NO) + " " +
            Col(pitch, COL_PITCH) + " " +
            Col(action, COL_ACTION) + " " +
            Col(result, COL_RESULT);

        records.Add(line);

        // 每條紀錄之間空一行
        string body = string.Join("\n\n", records);
        SafeSetRecordText(headerText + body);

        AutoScrollToBottom();
    }

    string Col(string s, int width)
    {
        if (string.IsNullOrEmpty(s)) s = "";
        s = s.Trim();
        // 不截斷字，直接 pad，並給兩個字元緩衝
        return s.PadRight(width + 2, ' ');
    }

    void SafeSetRecordText(string content)
    {
        if (recordTable == null) return;

        recordTable.text = content;

        // 讓 TMP 先更新 preferredHeight（換行後的高度）
        recordTable.ForceMeshUpdate();
        float prefH = recordTable.preferredHeight;

        // 撐 Text 自己的 Rect（避免被裁）
        var textRT = recordTable.rectTransform;
        textRT.anchorMin = new Vector2(0, 1);
        textRT.anchorMax = new Vector2(1, 1);
        textRT.pivot     = new Vector2(0, 1);
        textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prefH);

        // 撐 Content 高度（再加一點餘裕）
        if (recordContent != null)
        {
            recordContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prefH + 20f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(recordContent);
        }

        if (recordScrollView != null)
        {
            Canvas.ForceUpdateCanvases();
            if (recordScrollView.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(recordScrollView.content);
        }
    }

    void AutoScrollToBottom()
    {
        if (recordScrollView == null) return;
        StartCoroutine(CoScrollBottomNextFrame());
    }

    IEnumerator CoScrollBottomNextFrame()
    {
        yield return null; // 等 layout 完成
        Canvas.ForceUpdateCanvases();
        if (recordScrollView.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(recordScrollView.content);
        recordScrollView.verticalNormalizedPosition = 0f; // 0 = 底部
        Canvas.ForceUpdateCanvases();
    }

    // 依 Viewport 對齊左右邊界（更穩定，不會把 Content 壓到太窄）
    // 若有指定 titleArea，僅取其左右 padding 參考；沒有就用預設 16。
    void AlignRecordAreaToTitle()
    {
        if (recordScrollView == null || recordTable == null || recordContent == null) return;

        // 取得 viewport（ScrollRect 允許自定 viewport，沒有就取第一個子物件）
        RectTransform viewport = recordScrollView.viewport != null
            ? recordScrollView.viewport
            : recordScrollView.GetComponent<RectTransform>();

        // 參考 title 左右邊距（可選）
        float padL = 16f, padR = 16f;
        if (titleArea != null)
        {
            // offsetMin.x 是左邊距、offsetMax.x 是 -右邊距
            padL = Mathf.Max(0f, titleArea.offsetMin.x + extraPadding);
            padR = Mathf.Max(0f, -titleArea.offsetMax.x + extraPadding);
        }

        // ---- Content 佈局：頂端拉伸，跟 viewport 等寬，左右留邊 ----
        var c = recordContent;
        c.SetParent(viewport, worldPositionStays: true); // 確保就在 viewport 之下
        c.anchorMin = new Vector2(0, 1);
        c.anchorMax = new Vector2(1, 1);
        c.pivot     = new Vector2(0.5f, 1f);
        c.offsetMin = new Vector2(padL, c.offsetMin.y);
        c.offsetMax = new Vector2(-padR, c.offsetMax.y);

        // 如果可用寬度太小（< 150），用預設邊距回復，避免變成每行 1 個字
        float usableWidth = viewport.rect.width - padL - padR;
        if (usableWidth < 150f)
        {
            padL = 16f; padR = 16f;
            c.offsetMin = new Vector2(padL, c.offsetMin.y);
            c.offsetMax = new Vector2(-padR, c.offsetMax.y);
        }

        // ---- 文字 Rect 也吃滿寬（頂左對齊）----
        var t = recordTable.rectTransform;
        t.anchorMin = new Vector2(0, 1);
        t.anchorMax = new Vector2(1, 1);
        t.pivot     = new Vector2(0, 1);
        t.offsetMin = new Vector2(0, t.offsetMin.y);
        t.offsetMax = new Vector2(0, t.offsetMax.y);
        recordTable.alignment = TextAlignmentOptions.TopLeft;

        // 可選：若有 VerticalLayoutGroup，保證子物件吃滿寬
        var vlg = c.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlWidth     = true;
            vlg.childForceExpandWidth = true;
            vlg.padding.left  = Mathf.RoundToInt(padL);
            vlg.padding.right = Mathf.RoundToInt(padR);
        }

        // 重新建佈局並捲到底
        Canvas.ForceUpdateCanvases();
        if (recordScrollView.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(recordScrollView.content);
        AutoScrollToBottom();
    }

}
