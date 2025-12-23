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

    [Header("Status Logic")]
    public bool isFinished = false; 
    public bool isMoving = false;
    public bool isReversing = false; 
    public bool isSliding = false; // Status sedang geser
    private Transform slideTargetNode; 

    public int currentPointIndex = 0; 
    public bool hasEnteredHome = false; 

    private int stepsRemaining = 0; 
    private List<Transform> waypoints = new List<Transform>(); 
    private List<Transform> mainPathWaypoints = new List<Transform>(); 
    private List<Transform> reversePath = new List<Transform>(); 
    private int reverseIndex = 0;

    void Start()
    {
        InitWaypoints(); 
        if (baseNode != null) transform.position = baseNode.position;
        else if (waypoints.Count > startIndex) transform.position = waypoints[startIndex].position;
        currentPointIndex = startIndex; 
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

    public void ResetToBase()
    {
        isOut = false; isMoving = false; isFinished = false; hasEnteredHome = false; isSliding = false; isReversing = false;
        currentPointIndex = startIndex; 
        waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        if (baseNode != null) transform.position = baseNode.position;
    }

    // --- FITUR SLIDE (KHUSUS DANGER ZONE) ---
    public void StartSlideEffect(int targetIndex)
    {
        isMoving = false; isReversing = false; 
        isSliding = true; 

        if (mainPathWaypoints.Count > targetIndex)
            slideTargetNode = mainPathWaypoints[targetIndex];
        
        Debug.Log($"{gameObject.name} Geser Visual ke Index {targetIndex}");
        
        // Update Data Logika Langsung
        hasEnteredHome = false; 
        currentPointIndex = targetIndex;
        waypoints.Clear();
        waypoints.AddRange(mainPathWaypoints);
    }

    // --- FITUR REVERSE (KHUSUS CAPTURE) ---
    public void StartReverseEffect(int targetIndex, bool backToBase)
    {
        isMoving = false; isReversing = true; isSliding = false;
        reversePath.Clear();

        Debug.Log($"{gameObject.name} Mundur ke Base...");

        // 1. Mundur dari kandang (kalau ada)
        int currentTrack = currentPointIndex;
        if (hasEnteredHome)
        {
            for (int i = currentTrack; i >= 0; i--)
                if(i < waypoints.Count) reversePath.Add(waypoints[i]);
            
            currentTrack = mainPathWaypoints.IndexOf(entryNode);
        }

        // 2. Mundur di jalan raya
        int safetyLoop = 0;
        while (currentTrack != targetIndex && safetyLoop < 100)
        {
            if(currentTrack >= 0 && currentTrack < mainPathWaypoints.Count)
                reversePath.Add(mainPathWaypoints[currentTrack]);

            currentTrack--;
            if (currentTrack < 0) currentTrack = mainPathWaypoints.Count - 1; 
            safetyLoop++;
        }
        if (mainPathWaypoints.Count > targetIndex)
            reversePath.Add(mainPathWaypoints[targetIndex]);

        // 3. Masuk Base
        if (backToBase && baseNode != null) reversePath.Add(baseNode);

        reverseIndex = 0;
        
        // Update Logika
        if (backToBase)
        {
            isOut = false; hasEnteredHome = false; currentPointIndex = startIndex;
            waypoints.Clear(); waypoints.AddRange(mainPathWaypoints);
        }
    }

    public bool MoveSteps(int steps)
    {
        if (isFinished || isReversing || isSliding) return false; 
        if (!isOut) {
             if (steps == 6) {
                 isOut = true; 
                 if (waypoints.Count > startIndex) transform.position = waypoints[startIndex].position;
                 return true; 
             }
             return false; 
        }
        if (hasEnteredHome) {
            int stepsToEnd = (waypoints.Count - 1) - currentPointIndex;
            if (steps > stepsToEnd) return false; 
        }
        stepsRemaining = steps;
        isMoving = true;
        return true; 
    }

    void Update()
    {
        // 1. Logic Geser (Slide)
        if (isSliding)
        {
            if (slideTargetNode != null)
            {
                transform.position = Vector3.MoveTowards(transform.position, slideTargetNode.position, speed * Time.deltaTime);
                if (Vector3.Distance(transform.position, slideTargetNode.position) < 0.05f)
                {
                    transform.position = slideTargetNode.position;
                    isSliding = false; 
                }
            }
            return;
        }

        // 2. Logic Mundur (Reverse)
        if (isReversing)
        {
            if (reverseIndex < reversePath.Count)
            {
                Transform target = reversePath[reverseIndex];
                transform.position = Vector3.MoveTowards(transform.position, target.position, (speed * 3) * Time.deltaTime);
                if (Vector3.Distance(transform.position, target.position) < 0.1f) reverseIndex++;
            }
            else isReversing = false;
            return; 
        }

        // 3. Logic Maju
        if (!isMoving || isFinished) return;
        
        int nextIndex = currentPointIndex + 1;
        if (!hasEnteredHome && nextIndex >= waypoints.Count) nextIndex = 0; 
        Transform targetNode = waypoints[nextIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetNode.position, speed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetNode.position) < 0.05f)
        {
            transform.position = targetNode.position;
            if (!hasEnteredHome && targetNode == entryNode && homePathParent != null) SwitchToHomePath(); 
            else currentPointIndex = nextIndex;

            if (hasEnteredHome && currentPointIndex >= waypoints.Count - 1) {
                isFinished = true; isMoving = false; transform.localScale = Vector3.one * 0.5f; return;
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