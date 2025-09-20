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
    [Range(0f, 1f)] public float viewportY = 0.85f; // 0~1，越大越靠近頂端
    public float cameraBandWidthWorld = 14f;        // 在鏡頭頂端的一條水平帶寬（世界座標）
    public float cameraBandHeightMin = 4.0f;        // 從頂端往上跳多高
    public float cameraBandHeightMax = 7.0f;

    [Header("Sequence")]
    public Vector2 intervalRange = new Vector2(0.12f, 0.22f); // 連發間隔（隨機）
    public Vector2 yOffsetRange  = new Vector2(3.5f, 6.5f);   // 區域內再往上偏移

    [Header("Limits & Debug")]
    public int maxConcurrent = 24;
    public bool verboseLogs = false;

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
        extra.gameObject.SetActive(false);
        _pool.Add(extra);
        return extra;
    }

    Vector3 RandomPos()
    {
        // 1) 有指定 areaCenter → 用它 + 區域亂數
        if (areaCenter != null)
        {
            Vector3 c = areaCenter.position;
            float x = Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float y = Random.Range(yOffsetRange.x, yOffsetRange.y);
            float z = (areaSize.z <= 0.01f) ? 0f : Random.Range(-areaSize.z * 0.5f, areaSize.z * 0.5f);
            return c + new Vector3(x, y, z);
        }

        // 2) 沒指定就用主鏡頭頂端的一條水平帶（確保看得到）
        if (useCameraTopBandIfNoCenter && Camera.main != null)
        {
            var cam = Camera.main;
            // 以視窗頂部某點為基準
            Vector3 baseWorld = cam.ViewportToWorldPoint(new Vector3(0.5f, viewportY, Mathf.Max(5f, cam.nearClipPlane + 5f)));
            float half = cameraBandWidthWorld * 0.5f;
            float x = Random.Range(-half, half);
            float up = Random.Range(cameraBandHeightMin, cameraBandHeightMax);
            float z = baseWorld.z; // 2D 可直接用 0
            return new Vector3(baseWorld.x + x, baseWorld.y + up, z);
        }

        // 3) 最後防呆
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

        ps.transform.position = RandomPos();

        // 若 prefab 的 Renderer 沒設 Sorting Layer，可以在這裡幫忙設
        var r = ps.GetComponent<Renderer>();
        if (r != null && r.sortingLayerName == "Default")
        {
            // 專案若沒有 "Effects" 這層，try-catch 不會中斷
            try { r.sortingLayerName = "Effects"; r.sortingOrder = 10; } catch { }
        }

        ps.gameObject.SetActive(true);
        ps.Play(true);
        StartCoroutine(DisableWhenDone(ps));
    }

    public void PlaySequence(int count = 8, float interval = -1f)
    {
        if (_seqCo != null) StopCoroutine(_seqCo);
        _seqCo = StartCoroutine(CoSequence(count, interval));
    }

    // ★ 這就是 Console 抱怨找不到的協程
    IEnumerator CoSequence(int count, float interval)
    {
        if (verboseLogs) Debug.Log($"[FireworksManager] PlaySequence x{count}");
        for (int i = 0; i < count; i++)
        {
            PlayOnce();
            float dt = (interval > 0f)
                ? interval
                : Random.Range(intervalRange.x, intervalRange.y);
            yield return new WaitForSeconds(dt);
        }
        _seqCo = null;
    }

    IEnumerator DisableWhenDone(ParticleSystem ps)
    {
        yield return new WaitWhile(() => ps.IsAlive(true));
        ps.gameObject.SetActive(false);
    }

    // 再見轟用的大場面
    public void PlayBigShow()
    {
        PlaySequence(14, 0.12f);
    }

    // Scene 視覺化
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.25f);
        Vector3 c = (areaCenter != null) ? areaCenter.position : transform.position;
        Gizmos.DrawCube(
            c + Vector3.up * ((yOffsetRange.x + yOffsetRange.y) * 0.5f),
            new Vector3(areaSize.x, Mathf.Abs(yOffsetRange.y - yOffsetRange.x), Mathf.Max(0.05f, areaSize.z))
        );
    }

    // 右鍵測試
    [ContextMenu("DEBUG/PlayOnce")]
    void CtxPlayOnce() => PlayOnce();

    [ContextMenu("DEBUG/PlaySequence (8)")]
    void CtxPlaySeq() => PlaySequence(8, -1f);
}
