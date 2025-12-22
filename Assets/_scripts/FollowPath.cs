using UnityEngine;
using System.Collections.Generic;

public class FollowPath : MonoBehaviour
{
    [Header("Setup Jalur Utama")]
    public Transform pathParent;   
    public int startIndex = 0;     
    public float speed = 5f;

    [Header("Setup Masuk Kandang")]
    public Transform entryNode;      
    public Transform homePathParent;
    
    [Header("Setup Base")]
    public Transform baseNode; 
    public bool isOut = false; 

    [Header("Status (Jangan Diubah Manual)")]
    public bool isFinished = false; 
    public bool isMoving = false;   
    public int currentPointIndex = 0; // Posisi matematika pion (0-51)
    public bool hasEnteredHome = false; 

    // Variable Internal
    private int stepsRemaining = 0; 
    private List<Transform> waypoints = new List<Transform>();

    void Start()
    {
        InitWaypoints(); 
        
        // Atur posisi awal visual
        if (baseNode != null)
        {
            transform.position = baseNode.position;
        }
        else if (waypoints.Count > startIndex)
        {
            transform.position = waypoints[startIndex].position;
        }
        
        currentPointIndex = startIndex; 
    }

    // Fungsi inisialisasi jalur (biar bisa dipanggil saat reset)
    void InitWaypoints()
    {
        waypoints.Clear();
        if (pathParent != null)
        {
            foreach (Transform child in pathParent)
            {
                waypoints.Add(child);
            }
        }
    }

    // 👇 FUNGSI PENTING: DIPANGGIL SAAT DIMAKAN MUSUH
    public void ResetToBase()
    {
        isOut = false;
        isMoving = false;
        isFinished = false;
        hasEnteredHome = false;
        
        // Kembalikan logika ke titik start
        currentPointIndex = startIndex; 
        InitWaypoints(); // Reset jalur ke jalan utama

        // Kembalikan visual ke kandang
        if (baseNode != null)
        {
            transform.position = baseNode.position;
        }

        Debug.Log(gameObject.name + " MATI & KEMBALI KE BASE! 💀");
    }

    // Fungsi Jalan (Return True jika berhasil gerak)
    public bool MoveSteps(int steps)
    {
        if (isFinished) return false; 

        // Logika Keluar Kandang
        if (!isOut)
        {
             if (steps == 6)
             {
                 isOut = true; 
                 // Teleport visual ke titik start jalan raya
                 if (waypoints.Count > startIndex)
                 {
                     transform.position = waypoints[startIndex].position;
                 }
                 return true; 
             }
             return false; 
        }

        // Logika Exact Win (Cek sisa langkah)
        if (hasEnteredHome)
        {
            int stepsToEnd = (waypoints.Count - 1) - currentPointIndex;
            if (steps > stepsToEnd)
            {
                Debug.Log($"KELEBIHAN! Butuh {stepsToEnd}, dapat {steps}. Diam.");
                return false; 
            }
        }

        stepsRemaining = steps;
        isMoving = true;
        return true; 
    }

    void Update()
    {
        if (!isMoving || isFinished) return;

        int nextIndex = currentPointIndex + 1;

        // Logika Looping Jalan Raya
        if (!hasEnteredHome)
        {
            if (nextIndex >= waypoints.Count) nextIndex = 0; 
        }

        Transform targetNode = waypoints[nextIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetNode.position, speed * Time.deltaTime);

        // SAAT TIBA DI SATU KOTAK
        if (Vector3.Distance(transform.position, targetNode.position) < 0.05f)
        {
            transform.position = targetNode.position;

            // Cek Masuk Jalur Kandang
            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null)
            {
                SwitchToHomePath(); 
            }
            else
            {
                currentPointIndex = nextIndex;
            }

            // Cek Finish
            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1)
            {
                isFinished = true;
                isMoving = false;
                Debug.Log(gameObject.name + " SUDAH FINISH! 🏆");
                transform.localScale = Vector3.one * 0.5f; 
                return;
            }

            stepsRemaining--; 

            if (stepsRemaining <= 0) isMoving = false;
        }
    }

    void SwitchToHomePath()
    {
        hasEnteredHome = true;
        waypoints.Clear();
        foreach (Transform child in homePathParent)
        {
            waypoints.Add(child);
        }
        currentPointIndex = -1; 
    }
}