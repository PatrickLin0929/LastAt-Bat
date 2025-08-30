using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;

public class Game_Logic : MonoBehaviour
{
    // ===================== Main UI =====================
    [Header("Main UI")]
    public TMP_Text scenerioInfo;
    public TMP_Text outcomeInfo;
    public TMP_Dropdown swingTypeDropdown;

    [Header("Buttons")]
    public Button confirmButton;
    public Button swingButton;
    public Button takeButton;
    public Button newGameButton;

    [Header("Base Icons (UI Image)")]
    public GameObject base1B;
    public GameObject base2B;
    public GameObject base3B;

    // ===================== Detail Panel =====================
    [Header("Detail Info UI")]
    public Button openDetailInfoButton;
    public GameObject detailInfoCanvas;
    public TMP_Dropdown detailSwingDropdown;
    public Button closeDetailButton;
    public TMP_Text recordTable;

    [Header("Record ScrollView")]
    public ScrollRect recordScrollView;
    public RectTransform recordContent;
    public RectTransform titleArea;
    public float extraPadding = 0f;
    public bool tryAutoWire = true;

    // ===================== Lights =====================
    [Header("Count Lights")]
    public Image[] strikeLights = new Image[3];
    public Image[] ballLights   = new Image[4];
    public Color lightOff = Color.white;
    public Color strikeOn = new Color(1f, 0.85f, 0.2f); // 黃
    public Color ballOn   = new Color(0.2f, 1f, 0.4f);  // 綠

    // ===================== End Sequence / SFX =====================
    public enum ResultType { Strikeout, Walk, Hit, Homerun, Out, WalkOffWin, InningOver }

    [Header("End Sequence UI / SFX")]
    public GameObject endPanel;
    public TMP_Text endTitle;
    public TMP_Text endSubtitle;
    public Animator endAnimator;
    public string endTriggerName = "Show";

    public AudioSource sfx;      // SFX player (AudioSource)
    public AudioClip sfxCatch;   // 共用「未打擊 / 接到」音
    public AudioClip sfxCheer;   // 歡呼
    public AudioClip sfxHit;     // 打到球
    public AudioClip sfxStart;   // 開場

    [Header("SFX Pitches (可在 Inspector 微調)")]
    public float pitchTakeStrike = 0.90f;
    public float pitchTakeBall   = 1.05f;
    public float pitchSwingMiss  = 1.20f;
    public float pitchFoul       = 1.10f;
    public float pitchOut        = 0.85f;
    public float pitchStrikeout  = 0.80f;

    [Range(0.1f, 1f)] public float slowMoScale = 0.35f;
    public float slowMoDuration = 1.2f;
    public float endHoldSeconds = 2.0f;

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

    // record table columns
    private readonly List<string> records = new List<string>();
    private string headerText = "";
    const int COL_NO = 5;
    const int COL_PITCH = 8;
    const int COL_ACTION = 18;
    const int COL_RESULT = 10;

    // dropdown sync
    bool syncing = false;

    void Start()
    {
        if (!ValidateBindings()) return;

        if (tryAutoWire)
        {
            if (recordScrollView != null && recordContent == null)
                recordContent = recordScrollView.content;
        }

        // buttons
        swingButton.onClick.AddListener(() => SetChoice(Choice.Swing));
        takeButton.onClick.AddListener(() => SetChoice(Choice.Take));
        confirmButton.onClick.AddListener(ExecutePitch);
        newGameButton.onClick.AddListener(GenerateNewInning);

        // detail panel buttons
        openDetailInfoButton.onClick.AddListener(ShowDetailCanvas);
        closeDetailButton.onClick.AddListener(HideDetailCanvas);

        // dropdown sync
        if (swingTypeDropdown != null)
            swingTypeDropdown.onValueChanged.AddListener(OnMainDropdownChanged);
        if (detailSwingDropdown != null)
            detailSwingDropdown.onValueChanged.AddListener(OnDetailDropdownChanged);

        if (detailInfoCanvas != null) detailInfoCanvas.SetActive(false);

        // record table setting
        if (recordTable != null)
        {
            recordTable.enableAutoSizing = false;
            recordTable.fontSize = 20f;
            recordTable.textWrappingMode = TextWrappingModes.Normal;
            recordTable.overflowMode = TextOverflowModes.Overflow;
            recordTable.alignment = TextAlignmentOptions.TopLeft;
            recordTable.text = "";
        }

        // 進入場前音效
        PlayOneShot(sfxStart);

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
        return ok;
    }

