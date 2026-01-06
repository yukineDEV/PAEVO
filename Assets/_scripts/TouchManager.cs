using UnityEngine;

public class TouchManager : MonoBehaviour
{
    void Update()
    {
        // Cek apakah ada input (Support Mouse Editor & Touch iPad)
        if (IsInputDetected())
        {
            // Ambil posisi input (Mouse atau Jari)
            Vector3 inputPos = GetInputPosition();
            
            // Konversi posisi layar ke posisi World (karena game 2D)
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(inputPos);

            // Tembakkan Raycast untuk mendeteksi bidak/board yang disentuh
            RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

            if (hit.collider != null)
            {
                // Ganti ini dengan logika game Pachisi-mu
                Debug.Log("Benda tersentuh: " + hit.collider.name);
                
                // Contoh: hit.collider.GetComponent<PionScript>().Gerak();
            }
        }
    }

    // Fungsi helper biar rapi
    bool IsInputDetected()
    {
        // True jika klik kiri mouse ATAU ada sentuhan jari pertama
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    Vector3 GetInputPosition()
    {
        // Prioritaskan Touch jika ada
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }
        // Jika tidak, pakai posisi Mouse
        return Input.mousePosition;
    }
}