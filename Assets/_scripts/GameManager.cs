using UnityEngine;
using TMPro; 
using UnityEngine.UI; 
using System.Linq; // Wajib ada untuk cek Array Safe Zone

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
    
    [Header("Aturan Main")]
    // Index kotak yang aman (Shield)
    public int[] safeZones = { 51, 12, 25, 38, 7, 20, 33, 46 };

    private int lastDiceValue = 0; 

    void Start()
    {
        UpdateDiceButtons();
    }

    void Update()
    {
        if (isTurnActive)
        {
            // Tunggu pion berhenti jalan
            if (!players[activePlayerIndex].isMoving)
            {
                // 1. Cek Makan Lawan
                CheckAndCapture();
                
                // 2. Akhiri Giliran
                EndTurn();
            }
        }
    }

    // LOGIKA MAKAN LAWAN
    void CheckAndCapture()
    {
        FollowPath killer = players[activePlayerIndex];

        // Syarat Killer: Harus sudah keluar, belum tamat, dan belum masuk jalur kandang
        if (!killer.isOut || killer.isFinished || killer.hasEnteredHome) return;

        // Syarat Safe Zone: Kalau berdiri di bintang, tidak bisa perang
        if (safeZones.Contains(killer.currentPointIndex))
        {
            Debug.Log($"SAFE ZONE: {killer.name} aman di kotak {killer.currentPointIndex}");
            return;
        }

        // Cari Korban
        foreach (FollowPath victim in players)
        {
            if (victim == killer) continue;

            // Syarat Korban: Ada di luar, belum tamat, belum masuk kandang sendiri
            if (victim.isOut && !victim.isFinished && !victim.hasEnteredHome)
            {
                // JIKA POSISI SAMA
                if (victim.currentPointIndex == killer.currentPointIndex)
                {
                    Debug.Log($"⚔️ HIT! {killer.name} memakan {victim.name}");
                    victim.ResetToBase(); // Tendang Pulang
                }
            }
        }
    }

    public void RollDice()
    {
        if (isTurnActive) return;

        lastDiceValue = Random.Range(1, 7);
        // lastDiceValue = 6; // <-- Buka ini kalau mau cheat 6 terus
        
        if (diceTexts != null && diceTexts.Length > activePlayerIndex)
        {
             diceTexts[activePlayerIndex].text = lastDiceValue.ToString(); 
        }
        
        SetAllButtonsInteractable(false);

        // Coba Jalan
        bool isMovingSuccess = players[activePlayerIndex].MoveSteps(lastDiceValue);
        
        if (isMovingSuccess) 
        {
            isTurnActive = true;
        }
        else 
        {
            // Kalau gagal jalan (misal kelebihan langkah finish), langsung skip
            Invoke("EndTurn", 0.5f); 
        }
    }

    void EndTurn()
    {
        isTurnActive = false;

        // Cek Bonus Turn (Dapat 6 main lagi)
        if (lastDiceValue == 6 && !players[activePlayerIndex].isFinished)
        {
            Debug.Log("Dapat 6! Main Lagi.");
            UpdateDiceButtons(); 
            return; 
        }

        // Ganti Pemain (Skip yang sudah finish)
        int attempts = 0; 
        do
        {
            activePlayerIndex++;
            if (activePlayerIndex >= players.Length) activePlayerIndex = 0; 
            attempts++;
        } 
        while (players[activePlayerIndex].isFinished && attempts < players.Length);

        // Cek Game Over
        if (players[activePlayerIndex].isFinished)
        {
            Debug.Log("GAME OVER! Semua selesai.");
            SetAllButtonsInteractable(false);
            return;
        }

        Debug.Log("Giliran: Pemain " + activePlayerIndex);
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
        foreach (Button btn in diceButtons)
        {
            if (btn != null) btn.interactable = state;
        }
    }
}