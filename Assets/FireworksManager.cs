using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireworksManager : MonoBehaviour
{
    [Header("Prefabs & Pool")]
    public ParticleSystem fireworkPrefab;
    [Range(1, 64)] public int initialPoolSize = 12;

    [Header("Spawn Area (World Space)")]
    public Transform areaCenter;                 // 若為空，會採用主鏡頭上方的自動位置
    public Vector3 areaSize = new Vector3(12f, 4f, 0f); // XZ 範圍、Y 高度範圍

    [Header("Auto Camera Band (fallback when areaCenter == null)")]
    public bool useCameraTopBandIfNoCenter = true;
    [Range(0f, 1f)] public float viewportY = 0.80f; // 0~1，越大越靠近頂端
    public float cameraBandWidthWorld = 12f;        // 在鏡頭頂端的一條水平帶寬（世界座標）
    public float cameraBandHeightMin = 2.0f;        // 從頂端往上跳多高
    public float cameraBandHeightMax = 3.0f;

    [Header("Scale & Depth")]
    [Tooltip("整體放大倍率（Main.ScalingMode 必須是 Hierarchy）")]
    public float effectScale = 6f;
    [Tooltip("自動相機帶狀時與相機的距離（Perspective 下越小越大）")]
    public float spawnDistance = 10f;

    [Header("Sequence")]
    public Vector2 intervalRange = new Vector2(0.12f, 0.22f); // 連發間隔（隨機）
    public Vector2 yOffsetRange  = new Vector2(3.5f, 6.5f);   // 區域內再往上偏移

    [Header("Visibility Clamp")]
    public bool clampToCamera = true;
    public float visibleMarginWorld = 1.0f;
    public float approxBurstHeight = 3.5f;

    [Header("Limits & Debug")]
    public int maxConcurrent = 24;
    public bool verboseLogs = false;

    // ================== 新增：顏色控制 ==================
    public enum ColorMode
    {
        Palette,            // 從自訂色盤挑 1~2 色（可用 COL 漸層）
        HSV_Random,         // 用 HSV 範圍隨機
        GradientPresets     // 從預先做好的漸層中挑一個
    }

    [Header("Color Settings")]
    public ColorMode colorMode = ColorMode.Palette;

    [Tooltip("當 useColorOverLifetime = true 時，會把下面產生的顏色/漸層套到 Color over Lifetime；否則只設 Main.StartColor。")]
    public bool useColorOverLifetime = true;

    [Tooltip("若為 true，Palette 會隨機挑 2 色作為一條漸層（更像真實煙火色彩變化）。")]
    public bool paletteAsTwoColorGradient = true;

    [Tooltip("調色盤（可多加幾個：藍、黃、紫、橘等）。")]
    public Color[] palette = new Color[]
    {
        new Color(1f, 0.2f, 0.2f),   // 紅
        new Color(0.2f, 0.9f, 0.3f), // 綠
        new Color(0.25f, 0.5f, 1f),  // 藍
        new Color(1f, 0.85f, 0.25f), // 黃
        new Color(0.7f, 0.3f, 0.95f) // 紫
    };

    [Header("HSV Random (for ColorMode = HSV_Random)")]
    [Range(0f,1f)] public float hsvHueMin = 0f;
    [Range(0f,1f)] public float hsvHueMax = 1f;
    [Range(0f,1f)] public float hsvSatMin = 0.75f;
    [Range(0f,1f)] public float hsvSatMax = 1f;
    [Range(0f,1f)] public float hsvValMin = 0.8f;
    [Range(0f,1f)] public float hsvValMax = 1f;

    [Header("Gradient Presets (for ColorMode = GradientPresets)")]
    public Gradient[] gradientPresets;

    private readonly List<ParticleSystem> _pool = new List<ParticleSystem>();
    private Coroutine _seqCo;

    void Awake()
    {
        if (fireworkPrefab == null)
        {
            Debug.LogWarning("[FireworksManager] fireworkPrefab 未指定，將無法生成煙火。");
            return;
        }

        for (int i = 0; i < initialPoolSize; i++)
        {
            var ps = Instantiate(fireworkPrefab, transform);
            // 確保可被縮放、世界模擬
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ps.transform.localScale = Vector3.one * effectScale;
            ps.gameObject.SetActive(false);
            _pool.Add(ps);
        }
    }

    ParticleSystem GetOne()
    {
        foreach (var ps in _pool)
        {
            if (!ps.isPlaying && !ps.gameObject.activeSelf) return ps;
        }
        if (_pool.Count >= maxConcurrent) return null;

        var extra = Instantiate(fireworkPrefab, transform);
        var main = extra.main;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        extra.transform.localScale = Vector3.one * effectScale;
        extra.gameObject.SetActive(false);
        _pool.Add(extra);
        return extra;
    }

    Vector3 RandomPos()
    {
        if (areaCenter != null)
        {
            Vector3 c = areaCenter.position;
            bool isCameraCenter = areaCenter.GetComponent<Camera>() != null;

            float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float y = Random.Range(yOffsetRange.x, yOffsetRange.y);
            float z = isCameraCenter ? 0f :
                      (areaSize.z <= 0.01f ? c.z :
                       c.z + Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f));

            return new Vector3(c.x + x, c.y + y, z);
        }

        if (useCameraTopBandIfNoCenter && Camera.main != null)
        {
            var cam = Camera.main;
            float dist = Mathf.Clamp(spawnDistance, cam.nearClipPlane + 0.6f, cam.farClipPlane - 1f);
            Vector3 baseWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, viewportY, dist));

            float half = cameraBandWidthWorld * 0.5f;
            float x = Random.Range(-half, half);
            float up = Random.Range(cameraBandHeightMin, cameraBandHeightMax);

            float zWorld = cam.transform.position.z + dist * (cam.orthographic ? 0f : 1f);
            return new Vector3(baseWorld.x + x, baseWorld.y + up, zWorld);
        }

        return new Vector3(Random.Range(-2f, 2f), Random.Range(3f, 6f), 0f);
    }

    public void PlayOnce()
    {
        if (fireworkPrefab == null) return;

        var ps = GetOne();
        if (ps == null)
        {
            if (verboseLogs) Debug.Log("[FireworksManager] 已達到 maxConcurrent，忽略本次生成。");
            return;
        }

        Vector3 pos = RandomPos();
        if (clampToCamera) pos = ClampInsideCamera(pos);
        ps.transform.position = pos;

        // 排序層（避免被 UI 蓋住）
        var r = ps.GetComponent<Renderer>();
        if (r != null)
        {
            try { if (r.sortingLayerName == "Default") r.sortingLayerName = "Effects"; } catch {}
            if (r.sortingOrder < 100) r.sortingOrder = 100;
        }

        // 依當前 effectScale 更新縮放（預防你在遊戲中動態調整）
        ps.transform.localScale = Vector3.one * effectScale;

        // ★ 核心：套用隨機顏色（StartColor / Color over Lifetime）
        ApplyRandomColors(ps);

        ps.gameObject.SetActive(true);
        ps.Play(true);
        StartCoroutine(DisableWhenDone(ps));
    }

    public void PlaySequence(int count = 8, float interval = -1f)
    {
        if (_seqCo != null) StopCoroutine(_seqCo);
        _seqCo = StartCoroutine(CoSequence(count, interval));
    }

    IEnumerator CoSequence(int count, float interval)
    {
        if (verboseLogs) Debug.Log($"[FireworksManager] PlaySequence x{count}");
        for (int i = 0; i < count; i++)
        {
            PlayOnce();
            float dt = (interval > 0f) ? interval : Random.Range(intervalRange.x, intervalRange.y);
            yield return new WaitForSeconds(dt);
        }
        _seqCo = null;
    }

    IEnumerator DisableWhenDone(ParticleSystem ps)
    {
        yield return new WaitWhile(() => ps.IsAlive(true));
        ps.gameObject.SetActive(false);
    }

    public void PlayBigShow() => PlaySequence(14, 0.12f);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.25f);
        Vector3 c = (areaCenter != null) ? areaCenter.position : transform.position;
        Gizmos.DrawCube(
            c + Vector3.up * ((yOffsetRange.x + yOffsetRange.y) * 0.5f),
            new Vector3(areaSize.x, Mathf.Abs(yOffsetRange.y - yOffsetRange.x), Mathf.Max(0.05f, areaSize.z))
        );
    }

    [ContextMenu("DEBUG/PlayOnce")]  void CtxPlayOnce() => PlayOnce();
    [ContextMenu("DEBUG/PlaySequence (8)")] void CtxPlaySeq() => PlaySequence(8, -1f);

    // ---- Clamp ----
    Vector3 ClampInsideCamera(Vector3 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return worldPos;

        float near = cam.nearClipPlane;
        float far  = cam.farClipPlane;

        float distFromCam = cam.orthographic
            ? Mathf.Clamp(Mathf.Abs(worldPos.z - cam.transform.position.z), near + 0.5f, far - 0.5f)
            : Mathf.Clamp((worldPos - cam.transform.position).magnitude, near + 0.5f, far - 0.5f);

        Vector3 min = cam.ViewportToWorldPoint(new Vector3(0f, 0f, distFromCam));
        Vector3 max = cam.ViewportToWorldPoint(new Vector3(1f, 1f, distFromCam));

        float left   = min.x + visibleMarginWorld;
        float right  = max.x - visibleMarginWorld;
        float bottom = min.y + visibleMarginWorld;
        float top    = max.y - (visibleMarginWorld + approxBurstHeight);

        worldPos.x = Mathf.Clamp(worldPos.x, left, right);
        worldPos.y = Mathf.Clamp(worldPos.y, bottom, top);
        return worldPos;
    }

    // ================== 顏色產生與套用 ==================
    void ApplyRandomColors(ParticleSystem ps)
    {
        var main = ps.main;
        var colLifetime = ps.colorOverLifetime;
        colLifetime.enabled = false; // 預設先關掉，視設定再開

        switch (colorMode)
        {
            case ColorMode.Palette:
            {
                if (palette == null || palette.Length == 0)
                {
                    main.startColor = Color.white;
                    break;
                }

                if (useColorOverLifetime)
                {
                    // 用 1 或 2 色做一條漸層
                    Gradient g = new Gradient();
                    if (paletteAsTwoColorGradient && palette.Length >= 2)
                    {
                        Color a = palette[Random.Range(0, palette.Length)];
                        Color b = palette[Random.Range(0, palette.Length)];
                        g.SetKeys(
                            new GradientColorKey[] {
                                new GradientColorKey(a, 0f),
                                new GradientColorKey(b, 1f)
                            },
                            new GradientAlphaKey[] {
                                new GradientAlphaKey(a.a, 0f),
                                new GradientAlphaKey(0f, 1f) // 收尾淡出
                            }
                        );
                    }
                    else
                    {
                        Color c = palette[Random.Range(0, palette.Length)];
                        g.SetKeys(
                            new GradientColorKey[] {
                                new GradientColorKey(c, 0f),
                                new GradientColorKey(c, 1f)
                            },
                            new GradientAlphaKey[] {
                                new GradientAlphaKey(c.a, 0f),
                                new GradientAlphaKey(0f, 1f)
                            }
                        );
                    }

                    colLifetime.enabled = true;
                    colLifetime.color = new ParticleSystem.MinMaxGradient(g);
                    // StartColor 設白以免與 COL 疊色相乘失真
                    main.startColor = Color.white;
                }
                else
                {
                    // 直接給 StartColor（單色）
                    Color c = palette[Random.Range(0, palette.Length)];
                    main.startColor = new ParticleSystem.MinMaxGradient(c);
                }
                break;
            }

            case ColorMode.HSV_Random:
            {
                float h = Random.Range(hsvHueMin, hsvHueMax);
                float s = Random.Range(hsvSatMin, hsvSatMax);
                float v = Random.Range(hsvValMin, hsvValMax);
                Color c1 = Color.HSVToRGB(h, s, v);
                c1.a = 1f;

                if (useColorOverLifetime)
                {
                    // HSV 再做一點變化當尾端色
                    float h2 = Mathf.Repeat(h + Random.Range(0.05f, 0.18f), 1f);
                    Color c2 = Color.HSVToRGB(h2, s, Mathf.Clamp01(v * Random.Range(0.75f, 1f)));
                    c2.a = 0f; // 尾端淡出

                    Gradient g = new Gradient();
                    g.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(c1, 0f),
                            new GradientColorKey(c2, 1f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(1f, 0f),
                            new GradientAlphaKey(0f, 1f)
                        }
                    );
                    colLifetime.enabled = true;
                    colLifetime.color = new ParticleSystem.MinMaxGradient(g);
                    main.startColor = Color.white;
                }
                else
                {
                    main.startColor = new ParticleSystem.MinMaxGradient(c1);
                }
                break;
            }

            case ColorMode.GradientPresets:
            {
                if (gradientPresets != null && gradientPresets.Length > 0)
                {
                    Gradient g = gradientPresets[Random.Range(0, gradientPresets.Length)];
                    if (useColorOverLifetime)
                    {
                        colLifetime.enabled = true;
                        colLifetime.color = new ParticleSystem.MinMaxGradient(g);
                        main.startColor = Color.white;
                    }
                    else
                    {
                        // 取漸層起點色當 StartColor
                        var keys = g.colorKeys;
                        Color c = (keys != null && keys.Length > 0) ? keys[0].color : Color.white;
                        main.startColor = new ParticleSystem.MinMaxGradient(c);
                    }
                }
                else
                {
                    main.startColor = Color.white;
                }
                break;
            }
        }

        // 若你的粒子材質/Shader 會乘上顏色，以上就會生效。
        // 如果你還有 Trails Module，也可以同步顏色：
        var trails = ps.trails;
        if (trails.enabled)
        {
            // 讓尾跡跟著 Color over Lifetime（或 StartColor）走
            trails.colorOverLifetime = useColorOverLifetime ? colLifetime.color : main.startColor;
        }
    }
}
