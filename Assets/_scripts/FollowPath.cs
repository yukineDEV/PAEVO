using UnityEngine;
using System.Collections.Generic;

public class FollowPath : MonoBehaviour
{
    [Header("Setup Jalur Utama")]
    public Transform pathParent;   // MainPath (Jalur Putih)
    
    // 👇 INI YANG KAMU CARI! (Versi lama belum ada ini)
    public int startIndex = 0;     
    
    public float speed = 5f;

    [Header("Setup Masuk Kandang")]
    public Transform entryNode;      // Pintu belok
    public Transform homePathParent; // Jalur Kandang
    
    [Header("Debug Info")]
    public bool isMoving = false;
    public int stepsRemaining = 0; 
    public bool hasEnteredHome = false;

    private List<Transform> waypoints = new List<Transform>();
    private int currentPointIndex = 0;

    void Start()
    {
        // 1. Load Jalur Utama
        waypoints.Clear();
        if (pathParent != null)
        {
            foreach (Transform child in pathParent)
            {
                waypoints.Add(child);
            }
        }
        
        // 2. Teleport ke Posisi Start (Logic ini yang bikin pion gak numpuk di 0)
        if (waypoints.Count > startIndex)
        {
            currentPointIndex = startIndex; 
            transform.position = waypoints[currentPointIndex].position;
        }
    }

    public void MoveSteps(int steps)
    {
        stepsRemaining = steps;
        isMoving = true;
    }

    void Update()
    {
        if (!isMoving) return;

        int nextIndex = currentPointIndex + 1;

        // A. KALAU MASIH DI JALUR UTAMA
        if (!hasEnteredHome)
        {
            if (nextIndex >= waypoints.Count) nextIndex = 0; // Loop ke 0
        }
        // B. KALAU SUDAH DI KANDANG
        else 
        {
            if (nextIndex >= waypoints.Count)
            {
                isMoving = false;
                Debug.Log("Finish!");
                return;
            }
        }

        Transform targetNode = waypoints[nextIndex];

        transform.position = Vector3.MoveTowards(transform.position, targetNode.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetNode.position) < 0.05f)
        {
            transform.position = targetNode.position;

            // Cek Pintu Masuk
            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null)
            {
                Debug.Log("Masuk Kandang!");
                SwitchToHomePath(); 
            }
            else
            {
                currentPointIndex = nextIndex;
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