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
    public Button   confirmButton;
    public TMP_Text confirmButtonLabel;   // 「開始遊戲 / 上吧！！」按鈕上的 TMP_Text
    public Button   swingButton;
    public Button   takeButton;
    public Button   newGameButton;

    [Header("壘包圖示 (UI Image)")]
    public GameObject base1B;
    public GameObject base2B;
    public GameObject base3B;

    // ===================== Detail Panel =====================
    [Header("Detail Info UI")]
    public Button       openDetailInfoButton;
    public GameObject   detailInfoCanvas;
    public TMP_Dropdown detailSwingDropdown;
    public Button       closeDetailButton;
    public TMP_Text     recordTable;

    [Header("Record ScrollView")]
    public ScrollRect   recordScrollView;
    public RectTransform recordContent;
    public RectTransform titleArea;
    public float         extraPadding = 0f;
    public bool          tryAutoWire  = true;

    // ===================== 計數燈 =====================
    [Header("Count Lights")]
    public Image[] strikeLights = new Image[3];
    public Image[] ballLights   = new Image[4];
    public Color lightOff = Color.white;
    public Color strikeOn = new Color(1f, 0.85f, 0.2f); // 黃
    public Color ballOn   = new Color(0.2f, 1f, 0.4f);  // 綠

    // ===================== 結束演出 / 音效 =====================
    public enum ResultType { Strikeout, Walk, Hit, Homerun, Out, WalkOffWin, InningOver }

    [Header("End Sequence UI / SFX")]
    public GameObject endPanel;
    public TMP_Text   endTitle;
    public TMP_Text   endSubtitle;
    public Animator   endAnimator;
    public string     endTriggerName = "Show";

    public AudioSource sfx;      // SFX player (AudioSource)
    public AudioClip sfxCatch;   // 「接到/揮空/好壞球」等通用音
    public AudioClip sfxCheer;   // 歡呼
    public AudioClip sfxHit;     // 打到球
    public AudioClip sfxStart;   // 開場

    [Header("SFX 音高（可在 Inspector 調整）")]
    public float pitchTakeStrike = 0.90f;
    public float pitchTakeBall   = 1.05f;
    public float pitchSwingMiss  = 1.20f;
    public float pitchFoul       = 1.10f;
    public float pitchOut        = 0.85f;
    public float pitchStrikeout  = 0.80f;

    [Range(0.1f, 1f)] public float slowMoScale = 0.35f;
    public float slowMoDuration = 1.2f;
    public float endHoldSeconds = 2.0f;

    // ===================== 背景音樂 =====================
    [Header("Background Music (BGM)")]
    public AudioSource bgmSource;       // 指到 BGMPlayer 的 AudioSource（專放背景音）
    public AudioClip   bgmClip;         // 指到 background.wav
    [Range(0f, 1f)] public float bgmVolume     = 0.5f;
    public float             bgmFadeOutTime    = 0.25f;
    public float             bgmFadeInTime     = 0.35f;
    public float             bgmPauseExtra     = 0.80f;

    // ===================== 跑壘 UI（整合 UIRunnerQuick） =====================
    [Header("Runner UI")]
    public UIRunnerQuick uiRunner;      // 把 InfieldPanel 上的 UIRunnerQuick 拖進來

    // ===================== 狀態 =====================
    private enum Choice { None, Swing, Take }
    private Choice currentChoice = Choice.None;

    private int strikes = 0;
    private int balls   = 0;
    private int outs    = 0;

    private bool on1B = false;
    private bool on2B = false;
    private bool on3B = false;

    private int ourScore = 0;
    private int oppScore = 0;

    private bool inningOver = false;
    private int  pitchNumber = 0;

    // 中文球種
    private string        pitcherType  = "快速球";
    private readonly string[] pitchTypesZh = { "快速球", "滑球", "變速球", "曲球" };

    private enum AtBatState { Ready, InProgress, Complete }
    private AtBatState state = AtBatState.Ready;

    // 是否已按下「開始遊戲」
    private bool gameStarted = false;

    // 紀錄表
    private readonly List<string> records = new List<string>();
    private string headerText = "";

    // dropdown 同步旗標
    bool syncing = false;

    void Start()
    {
        if (!ValidateBindings()) return;

        if (tryAutoWire)
        {
            if (recordScrollView != null && recordContent == null)
                recordContent = recordScrollView.content;
        }

        // 綁定按鈕
        confirmButton.onClick.AddListener(OnConfirmPressed);
        swingButton  .onClick.AddListener(() => SetChoice(Choice.Swing));
        takeButton   .onClick.AddListener(() => SetChoice(Choice.Take));
        newGameButton.onClick.AddListener(GenerateNewInning);

        openDetailInfoButton .onClick.AddListener(ShowDetailCanvas);
        closeDetailButton     .onClick.AddListener(HideDetailCanvas);

        // Dropdown 同步
        if (swingTypeDropdown   != null) swingTypeDropdown  .onValueChanged.AddListener(OnMainDropdownChanged);
        if (detailSwingDropdown != null) detailSwingDropdown.onValueChanged.AddListener(OnDetailDropdownChanged);

        if (detailInfoCanvas != null) detailInfoCanvas.SetActive(false);

        // 紀錄表：一行顯示（不自動換行）
        if (recordTable != null)
        {
            recordTable.enableAutoSizing   = false;
            recordTable.fontSize           = 20f;
            recordTable.textWrappingMode   = TextWrappingModes.NoWrap;
            recordTable.overflowMode       = TextOverflowModes.Overflow;
            recordTable.alignment          = TextAlignmentOptions.TopLeft;
            recordTable.text               = "";
        }

        // 進入場前音效
        PlayOneShot(sfxStart);

        // 背景音樂
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip   = bgmClip;
            bgmSource.loop   = true;
            bgmSource.volume = bgmVolume;
            bgmSource.Play();
        }

        GenerateNewInning();
        AlignRecordAreaToTitle();
    }

    bool ValidateBindings()
    {
        bool ok = true;
        if (scenerioInfo == null)        { Debug.LogError("Scenerio Info 未指定"); ok = false; }
        if (outcomeInfo  == null)         { Debug.LogError("Outcome Info 未指定");  ok = false; }
        if (swingTypeDropdown == null)   { Debug.LogError("主畫面 Dropdown_strategy 未指定"); ok = false; }

        if (confirmButton == null)       { Debug.LogError("Confirm Button 未指定"); ok = false; }
        if (confirmButtonLabel == null)  { Debug.LogError("Confirm Button Label 未指定（指到按鈕內的TMP_Text）"); ok = false; }
        if (swingButton == null)         { Debug.LogError("SwingButton 未指定"); ok = false; }
        if (takeButton  == null)         { Debug.LogError("TakeButton 未指定");  ok = false; }
        if (newGameButton == null)       { Debug.LogError("New Game Button 未指定"); ok = false; }

        if (base1B == null || base2B == null || base3B == null) { Debug.LogError("壘包圖示未指定"); ok = false; }

        if (openDetailInfoButton == null){ Debug.LogError("Open Detail Info Button 未指定"); ok = false; }
        if (detailInfoCanvas == null)    { Debug.LogError("Detail Info Canvas 未指定"); ok = false; }
        if (detailSwingDropdown == null) { Debug.LogError("Detail Dropdown_strategy 未指定"); ok = false; }
        if (closeDetailButton == null)   { Debug.LogError("Close Button 未指定"); ok = false; }
        if (recordTable == null)         { Debug.LogError("Record Table 未指定"); ok = false; }
        if (recordScrollView == null)    { Debug.LogError("Record Scroll View 未指定"); ok = false; }
        return ok;
    }

    // ===================== Inning lifecycle =====================
    void GenerateNewInning()
    {
        inningOver  = false;

        strikes     = 0;
        balls       = 0;
        outs        = 2;
        pitchNumber = 0;

        ourScore = Random.Range(3, 6);
        oppScore = ourScore + Random.Range(1, 3);

        on1B = Random.value > 0.5f;
        on2B = Random.value > 0.5f;
        on3B = Random.value > 0.5f;

        pitcherType = pitchTypesZh[Random.Range(0, pitchTypesZh.Length)];

        currentChoice   = Choice.None;
        outcomeInfo.text = "";

        // 起始狀態：尚未開始 -> 只顯示「開始遊戲」，隱藏左右策略按鈕
        gameStarted = false;
        SetConfirmLabel("開始遊戲");
        SetStrategyButtonsVisible(false);
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

        // 起始隱藏跑者
        if (uiRunner != null) uiRunner.HideRunner();
    }

    void EndInning(string finalLine)
    {
        inningOver = true;
        state = AtBatState.Complete;
        SetUIForState();
        outcomeInfo.text = finalLine;

        if (detailInfoCanvas != null)
            detailInfoCanvas.SetActive(true);

        if (uiRunner != null)
            uiRunner.HideRunner();   // 收場隱藏跑者

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

                // 若尚未開始，只能看到「開始遊戲」
                SetStrategyButtonsVisible(gameStarted);
                swingButton.interactable = gameStarted;
                takeButton .interactable = gameStarted;

                newGameButton.gameObject.SetActive(false);
                break;

            case AtBatState.Complete:
                confirmButton.interactable = false;
                swingButton  .interactable = false;
                takeButton   .interactable = false;
                newGameButton.gameObject.SetActive(true);
                newGameButton.interactable = true;
                break;
        }
    }

    // ===================== 起始/確認按鈕邏輯 =====================
    void OnConfirmPressed()
    {
        if (!gameStarted)
        {
            // 第一次按：從「開始遊戲」切到正式出手狀態
            gameStarted = true;
            SetConfirmLabel("上吧！！");
            SetStrategyButtonsVisible(true);

            // 重新套用互動狀態
            SetUIForState();

            outcomeInfo.text = "";
            UpdateScenarioText();

            // 初次顯示跑者在本壘
            if (uiRunner != null) uiRunner.TeleportAdvance(0, 0);
            return;
        }

        // 已開始 -> 進入原本一次投球流程
        ExecutePitch();
    }

    void SetConfirmLabel(string text)
    {
        if (confirmButtonLabel != null) confirmButtonLabel.text = text;
    }

    void SetStrategyButtonsVisible(bool visible)
    {
        if (swingButton != null) swingButton.gameObject.SetActive(visible);
        if (takeButton  != null) takeButton .gameObject.SetActive(visible);
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
        if (inningOver || !gameStarted) return; // 未開始時禁止選擇

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
            outcomeInfo.text = "請先選擇「揮棒」或「看球」，再按「出手」！";
            return;
        }

        string thisPitchZh = pitchTypesZh[Random.Range(0, pitchTypesZh.Length)];
        string actionZh    = (currentChoice == Choice.Swing) ? GetSwingActionZh() : "看球";
        string resultZh    = (currentChoice == Choice.Take) ? ExecuteTake() : ExecuteSwing();

        pitchNumber++;
        AppendRecordLine(pitchNumber, thisPitchZh, actionZh, resultZh);

        if (outs >= 3)
        {
            string msg = $"半局結束 — 三出局。比數 {ourScore}:{oppScore}";
            EndInning(msg);
            FinishAtBat(ResultType.InningOver, msg);
            return;
        }
        if (ourScore > oppScore)
        {
            string msg = $"再見分！我們以 {ourScore}:{oppScore} 超前！";
            EndInning(msg);
            PlayOneShot(sfxCheer);
            FinishAtBat(ResultType.WalkOffWin, msg);
            return;
        }

        UpdateScenarioText();
        UpdateBaseIcons();
        AutoScrollToBottom();
    }

    // ----- Take: 55% 好球、45% 壞球 -----
    string ExecuteTake()
    {
        float r = Random.value;
        if (r < 0.55f)
        {
            strikes++;
            outcomeInfo.text = "好球（看）";
            PlayOneShot(sfxCatch, pitchTakeStrike);
            CheckStrikeout();
            UpdateCountLights();
            return "好球（看）";
        }
        else
        {
            balls++;
            PlayOneShot(sfxCatch, pitchTakeBall);
            if (balls >= 4)
            {
                outcomeInfo.text = "保送（推進）";
                WalkAdvance();
                FinishAtBat(ResultType.Walk, $"跑者推進。比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter();
                return "保送";
            }
            outcomeInfo.text = "壞球";
            UpdateCountLights();
            return "壞球";
        }
    }

    // ----- Swing 機率 -----
    // 全力：安打 20%、全壘打 4%、出局 40%、揮空 24%、界外 12%
    // 普通：安打 24%、全壘打 2%、出局 42%、揮空 17%、界外 15%
    string ExecuteSwing()
    {
        int swingType = (swingTypeDropdown != null) ? swingTypeDropdown.value : 1; // 0=全力, 1=普通
        float r = Random.value;

        if (swingType == 0) // 全力
        {
            if (r < 0.20f) { SingleAdvance(); outcomeInfo.text = "安打（全力）";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Hit, $"跑者前進。比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "安打"; }

            else if (r < 0.24f) { Homerun(); outcomeInfo.text = "全壘打（全力）";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Homerun, $"比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "全壘打"; }

            else if (r < 0.64f) { outs++; outcomeInfo.text = "出局（全力）";
                FinishAtBat(ResultType.Out, $"出局數：{outs}");
                ResetCountForNextBatter(); return "出局"; }

            else if (r < 0.88f) { strikes++; outcomeInfo.text = "揮空";
                PlayOneShot(sfxCatch, pitchSwingMiss);
                CheckStrikeout(); UpdateCountLights(); return "揮空"; }

            else { if (strikes < 2) strikes++; outcomeInfo.text = "界外";
                PlayOneShot(sfxCatch, pitchFoul);
                UpdateCountLights(); return "界外"; }
        }
        else // 普通
        {
            if (r < 0.24f) { SingleAdvance(); outcomeInfo.text = "安打（普通）";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Hit, $"跑者前進。比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "安打"; }

            else if (r < 0.26f) { Homerun(); outcomeInfo.text = "全壘打（普通）";
                PlayOneShot(sfxHit);
                FinishAtBat(ResultType.Homerun, $"比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "全壘打"; }

            else if (r < 0.68f) { outs++; outcomeInfo.text = "出局（普通）";
                FinishAtBat(ResultType.Out, $"出局數：{outs}");
                ResetCountForNextBatter(); return "出局"; }

            else if (r < 0.85f) { strikes++; outcomeInfo.text = "揮空";
                PlayOneShot(sfxCatch, pitchSwingMiss);
                CheckStrikeout(); UpdateCountLights(); return "揮空"; }

            else { if (strikes < 2) strikes++; outcomeInfo.text = "界外";
                PlayOneShot(sfxCatch, pitchFoul);
                UpdateCountLights(); return "界外"; }
        }
    }

    // 依 dropdown 取得「全力揮擊 / 普通揮擊」
    string GetSwingActionZh()
    {
        int v = (swingTypeDropdown != null) ? swingTypeDropdown.value : 1;
        return (v == 0) ? "全力揮擊" : "普通揮擊";
    }

    // ===================== 跑壘 / 得分 =====================
    void SingleAdvance()
    {
        if (on3B) { ourScore++; on3B = false; }
        if (on2B) { on3B = true; on2B = false; }
        if (on1B) { on2B = true; on1B = false; }
        on1B = true;

        if (uiRunner != null)
            uiRunner.TeleportAdvance(0, 1);   // 安打一壘
    }

    void Homerun()
    {
        int runs = 1;
        if (on1B) { runs++; on1B = false; }
        if (on2B) { runs++; on2B = false; }
        if (on3B) { runs++; on3B = false; }
        ourScore += runs;

        if (uiRunner != null)
            uiRunner.TeleportAdvance(0, 4);   // 全壘打：本壘->得分（繞四個壘）
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

        if (uiRunner != null)
            uiRunner.TeleportAdvance(0, 1);   // 保送：推進一壘
    }

    void ResetCountForNextBatter()
    {
        strikes = 0;
        balls   = 0;
        currentChoice = Choice.None;
        ResetChoiceHighlight();
        UpdateCountLights();
    }

    void CheckStrikeout()
    {
        if (strikes >= 3)
        {
            outs++;
            outcomeInfo.text = "三振出局！";
            FinishAtBat(ResultType.Strikeout, $"出局數：{outs}");
            ResetCountForNextBatter();
        }
    }

    // ===================== UI =====================
    void UpdateScenarioText()
    {
        string runners = (on1B || on2B || on3B) ? "壘上有人：" : "壘上無人";
        if (on1B) runners += " 一壘";
        if (on2B) runners += " 二壘";
        if (on3B) runners += " 三壘";

        int diff = Mathf.Max(0, oppScore - ourScore);
        string choiceText;

        if (!gameStarted)
            choiceText = "按下「開始遊戲」開始本打席";
        else
            choiceText = currentChoice == Choice.None ? "先選「揮棒」或「看球」，再按「出手」"
                        : (currentChoice == Choice.Swing ? $"已選：揮棒（{GetSwingActionZh()}）" : "已選：看球");

        scenerioInfo.text =
            $"{runners}\n" +
            $"目前落後 {diff} 分（{ourScore}:{oppScore}）\n" +
            $"出局數：{outs}\n" +
            $"投手球種：{pitcherType}\n" +
            $"球數：{balls} 壞 — {strikes} 好\n" +
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

    // ===================== 紀錄表 =====================
    void BuildHeader()
    {
        headerText = "編號  球種  動作  結果\n---------------------------\n\n";
    }

    void AppendRecordLine(int no, string pitchZh, string actionZh, string resultZh)
    {
        string line = $"#{no}  {pitchZh}  {actionZh}  {resultZh}";
        records.Add(line);

        string body = string.Join("\n\n", records);
        SafeSetRecordText(headerText + body);

        AutoScrollToBottom();
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
    }

    // ===================== 計數燈 =====================
    void UpdateCountLights()
    {
        for (int i = 0; i < strikeLights.Length; i++)
        {
            if (strikeLights[i] == null) continue;
            strikeLights[i].color = (i < strikes) ? strikeOn : lightOff;
        }
        for (int i = 0; i < ballLights.Length; i++)
        {
            if (ballLights[i] == null) continue;
            ballLights[i].color = (i < balls) ? ballOn : lightOff;
        }
    }

    // ===================== BGM 暫停/淡入淡出 =====================
    IEnumerator CoPauseBGMWithFade(float holdSeconds)
    {
        if (bgmSource == null) yield break;

        float startVol = bgmSource.volume;
        float t = 0f;
        while (t < bgmFadeOutTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, Mathf.Clamp01(t / bgmFadeOutTime));
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Pause();

        yield return new WaitForSecondsRealtime(holdSeconds + bgmPauseExtra);

        bgmSource.UnPause();
        t = 0f;
        while (t < bgmFadeInTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, Mathf.Clamp01(t / bgmFadeInTime));
            yield return null;
        }
        bgmSource.volume = bgmVolume;
    }

    // ===================== SFX & End Sequence =====================
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
            ResultType.Homerun     => "全壘打！",
            ResultType.Hit         => "安打！",
            ResultType.Strikeout   => "三振出局",
            ResultType.Walk        => "保送",
            ResultType.Out         => "出局",
            ResultType.WalkOffWin  => "再見分！",
            ResultType.InningOver  => "半局結束",
            _ => ""
        };

        switch (type)
        {
            case ResultType.Homerun:
                PlayOneShot(sfxHit);
                StartCoroutine(CoPauseBGMWithFade(slowMoDuration + endHoldSeconds));
                break;

            case ResultType.WalkOffWin:
                PlayOneShot(sfxCheer);
                StartCoroutine(CoPauseBGMWithFade(slowMoDuration + endHoldSeconds));
                break;

            case ResultType.Hit:
                PlayOneShot(sfxHit);
                break;

            case ResultType.Strikeout:
                PlayOneShot(sfxCatch, pitchStrikeout);
                break;

            case ResultType.Out:
                PlayOneShot(sfxCatch, pitchOut);
                break;

            case ResultType.Walk:
                PlayOneShot(sfxCatch, pitchTakeBall);
                break;

            case ResultType.InningOver:
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
            yield return null; // 等一幀，確保 Animator Ready
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
