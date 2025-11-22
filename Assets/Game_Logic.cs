using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;   // ★ 新增：使用 VideoPlayer
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Text;   // ★ 新增：給 BuildOutsSummary 用

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

    [Header("Base Lights (optional)")]
    public BaseLight baseLight1B;
    public BaseLight baseLight2B;
    public BaseLight baseLight3B;
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

    // ★ 新增：出局類型（打擊出局 / 三振出局）
    private enum OutDetailType
    {
        BattedOut,   // 打出去被接殺、滾地出局
        Strikeout    // 三振
    }

    // ★ 新增：本半局的出局紀錄（依序存入）
    private List<OutDetailType> outDetails = new List<OutDetailType>();

    [Header("End Sequence UI / SFX")]
    public GameObject endPanel;
    public TMP_Text   endTitle;
    public TMP_Text   endSubtitle;
    public Animator   endAnimator;
    public string     endTriggerName = "Show";

    [Header("SFX Players & Clips")]
    public AudioSource sfx;      // SFX player (AudioSource)
    public AudioClip sfxCatch;   // 「接到/揮空/好壞球」等通用音
    public AudioClip sfxCheer;   // 歡呼
    public AudioClip sfxHit;     // ★打到球（命中）音效：請在 Inspector 綁定你的 hit
    [Tooltip("若 sfxHit 為空，會嘗試從 Resources.Load 這個路徑載入（可選）")]
    public string hitClipResourcePath = "Sound/hit";
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

    // >>> 輸球 BGM（sadmusic） <<<
    [Header("Sad Music (on loss)")]
    public AudioClip sadMusic;                 // 指到 Assets/Sound/sadmusic.mp3
    [Range(0f,1f)] public float sadMusicVolume = 1f;
    [Tooltip("sadmusic 播完後，額外暫停 BGM 的秒數")]
    public float sadHoldExtra = 0.5f;
    private Coroutine sadRoutine;
    [Tooltip("專用的悲傷音樂 AudioSource（建議在 Inspector 綁定；若空則會自動建立）")]
    public AudioSource sadSource;

    // ===================== 跑壘 UI（整合 UIRunnerQuick） =====================
    [Header("Runner UI")]
    public UIRunnerQuick uiRunner;      // 把 InfieldPanel 上的 UIRunnerQuick 拖進來

    // ===================== 背景切換（新增） =====================
    [Header("Background Switch")]
    public Image  backgroundImage;   // 指到 Hierarchy 的「Background」（有 Image 元件）
    public Sprite bgBaseball;        // 原本背景（例如：baseball_picture）
    public Sprite bgFirework;        // 全壘打時的煙火背景（例如：baseball_picture_firework）
    public Sprite bgSadscene;        // 輸球時的悲傷背景（例如：baseball_picture_sadscene）

    // ===================== 影片播放（投手 / 打者） =====================
    [Header("Pitch & Batter Video")]
    [Tooltip("投手投球 mp4 的 VideoPlayer")]
    public VideoPlayer pitcherVideoPlayer;
    [Tooltip("打者打擊 mp4 的 VideoPlayer")]
    public VideoPlayer batterVideoPlayer;
    [Tooltip("如果有做一個外層 Panel 包住兩個影片，可以丟在這裡（沒有也可留空）")]
    public GameObject videoOverlayRoot;

    // ★ 新增：單獨的影片相機
    public Camera videoCamera;

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

    // ★★★ 新增：避免非第3好球卻重播三振語音的旗標
    private bool strikeoutHandled = false;

    // ★★★ 新增：控制「主播還在講開場」時，暫時不要顯示開始按鈕
    private bool waitingForIntro = false;

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

    // ===================== 語音 VO =====================
    [Header("Voice (VO)")]
    public AudioSource voice;  // 建議獨立一個 AudioSource 給語音

    // 基礎事件
    public AudioClip[] voStartAtBat;      // 開始/進入打席
    public AudioClip[] voChooseFirst;     // 請先選擇揮棒或看球

    // 球種（可選）
    public AudioClip[] voFastball;        // 快速球
    public AudioClip[] voSlider;          // 滑球
    public AudioClip[] voChangeup;        // 變速球
    public AudioClip[] voCurve;           // 曲球

    // 單一投球結果
    public AudioClip[] voBall;            // 壞球
    public AudioClip[] voStrikeLooking;   // 好球（看）
    public AudioClip[] voSwingMiss;       // 揮空
    public AudioClip[] voFoul;            // 界外
    public AudioClip[] voHit;             // 安打
    public AudioClip[] voHomerun;         // 全壘打
    public AudioClip[] voOut;             // 出局
    public AudioClip[] voStrikeout;       // 三振出局
    public AudioClip[] voWalk;            // 保送

    // 跑壘/得分與比數
    public AudioClip[] voPushRunner;      // 跑者推進
    public AudioClip[] voBasesLoaded;     // 滿壘
    public AudioClip[] voTie;             // 追平
    public AudioClip[] voLead;            // 超前
    public AudioClip[] voWalkoff;         // 再見分

    // 結束
    public AudioClip[] voInningOver;      // 三出局 半局結束

    // 常見球數（可選）
    public AudioClip[] voCountFull;       // 滿球數
    public AudioClip[] voCountOneStrike;  // 一好球
    public AudioClip[] voCountTwoStrikes; // 兩好球
    public AudioClip[] voCountTwoOne;     // 兩好一壞
    public AudioClip[] voCountTwoBalls;   // 兩壞球
    public AudioClip[] voCountThreeBalls; // 三壞球

    // ======= 新增：開場比分說明用的片段語音 =======
    [Header("Commentator Intro (Score Line)")]
    [Tooltip("『現在是』")]
    public AudioClip segNow;                  // 現在是
    [Tooltip("『比』")]
    public AudioClip segBi;                   // 比
    [Tooltip("『目前落後』")]
    public AudioClip segCurrentlyBehind;      // 目前落後
    [Tooltip("『分』")]
    public AudioClip segFen;                  // 分
    [Tooltip("『最後一棒打者兩出局』")]
    public AudioClip segLastTwoOut;           // 最後一棒打者兩出局
    [Tooltip("『看能否扭轉局面？』")]
    public AudioClip segCanComeback;          // 看能否扭轉局面？
    [Tooltip("數字語音：index 0 對應 0、1 對應 1...")]
    public AudioClip[] segNumbers;            // 0~9 或 1~9 的數字

    // ========== VO 播報節奏控制 ==========
    [Header("Voice Timing")]
    [Tooltip("球種播報與結果播報之間的延遲（秒）")]
    public float voDelayBetween = 0.6f;
    private bool isPitching = false;

    // ===================== End Panel 顯示選項 =====================
    [Header("End Panel Options")]
    [Tooltip("全壘打時是否顯示 endPanel（預設 false = 不顯示）")]
    public bool showEndPanelOnHomerun = false;

    [Header("Pitch-by-Pitch (逐球) 顯示")]
    [Tooltip("輸球時是否仍自動顯示逐球（Detail）畫面。預設 false = 不顯示，只保留場上畫面。")]
    public bool showPitchByPitchOnLoss = false;
    [Tooltip("全壘打時是否自動把 Detail 面板關閉（如果當下有打開）")]
    public bool autoHideDetailOnHomerun = true;

    // ===================== Fireworks =====================
    [Header("Fireworks")]
    public FireworksManager fireworks;          // 拖進場景上的 FX_Fireworks（含 FireworksManager）
    [Range(1, 48)] public int fireworksCount = 10;
    public float fireworksInterval = 0.15f;

    // ========= VO 工具方法 =========
    void Speak(AudioClip[] bank, float pitch = 1f, float volume = 1f)
    {
        if (voice == null || bank == null || bank.Length == 0) return;
        var clip = bank[Random.Range(0, bank.Length)];
        if (clip == null) return;
        float oldPitch = voice.pitch;
        float oldVol   = voice.volume;
        voice.pitch  = pitch;
        voice.volume = oldVol * volume;
        voice.PlayOneShot(clip);
        voice.pitch  = oldPitch;
        voice.volume = oldVol;
    }

    void SpeakCountCommon()
    {
        if (balls == 3 && strikes == 2) { Speak(voCountFull); return; }
        if (strikes == 2 && balls == 1) { Speak(voCountTwoOne); return; }
        if (strikes == 2 && balls == 0) { Speak(voCountTwoStrikes); return; }
        if (strikes == 1 && balls == 0) { Speak(voCountOneStrike); return; }
        if (balls == 2 && strikes == 0) { Speak(voCountTwoBalls); return; }
        if (balls == 3 && strikes == 0) { Speak(voCountThreeBalls); return; }
    }

    void SpeakBasesIfLoaded()
    {
        if (on1B && on2B && on3B) Speak(voBasesLoaded);
    }

    void SpeakScoreSwing()
    {
        if (ourScore == oppScore) Speak(voTie);
        else if (ourScore > oppScore) Speak(voLead);
    }

    void SpeakPitchType(string zh)
    {
        if (zh == "快速球") Speak(voFastball);
        else if (zh == "滑球") Speak(voSlider);
        else if (zh == "變速球") Speak(voChangeup);
        else if (zh == "曲球") Speak(voCurve);
    }

    // =========================================================

    void Start()
    {
        // （可選）自動載入 Resources/Sound/hit.mp3 作為 sfxHit 後備方案
        if (sfxHit == null && !string.IsNullOrEmpty(hitClipResourcePath))
        {
            var maybe = Resources.Load<AudioClip>(hitClipResourcePath);
            if (maybe != null) sfxHit = maybe;
        }

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

        // 若沒手動指定，動態建立 sadSource（避免跟 BGM 共用）
        if (sadSource == null)
        {
            sadSource = gameObject.AddComponent<AudioSource>();
            sadSource.loop = false;
            sadSource.playOnAwake = false;
            sadSource.spatialBlend = 0f; // 2D
            sadSource.volume = 1f;
        }

        // ---------- 背景初始化（新增） ----------
        if (backgroundImage == null)
        {
            var bgGO = GameObject.Find("Background");
            if (bgGO != null) backgroundImage = bgGO.GetComponent<Image>();
        }
        if (backgroundImage != null && bgBaseball == null)
        {
            // 若未設定原圖，取當前 Background 上的圖當作原圖
            bgBaseball = backgroundImage.sprite;
        }
        if (backgroundImage != null && bgBaseball != null)
        {
            backgroundImage.sprite = bgBaseball; // 先套回原圖
        }
        // --------------------------------------

        // 如果有影片外層 Panel，起始先關掉
        if (videoOverlayRoot != null)
            videoOverlayRoot.SetActive(false);
        if (pitcherVideoPlayer != null)
            pitcherVideoPlayer.gameObject.SetActive(false);
        if (batterVideoPlayer != null)
            batterVideoPlayer.gameObject.SetActive(false);

        // ★ 一開始不要讓 VideoCamera 畫東西
        if (videoCamera != null)
            videoCamera.enabled = false;

        GenerateNewInning();

        // 自動抓同物件上的 BaseLight（若你沒手動拖）
        if (baseLight1B == null && base1B != null) baseLight1B = base1B.GetComponent<BaseLight>();
        if (baseLight2B == null && base2B != null) baseLight2B = base2B.GetComponent<BaseLight>();
        if (baseLight3B == null && base3B != null) baseLight3B = base3B.GetComponent<BaseLight>();

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

        if (sfx == null)                 { Debug.LogError("SFX AudioSource 未指定"); }
        if (sfxHit == null)              { Debug.LogWarning("sfxHit 尚未指定；請在 Inspector 綁定 hit 音檔，或把檔案放在 Resources/Sound/hit"); }

        return ok;
    }

    // ===================== Inning lifecycle =====================
    void GenerateNewInning()
    {
        // ★ 重新開始：立刻停掉所有音訊/協程、收結束面板，避免追平/全壘打/歡呼殘留
        StopAllAudioNow();
        ResetEndUI();
        isPitching = false;

        // ★ 清空上一局可能殘留的煙火粒子
        ResetFireworksNow();

        // ★ 保留：把悲傷音樂關掉並恢復 BGM（雙保險）
        StopSadNow();

        // ★ 重置背景為原本的球場圖（新增）
        if (backgroundImage != null && bgBaseball != null)
            backgroundImage.sprite = bgBaseball;

        // 影片相關也順便關掉
        if (videoOverlayRoot != null)
            videoOverlayRoot.SetActive(false);
        if (pitcherVideoPlayer != null)
            pitcherVideoPlayer.gameObject.SetActive(false);
        if (batterVideoPlayer != null)
            batterVideoPlayer.gameObject.SetActive(false);

        inningOver  = false;

        strikes     = 0;
        balls       = 0;
        outs        = 2;
        pitchNumber = 0;

        // ★ 重置三振旗標
        strikeoutHandled = false;

        // ★ 新增：清空本半局出局紀錄
        outDetails.Clear();

        ourScore = Random.Range(3, 6);
        oppScore = ourScore + Random.Range(1, 3);

        on1B = Random.value > 0.5f;
        on2B = Random.value > 0.5f;
        on3B = Random.value > 0.5f;

        pitcherType = pitchTypesZh[Random.Range(0, pitchTypesZh.Length)];

        currentChoice   = Choice.None;
        outcomeInfo.text = "";

        // 起始狀態：尚未開始
        gameStarted = false;

        // ★★★ 一開始要「等主播講完」，先把 waitingForIntro 打開 & 把開始按鈕藏起來
        waitingForIntro = true;
        SetConfirmLabel("開始遊戲");
        SetStrategyButtonsVisible(false);
        ResetChoiceHighlight();
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

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

        // >>> 播放開場比分解說語音（講完才會放出「開始遊戲」按鈕）
        StartCoroutine(CoPlayIntroSentence());
    }

    void EndInning(string finalLine)
    {
        inningOver = true;
        state = AtBatState.Complete;

        // ★ 新增：把「這半局最後是怎麼出局」串進文字
        string outsSummary = BuildOutsSummary();
        if (!string.IsNullOrEmpty(outsSummary))
        {
            finalLine += "\n" + outsSummary;
        }

        SetUIForState();
        outcomeInfo.text = finalLine;

        bool lost = (ourScore < oppScore);

        // 輸球就換成悲傷背景（原邏輯保留）
        if (backgroundImage != null && lost && bgSadscene != null)
            backgroundImage.sprite = bgSadscene;

        if (detailInfoCanvas != null)
            detailInfoCanvas.SetActive(!lost || showPitchByPitchOnLoss);

        if (uiRunner != null)
            uiRunner.HideRunner();

        AutoScrollToBottom();
    }


    void SetUIForState()
    {
        switch (state)
        {
            case AtBatState.Ready:
                // ★★★ Ready 狀態下，如果還在等主播開場，就不要顯示開始按鈕
                if (confirmButton != null)
                {
                    bool show = !waitingForIntro;
                    confirmButton.gameObject.SetActive(show);
                    confirmButton.interactable = show;
                }
                SetStrategyButtonsVisible(gameStarted);
                if (newGameButton != null)
                    newGameButton.gameObject.SetActive(false);
                break;

            case AtBatState.InProgress:
                if (confirmButton != null)
                {
                    confirmButton.gameObject.SetActive(true);
                    confirmButton.interactable = true;
                }
                SetStrategyButtonsVisible(gameStarted);
                if (newGameButton != null)
                    newGameButton.gameObject.SetActive(false);
                break;

            case AtBatState.Complete:
                if (confirmButton != null)
                {
                    confirmButton.interactable = false;
                }
                if (swingButton != null) swingButton.interactable = false;
                if (takeButton  != null) takeButton .interactable = false;
                if (newGameButton != null)
                {
                    newGameButton.gameObject.SetActive(true);
                    newGameButton.interactable = true;
                }
                break;
        }
    }

    // ===================== 起始/確認按鈕邏輯 =====================
    void OnConfirmPressed()
    {
        if (!gameStarted)
        {
            // 保險：切入新打席前把殘留音全部停掉
            StopAllAudioNow();

            // 第一次按：從「開始遊戲」切到正式出手狀態
            gameStarted = true;
            SetConfirmLabel("Go！！");
            SetStrategyButtonsVisible(true);

            // 重新套用互動狀態
            state = AtBatState.InProgress;
            SetUIForState();

            outcomeInfo.text = "";
            UpdateScenarioText();

            // 初次顯示跑者在本壘
            if (uiRunner != null) uiRunner.TeleportAdvance(0, 0);

            // 開場語音
            Speak(voStartAtBat);
            return;
        }

        // 已開始 → 這裡改成：先播影片，再真正丟球
        if (inningOver) return;
        if (isPitching) return; // 避免狂按

        StartCoroutine(CoPlayVideosThenExecutePitch());
    }

    // ★ 新增：先播「投手影片 → 打者影片」，播完再呼叫 ExecutePitch()
    IEnumerator CoPlayVideosThenExecutePitch()
    {
        isPitching = true;

        // 按鈕暫時不能按
        if (confirmButton != null)
            confirmButton.interactable = false;
        
        // ★ 播影片前打開 VideoCamera
        if (videoCamera != null)
            videoCamera.enabled = true;

        // 打開影片外框（如果有）
        if (videoOverlayRoot != null)
            videoOverlayRoot.SetActive(true);

        // 投手影片
        if (pitcherVideoPlayer != null && pitcherVideoPlayer.clip != null)
        {
            pitcherVideoPlayer.gameObject.SetActive(true);
            if (batterVideoPlayer != null)
                batterVideoPlayer.gameObject.SetActive(false);

            pitcherVideoPlayer.Stop();
            pitcherVideoPlayer.Play();

            // 等影片播完
            yield return new WaitUntil(() => !pitcherVideoPlayer.isPlaying);
        }

        // 打者影片
        if (batterVideoPlayer != null && batterVideoPlayer.clip != null)
        {
            if (pitcherVideoPlayer != null)
                pitcherVideoPlayer.gameObject.SetActive(false);

            batterVideoPlayer.gameObject.SetActive(true);
            batterVideoPlayer.Stop();
            batterVideoPlayer.Play();

            // 等影片播完
            yield return new WaitUntil(() => !batterVideoPlayer.isPlaying);
        }

        // 關掉影片 UI
        if (pitcherVideoPlayer != null)
            pitcherVideoPlayer.gameObject.SetActive(false);
        if (batterVideoPlayer != null)
            batterVideoPlayer.gameObject.SetActive(false);
        if (videoOverlayRoot != null)
            videoOverlayRoot.SetActive(false);

        // ★ 關掉 VideoCamera，回到正常畫面
        if (videoCamera != null)
            videoCamera.enabled = false;

        // 按鈕恢復可以按
        if (confirmButton != null)
            confirmButton.interactable = true;

        // 影片播完才真正進入「這球的邏輯」
        isPitching = false;   // 還原，讓 ExecutePitch 自己再跑它的 isPitching 流程
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

    // ===== 使用 Coroutine：分離「球種 →（延遲）→ 結果」=====
    void ExecutePitch()
    {
        if (inningOver) return;

        if (currentChoice == Choice.None)
        {
            outcomeInfo.text = "請先選擇「揮棒」或「看球」，再按「出手」！";
            Speak(voChooseFirst);
            return;
        }

        if (isPitching) return; // 避免重複出手
        isPitching = true;

        string thisPitchZh = pitchTypesZh[Random.Range(0, pitchTypesZh.Length)];
        pitchNumber++;

        StartCoroutine(CoPitchAndResult(thisPitchZh));
    }

    IEnumerator CoPitchAndResult(string thisPitchZh)
    {
        // 先播球種
        SpeakPitchType(thisPitchZh);

        // 等待設定的延遲
        yield return new WaitForSeconds(voDelayBetween);

        string actionZh = (currentChoice == Choice.Swing) ? GetSwingActionZh() : "看球";
        string resultZh = (currentChoice == Choice.Take) ? ExecuteTake() : ExecuteSwing();

        AppendRecordLine(pitchNumber, thisPitchZh, actionZh, resultZh);
        if (outs >= 3)
        {
            string msg = $"半局結束 — 三出局。比數 {ourScore}:{oppScore}";
            EndInning(msg);
            if (ourScore < oppScore) StartSadNow();  // 若落後，播 sad
            Speak(voInningOver);
            FinishAtBat(ResultType.InningOver, msg);
            isPitching = false;
            yield break;
        }
        if (ourScore > oppScore)
        {
            string msg = $"再見分！我們以 {ourScore}:{oppScore} 超前！";
            EndInning(msg);
            PlayOneShot(sfxCheer);
            Speak(voWalkoff);
            FinishAtBat(ResultType.WalkOffWin, msg);
            isPitching = false;
            yield break;
        }

        UpdateScenarioText();
        UpdateBaseIcons();
        AutoScrollToBottom();

        isPitching = false;
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
            Speak(voStrikeLooking);
            CheckStrikeout();
            UpdateCountLights();
            SpeakCountCommon();
            return "好球（看）";
        }
        else
        {
            balls++;
            PlayOneShot(sfxCatch, pitchTakeBall);
            Speak(voBall);
            if (balls >= 4)
            {
                outcomeInfo.text = "保送（推進）";
                WalkAdvance();
                Speak(voWalk);
                Speak(voPushRunner);
                SpeakBasesIfLoaded();
                SpeakScoreSwing();

                FinishAtBat(ResultType.Walk, $"跑者推進。比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter();
                return "保送";
            }
            outcomeInfo.text = "壞球";
            UpdateCountLights();
            SpeakCountCommon();
            return "壞球";
        }
    }

    // ----- Swing 機率 -----
    string ExecuteSwing()
    {
        int swingType = (swingTypeDropdown != null) ? swingTypeDropdown.value : 1; // 0=全力, 1=普通
        float r = Random.value;

        if (swingType == 0) // 全力
        {
            if (r < 0.20f)
            {
                SingleAdvance(); outcomeInfo.text = "安打（全力）";
                PlayOneShot(sfxHit);                    // ★命中：播放 hit
                Speak(voHit);
                Speak(voPushRunner);
                SpeakBasesIfLoaded();
                SpeakScoreSwing();

                FinishAtBat(ResultType.Hit, $"跑者前進。比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "安打";
            }
            else if (r < 0.28f)
            {
                Homerun(); outcomeInfo.text = "全壘打（全力）";
                PlayOneShot(sfxHit);                    // ★命中：播放 hit
                PlayOneShot(sfxCheer);
                Speak(voHomerun);
                SpeakScoreSwing();

                FinishAtBat(ResultType.Homerun, $"比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "全壘打";
            }
            else if (r < 0.44f)
            {
                // ★ 打出去被接殺/滾地出局
                RegisterOut(OutDetailType.BattedOut);

                outs++; outcomeInfo.text = "出局（全力）";
                Speak(voOut);
                StartSadNow();
                FinishAtBat(ResultType.Out, $"出局數：{outs}");
                ResetCountForNextBatter(); return "出局";
            }
            else if (r < 0.68f)
            {
                strikes++; outcomeInfo.text = "揮空";
                PlayOneShot(sfxCatch, pitchSwingMiss);
                Speak(voSwingMiss);
                CheckStrikeout(); UpdateCountLights(); SpeakCountCommon(); return "揮空";
            }
            else
            {
                if (strikes < 2) strikes++;
                outcomeInfo.text = "界外";
                PlayOneShot(sfxCatch, pitchFoul);
                Speak(voFoul);
                UpdateCountLights(); SpeakCountCommon(); return "界外";
            }
        }
        else // 普通
        {
            if (r < 0.24f)
            {
                SingleAdvance(); outcomeInfo.text = "安打（普通）";
                PlayOneShot(sfxHit);                    // ★命中：播放 hit
                Speak(voHit);
                Speak(voPushRunner);
                SpeakBasesIfLoaded();
                SpeakScoreSwing();

                FinishAtBat(ResultType.Hit, $"跑者前進。比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "安打";
            }
            else if (r < 0.29f)
            {
                Homerun(); outcomeInfo.text = "全壘打（普通）";
                PlayOneShot(sfxHit);                    // ★命中：播放 hit
                PlayOneShot(sfxCheer);
                Speak(voHomerun);
                SpeakScoreSwing();

                FinishAtBat(ResultType.Homerun, $"比數 {ourScore}:{oppScore}");
                ResetCountForNextBatter(); return "全壘打";
            }
            else if (r < 0.48f)
            {
                // ★ 打出去被接殺/滾地出局
                RegisterOut(OutDetailType.BattedOut);

                outs++; outcomeInfo.text = "出局（普通）";
                Speak(voOut);
                StartSadNow();
                FinishAtBat(ResultType.Out, $"出局數：{outs}");
                ResetCountForNextBatter(); return "出局";
            }
            else if (r < 0.65f)
            {
                strikes++; outcomeInfo.text = "揮空";
                PlayOneShot(sfxCatch, pitchSwingMiss);
                Speak(voSwingMiss);
                CheckStrikeout(); UpdateCountLights(); SpeakCountCommon(); return "揮空";
            }
            else
            {
                if (strikes < 2) strikes++;
                outcomeInfo.text = "界外";
                PlayOneShot(sfxCatch, pitchFoul);
                Speak(voFoul);
                UpdateCountLights(); SpeakCountCommon(); return "界外";
            }
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

        // ★ 全壘打 → 換成煙火背景（新增）
        if (backgroundImage != null && bgFirework != null)
            backgroundImage.sprite = bgFirework;

        // ★ 觸發煙火（一般全壘打）
        if (fireworks != null)
        {
            Debug.Log("[HR] Fireworks PlaySequence triggered.");
            fireworks.PlaySequence(fireworksCount, fireworksInterval);
        }
        else
        {
            Debug.LogWarning("[HR] fireworks == null，請把場景的 FX_Fireworks 拖到 Game_Logic.fireworks 欄位");
        }
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
        strikeoutHandled = false; // ★ 重置，下一位打者才可能再次三振
        currentChoice = Choice.None;
        ResetChoiceHighlight();
        UpdateCountLights();
    }

    void CheckStrikeout()
    {
        if (strikes > 3) strikes = 3;

        if (strikes == 3 && !strikeoutHandled)
        {
            strikeoutHandled = true;

            // ★ 新增：紀錄「三振出局」
            RegisterOut(OutDetailType.Strikeout);

            outs++;
            outcomeInfo.text = "三振出局！";
            Speak(voStrikeout);
            StartSadNow();
            FinishAtBat(ResultType.Strikeout, $"出局數：{outs}");
            ResetCountForNextBatter();
        }
        else
        {
            UpdateCountLights();
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
        if (baseLight1B != null) baseLight1B.SetActive(on1B);
        else SetBaseColor(base1B, on1B ? Color.red : Color.white);

        if (baseLight2B != null) baseLight2B.SetActive(on2B);
        else SetBaseColor(base2B, on2B ? Color.red : Color.white);

        if (baseLight3B != null) baseLight3B.SetActive(on3B);
        else SetBaseColor(base3B, on3B ? Color.red : Color.white);
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

    // >>> sadmusic：停止/播放/協程 <<<
    void StopSadNow()
    {
        if (sadRoutine != null)
        {
            StopCoroutine(sadRoutine);
            sadRoutine = null;
        }
        if (sadSource != null) sadSource.Stop();

        if (bgmSource != null)
        {
            if (!bgmSource.isPlaying)
            {
                bgmSource.clip = bgmClip;
                bgmSource.loop = true;
                bgmSource.volume = bgmVolume;
                bgmSource.Play();
            }
            else
            {
                bgmSource.volume = bgmVolume;
            }
        }
    }

    void StartSadNow()
    {
        if (sadSource == null || sadMusic == null) return;
        if (sadRoutine != null) { StopCoroutine(sadRoutine); sadRoutine = null; }
        sadSource.Stop();
        sadRoutine = StartCoroutine(CoPlaySadMusic());
    }

    IEnumerator CoPlaySadMusic()
    {
        if (bgmSource == null || sadSource == null || sadMusic == null)
        {
            sadRoutine = null;
            yield break;
        }

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

        sadSource.clip = sadMusic;
        sadSource.volume = sadMusicVolume;
        sadSource.Play();

        yield return new WaitWhile(() => sadSource.isPlaying);
        yield return new WaitForSecondsRealtime(sadHoldExtra);

        bgmSource.UnPause();
        t = 0f;
        while (t < bgmFadeInTime)
        {
            t += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, Mathf.Clamp01(t / bgmFadeInTime));
            yield return null;
        }
        bgmSource.volume = bgmVolume;

        sadRoutine = null;
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
                PlayOneShot(sfxCheer);
                StartCoroutine(CoPauseBGMWithFade(slowMoDuration + endHoldSeconds));
                if (autoHideDetailOnHomerun && detailInfoCanvas != null)
                    detailInfoCanvas.SetActive(false);
                break;

            case ResultType.WalkOffWin:
                PlayOneShot(sfxCheer);
                StartCoroutine(CoPauseBGMWithFade(slowMoDuration + endHoldSeconds));
                if (fireworks != null) fireworks.PlayBigShow();
                break;

            case ResultType.Hit:
                PlayOneShot(sfxHit);
                break;

            case ResultType.Strikeout:
                PlayOneShot(sfxCatch, pitchStrikeout);
                StartSadNow();
                break;

            case ResultType.Out:
                PlayOneShot(sfxCatch, pitchOut);
                StartSadNow();
                break;

            case ResultType.Walk:
                PlayOneShot(sfxCatch, pitchTakeBall);
                break;

            case ResultType.InningOver:
                if (ourScore < oppScore) StartSadNow();
                break;
        }

        bool showPanel = true;
        if (type == ResultType.Homerun && !showEndPanelOnHomerun)
            showPanel = false;

        StartCoroutine(CoEndSequence(title, subtitle, showPanel));
    }

    IEnumerator CoEndSequence(string title, string subtitle, bool showPanel = true)
    {
        if (endPanel != null && showPanel)
        {
            endPanel.SetActive(true);
            if (endTitle)    endTitle.text    = title;
            if (endSubtitle) endSubtitle.text = subtitle;
            yield return null;
            if (endAnimator != null && !string.IsNullOrEmpty(endTriggerName))
                endAnimator.SetTrigger(endTriggerName);
        }

        float old = Time.timeScale;
        Time.timeScale = slowMoScale;
        yield return new WaitForSecondsRealtime(slowMoDuration);
        Time.timeScale = old;

        yield return new WaitForSecondsRealtime(endHoldSeconds);

        if (endPanel != null && showPanel) endPanel.SetActive(false);

        state = AtBatState.Complete;
        SetUIForState();
    }

    // ===================== 立即停止所有音訊與重置結束 UI =====================
    void StopAllAudioNow()
    {
        StopAllCoroutines();
        sadRoutine = null;

        if (voice != null)    voice.Stop();
        if (sfx != null)      sfx.Stop();
        if (sadSource != null) sadSource.Stop();

        if (bgmSource != null)
        {
            bgmSource.volume = bgmVolume;
            if (!bgmSource.isPlaying)
            {
                if (bgmClip != null)
                {
                    bgmSource.clip = bgmClip;
                    bgmSource.loop = true;
                    bgmSource.Play();
                }
            }
        }
    }

    void ResetEndUI()
    {
        if (endPanel != null) endPanel.SetActive(false);

        if (endAnimator != null)
        {
            endAnimator.Rebind();

            if (endAnimator.isActiveAndEnabled)
            {
                endAnimator.Update(0f);
            }
        }
    }

    // ===================== 重設煙火（清除所有粒子） =====================
    void ResetFireworksNow()
    {
        if (fireworks == null) return;

        var allPS = fireworks.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allPS)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }
    }

    // ===================== 出局方式紀錄相關（新增） =====================

    void RegisterOut(OutDetailType t)
    {
        outDetails.Add(t);
    }

    string OutDetailToText(OutDetailType t)
    {
        switch (t)
        {
            case OutDetailType.BattedOut: return "打擊出局";
            case OutDetailType.Strikeout: return "三振出局";
            default: return "出局";
        }
    }

    string BuildOutsSummary()
    {
        if (outDetails == null || outDetails.Count == 0) return "";

        if (outs >= 3 && outDetails.Count == 1)
        {
            return "本半局第三個出局：" + OutDetailToText(outDetails[0]);
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("本半局出局方式：");
        for (int i = 0; i < outDetails.Count; i++)
        {
            if (i > 0) sb.Append("、");
            sb.Append(OutDetailToText(outDetails[i]));
        }
        return sb.ToString();
    }

    // ===================== 開場比分說明（組合語音） =====================

    IEnumerator CoPlayClip(AudioClip clip, float gap = 0.05f)
    {
        if (voice == null || clip == null) yield break;

        voice.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length + gap);
    }

    IEnumerator CoPlayNumber(int value)
    {
        if (voice == null || segNumbers == null || segNumbers.Length == 0)
            yield break;

        if (value >= 0 && value < segNumbers.Length && segNumbers[value] != null)
        {
            yield return CoPlayClip(segNumbers[value]);
        }
        else
        {
            string s = value.ToString();
            foreach (char ch in s)
            {
                int d = ch - '0';
                if (d >= 0 && d < segNumbers.Length && segNumbers[d] != null)
                {
                    yield return CoPlayClip(segNumbers[d]);
                }
            }
        }
    }

    IEnumerator CoPlayIntroSentence()
    {
        int xScore = oppScore;
        int yScore = ourScore;
        int diff   = Mathf.Max(0, xScore - yScore);

        // 稍微等一下，避免和 sfxStart 擠在一起
        yield return new WaitForSeconds(0.2f);

        // 若沒有落後，就不要講這段，直接開放按鈕
        if (diff <= 0)
        {
            waitingForIntro = false;
            SetUIForState();
            yield break;
        }

        // 「現在是 X 比 Y，目前 Z 分落後，最後一棒打者兩出局，看能否扭轉局面？」
        yield return CoPlayClip(segNow);                 // 現在是
        yield return CoPlayNumber(xScore);               // X
        yield return CoPlayClip(segBi);                  // 比
        yield return CoPlayNumber(yScore);               // Y
        yield return CoPlayClip(segCurrentlyBehind);     // 目前落後
        yield return CoPlayNumber(diff);                 // Z
        yield return CoPlayClip(segFen);                 // 分
        yield return CoPlayClip(segLastTwoOut);          // 最後一棒打者兩出局
        yield return CoPlayClip(segCanComeback);         // 看能否扭轉局面？

        // ★★★ 主播講完了，才開放「開始遊戲」按鈕
        waitingForIntro = false;
        SetUIForState();
    }
}
