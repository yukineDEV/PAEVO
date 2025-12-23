using UnityEngine;
using TMPro; 
using UnityEngine.UI; 
using System.Linq; 

public class GameManager : MonoBehaviour
{
    [Header("Daftar Pemain")]
    public FollowPath[] players; 
    
    [Header("UI Per Pemain")]
    public TextMeshProUGUI[] diceTexts; 
    public Button[] diceButtons;        

    [Header("Status Game")]
    public int activePlayerIndex = 0; 
    public bool isTurnActive = false;
    
    [Header("Aturan Main & Paevo")]
    // Safe Zone: Start Point & Bintang
    public int[] safeZones = { 51, 12, 25, 38, 7, 20, 33, 46 }; 

    // Index Kotak Skill (Akan otomatis diisi kalau kosong)
    public int[] skillZones = { 4, 17, 30, 43 }; 

    // Index Jebakan Tengkorak
    public int dangerHomeIndex = 2; 

    private int lastDiceValue = 0; 
    private bool eventProcessed = false;

    void Start()
    {
        // --- FITUR ANTI-LUPA INSPECTOR ---
        // Kalau di Inspector kosong, paksa isi pakai default kode
        if (skillZones == null || skillZones.Length == 0)
        {
            skillZones = new int[] { 4, 17, 30, 43 };
            Debug.Log("🔧 Auto-Fix: Skill Zones diisi otomatis { 4, 17, 30, 43 }");
        }

        Debug.Log("✅ GAME MANAGER SIAP! Menunggu kocokan dadu...");
        UpdateDiceButtons();
    }

    void Update()
    {
        if (isTurnActive)
        {
            FollowPath currPlayer = players[activePlayerIndex];

            // Tunggu sampai pion diam (tidak gerak, tidak mundur, tidak geser)
            if (!currPlayer.isMoving && !currPlayer.isReversing && !currPlayer.isSliding)
            {
                HandleTurnEvents();
            }
        }
    }

    void HandleTurnEvents()
    {
        if (eventProcessed) return; 

        FollowPath currentPlayer = players[activePlayerIndex];
        bool eventTriggered = false; 

        // 👇 DEBUG PENTING: Lapor posisi pion saat berhenti
        Debug.Log($"📍 CEK POSISI: {currentPlayer.name} berhenti di Index {currentPlayer.currentPointIndex} (Home: {currentPlayer.hasEnteredHome})");

        // --- 1. CEK DANGER ZONE (SLIDE MODE) ---
        if (currentPlayer.hasEnteredHome && currentPlayer.currentPointIndex == dangerHomeIndex)
        {
            Debug.Log($"⚠️ DANGER ZONE! {currentPlayer.name} Geser ke samping...");
            
            int totalMainNodes = 52; 
            int targetIndex = (currentPlayer.startIndex + 2) % totalMainNodes; 
            
            // Geser Langsung
            currentPlayer.StartSlideEffect(targetIndex);
            
            eventTriggered = true;
        }

        // --- 2. CEK SKILL ZONE (GACHA) ---
        // Cek apakah index sekarang ada di dalam daftar skillZones?
        else if (!currentPlayer.hasEnteredHome && skillZones.Contains(currentPlayer.currentPointIndex))
        {
             // 👇 INI OUTPUT YANG KAMU CARI
             Debug.Log($"✨ SKILL GET! {currentPlayer.name} dapat Skill di kotak {currentPlayer.currentPointIndex}");
             // Nanti logika skillnya ditaruh di sini
        }

        // --- 3. CEK MAKAN LAWAN (CAPTURE) ---
        if (!eventTriggered)
        {
            eventTriggered = CheckAndCapture();
        }

        eventProcessed = true; 
        
        float delay = eventTriggered ? 0.8f : 0.5f; 
        Invoke("EndTurn", delay);
    }

    bool CheckAndCapture()
    {
        FollowPath killer = players[activePlayerIndex];
        bool captured = false;

        if (!killer.isOut || killer.isFinished || killer.hasEnteredHome) return false;

        if (safeZones.Contains(killer.currentPointIndex)) 
        {
            Debug.Log($"🛡️ AMAN: {killer.name} di Safe Zone {killer.currentPointIndex}");
            return false;
        }

        foreach (FollowPath victim in players)
        {
            if (victim == killer) continue;

            if (victim.isOut && !victim.isFinished && !victim.hasEnteredHome)
            {
                if (victim.currentPointIndex == killer.currentPointIndex)
                {
                    Debug.Log($"⚔️ HIT! {killer.name} MEMAKAN {victim.name}");
                    
                    // Korban mundur ke Base
                    victim.StartReverseEffect(victim.startIndex, true);
                    
                    captured = true;
                }
            }
        }
        return captured;
    }

    public void RollDice()
    {
        if (isTurnActive) return;

        eventProcessed = false; 
        lastDiceValue = Random.Range(1, 7);
        // lastDiceValue = 4; // Cheat
        
        Debug.Log($"🎲 Pemain {activePlayerIndex} Roll: {lastDiceValue}");

        if (diceTexts != null && diceTexts.Length > activePlayerIndex)
        {
             diceTexts[activePlayerIndex].text = lastDiceValue.ToString(); 
        }
        
        SetAllButtonsInteractable(false);
        
        bool isMovingSuccess = players[activePlayerIndex].MoveSteps(lastDiceValue);
        
        if (isMovingSuccess) isTurnActive = true;
        else 
        {
            Debug.Log("🚫 Gagal Jalan (Kandang/Overflow).");
            Invoke("EndTurn", 0.5f); 
        }
    }

    void EndTurn()
    {
        // Pastikan semua animasi selesai
        if (players[activePlayerIndex].isReversing || players[activePlayerIndex].isSliding) 
        {
            Invoke("EndTurn", 0.5f);
            return;
        }

        isTurnActive = false;
        eventProcessed = false;

        if (lastDiceValue == 6 && !players[activePlayerIndex].isFinished)
        {
            Debug.Log("🎉 Bonus Turn (Angka 6)!");
            UpdateDiceButtons(); 
            return; 
        }

        int attempts = 0; 
        do
        {
            activePlayerIndex++;
            if (activePlayerIndex >= players.Length) activePlayerIndex = 0; 
            attempts++;
        } 
        while (players[activePlayerIndex].isFinished && attempts < players.Length);

        if (players[activePlayerIndex].isFinished)
        {
            Debug.Log("🏁 GAME OVER!");
            SetAllButtonsInteractable(false);
            return;
        }

        Debug.Log("👉 Giliran Pemain: " + activePlayerIndex);
        UpdateDiceButtons();
    }

    void UpdateDiceButtons()
    {
        if (diceButtons == null || diceButtons.Length == 0) return;

        for (int i = 0; i < diceButtons.Length; i++)
        {
            if (i == activePlayerIndex && !players[i].isFinished)
            {
                diceButtons[i].interactable = true;
                diceButtons[i].transform.localScale = Vector3.one * 1.1f; 
            }
            else
            {
                diceButtons[i].interactable = false;
                diceButtons[i].transform.localScale = Vector3.one; 
            }
        }
    }

    void SetAllButtonsInteractable(bool state)
    {
        foreach (Button btn in diceButtons) if (btn != null) btn.interactable = state;
    }
}