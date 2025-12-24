using UnityEngine;
using System.Collections.Generic;
using System.Collections; // Wajib ada untuk Coroutine

public class FollowPath : MonoBehaviour
{
    [Header("Identitas Pion (WAJIB DIISI)")]
    // 0=Biru, 1=Merah, 2=Hijau, 3=Kuning
    public int ownerPlayerIndex; 

    [Header("Setup Jalur & Base")]
    public Transform pathParent;   
    public Transform baseNode;     
    public int startIndex = 0;     
    
    [Header("Pengaturan Gerak & Audio")]
    public float speed = 5f;          // ✅ KECEPATAN GESER (Fixed: Ditambahkan kembali)
    public float jumpSpeed = 5f;      // Kecepatan lompat
    public float jumpHeight = 0.5f;   // Tinggi lompatan
    public AudioSource audioSource;   
    public AudioClip jumpSound;       

    [Header("Setup Masuk Kandang")]
    public Transform entryNode;      
    public Transform homePathParent;
    
    [Header("Status Logic")]
    public bool isOut = false; 
    public bool isFinished = false; 
    public bool isMoving = false;
    public bool isReversing = false; 
    public bool isSliding = false; 

    public bool hasShield = false; 
    public bool isFrozen = false;

    public int currentPointIndex = 0; 
    public bool hasEnteredHome = false; 
    
    private List<Transform> waypoints = new List<Transform>(); 
    private List<Transform> mainPathWaypoints = new List<Transform>(); 
    private List<Transform> reversePath = new List<Transform>(); 
    private Transform slideTargetNode; 
    private int reverseIndex = 0;

    void Start()
    {
        InitWaypoints(); 
        
        if (baseNode != null) transform.position = baseNode.position;
        else if (waypoints.Count > startIndex) transform.position = waypoints[startIndex].position;
        
        currentPointIndex = startIndex; 

        // ✅ AUTO DETECT AUDIO
        // Jika AudioSource belum di-drag, cari otomatis
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        
        // Jika JumpSound belum di-drag di script, coba ambil dari AudioSource
        if (jumpSound == null && audioSource != null) {
            jumpSound = audioSource.clip;
        }
    }

