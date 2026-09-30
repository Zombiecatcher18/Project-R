using UnityEngine;

public class SmoothNormals : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var mesh = GetComponent<MeshFilter>().mesh;
        var normals = mesh.normals;

        for (int i = 0; i < normals.Length; i++)
            normals[i] = normals[i].normalized;

        mesh.normals = normals;
    }
}
