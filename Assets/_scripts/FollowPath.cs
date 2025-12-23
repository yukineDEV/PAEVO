using UnityEngine;
using System.Collections.Generic;

public class FollowPath : MonoBehaviour
{
    [Header("Identitas Pion (WAJIB DIISI)")]
    // 0=Biru, 1=Merah, 2=Hijau, 3=Kuning
    public int ownerPlayerIndex; 

    [Header("Setup Jalur & Base")]
    public Transform pathParent;   
    public Transform baseNode; // Drag BasePoint masing-masing di sini!
    public int startIndex = 0;     
    public float speed = 5f;

    [Header("Setup Masuk Kandang")]
    public Transform entryNode;      
    public Transform homePathParent;
    
    [Header("Status Logic")]
    // JANGAN DICENTANG DI INSPECTOR! Biarkan False saat mulai.
    public bool isOut = false; 
    public bool isFinished = false; 
    public bool isMoving = false;
    public bool isReversing = false; 
    public bool isSliding = false; 

    // STATUS EFEK SKILL
    public bool hasShield = false; 
    public bool isFrozen = false;

    // DATA NAVIGASI
    public int currentPointIndex = 0; 
    public bool hasEnteredHome = false; 
    
    private int stepsRemaining = 0; 
    private List<Transform> waypoints = new List<Transform>(); 
    private List<Transform> mainPathWaypoints = new List<Transform>(); 
    private List<Transform> reversePath = new List<Transform>(); 
    private Transform slideTargetNode; 
    private int reverseIndex = 0;

    void Start()
    {
        InitWaypoints(); 
        
        // POSISIKAN PION DI BASE SAAT GAME MULAI
        if (baseNode != null) 
        {
            transform.position = baseNode.position;
        }
        else if (waypoints.Count > startIndex) 
        {
            transform.position = waypoints[startIndex].position;
        }
        
        // Reset index ke start
        currentPointIndex = startIndex; 
    }

