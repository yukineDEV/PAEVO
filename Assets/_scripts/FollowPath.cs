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

    [Header("Arah Saat Di Base / Keluar Base")]
    [Tooltip("0 = Front (bawah), 1 = Back (atas), 2 = Left, 3 = Right")]
    public int baseDirection = 0;

    [Header("Animasi")]
    public Animator animator;
    [HideInInspector] public bool isRunningMove;   // di-set dari GameManager: true = run, false = walk

    private const string PARAM_IS_OUT     = "IsOut";
    private const string PARAM_IS_MOVING  = "IsMoving";
    private const string PARAM_IS_RUNNING = "IsRunning";
    private const string PARAM_DIRECTION  = "Direction";

    private List<Transform> waypoints          = new List<Transform>();
    private List<Transform> mainPathWaypoints  = new List<Transform>();
    private List<Transform> reversePath        = new List<Transform>();
    private Transform slideTargetNode;
    private int reverseIndex = 0;
    private int finalReverseTargetIndex = -1;

    // Untuk arah responsif (mengikuti velocity setiap frame)
    private Vector3 lastPosition;

    void Start()
    {
        InitWaypoints();
        originalScale = transform.localScale;

        if (baseNode != null)
            transform.position = baseNode.position;
        else if (waypoints.Count > startIndex)
            transform.position = waypoints[startIndex].position;

        currentPointIndex = startIndex;
        lastPosition = transform.position;

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (jumpSound == null && audioSource != null) jumpSound = audioSource.clip;
        if (animator == null) animator = GetComponent<Animator>();

        if (animator != null)
        {
            animator.SetBool(PARAM_IS_OUT, isOut);
            animator.SetBool(PARAM_IS_MOVING, false);
            animator.SetBool(PARAM_IS_RUNNING, false);
            animator.SetInteger(PARAM_DIRECTION, baseDirection);   // arah dasar di base
        }
    }

    void OnMouseDown()
    {
        if (GameManager.Instance != null && !isMoving && !isReversing && !isSliding)
            GameManager.Instance.OnPawnClicked(this);
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
            return waypoints[currentPointIndex].position;
        return transform.position;
    }

    #region Anim Helpers

    void SetMoveAnimation(bool moving, bool running)
    {
        if (!animator) return;
        animator.SetBool(PARAM_IS_MOVING, moving);
        animator.SetBool(PARAM_IS_RUNNING, running);
    }

    // Versi "dari 2 node" (dipanggil di MoveRoutine/Slide/Reverse)
    // Ganti fungsi UpdateDirectionForStep yang lama dengan ini
