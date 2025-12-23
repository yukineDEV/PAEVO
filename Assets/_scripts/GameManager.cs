using UnityEngine;
using TMPro; 
using UnityEngine.UI; 
using System.Linq; 
using System.Collections.Generic;

public enum SkillType {
    None,
    Shield,        
    ExtraRoll,
    ChooseDice,    
    TeleportSafe,  
    SwapPosition,  
    FreezeEnemy,   
    PullEnemy,     
    ExtraBlock     // MAJU 2 KOTAK
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Daftar Pemain (TOTAL 16 PION)")]
    // Urutan: 0-3 (Biru), 4-7 (Merah), 8-11 (Hijau), 12-15 (Kuning)
    public FollowPath[] players; 
    
    [Header("UI Dadu")]
    public TextMeshProUGUI[] diceTexts; 
    public Button[] diceButtons;        

    [Header("UI Inventory Skill")]
    public Button[] blueSkillButtons; 
    public Button[] redSkillButtons; 
    public Button[] greenSkillButtons; 
    public Button[] yellowSkillButtons; 

    [Header("Status Game")]
    public int activePlayerIndex = 0; 
    public bool isTurnActive = false; 
    public bool isWaitingForMove = false; 
    
    // VARIABLES SKILL & LOGIC
    private SkillType[,] skillInventory = new SkillType[4, 3]; 
    private bool isTargetingMode = false;
    private bool isDicePickingMode = false; 
    private int skillUserIndex = -1; 
    private SkillType selectedSkill = SkillType.None;
    private int selectedSlot = -1;
    private int storedTurnIndex = -1; 
    
    // VARIABLE SWAP
    private FollowPath firstSwapTarget = null;

    [Header("Aturan Main")]
    public int[] safeZones = { 51, 12, 25, 38, 7, 20, 33, 46 }; 
    public int[] skillZones = { 4, 17, 30, 43 }; 
    public int dangerHomeIndex = 2; 

    private int lastDiceValue = 1; 
    private bool eventProcessed = false;

    void Awake() { Instance = this; }

    void Start()
    {
        // 1. Setup UI Otomatis
        FindButtonsByColor("Blue", ref blueSkillButtons);
        FindButtonsByColor("Red", ref redSkillButtons);
        FindButtonsByColor("Green", ref greenSkillButtons);
        FindButtonsByColor("Yellow", ref yellowSkillButtons);

        if (skillZones == null || skillZones.Length == 0) skillZones = new int[] { 4, 17, 30, 43 };

        SetupSkillListeners(blueSkillButtons, 0);
        SetupSkillListeners(redSkillButtons, 1);
        SetupSkillListeners(greenSkillButtons, 2);
        SetupSkillListeners(yellowSkillButtons, 3);

        Debug.Log("✅ GAME MANAGER SIAP: Smart Dice + Auto Move.");
        UpdateDiceButtons();
        UpdateAllSkillUI();
    }

    // --- AUTO SETUP UI ---
    void FindButtonsByColor(string colorName, ref Button[] buttonArray)
    {
        List<Button> foundButtons = new List<Button>();
        string baseName = colorName + "_SkillButton";
        GameObject btn1 = GameObject.Find(baseName); if (btn1 != null) foundButtons.Add(btn1.GetComponent<Button>());
        for (int i = 1; i < 3; i++) {
            string nameWithIndex = baseName + " (" + i + ")";
            GameObject btnNext = GameObject.Find(nameWithIndex); if (btnNext != null) foundButtons.Add(btnNext.GetComponent<Button>());
        }
        buttonArray = foundButtons.ToArray();
    }

