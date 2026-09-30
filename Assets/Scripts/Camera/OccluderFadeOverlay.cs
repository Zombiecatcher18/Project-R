using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Renderer))]
public class OccluderFadeOverlay : MonoBehaviour
{
    [Tooltip("Transparent material used for the fade overlay.")]
    public Material overlayMaterial;
    public float fadeSpeed = 5f;
    public float fadedAlpha = 0.25f;
    public float visibleAlpha = 1f;

    private Renderer baseRenderer;
    private Renderer overlayRenderer;
    private GameObject overlayGO;
    private MaterialPropertyBlock mpb;
    private Coroutine fadeRoutine;

    void Awake()
    {
        baseRenderer = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
    }

    // Called externally (e.g. by CameraObstructionHandler)
    public void FadeOut()
    {
        EnsureOverlay();
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        overlayRenderer.enabled = true;
        fadeRoutine = StartCoroutine(FadeTo(fadedAlpha));
    }

    public void FadeIn()
    {
        if (overlayRenderer == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeTo(visibleAlpha, disableAtEnd: true));
    }

    IEnumerator FadeTo(float target, bool disableAtEnd = false)
    {
        float startAlpha = GetAlpha();
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * fadeSpeed;
            float newAlpha = Mathf.Lerp(startAlpha, target, t);
            mpb.SetColor("_BaseColor", new Color(1f, 1f, 1f, newAlpha));
            overlayRenderer.SetPropertyBlock(mpb);
            yield return null;
        }

        if (disableAtEnd)
            overlayRenderer.enabled = false;
    }

    float GetAlpha()
    {
        overlayRenderer.GetPropertyBlock(mpb);
        Color c = Color.white;
        if (overlayRenderer.sharedMaterial.HasProperty("_BaseColor"))
            c = overlayRenderer.sharedMaterial.GetColor("_BaseColor");
        return c.a;
    }

    void EnsureOverlay()
    {
        if (overlayRenderer != null) return;

        overlayGO = new GameObject(gameObject.name + "_Overlay");
        overlayGO.transform.SetParent(transform, false);
        overlayGO.transform.localPosition = Vector3.zero;
        overlayGO.transform.localRotation = Quaternion.identity;
        overlayGO.transform.localScale = Vector3.one;

        // Copy mesh type
        var mf = GetComponent<MeshFilter>();
        if (mf)
        {
            var newMF = overlayGO.AddComponent<MeshFilter>();
            newMF.sharedMesh = mf.sharedMesh;
            overlayRenderer = overlayGO.AddComponent<MeshRenderer>();
        }
        else
        {
            var smr = GetComponent<SkinnedMeshRenderer>();
            if (smr)
            {
                var newSMR = overlayGO.AddComponent<SkinnedMeshRenderer>();
                newSMR.sharedMesh = smr.sharedMesh;
                newSMR.bones = smr.bones;
                newSMR.rootBone = smr.rootBone;
                overlayRenderer = newSMR;
            }
            else
            {
                Debug.LogWarning($"{name}: No MeshFilter or SkinnedMeshRenderer found.");
                return;
            }
        }

        overlayRenderer.sharedMaterial = overlayMaterial;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.enabled = false;
    }
}