void UpdateDirectionForStep(Transform from, Transform to)
{
    if (!animator) return;

    // Jika sudah masuk kandang (Home Path), gunakan logika vektor (karena index home beda lagi)
    if (hasEnteredHome)
    {
        Vector3 dir = to.position - from.position;
        UpdateDirectionFromVector(dir);
        return;
    }

    // Gunakan index saat ini untuk menentukan arah sesuai permintaan Anda
    int index = currentPointIndex;
    int targetDir = baseDirection; // Default

    // Logic sesuai permintaan:
    // 0 = Front, 1 = Back, 2 = Left, 3 = Right

    // BACK (1): 50-3, 17-22, 9-10
    if ((index >= 50 || index <= 3) || (index >= 17 && index <= 22) || (index >= 9 && index <= 10))
    {
        targetDir = 1;
    }
    // RIGHT (3): 11-16, 22-23, 30-34
    else if ((index >= 11 && index <= 16) || (index == 22 || index == 23) || (index >= 30 && index <= 34))
    {
        targetDir = 3;
    }
    // FRONT (0): 24-29, 35-36, 43-47
    else if ((index >= 24 && index <= 29) || (index == 35 || index == 36) || (index >= 43 && index <= 47))
    {
        targetDir = 0;
    }
    // LEFT (2): 48-49, 4-8, 37-42
    else if ((index >= 48 && index <= 49) || (index >= 4 && index <= 8) || (index >= 37 && index <= 42))
    {
        targetDir = 2;
    }

    animator.SetInteger(PARAM_DIRECTION, targetDir);
}

    // Versi "dari vector" (dipakai di UpdateFacingByVelocity)
    void UpdateDirectionFromVector(Vector3 dir)
    {
        if (!animator) return;
        if (dir.sqrMagnitude < 0.0001f) return;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            if (dir.x > 0f) animator.SetInteger(PARAM_DIRECTION, 3); // Right
            else            animator.SetInteger(PARAM_DIRECTION, 2); // Left
        }
        else
        {
            if (dir.y > 0f) animator.SetInteger(PARAM_DIRECTION, 1); // Back (naik layar)
            else            animator.SetInteger(PARAM_DIRECTION, 0); // Front (turun layar)
        }
    }

    public void PlayAttackAnimation() { if (animator) animator.SetTrigger("Attack"); }
    public void PlayHurtAnimation()   { if (animator) animator.SetTrigger("Hurt");   }
    public void PlayDieAnimation()    { if (animator) animator.SetTrigger("Die");    }

    public void FreezeWithAnim()    {
        // jangan dobel-dobel
        if (isFrozen) return;

        isFrozen = true;

        // Pakai arah terakhir (PARAM_DIRECTION sudah di-set dari gerakan/idle)
        PlayDieAnimation();   // trigger "Die" di Animator
    }

    #endregion

    #region Keluar Base & Gerak Normal

    public void LeaveBase()
    {
        if (isOut) return;
        isOut = true;
        isMoving = false;
        hasEnteredHome = false;

        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);

        if (waypoints.Count > startIndex)
        {
            transform.position = waypoints[startIndex].position;
            currentPointIndex  = startIndex;
        }

        Debug.Log($"🟢 {name} (Player {ownerPlayerIndex}) KELUAR DARI KANDANG!");

        if (GameManager.Instance != null) GameManager.Instance.PlayMoveSound();

        if (animator != null)
        {
            animator.SetBool(PARAM_IS_OUT, true);
            animator.SetInteger(PARAM_DIRECTION, baseDirection); // hadap sesuai warna
            SetMoveAnimation(false, false);

            // efek jatuh sekali saat keluar base
            PlayDieAnimation();    // pastikan Animator: AnyState->Dying (Die), Dying->Idle
        }

        lastPosition = transform.position;
    }

    public bool CheckPossibleMove(int steps)
    {
        if (isFrozen)
        {
            Debug.Log($"❄️ {name} sedang BEKU!");
            return false;
        }

        if (isFinished || isReversing || isSliding || isMoving || !isOut || waypoints.Count == 0)
            return false;

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
            // Safety index
            if (currentPointIndex >= waypoints.Count)
            {
                Debug.LogWarning($"⚠️ [FIX] Index {name} ({currentPointIndex}) > jalur ({waypoints.Count}). Reset ke endpoint.");
                currentPointIndex = waypoints.Count - 1;
            }

            // Kalau tepat di gerbang karena swap/pull → paksa ke home path
            if (!hasEnteredHome && waypoints[currentPointIndex] == entryNode && homePathParent != null)
            {
                Debug.Log($"🔀 Efek Swap: {name} di Gerbang -> Masuk Home.");
                SwitchToHomePath();

                int nextIndexInside = 0;
                if (nextIndexInside < waypoints.Count)
                {
                    Transform targetNodeInside = waypoints[nextIndexInside];
                    UpdateDirectionForStep(transform, targetNodeInside);

                    if (GameManager.Instance) GameManager.Instance.PlayMoveSound();
                    yield return MoveToTarget(targetNodeInside.position);

                    currentPointIndex = nextIndexInside;
                    if (currentPointIndex >= waypoints.Count - 1)
                    {
                        HandleFinish();
                        break;
                    }
                }
                continue;
            }

            // Hitung index berikut
            int nextIndex = currentPointIndex + 1;

            if (hasEnteredHome)
            {
                if (nextIndex >= waypoints.Count)
                {
                    isFinished = true;
                    HandleFinish();
                    break;
                }
            }
            else
            {
                if (nextIndex >= waypoints.Count) nextIndex = 0;
            }

            Transform startNode  = waypoints[currentPointIndex];
            Transform targetNode = waypoints[nextIndex];

            // Arah awal langkah (node -> node)
            UpdateDirectionForStep(startNode, targetNode);

            if (GameManager.Instance) GameManager.Instance.PlayMoveSound();
            yield return MoveToTarget(targetNode.position);

            transform.position = targetNode.position;

            // Pindah ke home path (normal)
            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null)
            {
                SwitchToHomePath();
                currentPointIndex = -1; // langkah berikutnya mulai dari 0
            }
            else
            {
                currentPointIndex = nextIndex;
            }

            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1)
            {
                HandleFinish();
                break;
            }
        }

        isMoving = false;
        SetMoveAnimation(false, false);
        transform.localScale = originalScale;
    }

    IEnumerator MoveToTarget(Vector3 targetPos)
    {
        Vector3 startPos = transform.position;
        float journey = 0f;

        while (journey <= 1f)
        {
            journey += Time.deltaTime * jumpSpeed;
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, journey);
            currentPos.y += Mathf.Sin(journey * Mathf.PI) * jumpHeight;
            transform.position = currentPos;
            yield return null;
        }

        transform.position = targetPos;
        lastPosition = transform.position;
    }

    void HandleFinish()
    {
        isFinished = true;
        transform.localScale = originalScale * 0.5f;
        Debug.Log($"🏁 {name} FINISH!");
        if (GameManager.Instance) GameManager.Instance.PlayFinishedSound();
    }

    void SwitchToHomePath()
    {
        hasEnteredHome = true;
        waypoints.Clear();
        foreach (Transform child in homePathParent) waypoints.Add(child);
        currentPointIndex = -1;
    }

    #endregion

    #region Slide, Reverse, Teleport, Reset

    public void TeleportToPosition(int targetIndex, Vector3 worldPos)
    {
        StopAllCoroutines();

        hasEnteredHome = false;
        isMoving = false;
        isReversing = false;
        isSliding = false;

        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);

        if (targetIndex >= waypoints.Count) targetIndex = 0;
        if (targetIndex < 0) targetIndex = 0;

        currentPointIndex = targetIndex;
        transform.position = worldPos;
        lastPosition = transform.position;

        Debug.Log($"📍 {name} Teleport ke Index: {currentPointIndex}");
    }

    public void StartSlideEffect(int targetIndex)
    {
        StopAllCoroutines();
        isMoving = false;
        isReversing = false;
        isSliding = true;

        hasEnteredHome = false;
        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);

        if (mainPathWaypoints.Count > targetIndex)
            slideTargetNode = mainPathWaypoints[targetIndex];

        currentPointIndex = targetIndex;

        if (slideTargetNode != null)
            UpdateDirectionForStep(transform, slideTargetNode);

        SetMoveAnimation(true, true);
    }

    public void StartReverseEffect(int targetIndex, bool backToBase)
    {
        if (backToBase && hasShield)
        {
            hasShield = false;
            return;
        }

        StopAllCoroutines();
        isMoving = false;
        isReversing = true;
        isSliding = false;

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

        if (mainPathWaypoints.Count > targetIndex)
            reversePath.Add(mainPathWaypoints[targetIndex]);

        if (backToBase && baseNode != null)
            reversePath.Add(baseNode);

        reverseIndex = 0;

        if (backToBase)
        {
            isOut = false;
            hasEnteredHome = false;
            currentPointIndex = startIndex;
            waypoints.Clear();
            waypoints.AddRange(mainPathWaypoints);
            hasShield = false;

            if (animator != null)
            {
                animator.SetBool(PARAM_IS_OUT, false);
                animator.SetInteger(PARAM_DIRECTION, baseDirection);
            }
        }
    }

    public void ResetToBase()
    {
        if (hasShield) { hasShield = false; return; }

        StopAllCoroutines();
        isOut = false;
        isMoving = false;
        isFinished = false;
        hasEnteredHome = false;
        isSliding = false;
        isReversing = false;
        hasShield = false;
        isFrozen = false;

        currentPointIndex = startIndex;
        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);

        if (baseNode != null)
            transform.position = baseNode.position;

        if (animator != null)
        {
            animator.SetBool(PARAM_IS_OUT, false);
            animator.SetInteger(PARAM_DIRECTION, baseDirection);
            SetMoveAnimation(false, false);
        }

        transform.localScale = originalScale;
        lastPosition = transform.position;
    }

    #endregion

    void Update()
    {
        // 0. update arah berdasar velocity (paling responsif)
        UpdateFacingByVelocity();

        // 1. SLIDE
        if (isSliding && slideTargetNode != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                slideTargetNode.position,
                speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, slideTargetNode.position) < 0.05f)
            {
                transform.position = slideTargetNode.position;
                isSliding = false;
                SetMoveAnimation(false, false);
            }
            return;
        }

        // 2. REVERSE
        if (isReversing)
        {
            if (reverseIndex < reversePath.Count)
            {
                Transform target = reversePath[reverseIndex];
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    target.position,
                    (speed * 3) * Time.deltaTime);

                if (Vector3.Distance(transform.position, target.position) < 0.1f)
                    reverseIndex++;
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

    void UpdateFacingByVelocity()
{
    // Jika di jalur utama, kita pakai aturan node (UpdateDirectionForStep), 
    // jadi kita skip UpdateFacingByVelocity agar tidak bentrok.
    if (!hasEnteredHome && isOut && !isReversing) 
    {
        lastPosition = transform.position;
        return;
    }

    bool movingNow = isMoving || isSliding || isReversing;
    if (!movingNow)
    {
        lastPosition = transform.position;
        return;
    }

    Vector3 delta = transform.position - lastPosition;
    if (delta.sqrMagnitude > 0.0001f)
        UpdateDirectionFromVector(delta);

    lastPosition = transform.position;
}
}