    void SetupSkillListeners(Button[] btns, int pIndex)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++) {
            int slot = i; 
            btns[i].onClick.RemoveAllListeners();
            btns[i].onClick.AddListener(() => OnSkillSlotClicked(pIndex, slot));
        }
    }

    // --- SYSTEM SKILL GACHA ---
    public void GetRandomSkill(FollowPath pawn)
    {
        int pIdx = pawn.ownerPlayerIndex;
        int emptySlot = -1;
        for(int i=0; i<3; i++) {
            if(skillInventory[pIdx, i] == SkillType.None) { emptySlot = i; break; }
        }

        if(emptySlot == -1) { Debug.Log($"🎒 Tas Penuh!"); return; }

        SkillType[] possible = { 
            SkillType.Shield, SkillType.ExtraRoll, SkillType.ChooseDice, 
            SkillType.SwapPosition, SkillType.FreezeEnemy, SkillType.PullEnemy, SkillType.ExtraBlock 
        }; 

        SkillType newSkill = possible[Random.Range(0, possible.Length)];
        skillInventory[pIdx, emptySlot] = newSkill;
        Debug.Log($"✨ Player {pIdx} dapat {GetSkillDisplayName(newSkill)}");
        UpdateAllSkillUI();
    }

    // --- INTERAKSI SLOT SKILL ---
    void OnSkillSlotClicked(int ownerIdx, int slotIdx)
    {
        SkillType skill = skillInventory[ownerIdx, slotIdx];
        if(skill == SkillType.None) return;

        skillUserIndex = ownerIdx; 

        // INSTANT SKILLS
        if (skill == SkillType.ExtraRoll) {
            Debug.Log($"🎲 EXTRA ROLL!");
            skillInventory[ownerIdx, slotIdx] = SkillType.None; 
            if (storedTurnIndex == -1) storedTurnIndex = activePlayerIndex; 
            activePlayerIndex = ownerIdx;
            isTurnActive = false; isTargetingMode = false; isDicePickingMode = false; isWaitingForMove = false;
            UpdateDiceButtons(); UpdateAllSkillUI(); return;
        }

        if (skill == SkillType.ChooseDice) {
            Debug.Log($"🔢 CHOOSE DICE: Klik Dadu Manual.");
            isTargetingMode = false; isDicePickingMode = true; selectedSlot = slotIdx; 
            if (storedTurnIndex == -1) storedTurnIndex = activePlayerIndex;
            activePlayerIndex = ownerIdx;
            isTurnActive = false; isWaitingForMove = false; UpdateDiceButtons(); return;
        }

        // TARGETING SKILLS
        isTargetingMode = true; isDicePickingMode = false; selectedSkill = skill; selectedSlot = slotIdx; firstSwapTarget = null;
        if (skill == SkillType.SwapPosition) Debug.Log($"🔄 SWAP: Klik PION 1.");
        else if (skill == SkillType.ExtraBlock) Debug.Log($"⏩ MAJU 2: Klik Siapa Saja.");
        else if (IsOffensiveSkill(skill)) Debug.Log($"🔥 Klik MUSUH.");
        else Debug.Log($"🛡️ Klik SENDIRI.");
    }

    // --- KLIK PION (INPUT HANDLER) ---
    public void OnPawnClicked(FollowPath clickedPawn)
    {
        // 1. Mode Skill
        if (isDicePickingMode || isTargetingMode) {
            HandleSkillTargeting(clickedPawn);
            return;
        }

        // 2. Mode Jalan Manual (Selector)
        if (isWaitingForMove) {
            if (clickedPawn.ownerPlayerIndex != activePlayerIndex) { Debug.Log("❌ Bukan pionmu!"); return; }
            if (clickedPawn.isFinished) return;

            ExecuteMove(clickedPawn);
        }
    }

    // --- EKSEKUSI PERGERAKAN ---
    void ExecuteMove(FollowPath pawn)
    {
        // A. Keluar Kandang
        if (!pawn.isOut) {
            if (lastDiceValue == 6) {
                Debug.Log("🚪 Keluar Kandang!");
                pawn.LeaveBase();
                isWaitingForMove = false; eventProcessed = false; 
                Invoke("EndTurn", 0.5f);
            }
        }
        // B. Jalan Biasa
        else {
            if (pawn.CheckPossibleMove(lastDiceValue)) {
                Debug.Log($"🏃 Jalan {lastDiceValue} langkah.");
                pawn.MoveSteps(lastDiceValue);
                isTurnActive = true; 
                isWaitingForMove = false;
            } else {
                Debug.Log("❌ Langkah tidak valid.");
            }
        }
    }

    // --- SMART DICE SYSTEM (PROBABILITY) 🧠🎲 ---
    int GetWeightedDiceRoll(int playerIdx)
    {
        // Hitung pion di base
        int pawnsInBase = 0;
        foreach (var p in players) {
            if (p != null && p.ownerPlayerIndex == playerIdx && !p.isOut && !p.isFinished) {
                pawnsInBase++;
            }
        }

        // Tentukan peluang angka 6
        float chanceForSix = 0.166f; // Normal ~16%
        switch (pawnsInBase) {
            case 4: chanceForSix = 0.55f; break; // 55%
            case 3: chanceForSix = 0.40f; break; // 40%
            case 2: chanceForSix = 0.30f; break; // 30%
            case 1: chanceForSix = 0.20f; break; // 20%
        }

        // Gacha
        if (Random.value < chanceForSix) return 6;
        else return Random.Range(1, 6);
    }

    // --- ROLL DADU UTAMA ---
    public void RollDice()
    {
        // Cek Mode Pilih Dadu (Skill)
        if (isDicePickingMode) {
            lastDiceValue++; if (lastDiceValue > 6) lastDiceValue = 1;
            if (diceTexts.Length > activePlayerIndex) diceTexts[activePlayerIndex].text = lastDiceValue.ToString();
            return;
        }

        if (isTurnActive || isWaitingForMove) return;

        // Reset
        isTargetingMode = false; eventProcessed = false; 
        
        // GUNAKAN SMART DICE
        lastDiceValue = GetWeightedDiceRoll(activePlayerIndex);
        // lastDiceValue = 6; // Uncomment untuk Cheat selalu 6
        
        Debug.Log($"🎲 Roll: {lastDiceValue} (Pemain: {activePlayerIndex})");
        if (diceTexts.Length > activePlayerIndex) diceTexts[activePlayerIndex].text = lastDiceValue.ToString(); 
        SetAllDiceButtonsInteractable(false);

        // --- AUTO-MOVE LOGIC ---
        List<FollowPath> validPawns = GetValidPawns(activePlayerIndex, lastDiceValue);

        if (validPawns.Count == 0) {
            Debug.Log("🚫 Tidak ada langkah valid. Skip.");
            Invoke("EndTurn", 1.0f);
        }
        else if (validPawns.Count == 1) {
            Debug.Log("⚡ Auto-Move (Cuma 1 Opsi).");
            ExecuteMove(validPawns[0]);
        }
        else {
            // Cek apakah semua opsi adalah "Keluar Kandang"?
            bool allInBase = true;
            foreach (var p in validPawns) { if (p.isOut) { allInBase = false; break; } }

            if (allInBase) {
                // Jika semua di base, pilih sembarang (karena hasilnya sama)
                Debug.Log("⚡ Auto-Out (Semua di Base).");
                ExecuteMove(validPawns[0]);
            } else {
                // Ada beda strategi, User wajib pilih
                Debug.Log($"👉 Ada {validPawns.Count} pilihan. KLIK PION!");
                isWaitingForMove = true; 
            }
        }
    }

    // Helper: Cari pion valid
    List<FollowPath> GetValidPawns(int playerIdx, int diceVal) {
        List<FollowPath> validList = new List<FollowPath>();
        foreach (var p in players) {
            if (p == null) continue;
            if (p.ownerPlayerIndex == playerIdx && !p.isFinished) {
                if (!p.isOut && diceVal == 6) validList.Add(p);
                else if (p.isOut && p.CheckPossibleMove(diceVal)) validList.Add(p);
            }
        }
        return validList;
    }

    // --- LOGIKA SKILL EXECUTION ---
    void HandleSkillTargeting(FollowPath clickedPawn) {
        if (isDicePickingMode) {
            if (clickedPawn.ownerPlayerIndex != skillUserIndex) return; 
            Debug.Log($"🔢 Jalan Manual {lastDiceValue}."); eventProcessed = false; 
            if(clickedPawn.MoveSteps(lastDiceValue)) {
                skillInventory[skillUserIndex, selectedSlot] = SkillType.None;
                isDicePickingMode = false; isTurnActive = true; UpdateAllSkillUI();
            } return;
        }

        if(!isTargetingMode) return;
        bool isMyPawn = (clickedPawn.ownerPlayerIndex == skillUserIndex);

        if (selectedSkill == SkillType.SwapPosition) {
            if (!clickedPawn.isOut || clickedPawn.isFinished || clickedPawn.hasEnteredHome) { Debug.Log("❌ Invalid Target."); return; }
            if (firstSwapTarget == null) { firstSwapTarget = clickedPawn; Debug.Log($"1️⃣ Target A OK. Klik Target B."); return; }
            else { if (clickedPawn == firstSwapTarget) return; PerformSwap(firstSwapTarget, clickedPawn); firstSwapTarget = null; FinishSkillUsage(); return; }
        }

        if (IsOffensiveSkill(selectedSkill)) {
            if (isMyPawn) { Debug.Log("❌ Serang musuh!"); return; }
            if (clickedPawn.hasShield) { Debug.Log("🛡️ Blocked!"); clickedPawn.hasShield = false; FinishSkillUsage(); return; }
        } else if (selectedSkill != SkillType.ExtraBlock) { 
            if (!isMyPawn) { Debug.Log("❌ Buff sendiri!"); return; }
        }

        if (selectedSkill == SkillType.PullEnemy) {
            FollowPath myPawn = GetMyActivePawn(skillUserIndex);
            if (myPawn == null) { Debug.Log("❌ Butuh pion aktif!"); return; }
            int dist = CalculateBoardDistance(myPawn.currentPointIndex, clickedPawn.currentPointIndex);
            if (dist > 0 && dist <= 10) { ApplySkillToPawn(clickedPawn, selectedSkill); FinishSkillUsage(); }
            else { Debug.Log($"❌ Kejauhan."); isTargetingMode = false; return; }
        } else {
            ApplySkillToPawn(clickedPawn, selectedSkill); FinishSkillUsage();
        }
    }

    void ApplySkillToPawn(FollowPath pawn, SkillType skill) {
        switch(skill) {
            case SkillType.Shield: pawn.hasShield = true; Debug.Log($"🛡️ SHIELD!"); break;
            case SkillType.FreezeEnemy: pawn.isFrozen = true; Debug.Log($"❄️ FREEZE!"); break;
            case SkillType.PullEnemy:
                FollowPath userPawn = GetMyActivePawn(skillUserIndex);
                if (userPawn != null) {
                    int targetPos = userPawn.currentPointIndex - 1; if (targetPos < 0) targetPos = 51; 
                    pawn.StartReverseEffect(targetPos, false); 
                } break;
            case SkillType.ExtraBlock:
                Debug.Log($"⏩ MAJU 2!"); int forwardPos = (pawn.currentPointIndex + 2) % 52; pawn.StartSlideEffect(forwardPos); break;
            case SkillType.TeleportSafe: pawn.StartSlideEffect(FindNearestSafeZone(pawn.currentPointIndex)); break;
        }
    }

    void PerformSwap(FollowPath p1, FollowPath p2) {
        int i1 = p1.currentPointIndex; Vector3 v1 = p1.transform.position;
        int i2 = p2.currentPointIndex; Vector3 v2 = p2.transform.position;
        p1.TeleportToPosition(i2, v2); p2.TeleportToPosition(i1, v1);
    }
    void FinishSkillUsage() { skillInventory[skillUserIndex, selectedSlot] = SkillType.None; isTargetingMode = false; selectedSkill = SkillType.None; selectedSlot = -1; skillUserIndex = -1; UpdateAllSkillUI(); }

    // --- GAME LOOP ---
    void Update() {
        if (isTurnActive) {
            bool anyMoving = false;
            foreach(var p in players) { if (p != null && (p.isMoving || p.isReversing || p.isSliding)) { anyMoving = true; break; } }
            if (!anyMoving) HandleTurnEvents();
        }
    }

    void HandleTurnEvents() {
        if (eventProcessed) return; 
        foreach(var p in players) {
            if (p.ownerPlayerIndex == activePlayerIndex && p.isOut && !p.isFinished) {
                 if (!p.hasEnteredHome && skillZones.Contains(p.currentPointIndex)) GetRandomSkill(p);
            }
        }
        bool captured = CheckAndCapture();
        eventProcessed = true; 
        Invoke("EndTurn", captured ? 1.0f : 0.5f);
    }

    bool CheckAndCapture() {
        bool captured = false;
        foreach (FollowPath killer in players) {
            if (killer.ownerPlayerIndex != activePlayerIndex || !killer.isOut || killer.hasEnteredHome) continue;
            foreach (FollowPath victim in players) {
                if (victim.ownerPlayerIndex == activePlayerIndex || !victim.isOut || victim.hasEnteredHome) continue;
                if (safeZones.Contains(victim.currentPointIndex)) continue; 
                if (victim.currentPointIndex == killer.currentPointIndex) {
                    if (victim.hasShield) { Debug.Log($"🛡️ Shield!"); victim.hasShield = false; }
                    else { Debug.Log($"⚔️ Makan!"); victim.StartReverseEffect(victim.startIndex, true); captured = true; }
                }
            }
        }
        return captured;
    }

    void EndTurn() {
        isTurnActive = false; isWaitingForMove = false; eventProcessed = false; isTargetingMode = false; isDicePickingMode = false;
        if (lastDiceValue == 6) { Debug.Log("🎉 Bonus Turn!"); UpdateDiceButtons(); return; }
        if (storedTurnIndex != -1) { activePlayerIndex = storedTurnIndex; storedTurnIndex = -1; UpdateDiceButtons(); UpdateAllSkillUI(); return; }
        
        int attempts = 0; 
        do { activePlayerIndex++; if (activePlayerIndex >= 4) activePlayerIndex = 0; attempts++; } 
        while (IsPlayerFinished(activePlayerIndex) && attempts < 4);
        Debug.Log("👉 Giliran: " + activePlayerIndex); UpdateDiceButtons(); UpdateAllSkillUI(); 
    }

    // --- HELPERS ---
    bool IsPlayerFinished(int pIdx) { int c = 0; foreach(var p in players) if(p.ownerPlayerIndex == pIdx && p.isFinished) c++; return c == 4; }
    bool IsOffensiveSkill(SkillType skill) { return skill == SkillType.FreezeEnemy || skill == SkillType.PullEnemy; }
    int CalculateBoardDistance(int f, int t) { return (t - f + 52) % 52; }
    int FindNearestSafeZone(int c) { foreach (int s in safeZones) if (s > c) return s; return safeZones[0]; }
    FollowPath GetMyActivePawn(int idx) { foreach(var p in players) if(p.ownerPlayerIndex == idx && p.isOut && !p.isFinished && !p.hasEnteredHome) return p; return null; }
    string GetSkillDisplayName(SkillType s) { return s.ToString().ToUpper().Replace("SKILLTYPE.", ""); }
    void UpdateAllSkillUI() { 
        void RefreshButtons(Button[] btns, int pIdx) {
            if (btns == null) return;
            for(int i=0; i<btns.Length; i++) {
                SkillType s = skillInventory[pIdx, i]; 
                TextMeshProUGUI tmp = btns[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if(tmp != null) tmp.text = GetSkillDisplayName(s);
                else { Text leg = btns[i].GetComponentInChildren<Text>(true); if(leg != null) leg.text = GetSkillDisplayName(s); }
                if(s != SkillType.None) btns[i].interactable = true; else btns[i].interactable = false;
            }
        }
        RefreshButtons(blueSkillButtons, 0); RefreshButtons(redSkillButtons, 1); RefreshButtons(greenSkillButtons, 2); RefreshButtons(yellowSkillButtons, 3);
    } 
    void UpdateDiceButtons() { 
        if(diceButtons==null)return; 
        for(int i=0; i<diceButtons.Length; i++) { 
            bool myTurn = (i == activePlayerIndex && !IsPlayerFinished(i)); 
            diceButtons[i].interactable = myTurn; 
            diceButtons[i].transform.localScale = myTurn ? Vector3.one*1.1f : Vector3.one; 
        } 
    }
    void SetAllDiceButtonsInteractable(bool s) { foreach(var b in diceButtons) if(b) b.interactable=s; }
}