// Assets/Editor/WatercolorTextureGenerator.cs
using UnityEngine;
using UnityEditor;
using System.IO;

public class WatercolorTextureGenerator : MonoBehaviour
{
    private const string OUT_FOLDER = "Assets/Textures/Watercolor";

    [MenuItem("Tools/Watercolor Textures/Generate All")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(OUT_FOLDER)) Directory.CreateDirectory(OUT_FOLDER);

        GenerateNoise(512, 512, 4.0f, "noise.png");
        GenerateRamp(256, 16, 3, false, "ramp_soft.png");      // 3-step soft ramp
        GenerateRamp(256, 16, 2, true,  "ramp_hard.png");      // 2-step hard toon ramp
        GeneratePaper(1024, 1024, 6.0f, 0.4f, "paper.png");
        GenerateStain(512, 512, "stain.png");

        AssetDatabase.Refresh();
        Debug.Log("Watercolor textures generated in " + OUT_FOLDER);
    }

    [MenuItem("Tools/Watercolor Textures/Generate Noise")]
    public static void MenuGenerateNoise() { GenerateNoise(512,512,4.0f,"noise.png"); AssetDatabase.Refresh(); }

    [MenuItem("Tools/Watercolor Textures/Generate Ramp (soft)")]
    public static void MenuGenerateRampSoft() { GenerateRamp(256,16,3,false,"ramp_soft.png"); AssetDatabase.Refresh(); }

    [MenuItem("Tools/Watercolor Textures/Generate Paper")]
    public static void MenuGeneratePaper() { GeneratePaper(1024,1024,6.0f,0.4f,"paper.png"); AssetDatabase.Refresh(); }

    [MenuItem("Tools/Watercolor Textures/Generate Stain")]
    public static void MenuGenerateStain() { GenerateStain(512,512,"stain.png"); AssetDatabase.Refresh(); }

    // ---------- GENERATORS ----------

    public static void GenerateNoise(int width, int height, float scale, string filename)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width * scale;
                float v = (float)y / height * scale;

                // Tileable Perlin: sample four corners and blend:
                float n1 = Mathf.PerlinNoise(u, v);
                float n2 = Mathf.PerlinNoise(u + scale, v);
                float n3 = Mathf.PerlinNoise(u, v + scale);
                float n4 = Mathf.PerlinNoise(u + scale, v + scale);
                float t = Mathf.PerlinNoise(u * 0.5f + 2.0f, v * 0.5f - 1.0f); // small extra variation

                // simple tileable trick: combine and remap
                float n = (n1 + n2 + n3 + n4) * 0.25f * 0.9f + t * 0.1f;
                tex.SetPixel(x, y, new Color(n, n, n, 1.0f));
            }
        tex.Apply();
        SavePNG(tex, Path.Combine(OUT_FOLDER, filename));
    }

    public static void GenerateRamp(int width, int height, int steps, bool hardStep, string filename)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        // Example palette for 90s anime: muted midtones
        Color shadow = new Color(0.12f, 0.10f, 0.08f);
        Color mid = new Color(0.62f, 0.52f, 0.48f);
        Color highlight = new Color(0.94f, 0.88f, 0.8f);

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1);
            Color c;
            if (hardStep)
            {
                // quantize into 'steps' levels
                float q = Mathf.Floor(t * steps) / (steps - 1);
                c = Color.Lerp(shadow, highlight, q);
            }
            else
            {
                // smooth interpolation with discrete bands
                float band = t * (steps - 1);
                int i = Mathf.FloorToInt(band);
                float local = band - i;
                if (i == 0) c = Color.Lerp(shadow, mid, local);
                else c = Color.Lerp(mid, highlight, local);
            }

            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        SavePNG(tex, Path.Combine(OUT_FOLDER, filename));
    }

    public static void GeneratePaper(int width, int height, float noiseScale, float contrast, string filename)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        // base light paper color
        Color baseC = new Color(0.96f, 0.94f, 0.90f);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width * noiseScale;
                float v = (float)y / height * noiseScale;
                float n = Mathf.PerlinNoise(u, v);
                // increase contrast
                n = Mathf.Pow(n, 1.0f - contrast * 0.6f);
                float r = Mathf.Clamp01(baseC.r * (0.9f + 0.2f * n));
                float g = Mathf.Clamp01(baseC.g * (0.9f + 0.2f * n));
                float b = Mathf.Clamp01(baseC.b * (0.9f + 0.2f * n));
                tex.SetPixel(x, y, new Color(r, g, b, 1.0f));
            }
        tex.Apply();
        SavePNG(tex, Path.Combine(OUT_FOLDER, filename));
    }

    public static void GenerateStain(int width, int height, string filename)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        // clear to transparent
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) tex.SetPixel(x, y, Color.clear);

        // create one or more random blobs
        int blobs = Random.Range(1, 4);
        for (int b = 0; b < blobs; b++)
        {
            Vector2 center = new Vector2(Random.Range(0.2f, 0.8f) * width, Random.Range(0.2f, 0.8f) * height);
            float radius = Random.Range(width * 0.15f, width * 0.45f);
            Color stainColor = new Color(Random.Range(0.1f, 0.6f), Random.Range(0.05f, 0.45f), Random.Range(0.05f, 0.35f), 1.0f);

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01(dist / radius);
                    // falloff with some noise to get feathered, irregular edges
                    float edgeNoise = Mathf.PerlinNoise(x * 0.05f + b * 10f, y * 0.05f + b * 20f) * 0.5f + 0.5f;
                    float alpha = Mathf.SmoothStep(1f, 0f, t) * edgeNoise * 0.9f;
                    if (alpha < 0.01f) continue;

                    Color prev = tex.GetPixel(x, y);
                    // blend: premultiplied alpha-like
                    Color blended = Color.Lerp(prev, stainColor, alpha * stainColor.a);
                    blended.a = Mathf.Clamp01(prev.a + alpha * 0.9f);
                    tex.SetPixel(x, y, blended);
                }
        }

        tex.Apply();
        SavePNG(tex, Path.Combine(OUT_FOLDER, filename));
    }

    // ---------- UTIL ----------
    private static void SavePNG(Texture2D tex, string path)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Debug.Log("Saved texture: " + path);

        // Import and set texture settings
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = false; // default to linear for noise/paper; you'll set sRGB for color maps later
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Bilinear;
            ti.mipmapEnabled = false;
            AssetDatabase.WriteImportSettingsIfDirty(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
    }
}
