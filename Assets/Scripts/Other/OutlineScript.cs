using UnityEngine;

[DisallowMultipleComponent]
public class AnimeOutlineRenderer : MonoBehaviour
{
    public Material outlineMaterial;
    public float outlineWidth = 0.03f;
    public Color outlineColor = Color.black;

    private GameObject outlineObj;
    private Material runtimeMat;

    void Start()
    {
        // 1. Create duplicate object
        outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform);

        outlineObj.transform.localPosition = Vector3.zero;
        outlineObj.transform.localRotation = Quaternion.identity;
        outlineObj.transform.localScale = Vector3.one;

        // 2. Copy mesh
        MeshFilter sourceMF = GetComponent<MeshFilter>();
        MeshRenderer sourceMR = GetComponent<MeshRenderer>();

        MeshFilter outlineMF = outlineObj.AddComponent<MeshFilter>();
        outlineMF.sharedMesh = sourceMF.sharedMesh;

        MeshRenderer outlineMR = outlineObj.AddComponent<MeshRenderer>();
        
        // 3. Create runtime copy of the outline material
        runtimeMat = new Material(outlineMaterial);
        runtimeMat.SetFloat("_OutlineWidth", outlineWidth);
        runtimeMat.SetColor("_OutlineColor", outlineColor);
        
        outlineMR.material = runtimeMat;

        // 4. Make sure it only renders backfaces
        outlineMR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineMR.receiveShadows = false;
    }

    void LateUpdate()
    {
        // Ensure the outline matches object scale (critical!)
        outlineObj.transform.localScale = Vector3.one;
    }
}
