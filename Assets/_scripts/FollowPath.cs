using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FollowPath : MonoBehaviour
{
    [Header("Identitas Pion (WAJIB DIISI)")]
    public int ownerPlayerIndex;

    [Header("Setup Jalur & Base")]
    public Transform pathParent;
    public Transform baseNode;
    public int startIndex = 0;

    [Header("Pengaturan Gerak")]
    public float speed = 5f;
    public float jumpSpeed = 5f;
    public float jumpHeight = 0.5f;

    // [AUDIO] Variabel lokal ini bisa dikosongkan jika menggunakan GameManager
    [Header("Audio (Opsional - Diatur GameManager)")]
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

    // Simpan ukuran asli untuk referensi visual
    [HideInInspector] public Vector3 originalScale;

    [Header("Animasi (opsional)")]
    public Animator animator;
    [HideInInspector] public bool isRunningMove;

    // Parameter Animator Strings
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
        originalScale = transform.localScale; // Simpan scale awal (Normal)

        if (baseNode != null) 
        {
            transform.position = baseNode.position;
        }
        else if (waypoints.Count > startIndex) 
        {
            transform.position = waypoints[startIndex].position;
        }

        currentPointIndex = startIndex;

        // Setup Audio Source Lokal (Backup)
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (jumpSound == null && audioSource != null) jumpSound = audioSource.clip;
        
        if (animator == null) animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.SetBool(PARAM_IS_OUT, isOut);
            animator.SetBool(PARAM_IS_MOVING, false);
            animator.SetBool(PARAM_IS_RUNNING, false);
            animator.SetInteger(PARAM_DIRECTION, 0); 
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
            foreach (Transform child in pathParent)
            {
                waypoints.Add(child);
                mainPathWaypoints.Add(child);
            }
        }
    }

    public Vector3 GetCurrentAnchorPosition()
    {
        if (waypoints != null && currentPointIndex >= 0 && currentPointIndex < waypoints.Count)
        {
            return waypoints[currentPointIndex].position;
        }
        return transform.position;
    }

    #region Animation Helpers
    void SetMoveAnimation(bool moving, bool running)
    {
        if (animator == null) return;
        animator.SetBool(PARAM_IS_MOVING, moving);
        animator.SetBool(PARAM_IS_RUNNING, running);
    }

    void UpdateDirectionForStep(Transform from, Transform to)
    {
        if (animator == null || from == null || to == null) return;
        Vector3 dir = (to.position - from.position);

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            if (dir.x > 0f) animator.SetInteger(PARAM_DIRECTION, 3); // Kanan
            else if (dir.x < 0f) animator.SetInteger(PARAM_DIRECTION, 2); // Kiri
        }
        else
        {
            if (dir.y > 0f) animator.SetInteger(PARAM_DIRECTION, 1); // Belakang
            else if (dir.y < 0f) animator.SetInteger(PARAM_DIRECTION, 0); // Depan
        }
    }

    public void PlayAttackAnimation() { if (animator) animator.SetTrigger("Attack"); }
    public void PlayHurtAnimation() { if (animator) animator.SetTrigger("Hurt"); }
    public void PlayDieAnimation() { if (animator) animator.SetTrigger("Die"); }
    #endregion

    public void LeaveBase()
    {
        if (isOut) return;

        isOut = true;
        isMoving = false;
        hasEnteredHome = false;

        if (waypoints.Count > startIndex)
        {
            transform.position = waypoints[startIndex].position;
            currentPointIndex = startIndex;
        }

        Debug.Log($"{name} KELUAR DARI KANDANG!");

        // [AUDIO UPDATE] Gunakan GameManager untuk suara keluar (sama dengan Move/Jump)
        if (GameManager.Instance != null) GameManager.Instance.PlayMoveSound();
        else if (audioSource && jumpSound) audioSource.PlayOneShot(jumpSound); // Fallback

        if (animator != null)
        {
            animator.SetBool(PARAM_IS_OUT, true);
            if (waypoints.Count > 1)
            {
                int nextIndex = startIndex + 1;
                if (!hasEnteredHome && nextIndex >= waypoints.Count) nextIndex = 0;
                
                if (nextIndex >= 0 && nextIndex < waypoints.Count)
                    UpdateDirectionForStep(waypoints[startIndex], waypoints[nextIndex]);
            }
            SetMoveAnimation(false, false);
        }
    }

    public bool CheckPossibleMove(int steps)
    {
        if (isFrozen)
        {
            Debug.Log($"❄️ {name} sedang BEKU! Tidak bisa jalan.");
            return false;
        }

        if (isFinished || isReversing || isSliding || isMoving) return false;
        if (!isOut) return false;
        if (waypoints == null || waypoints.Count == 0) return false;

        if (hasEnteredHome)
        {
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

    IEnumerator MoveRoutine(int steps)
    {
        isMoving = true;
        SetMoveAnimation(true, isRunningMove);

        for (int i = 0; i < steps; i++)
        {
            int nextIndex = currentPointIndex + 1;

            if (hasEnteredHome)
            {
                if (nextIndex >= waypoints.Count) { isFinished = true; break; }
            }
            else
            {
                if (nextIndex >= waypoints.Count) nextIndex = 0;
            }

            if (nextIndex < 0 || nextIndex >= waypoints.Count) break;

            Transform startNode = waypoints[currentPointIndex];
            Transform targetNode = waypoints[nextIndex];

            UpdateDirectionForStep(startNode, targetNode);

            // [AUDIO UPDATE] Panggil suara jalan/lompat dari GameManager
            if (GameManager.Instance != null) 
            {
                GameManager.Instance.PlayMoveSound();
            }
            else if (audioSource != null && jumpSound != null) 
            {
                // Fallback jika GameManager belum siap
                audioSource.PlayOneShot(jumpSound);
            }

            Vector3 startPos = transform.position;
            Vector3 endPos = targetNode.position;
            float journey = 0f;

            while (journey <= 1f)
            {
                journey += Time.deltaTime * jumpSpeed;
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, journey);
                float height = Mathf.Sin(journey * Mathf.PI) * jumpHeight;
                currentPos.y += height;
                transform.position = currentPos;
                yield return null;
            }

            transform.position = endPos;

            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null)
            {
                SwitchToHomePath();
            }
            else
            {
                currentPointIndex = nextIndex;
            }

            // [AUDIO UPDATE] Cek Finish
            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1)
            {
                isFinished = true;
                transform.localScale = Vector3.one * 0.5f; 
                
                Debug.Log("FINISH");
                
                // Bunyikan Suara Finish dari GameManager
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.PlayFinishedSound();
                }

                break;
            }
        }

        isMoving = false;
        SetMoveAnimation(false, false);
    }

    void SwitchToHomePath()
    {
        hasEnteredHome = true;
        waypoints.Clear();
        foreach (Transform child in homePathParent) waypoints.Add(child);
        currentPointIndex = 0;
    }

    public void TeleportToPosition(int targetIndex, Vector3 worldPos)
    {
        StopAllCoroutines();
        hasEnteredHome = false;
        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);
        if (targetIndex >= waypoints.Count) targetIndex = 0;
        currentPointIndex = targetIndex;
        isMoving = false;
        transform.position = worldPos;
    }

    public void StartSlideEffect(int targetIndex)
    {
        StopAllCoroutines();
        isMoving = false; isReversing = false; isSliding = true;

        if (mainPathWaypoints.Count > targetIndex)
            slideTargetNode = mainPathWaypoints[targetIndex];

        hasEnteredHome = false;
        currentPointIndex = targetIndex;
        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);

        if (slideTargetNode != null) UpdateDirectionForStep(transform, slideTargetNode);
        SetMoveAnimation(true, true);
    }

    public void StartReverseEffect(int targetIndex, bool backToBase)
    {
        if (backToBase && hasShield) { hasShield = false; return; }

        StopAllCoroutines();
        isMoving = false; isReversing = true; isSliding = false;

        finalReverseTargetIndex = backToBase ? startIndex : targetIndex;
        reversePath.Clear();
        int currentTrack = currentPointIndex;

        if (hasEnteredHome)
        {
            for (int i = currentTrack; i >= 0; i--)
                if (i < waypoints.Count) reversePath.Add(waypoints[i]);
            currentTrack = mainPathWaypoints.IndexOf(entryNode);
        }

        int safetyLoop = 0;
        while (currentTrack != targetIndex && safetyLoop < 100)
        {
            if (currentTrack >= 0 && currentTrack < mainPathWaypoints.Count)
                reversePath.Add(mainPathWaypoints[currentTrack]);
            currentTrack--;
            if (currentTrack < 0) currentTrack = mainPathWaypoints.Count - 1;
            safetyLoop++;
        }

        if (mainPathWaypoints.Count > targetIndex) reversePath.Add(mainPathWaypoints[targetIndex]);
        if (backToBase && baseNode != null) reversePath.Add(baseNode);

        reverseIndex = 0;

        if (backToBase)
        {
            isOut = false; hasEnteredHome = false;
            currentPointIndex = startIndex;
            waypoints.Clear();
            waypoints.AddRange(mainPathWaypoints);
            hasShield = false;

            if (animator != null)
            {
                animator.SetBool(PARAM_IS_OUT, false);
                animator.SetInteger(PARAM_DIRECTION, 0);
            }
        }
    }

    public void ResetToBase()
    {
        if (hasShield) { hasShield = false; return; }

        StopAllCoroutines();
        isOut = false; isMoving = false; isFinished = false;
        hasEnteredHome = false; isSliding = false; isReversing = false;
        hasShield = false; isFrozen = false;

        currentPointIndex = startIndex;
        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);

        if (baseNode != null) transform.position = baseNode.position;

        if (animator != null)
        {
            animator.SetBool(PARAM_IS_OUT, false);
            animator.SetInteger(PARAM_DIRECTION, 0);
            SetMoveAnimation(false, false);
        }
        
        transform.localScale = originalScale; 
    }

    void Update()
    {
        if (isSliding)
        {
            if (slideTargetNode != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, slideTargetNode.position, speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, slideTargetNode.position) < 0.05f)
                {
                    transform.position = slideTargetNode.position;
                    isSliding = false;
                    SetMoveAnimation(false, false);
                }
            }
            return;
        }

        if (isReversing)
        {
            if (reverseIndex < reversePath.Count)
            {
                Transform target = reversePath[reverseIndex];
                transform.position = Vector3.MoveTowards(transform.position, target.position, (speed * 3) * Time.deltaTime);
                if (Vector3.Distance(transform.position, target.position) < 0.1f) reverseIndex++;
            }
            else
            {
                isReversing = false;
                if (isOut && finalReverseTargetIndex != -1)
                {
                    currentPointIndex = finalReverseTargetIndex;
                    hasEnteredHome = false;
                    waypoints.Clear();
                    waypoints.AddRange(mainPathWaypoints);
                }
                SetMoveAnimation(false, false);
            }
            return;
        }
    }
}