    // ===================== Inning lifecycle =====================
    void GenerateNewInning()
    {
        inningOver = false;

        strikes = 0;
        balls = 0;
        outs = 2;
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

        // 清空紀錄 + 表頭
        records.Clear();
        BuildHeader();
        SafeSetRecordText(headerText);

        state = AtBatState.Ready;
        SetUIForState();
        UpdateScenarioText();
        UpdateBaseIcons();
        SyncDetailDropdownFromMain();
        UpdateCountLights();

        AutoScrollToBottom();
        AlignRecordAreaToTitle();

        // 新打席開場音效
        PlayOneShot(sfxStart);
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
        if (swingButton != null) swingButton.image.color = (choice == Choice.Swing) ? Color.white : new Color(1, 1, 1, 0.6f);
        if (takeButton  != null) takeButton .image.color = (choice == Choice.Take)  ? Color.white : new Color(1, 1, 1, 0.6f);

        state = AtBatState.InProgress;
        SetUIForState();
        UpdateScenarioText();
    }

    void ResetChoiceHighlight()
    {
        if (swingButton != null) swingButton.image.color = Color.white;
        if (takeButton  != null) takeButton .image.color = Color.white;
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
            string msg = $"Inning over — 3 outs. Final: {ourScore}:{oppScore}";
            EndInning(msg);
            FinishAtBat(ResultType.InningOver, msg);
            return;
        }
        if (ourScore > oppScore)
        {
            string msg = $"Walk-off! We lead {ourScore}:{oppScore}!";
            EndInning(msg);
            PlayOneShot(sfxCheer);
            FinishAtBat(ResultType.WalkOffWin, msg);
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
            PlayOneShot(sfxCatch, pitchTakeStrike);   // ★ 與 Swing 區分：較低音
            CheckStrikeout();
            UpdateCountLights();
            return "Strike (Taken)";
        }
        else
        {
            balls++;
            PlayOneShot(sfxCatch, pitchTakeBall);     // ★ Ball：稍高音
            if (balls >= 4)
            {
                outcomeInfo.text = "Walk (force advance)";
                WalkAdvance();
                FinishAtBat(ResultType.Walk, $"Bases advance. Score {ourScore}:{oppScore}");
                ResetCountForNextBatter();
                return "Walk";
            }
            outcomeInfo.text = "Ball";
            UpdateCountLights();
            return "Ball";
        }
    }

    // ----- Swing probabilities -----
    // Power:  Hit 20%, HR 4%, Out 40%, Miss 24%, Foul 12%
    // Normal: Hit 24%, HR 2%, Out 42%, Miss 17%, Foul 15%
    string ExecuteSwing()
    {
        int swingType = (swingTypeDropdown != null) ? swingTypeDropdown.value : 1; // default Normal
        float r = Random.value;

        if (swingType == 0) // Power
        {
            if (r < 0.20f) { SingleAdvance(); outcomeInfo.text = "Hit (Power)";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Hit, $"Runners advance. Score {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "Hit"; }

            else if (r < 0.24f) { Homerun(); outcomeInfo.text = "Homerun (Power)";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Homerun, $"Score now {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "Homerun"; }

            else if (r < 0.64f) { outs++; outcomeInfo.text = "Out (Power)";
                // 不在這裡播音效，交給 FinishAtBat -> 使用較低音的 catch
                FinishAtBat(ResultType.Out, $"Outs: {outs}");
                ResetCountForNextBatter(); return "Out"; }

            else if (r < 0.88f) { strikes++; outcomeInfo.text = "Swing & Miss";
                PlayOneShot(sfxCatch, pitchSwingMiss);    // ★ 揮空：最高音
                CheckStrikeout(); UpdateCountLights(); return "Swing & Miss"; }

            else { if (strikes < 2) strikes++; outcomeInfo.text = "Foul";
                PlayOneShot(sfxCatch, pitchFoul);         // ★ 界外：微高音
                UpdateCountLights(); return "Foul"; }
        }
        else // Normal
        {
            if (r < 0.24f) { SingleAdvance(); outcomeInfo.text = "Hit (Normal)";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Hit, $"Runners advance. Score {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "Hit"; }

            else if (r < 0.26f) { Homerun(); outcomeInfo.text = "Homerun (Normal)";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Homerun, $"Score now {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "Homerun"; }

            else if (r < 0.68f) { outs++; outcomeInfo.text = "Out (Normal)";
                FinishAtBat(ResultType.Out, $"Outs: {outs}");
                ResetCountForNextBatter(); return "Out"; }

            else if (r < 0.85f) { strikes++; outcomeInfo.text = "Swing & Miss";
                PlayOneShot(sfxCatch, pitchSwingMiss);
                CheckStrikeout(); UpdateCountLights(); return "Swing & Miss"; }

            else { if (strikes < 2) strikes++; outcomeInfo.text = "Foul";
                PlayOneShot(sfxCatch, pitchFoul);
                UpdateCountLights(); return "Foul"; }
        }
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
        UpdateCountLights();
    }

    void CheckStrikeout()
    {
        if (strikes >= 3)
        {
            outs++;
            outcomeInfo.text = "Strikeout!";
            FinishAtBat(ResultType.Strikeout, $"Outs: {outs}");
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
            Col("No",   COL_NO)      + " " +
            Col("Pitch",COL_PITCH)   + " " +
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

        string body = string.Join("\n\n", records);
        SafeSetRecordText(headerText + body);

        AutoScrollToBottom();
    }

    string Col(string s, int width)
    {
        if (string.IsNullOrEmpty(s)) s = "";
        s = s.Trim();
        return s.PadRight(width + 2, ' ');
    }

    void SafeSetRecordText(string content)
    {
        if (recordTable == null) return;

        recordTable.text = content;

        recordTable.ForceMeshUpdate();
        float prefH = recordTable.preferredHeight;

        var textRT = recordTable.rectTransform;
        textRT.anchorMin = new Vector2(0, 1);
        textRT.anchorMax = new Vector2(1, 1);
        textRT.pivot     = new Vector2(0, 1);
        textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, prefH);

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
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (recordScrollView.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(recordScrollView.content);
        recordScrollView.verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
    }

    // 依 Title/Viewport 對齊
    void AlignRecordAreaToTitle()
    {
        if (recordScrollView == null || recordTable == null || recordContent == null) return;

        RectTransform viewport = recordScrollView.viewport != null
            ? recordScrollView.viewport
            : recordScrollView.GetComponent<RectTransform>();

        float padL = 16f, padR = 16f;
        if (titleArea != null)
        {
            padL = Mathf.Max(0f, titleArea.offsetMin.x + extraPadding);
            padR = Mathf.Max(0f, -titleArea.offsetMax.x + extraPadding);
        }

        var c = recordContent;
        c.SetParent(viewport, true);
        c.anchorMin = new Vector2(0, 1);
        c.anchorMax = new Vector2(1, 1);
        c.pivot     = new Vector2(0.5f, 1f);
        c.offsetMin = new Vector2(padL, c.offsetMin.y);
        c.offsetMax = new Vector2(-padR, c.offsetMax.y);

        float usableWidth = viewport.rect.width - padL - padR;
        if (usableWidth < 150f)
        {
            padL = 16f; padR = 16f;
            c.offsetMin = new Vector2(padL, c.offsetMin.y);
            c.offsetMax = new Vector2(-padR, c.offsetMax.y);
        }

        var t = recordTable.rectTransform;
        t.anchorMin = new Vector2(0, 1);
        t.anchorMax = new Vector2(1, 1);
        t.pivot     = new Vector2(0, 1);
        t.offsetMin = new Vector2(0, t.offsetMin.y);
        t.offsetMax = new Vector2(0, t.offsetMax.y);
        recordTable.alignment = TextAlignmentOptions.TopLeft;

        var vlg = c.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            vlg.childControlWidth     = true;
            vlg.childForceExpandWidth = true;
            vlg.padding.left  = Mathf.RoundToInt(padL);
            vlg.padding.right = Mathf.RoundToInt(padR);
        }

        Canvas.ForceUpdateCanvases();
        if (recordScrollView.content != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(recordScrollView.content);
        AutoScrollToBottom();
    }

    // ===================== Lights =====================
    void UpdateCountLights()
    {
        // strikes
        for (int i = 0; i < strikeLights.Length; i++)
        {
            if (strikeLights[i] == null) continue;
            strikeLights[i].color = (i < strikes) ? strikeOn : lightOff;
        }
        // balls
        for (int i = 0; i < ballLights.Length; i++)
        {
            if (ballLights[i] == null) continue;
            ballLights[i].color = (i < balls) ? ballOn : lightOff;
        }
    }

    // ===================== SFX Helpers & End Sequence =====================
    void PlayOneShot(AudioClip clip, float pitch = 1f, float volume = 1f)
    {
        if (sfx == null || clip == null) return;
        float oldPitch = sfx.pitch;
        float oldVol   = sfx.volume;
        sfx.pitch  = pitch;
        sfx.volume = oldVol * volume;
        sfx.PlayOneShot(clip);
        sfx.pitch  = oldPitch;
        sfx.volume = oldVol;
    }

    void FinishAtBat(ResultType type, string subtitle)
    {
        string title = type switch
        {
            ResultType.Homerun     => "HOMERUN!",
            ResultType.Hit         => "HIT!",
            ResultType.Strikeout   => "STRIKEOUT",
            ResultType.Walk        => "WALK",
            ResultType.Out         => "OUT",
            ResultType.WalkOffWin  => "WALK-OFF!",
            ResultType.InningOver  => "INNING OVER",
            _ => ""
        };

        // 在這裡統一決定「一次」的 SFX，避免重複
        switch (type)
        {
            case ResultType.Homerun:
            case ResultType.Hit:
                PlayOneShot(sfxHit); break;

            case ResultType.Strikeout:
                PlayOneShot(sfxCatch, pitchStrikeout); break;

            case ResultType.Out:
                PlayOneShot(sfxCatch, pitchOut); break;

            case ResultType.Walk:
                PlayOneShot(sfxCatch, pitchTakeBall); break;

            case ResultType.WalkOffWin:
                PlayOneShot(sfxCheer); break;

            case ResultType.InningOver:
                // 無需特別音效，保留畫面演出
                break;
        }

        StartCoroutine(CoEndSequence(title, subtitle));
    }

    IEnumerator CoEndSequence(string title, string subtitle)
    {
        if (endPanel != null)
        {
            endPanel.SetActive(true);
            if (endTitle)    endTitle.text    = title;
            if (endSubtitle) endSubtitle.text = subtitle;
            // 等一幀再觸發，避免剛啟用時 Animator 還沒 ready
            yield return null;
            if (endAnimator != null && !string.IsNullOrEmpty(endTriggerName))
                endAnimator.SetTrigger(endTriggerName);
        }

        float old = Time.timeScale;
        Time.timeScale = slowMoScale;
        yield return new WaitForSecondsRealtime(slowMoDuration);
        Time.timeScale = old;

        yield return new WaitForSecondsRealtime(endHoldSeconds);

        if (endPanel != null) endPanel.SetActive(false);

        state = AtBatState.Complete;
        SetUIForState();
    }
}