    void OnMouseDown()
    {
        // Hubungkan klik mouse ke GameManager
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnPawnClicked(this);
        }
    }

    void InitWaypoints()
    {
        waypoints.Clear();
        mainPathWaypoints.Clear(); 
        
        if (pathParent != null)
        {
            foreach (Transform child in pathParent) {
                waypoints.Add(child);
                mainPathWaypoints.Add(child);
            }
        }
    }

    // --- LOGIKA KELUAR DARI BASE (RULE OF 6) ---
    public void LeaveBase()
    {
        if (isOut) return; // Kalau sudah keluar, abaikan
        
        isOut = true;
        isMoving = false;
        hasEnteredHome = false;
        
        // Pindah visual ke titik Start
        if (waypoints.Count > startIndex) 
        {
            transform.position = waypoints[startIndex].position;
        }
            
        currentPointIndex = startIndex;
        Debug.Log($"{name} KELUAR DARI KANDANG! (Status IsOut = True)");
    }

   // --- UPDATE FUNGSI INI DI FOLLOWPATH.CS ---
    public bool CheckPossibleMove(int steps)
    {
        // DEBUGGER: Cek kenapa ditolak
        if (isFinished) { return false; }
        if (isReversing) { return false; }
        if (isSliding) { return false; }
        if (isFrozen) { return false; }
        if (isMoving) { return false; }
        
        if (!isOut) { 
            // Jangan spam log kalau emang belum keluar
            return false; 
        }

        // Cek apakah Waypoints (Jalur) Kosong? (Sering terjadi saat Duplicate)
        if (waypoints == null || waypoints.Count == 0) {
            Debug.LogError($"⛔ ERROR: {name} tidak punya Jalur (Waypoints 0). Cek Inspector 'Path Parent'!");
            return false;
        }

        // Cek Sisa Langkah ke Home
        if (hasEnteredHome) {
            int stepsToEnd = (waypoints.Count - 1) - currentPointIndex;
            if (steps > stepsToEnd) {
                Debug.Log($"🚫 {name} ditolak: Langkah {steps} kejauhan (Sisa {stepsToEnd}).");
                return false; 
            }
        }

        // Kalau lolos semua, berarti AMAN
        return true; 
    }

    // --- EKSEKUSI JALAN ---
    public bool MoveSteps(int steps)
    {
        // Validasi ulang (Double Check)
        if (!CheckPossibleMove(steps)) return false;

        stepsRemaining = steps;
        isMoving = true;
        return true; 
    }

    // --- RESET KE BASE (EFEK DIMAKAN/KICK) ---
    public void ResetToBase()
    {
        if (hasShield) {
            Debug.Log($"🛡️ {gameObject.name} SELAMAT (Shield)!");
            hasShield = false; return;
        }

        isOut = false; 
        isMoving = false; 
        isFinished = false; 
        hasEnteredHome = false; 
        isSliding = false; 
        isReversing = false;
        hasShield = false; 
        isFrozen = false;

        currentPointIndex = startIndex; 
        
        // Reset jalur ke jalur utama
        waypoints.Clear(); 
        waypoints.AddRange(mainPathWaypoints);
        
        // Balik ke tempat duduk
        if (baseNode != null) transform.position = baseNode.position;
    }

    // --- SKILL: TELEPORT (SWAP) ---
    public void TeleportToPosition(int targetIndex, Vector3 worldPos)
    {
        currentPointIndex = targetIndex;
        hasEnteredHome = false; 
        
        waypoints.Clear(); 
        waypoints.AddRange(mainPathWaypoints);
        
        transform.position = worldPos;
    }

    // --- SKILL: SLIDE (MAJU/SAFE ZONE) ---
    public void StartSlideEffect(int targetIndex)
    {
        isMoving = false; isReversing = false; isSliding = true; 
        
        if (mainPathWaypoints.Count > targetIndex)
            slideTargetNode = mainPathWaypoints[targetIndex];
        
        hasEnteredHome = false; 
        currentPointIndex = targetIndex;
        
        waypoints.Clear(); 
        waypoints.AddRange(mainPathWaypoints);
    }

    // --- SKILL: REVERSE (MUNDUR/PULL) ---
    public void StartReverseEffect(int targetIndex, bool backToBase)
    {
        if (backToBase && hasShield) {
            Debug.Log($"🛡️ {gameObject.name} BLOCK MUNDUR (Shield)!");
            hasShield = false; return;
        }

        isMoving = false; 
        isReversing = true; 
        isSliding = false;
        
        reversePath.Clear();
        int currentTrack = currentPointIndex;
        
        // Logic mundur dari home path
        if (hasEnteredHome) {
            for (int i = currentTrack; i >= 0; i--)
                if(i < waypoints.Count) reversePath.Add(waypoints[i]);
            currentTrack = mainPathWaypoints.IndexOf(entryNode);
        }

        int safetyLoop = 0;
        // Cari jalur mundur di papan utama
        while (currentTrack != targetIndex && safetyLoop < 100) {
            if(currentTrack >= 0 && currentTrack < mainPathWaypoints.Count)
                reversePath.Add(mainPathWaypoints[currentTrack]);

            currentTrack--;
            if (currentTrack < 0) currentTrack = mainPathWaypoints.Count - 1; 
            safetyLoop++;
        }
        
        if (mainPathWaypoints.Count > targetIndex)
            reversePath.Add(mainPathWaypoints[targetIndex]);

        if (backToBase && baseNode != null) reversePath.Add(baseNode);

        reverseIndex = 0;
        
        // Reset status jika pulang ke base
        if (backToBase) {
            isOut = false; hasEnteredHome = false; currentPointIndex = startIndex;
            waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
            hasShield = false; 
        }
    }

    void Update()
    {
        // 1. Logic SLIDE
        if (isSliding) {
            if (slideTargetNode != null) {
                transform.position = Vector3.MoveTowards(transform.position, slideTargetNode.position, speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, slideTargetNode.position) < 0.05f) {
                    transform.position = slideTargetNode.position; isSliding = false; 
                }
            }
            return;
        }

        // 2. Logic REVERSE
        if (isReversing) {
            if (reverseIndex < reversePath.Count) {
                Transform target = reversePath[reverseIndex];
                transform.position = Vector3.MoveTowards(transform.position, target.position, (speed * 3) * Time.deltaTime);
                if (Vector3.Distance(transform.position, target.position) < 0.1f) reverseIndex++;
            }
            else 
            {
                isReversing = false;
                // PENTING: Update data lokasi setelah mundur selesai
                if (reversePath.Count > 0) {
                    Transform finalNode = reversePath[reversePath.Count - 1];
                    if (finalNode != baseNode) { 
                        int finalIndex = mainPathWaypoints.IndexOf(finalNode);
                        if (finalIndex != -1) {
                            currentPointIndex = finalIndex;
                            hasEnteredHome = false; 
                            waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
                        }
                    }
                }
            }
            return; 
        }

        // 3. Logic MOVE NORMAL
        if (!isMoving || isFinished) return;
        
        int nextIndex = currentPointIndex + 1;
        if (!hasEnteredHome && nextIndex >= waypoints.Count) nextIndex = 0; 
        
        Transform targetNode = waypoints[nextIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetNode.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetNode.position) < 0.05f) {
            transform.position = targetNode.position;
            
            // Cek masuk Home Path
            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null) 
                SwitchToHomePath(); 
            else 
                currentPointIndex = nextIndex;

            // Cek Finish
            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1) {
                isFinished = true; isMoving = false; 
                transform.localScale = Vector3.one * 0.5f; // Kecilin pion tanda finish
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
        foreach (Transform child in homePathParent) waypoints.Add(child);
        currentPointIndex = -1; 
    }
}