    void OnMouseDown()
    {
        if (GameManager.Instance != null && !isMoving && !isReversing && !isSliding)
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

    public void LeaveBase()
    {
        if (isOut) return;
        
        isOut = true;
        isMoving = false;
        hasEnteredHome = false;
        
        if (waypoints.Count > startIndex) transform.position = waypoints[startIndex].position;
        currentPointIndex = startIndex;
        Debug.Log($"{name} KELUAR DARI KANDANG!");

        // Mainkan suara saat keluar
        if(audioSource && jumpSound) audioSource.PlayOneShot(jumpSound);
    }

    public bool CheckPossibleMove(int steps)
    {
        if (isFinished || isReversing || isSliding || isFrozen || isMoving) return false; 
        if (!isOut) return false; 
        if (waypoints == null || waypoints.Count == 0) return false;

        if (hasEnteredHome) {
            int stepsToEnd = (waypoints.Count - 1) - currentPointIndex;
            if (steps > stepsToEnd) return false; 
        }
        return true; 
    }

    public bool MoveSteps(int steps)
    {
        if (!CheckPossibleMove(steps)) return false;
        StartCoroutine(MoveRoutine(steps));
        return true; 
    }

    // --- LOGIKA LOMPAT ---
    IEnumerator MoveRoutine(int steps)
    {
        isMoving = true;

        for (int i = 0; i < steps; i++)
        {
            int nextIndex = currentPointIndex + 1;
            if (!hasEnteredHome && nextIndex >= waypoints.Count) nextIndex = 0;
            
            Transform startNode = waypoints[currentPointIndex];
            Transform targetNode = waypoints[nextIndex];

            // 🔊 Play Sound
            if (audioSource != null && jumpSound != null) 
                audioSource.PlayOneShot(jumpSound);

            Vector3 startPos = transform.position;
            Vector3 endPos = targetNode.position;
            float journey = 0f;

            while (journey <= 1f)
            {
                journey += Time.deltaTime * jumpSpeed;
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, journey);
                // Efek Parabola (Lompat)
                float height = Mathf.Sin(journey * Mathf.PI) * jumpHeight;
                currentPos.y += height;
                transform.position = currentPos;
                yield return null; 
            }

            transform.position = endPos;

            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null) {
                SwitchToHomePath();
            } 
            else {
                currentPointIndex = nextIndex;
            }

            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1) {
                isFinished = true; 
                transform.localScale = Vector3.one * 0.5f; 
                Debug.Log("🎉 FINISH!");
                break; 
            }
        }
        isMoving = false;
    }

    void SwitchToHomePath()
    {
        hasEnteredHome = true;
        waypoints.Clear();
        foreach (Transform child in homePathParent) waypoints.Add(child);
        currentPointIndex = -1; 
    }

    public void ResetToBase()
    {
        if (hasShield) {
            Debug.Log($"🛡️ {gameObject.name} SELAMAT (Shield)!");
            hasShield = false; return;
        }

        StopAllCoroutines(); 
        isOut = false; isMoving = false; isFinished = false; 
        hasEnteredHome = false; isSliding = false; isReversing = false;
        hasShield = false; isFrozen = false;

        currentPointIndex = startIndex; 
        waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        if (baseNode != null) transform.position = baseNode.position;
    }

    public void TeleportToPosition(int targetIndex, Vector3 worldPos)
    {
        StopAllCoroutines();
        currentPointIndex = targetIndex;
        hasEnteredHome = false; 
        isMoving = false;
        waypoints.Clear(); 
        waypoints.AddRange(mainPathWaypoints);
        transform.position = worldPos;
    }

    public void StartSlideEffect(int targetIndex)
    {
        StopAllCoroutines();
        isMoving = false; isReversing = false; isSliding = true; 
        if (mainPathWaypoints.Count > targetIndex) slideTargetNode = mainPathWaypoints[targetIndex];
        hasEnteredHome = false; 
        currentPointIndex = targetIndex;
        waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
    }

    public void StartReverseEffect(int targetIndex, bool backToBase)
    {
        if (backToBase && hasShield) { hasShield = false; return; }

        StopAllCoroutines();
        isMoving = false; isReversing = true; isSliding = false;
        reversePath.Clear();

        int currentTrack = currentPointIndex;
        if (hasEnteredHome) {
            for (int i = currentTrack; i >= 0; i--)
                if(i < waypoints.Count) reversePath.Add(waypoints[i]);
            currentTrack = mainPathWaypoints.IndexOf(entryNode);
        }

        int safetyLoop = 0;
        while (currentTrack != targetIndex && safetyLoop < 100) {
            if(currentTrack >= 0 && currentTrack < mainPathWaypoints.Count)
                reversePath.Add(mainPathWaypoints[currentTrack]);
            currentTrack--;
            if (currentTrack < 0) currentTrack = mainPathWaypoints.Count - 1; 
            safetyLoop++;
        }
        
        if (mainPathWaypoints.Count > targetIndex) reversePath.Add(mainPathWaypoints[targetIndex]);
        if (backToBase && baseNode != null) reversePath.Add(baseNode);

        reverseIndex = 0;
        if (backToBase) {
            isOut = false; hasEnteredHome = false; currentPointIndex = startIndex;
            waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
            hasShield = false; 
        }
    }

    void Update()
    {
        // 1. Logic SLIDE (Sekarang aman karena variabel 'speed' sudah ada)
        if (isSliding) {
            if (slideTargetNode != null) {
                transform.position = Vector3.MoveTowards(transform.position, slideTargetNode.position, speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, slideTargetNode.position) < 0.05f) {
                    transform.position = slideTargetNode.position; isSliding = false; 
                }
            }
            return;
        }

        // 2. Logic REVERSE (Sekarang aman)
        if (isReversing) {
            if (reverseIndex < reversePath.Count) {
                Transform target = reversePath[reverseIndex];
                transform.position = Vector3.MoveTowards(transform.position, target.position, (speed * 3) * Time.deltaTime);
                if (Vector3.Distance(transform.position, target.position) < 0.1f) reverseIndex++;
            }
            else 
            {
                isReversing = false;
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
    }
}