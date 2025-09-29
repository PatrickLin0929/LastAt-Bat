using UnityEngine;
using UnityEditor;
using System.IO;

public class MakeSoftCircleTexture : MonoBehaviour
{
    [MenuItem("Tools/Particles/Generate Soft Circle (256x256)")]
    static void MakeTex()
    {
        int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        // 產生「中心白、邊緣漸淡」的圓形 Alpha
        float radius = 0.48f;       // 圓半徑（0~0.5 之間）
        float feather = 0.08f;      // 羽化寬度
        var cols = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f) / size * 2f - 1f; // -1..1
            float ny = (y + 0.5f) / size * 2f - 1f;
            float d = Mathf.Sqrt(nx * nx + ny * ny); // 距離圓心
            float a = Mathf.InverseLerp(radius + feather, radius, d); // 邊緣羽化
            a = Mathf.Clamp01(a);
            cols[y * size + x] = new Color(1f, 1f, 1f, a); // 白色 + 漸層 Alpha
        }

        tex.SetPixels(cols);
        tex.Apply(false);

        // 存成 PNG
        var bytes = tex.EncodeToPNG();
        string dir = "Assets/Textures";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "particle_soft_circle_256.png");
        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        // 設定匯入參數
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.filterMode = FilterMode.Bilinear;
        ti.sRGBTexture = true;
        ti.SaveAndReimport();

        Debug.Log("Generated: " + path);
    }
}

