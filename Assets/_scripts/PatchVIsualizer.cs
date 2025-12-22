using UnityEngine;
using System.Collections.Generic;

public class PathVisualizer : MonoBehaviour
{
    public Color pathColor = Color.yellow;
    public float sphereSize = 0.3f;
    
    // List titik jalan (otomatis terisi)
    [HideInInspector]
    public List<Transform> nodes = new List<Transform>();

    // Refresh otomatis saat ada perubahan di editor
    void OnValidate()
    {
        nodes.Clear();
        foreach (Transform child in transform)
        {
            nodes.Add(child);
        }
    }

    // Fungsi menggambar visual (HANYA BOLEH ADA SATU)
    void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        Gizmos.color = pathColor;

        // 1. Gambar Bola di Setiap Titik
        foreach (Transform node in nodes)
        {
            if (node != null)
            {
                 Gizmos.DrawSphere(node.position, sphereSize);
            }
        }

        // 2. Gambar Garis Penghubung (Jalur)
        if (nodes.Count < 2) return;

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            if (nodes[i] != null && nodes[i+1] != null)
            {
                Gizmos.DrawLine(nodes[i].position, nodes[i+1].position);
            }
        }

        // 3. FITUR LOOP: Sambungkan Titik Terakhir kembali ke Titik Pertama
        if (nodes[0] != null && nodes[nodes.Count - 1] != null)
        {
            Gizmos.DrawLine(nodes[nodes.Count - 1].position, nodes[0].position);
        }
    }
}