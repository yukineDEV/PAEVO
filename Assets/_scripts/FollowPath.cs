using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FollowPath : MonoBehaviour
{
    [Header("Identitas Pion")]
    public int ownerPlayerIndex;

    [Header("Setup Jalur & Base")]
    public Transform pathParent;    
    public Transform baseNode;      
    public int startIndex = 0;      

    [Header("Pengaturan Gerak")]
    public float speed = 5f;
    public float jumpSpeed = 5f;
    public float jumpHeight = 0.5f;

    [Header("Audio")]
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
    [HideInInspector] public Vector3 originalScale;

    [Header("Animasi")]
    public Animator animator;
    [HideInInspector] public bool isRunningMove;

    private const string PARAM_IS_OUT     = "IsOut";
    private const string PARAM_IS_MOVING  = "IsMoving";
    private const string PARAM_IS_RUNNING = "IsRunning";
    private const string PARAM_DIRECTION  = "Direction";

    private List<Transform> waypoints = new List<Transform>();
    private List<Transform> mainPathWaypoints = new List<Transform>();
    private List<Transform> reversePath = new List<Transform>();
    private Transform slideTargetNode;
    private int reverseIndex = 0;
    private int finalReverseTargetIndex = -1;

    void Start()
    {
        InitWaypoints();
        originalScale = transform.localScale; 
        if (baseNode != null) transform.position = baseNode.position;
        else if (waypoints.Count > startIndex) transform.position = waypoints[startIndex].position;
        currentPointIndex = startIndex;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (jumpSound == null && audioSource != null) jumpSound = audioSource.clip;
        if (animator == null) animator = GetComponent<Animator>();
        if (animator != null) {
            animator.SetBool(PARAM_IS_OUT, isOut);
            animator.SetBool(PARAM_IS_MOVING, false);
            animator.SetBool(PARAM_IS_RUNNING, false);
            animator.SetInteger(PARAM_DIRECTION, 0); 
        }
    }

    void OnMouseDown() { if (GameManager.Instance != null && !isMoving && !isReversing && !isSliding) GameManager.Instance.OnPawnClicked(this); }

    void InitWaypoints()
    {
        waypoints.Clear(); mainPathWaypoints.Clear();
        if (pathParent != null) foreach (Transform child in pathParent) { waypoints.Add(child); mainPathWaypoints.Add(child); }
    }

    public Vector3 GetCurrentAnchorPosition() {
        if (waypoints != null && currentPointIndex >= 0 && currentPointIndex < waypoints.Count) return waypoints[currentPointIndex].position;
        return transform.position;
    }

    void SetMoveAnimation(bool moving, bool running) { if (animator) { animator.SetBool(PARAM_IS_MOVING, moving); animator.SetBool(PARAM_IS_RUNNING, running); } }
    void UpdateDirectionForStep(Transform from, Transform to) {
        if (!animator || !from || !to) return;
        Vector3 dir = (to.position - from.position);
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y)) animator.SetInteger(PARAM_DIRECTION, (dir.x > 0) ? 3 : 2);
        else animator.SetInteger(PARAM_DIRECTION, (dir.y > 0) ? 1 : 0);
    }
    public void PlayAttackAnimation() { if (animator) animator.SetTrigger("Attack"); }
    public void PlayHurtAnimation() { if (animator) animator.SetTrigger("Hurt"); }
    public void PlayDieAnimation() { if (animator) animator.SetTrigger("Die"); }

    public void LeaveBase()
    {
        if (isOut) return;
        isOut = true; isMoving = false; hasEnteredHome = false;
        
        waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        if (waypoints.Count > startIndex) { transform.position = waypoints[startIndex].position; currentPointIndex = startIndex; }

        // [DEBUGGER FIX] Info lebih jelas
        Debug.Log($"🟢 {name} (Player {ownerPlayerIndex}) KELUAR DARI KANDANG!"); 
        
        if (GameManager.Instance != null) GameManager.Instance.PlayMoveSound();
        if (animator != null) {
            animator.SetBool(PARAM_IS_OUT, true);
            if (waypoints.Count > startIndex + 1) UpdateDirectionForStep(waypoints[startIndex], waypoints[startIndex + 1]);
            SetMoveAnimation(false, false);
        }
    }

    public bool CheckPossibleMove(int steps)
    {
        if (isFrozen) { Debug.Log($"❄️ {name} sedang BEKU!"); return false; }
        if (isFinished || isReversing || isSliding || isMoving || !isOut || waypoints.Count == 0) return false;
        if (hasEnteredHome) {
            int stepsToEnd = (waypoints.Count - 1) - currentPointIndex;
            if (steps > stepsToEnd) return false;
        }
        return true;
    }

    public bool MoveSteps(int steps) {
        if (!CheckPossibleMove(steps)) return false;
        StartCoroutine(MoveRoutine(steps));
        return true;
    }

    IEnumerator MoveRoutine(int steps)
    {
        isMoving = true; SetMoveAnimation(true, isRunningMove);

        for (int i = 0; i < steps; i++)
        {
            // [CRASH FIX] Mencegah error jika index > waypoints.count
            if (currentPointIndex >= waypoints.Count) 
            {
                Debug.LogError($"🚨 ERROR INDEX: {name} ada di index {currentPointIndex} tapi jalur cuma {waypoints.Count}. Resetting...");
                currentPointIndex = waypoints.Count - 1;
            }

            // [SWAP FIX: Handle jika mendadak di gerbang]
            if (!hasEnteredHome && waypoints[currentPointIndex] == entryNode && homePathParent != null)
            {
                Debug.Log($"🔀 Efek Swap: {name} ada di Gerbang -> Masuk Home.");
                SwitchToHomePath();
                
                int nextIndexInside = 0;
                // Safety check lagi
                if (nextIndexInside < waypoints.Count)
                {
                    Transform targetNodeInside = waypoints[nextIndexInside];
                    UpdateDirectionForStep(transform, targetNodeInside);
                    if (GameManager.Instance) GameManager.Instance.PlayMoveSound();
                    
                    yield return MoveToTarget(targetNodeInside.position);
                    
                    currentPointIndex = nextIndexInside;
                    if (currentPointIndex >= waypoints.Count - 1) { HandleFinish(); break; }
                }
                continue; 
            }

            int nextIndex = currentPointIndex + 1;
            if (hasEnteredHome) {
                if (nextIndex >= waypoints.Count) { isFinished = true; HandleFinish(); break; }
            } else {
                if (nextIndex >= waypoints.Count) nextIndex = 0;
            }

            Transform startNode = waypoints[currentPointIndex];
            Transform targetNode = waypoints[nextIndex];

            UpdateDirectionForStep(startNode, targetNode);
            if (GameManager.Instance) GameManager.Instance.PlayMoveSound();

            yield return MoveToTarget(targetNode.position);
            transform.position = targetNode.position;

            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null)
            {
                SwitchToHomePath();
                currentPointIndex = -1; // Reset agar next loop mulai dari 0
            }
            else
            {
                currentPointIndex = nextIndex;
            }

            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1) { HandleFinish(); break; }
        }

        isMoving = false; SetMoveAnimation(false, false);
    }

    IEnumerator MoveToTarget(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        float journey = 0f;
        while (journey <= 1f) {
            journey += Time.deltaTime * jumpSpeed;
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, journey);
            currentPos.y += Mathf.Sin(journey * Mathf.PI) * jumpHeight;
            transform.position = currentPos;
            yield return null;
        }
        transform.position = targetPos;
    }

    void HandleFinish() {
        isFinished = true; transform.localScale = Vector3.one * 0.5f; 
        Debug.Log($"🏁 {name} FINISH!");
        if (GameManager.Instance) GameManager.Instance.PlayFinishedSound();
    }

    void SwitchToHomePath() {
        hasEnteredHome = true;
        waypoints.Clear();
        foreach (Transform child in homePathParent) waypoints.Add(child);
        currentPointIndex = -1;
    }

    public void TeleportToPosition(int targetIndex, Vector3 worldPos)
    {
        StopAllCoroutines();
        hasEnteredHome = false; isMoving = false; isReversing = false; isSliding = false;
        
        // Reset jalur ke Main Path agar index valid
        waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        
        if (targetIndex >= waypoints.Count) targetIndex = 0;
        if (targetIndex < 0) targetIndex = 0;
        currentPointIndex = targetIndex;
        
        transform.position = worldPos;
        Debug.Log($"📍 {name} Teleport ke Index: {currentPointIndex}");
    }

    public void StartSlideEffect(int targetIndex)
    {
        StopAllCoroutines(); isMoving = false; isReversing = false; isSliding = true;
        if (mainPathWaypoints.Count > targetIndex) slideTargetNode = mainPathWaypoints[targetIndex];
        hasEnteredHome = false; currentPointIndex = targetIndex;
        waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        if (slideTargetNode != null) UpdateDirectionForStep(transform, slideTargetNode);
        SetMoveAnimation(true, true);
    }

    public void StartReverseEffect(int targetIndex, bool backToBase)
    {
        if (backToBase && hasShield) { hasShield = false; return; }
        StopAllCoroutines(); isMoving = false; isReversing = true; isSliding = false;

        finalReverseTargetIndex = backToBase ? startIndex : targetIndex;
        reversePath.Clear();
        int currentTrack = currentPointIndex;

        if (hasEnteredHome) {
            for (int i = currentTrack; i >= 0; i--) if (i < waypoints.Count) reversePath.Add(waypoints[i]);
            currentTrack = mainPathWaypoints.IndexOf(entryNode);
        }

        int safety = 0;
        while (currentTrack != targetIndex && safety < 100) {
            if (currentTrack >= 0 && currentTrack < mainPathWaypoints.Count) reversePath.Add(mainPathWaypoints[currentTrack]);
            currentTrack--; if (currentTrack < 0) currentTrack = mainPathWaypoints.Count - 1; safety++;
        }
        if (mainPathWaypoints.Count > targetIndex) reversePath.Add(mainPathWaypoints[targetIndex]);
        if (backToBase && baseNode != null) reversePath.Add(baseNode);

        reverseIndex = 0;
        if (backToBase) {
            isOut = false; hasEnteredHome = false; currentPointIndex = startIndex;
            waypoints.Clear(); waypoints.AddRange(mainPathWaypoints); hasShield = false;
            if (animator != null) { animator.SetBool(PARAM_IS_OUT, false); animator.SetInteger(PARAM_DIRECTION, 0); }
        }
    }

    public void ResetToBase()
    {
        if (hasShield) { hasShield = false; return; }
        StopAllCoroutines(); isOut = false; isMoving = false; isFinished = false;
        hasEnteredHome = false; isSliding = false; isReversing = false; hasShield = false; isFrozen = false;
        currentPointIndex = startIndex; waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        if (baseNode != null) transform.position = baseNode.position;
        if (animator != null) { animator.SetBool(PARAM_IS_OUT, false); animator.SetInteger(PARAM_DIRECTION, 0); SetMoveAnimation(false, false); }
        transform.localScale = originalScale; 
    }

    void Update()
    {
        if (isSliding && slideTargetNode != null) {
            transform.position = Vector3.MoveTowards(transform.position, slideTargetNode.position, speed * Time.deltaTime);
            if (Vector3.Distance(transform.position, slideTargetNode.position) < 0.05f) { transform.position = slideTargetNode.position; isSliding = false; SetMoveAnimation(false, false); }
        }
        if (isReversing) {
            if (reverseIndex < reversePath.Count) {
                transform.position = Vector3.MoveTowards(transform.position, reversePath[reverseIndex].position, (speed * 3) * Time.deltaTime);
                if (Vector3.Distance(transform.position, reversePath[reverseIndex].position) < 0.1f) reverseIndex++;
            } else {
                isReversing = false;
                if (isOut && finalReverseTargetIndex != -1) { currentPointIndex = finalReverseTargetIndex; hasEnteredHome = false; waypoints.Clear(); waypoints.AddRange(mainPathWaypoints); }
                SetMoveAnimation(false, false);
            }
        }
    }
}