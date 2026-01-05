using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;

public enum SkillType
{
    None, 
    Shield, 
    ExtraRoll, 
    ChooseDice, 
    TeleportSafe,
    SwapPosition, 
    FreezeEnemy, 
    PullEnemy, 
    ExtraBlock
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // ==========================================
    // 1. REFERENSI PEMAIN & ASET
    // ==========================================
    [Header("Daftar Pemain (TOTAL 16 PION)")]
    public FollowPath[] players;

    [System.Serializable]
    public class DiceSet
    {
        public Sprite[] faces;
    }

    [Header("UI Dadu Visual")]
    public DiceSet[] playerDiceAssets;
    public Image[] diceImages;

    [Header("UI Inventory Skill (Tombol)")]
    public Button[] blueSkillButtons;
    public Button[] redSkillButtons;
    public Button[] greenSkillButtons;
    public Button[] yellowSkillButtons;

    [Header("Aset Ikon Skill")]
    public Sprite emptySlotSprite; 
    public Sprite shieldIcon;
    public Sprite extraRollIcon;
    public Sprite chooseDiceIcon;
    public Sprite teleportIcon;
    public Sprite swapIcon;
    public Sprite freezeIcon;
    public Sprite pullIcon;
    public Sprite extraBlockIcon;

    // ==========================================
    // [AUDIO] SYSTEM VARIABLES
    // ==========================================
    [Header("Audio System References")]
    public AudioSource bgmSource;       
    public AudioSource sfxSource;       

    [Header("Audio Clips")]
    public AudioClip backgroundMusic;   
    public AudioClip moveStepSound;     
    public AudioClip returnToBaseSound; 
    public AudioClip getSkillSound;     
    public AudioClip finishedSound;     
    public AudioClip attackSound;       
    public AudioClip diceRollSound;
    public AudioClip clickSkillSound;

    // ==========================================
    // 2. STATUS GAME
    // ==========================================
    [Header("Status Game")]
    public int activePlayerIndex = 0;
    public bool isTurnActive = false;
    public bool isWaitingForMove = false;

    // Variabel Internal
    private SkillType[,] skillInventory = new SkillType[4, 3];
    private bool isTargetingMode = false;
    private bool isDicePickingMode = false;
    private bool isDiceRolling = false;
    private int skillUserIndex = -1;
    private SkillType selectedSkill = SkillType.None;
    private int selectedSlot = -1;
    private int storedTurnIndex = -1;

    // Skill Specific
    private FollowPath pullAnchorPawn = null;
    private bool isSelectingPullAnchor = false;
    private FollowPath firstSwapTarget = null;

    [Header("Aturan Main")]
    public int[] safeZones = { 51, 12, 25, 38, 7, 20, 33, 46 };
    public int[] skillZones = { 4, 17, 30, 43 };

    [Header("Dadu Setup")]
    public Button[] diceButtons;
    private int lastDiceValue = 1;
    private bool eventProcessed = false;

    void Awake() 
    { 
        Instance = this; 
    }

    void Start()
    {
        FindButtonsByColor("Blue", ref blueSkillButtons);
        FindButtonsByColor("Red", ref redSkillButtons);
        FindButtonsByColor("Green", ref greenSkillButtons);
        FindButtonsByColor("Yellow", ref yellowSkillButtons);

        if (skillZones == null || skillZones.Length == 0)
            skillZones = new int[] { 4, 17, 30, 43 };

        SetupSkillListeners(blueSkillButtons, 0);
        SetupSkillListeners(redSkillButtons, 1);
        SetupSkillListeners(greenSkillButtons, 2);
        SetupSkillListeners(yellowSkillButtons, 3);

        Debug.Log("✅ GAME MANAGER: Anti-Blink (Z-Offset) Aktif.");

        // Fix: Tampilkan Dadu 1 di awal
        for (int i = 0; i < 4; i++)
        {
            UpdateDiceVisual(i, 1);
        }

        if (diceButtons != null && diceButtons.Length > 0) 
        {
            UpdateDiceButtons();
        }
        
        UpdateAllSkillUI();

        // [AUDIO] Play Background Music
        PlayBGM();
    }

    void Update()
    {
        HandleKeyboardInput();
        
        if (isTurnActive)
        {
            bool anyMoving = false;
            if (players != null)
            {
                foreach (var p in players)
                {
                    if (p != null && (p.isMoving || p.isReversing || p.isSliding))
                    {
                        anyMoving = true; break;
                    }
                }
            }
            if (!anyMoving) HandleTurnEvents();
        }
    }

    void LateUpdate()
    {
        HandlePawnVisuals();
    }

    // ==========================================
    // 3. VISUAL SYSTEM
    // ==========================================
    void HandlePawnVisuals()
    {
        if (players == null) return;

        // A. PION DI BASE
        foreach (var p in players)
        {
            if (p != null && !p.isOut)
            {
                p.transform.localScale = Vector3.Lerp(p.transform.localScale, p.originalScale, Time.deltaTime * 10f);
            }
        }

        // B. PION DI PAPAN
        var activePawns = players.Where(p => p != null && p.isOut && !p.isFinished && !p.isMoving && !p.isReversing && !p.isSliding);
        var groups = activePawns.GroupBy(p => GetPawnPositionHash(p));

        foreach (var group in groups)
        {
            var pawnList = group.OrderBy(p => p.ownerPlayerIndex).ToList();
            int count = pawnList.Count;

            if (count > 1)
            {
                bool hasEnemies = group.Select(p => p.ownerPlayerIndex).Distinct().Count() > 1;
                float spacing = 0.2f; 
                float targetScale = 1.0f;

                for (int i = 0; i < count; i++)
                {
                    var p = pawnList[i];
                    if (hasEnemies)
                    {
                        bool isMyTurn = (p.ownerPlayerIndex == activePlayerIndex);
                        targetScale = isMyTurn ? 1.0f : 0.6f;
                    }
                    else
                    {
                        targetScale = 0.85f; 
                    }

                    p.transform.localScale = Vector3.Lerp(p.transform.localScale, p.originalScale * targetScale, Time.deltaTime * 10f);
                    
                    Vector3 anchorPos = p.GetCurrentAnchorPosition();
                    float xOffset = (i - (count - 1) / 2.0f) * spacing;
                    float zOffset = -0.05f * i; 

                    Vector3 targetPos = anchorPos + new Vector3(xOffset, 0, zOffset);
                    p.transform.position = Vector3.Lerp(p.transform.position, targetPos, Time.deltaTime * 15f);
                }
            }
            else
            {
                foreach (var p in group)
                {
                    p.transform.localScale = Vector3.Lerp(p.transform.localScale, p.originalScale, Time.deltaTime * 10f);
                    Vector3 anchorPos = p.GetCurrentAnchorPosition();
                    if (Vector3.Distance(p.transform.position, anchorPos) > 0.01f)
                    {
                        p.transform.position = Vector3.Lerp(p.transform.position, anchorPos, Time.deltaTime * 15f);
                    }
                }
            }
        }
    }

    string GetPawnPositionHash(FollowPath p)
    {
        if (!p.isOut) return $"Base_{p.ownerPlayerIndex}";
        if (p.hasEnteredHome) return $"Home_{p.ownerPlayerIndex}_{p.currentPointIndex}";
        return $"Main_{p.currentPointIndex}";
    }

    // ==========================================
    // 4. INPUT & UI LOGIC
    // ==========================================
    void UpdateDiceVisual(int pIdx, int val)
    {
        if (playerDiceAssets != null && pIdx < playerDiceAssets.Length && diceImages != null && pIdx < diceImages.Length && diceImages[pIdx] != null)
        {
            if (playerDiceAssets[pIdx].faces != null && playerDiceAssets[pIdx].faces.Length >= val)
            {
                diceImages[pIdx].sprite = playerDiceAssets[pIdx].faces[val - 1];
            }
        }
    }

    void HandleKeyboardInput()
    {
        if (isTurnActive || diceButtons == null) return;

        if (Input.GetKeyDown(KeyCode.Space) && !isWaitingForMove)
        {
            if (activePlayerIndex < diceButtons.Length && diceButtons[activePlayerIndex] != null)
            {
                if (diceButtons[activePlayerIndex].interactable) RollDice();
            }
        }

        if (!isTargetingMode && !isDicePickingMode && !isWaitingForMove)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) OnSkillSlotClicked(activePlayerIndex, 0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) OnSkillSlotClicked(activePlayerIndex, 1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) OnSkillSlotClicked(activePlayerIndex, 2);
        }

        int baseIndex = activePlayerIndex * 4;
        if (Input.GetKeyDown(KeyCode.Z)) SelectPawnByIndex(baseIndex + 0);
        if (Input.GetKeyDown(KeyCode.X)) SelectPawnByIndex(baseIndex + 1);
        if (Input.GetKeyDown(KeyCode.C)) SelectPawnByIndex(baseIndex + 2);
        if (Input.GetKeyDown(KeyCode.V)) SelectPawnByIndex(baseIndex + 3);
    }

    void SelectPawnByIndex(int globalIndex)
    {
        if (players != null && globalIndex >= 0 && globalIndex < players.Length)
        {
            FollowPath targetPawn = players[globalIndex];
            if (targetPawn != null) OnPawnClicked(targetPawn);
        }
    }

    // ==========================================
    // 5. SKILL SYSTEM LOGIC
    // ==========================================
    void SetupSkillListeners(Button[] btns, int pIndex)
    {
        if (btns == null) return;
        for (int i = 0; i < btns.Length; i++)
        {
            if (btns[i] == null) continue;
            int slot = i;
            btns[i].onClick.RemoveAllListeners();
            btns[i].onClick.AddListener(() => OnSkillSlotClicked(pIndex, slot));
        }
    }

    public void GetRandomSkill(FollowPath pawn)
    {
        if (pawn == null) return;
        int pIdx = pawn.ownerPlayerIndex;
        int emptySlot = -1;
        
        for (int i = 0; i < 3; i++)
        {
            if (skillInventory[pIdx, i] == SkillType.None) 
            { 
                emptySlot = i; 
                break; 
            }
        }

        if (emptySlot == -1) 
        { 
            Debug.Log($"🎒 Tas Skill Penuh!"); 
            return; 
        }

        SkillType[] possible = { 
            SkillType.Shield, SkillType.ExtraRoll, SkillType.ChooseDice, 
            SkillType.SwapPosition, SkillType.FreezeEnemy, SkillType.PullEnemy, 
            SkillType.ExtraBlock 
        };
        
        SkillType newSkill = possible[Random.Range(0, possible.Length)];
        skillInventory[pIdx, emptySlot] = newSkill;
        
        // [AUDIO] Play Get Skill
        PlaySFX(getSkillSound);
        
        Debug.Log($"✨ Player {pIdx} dapat {newSkill}");
        UpdateAllSkillUI();
    }

    void OnSkillSlotClicked(int ownerIdx, int slotIdx)
    {
        if (ownerIdx < 0 || ownerIdx >= 4) return;
        
        // Cek giliran (kecuali skill Choose Dice bisa dipakai kapan saja sebelum roll)
        if (ownerIdx != activePlayerIndex && !isDicePickingMode) 
        {
            Debug.Log("⛔ Bukan giliranmu!"); return;
        }

        SkillType skill = skillInventory[ownerIdx, slotIdx];
        
        // Kalau slot kosong, jangan bunyi, jangan lanjut
        if (skill == SkillType.None) return;

        // [AUDIO] Play Suara Klik Skill Disini!
        // Bunyi hanya jika skill ada isinya dan valid
        PlaySFX(clickSkillSound);

        skillUserIndex = ownerIdx;

        // --- Logic Skill ---

        // Choose Dice
        if (skill == SkillType.ChooseDice)
        {
            if (activePlayerIndex != ownerIdx) return;
            Debug.Log($"🔢 CHOOSE DICE");
            isTargetingMode = false; isDicePickingMode = true; selectedSlot = slotIdx;
            if (storedTurnIndex == -1) storedTurnIndex = activePlayerIndex;
            activePlayerIndex = ownerIdx; isTurnActive = false; isWaitingForMove = false;
            UpdateDiceButtons(); return;
        }

        // Extra Roll
        if (skill == SkillType.ExtraRoll)
        {
            Debug.Log($"🎲 EXTRA ROLL!");
            skillInventory[ownerIdx, slotIdx] = SkillType.None;
            if (storedTurnIndex == -1) storedTurnIndex = activePlayerIndex;
            activePlayerIndex = ownerIdx;
            isTurnActive = false; isTargetingMode = false; isDicePickingMode = false; isWaitingForMove = false;
            UpdateDiceButtons(); UpdateAllSkillUI(); return;
        }

        // Pull Enemy
        if (skill == SkillType.PullEnemy)
        {
            isTargetingMode = true; isSelectingPullAnchor = true; selectedSkill = skill; selectedSlot = slotIdx; pullAnchorPawn = null;
            Debug.Log($"🧲 PULL ENEMY: Langkah 1");
            return;
        }

        // Skill Lainnya (Targeting)
        isTargetingMode = true; isDicePickingMode = false; isSelectingPullAnchor = false;
        selectedSkill = skill; selectedSlot = slotIdx; firstSwapTarget = null;
        Debug.Log($"⚔️ Pakai {skill}");
    }

    public void OnPawnClicked(FollowPath clickedPawn)
    {
        if (clickedPawn == null) return;

        if (isDicePickingMode || isTargetingMode)
        {
            HandleSkillTargeting(clickedPawn);
            return;
        }

        if (isWaitingForMove)
        {
            if (clickedPawn.ownerPlayerIndex != activePlayerIndex) { Debug.Log("❌ Bukan pionmu!"); return; }
            if (clickedPawn.isFinished) return;
            ExecuteMove(clickedPawn);
        }
    }

    void HandleSkillTargeting(FollowPath clickedPawn)
    {
        if (clickedPawn == null) return;

        if (isDicePickingMode)
        {
            if (clickedPawn.ownerPlayerIndex != skillUserIndex) return;
            eventProcessed = false;
            if (clickedPawn.MoveSteps(lastDiceValue))
            {
                skillInventory[skillUserIndex, selectedSlot] = SkillType.None;
                isDicePickingMode = false; isTurnActive = true; UpdateAllSkillUI();
            }
            return;
        }

        if (!isTargetingMode) return;
        bool isMyPawn = (clickedPawn.ownerPlayerIndex == skillUserIndex);

        if (selectedSkill == SkillType.PullEnemy)
        {
            if (isSelectingPullAnchor)
            {
                if (!isMyPawn) return;
                if (!clickedPawn.isOut || clickedPawn.isFinished) return;
                pullAnchorPawn = clickedPawn; isSelectingPullAnchor = false; return;
            }
            else
            {
                if (isMyPawn) return;
                if (!clickedPawn.isOut || clickedPawn.hasEnteredHome) return;
                if (clickedPawn.hasShield) { clickedPawn.hasShield = false; FinishSkillUsage(); return; }
                ApplySkillToPawn(clickedPawn, selectedSkill); FinishSkillUsage(); return;
            }
        }

        if (selectedSkill == SkillType.SwapPosition)
        {
            if (!clickedPawn.isOut || clickedPawn.isFinished || clickedPawn.hasEnteredHome) return;
            if (firstSwapTarget == null) { firstSwapTarget = clickedPawn; return; }
            else { if (clickedPawn == firstSwapTarget) return; PerformSwap(firstSwapTarget, clickedPawn); firstSwapTarget = null; FinishSkillUsage(); return; }
        }

        if (selectedSkill == SkillType.Shield) { if (!isMyPawn) return; ApplySkillToPawn(clickedPawn, selectedSkill); FinishSkillUsage(); return; }

        if (selectedSkill == SkillType.ExtraBlock || selectedSkill == SkillType.TeleportSafe)
        {
            if (!isMyPawn) return;
            if (!clickedPawn.isOut) return;
        }

        if (selectedSkill == SkillType.FreezeEnemy)
        {
            if (isMyPawn) return;
            if (!clickedPawn.isOut) return;
            if (clickedPawn.hasShield) { clickedPawn.hasShield = false; FinishSkillUsage(); return; }
        }

        ApplySkillToPawn(clickedPawn, selectedSkill);
        FinishSkillUsage();
    }

    void ApplySkillToPawn(FollowPath pawn, SkillType skill)
    {
        if (pawn == null) return;
        switch (skill)
        {
            case SkillType.Shield: pawn.hasShield = true; break;
            case SkillType.FreezeEnemy: pawn.isFrozen = true; break;
            case SkillType.PullEnemy: 
                if (pullAnchorPawn != null)
                {
                    int targetPos = pullAnchorPawn.currentPointIndex - 1; 
                    if (targetPos < 0) targetPos = 51;
                    pawn.StartReverseEffect(targetPos, false);
                    isTurnActive = true; eventProcessed = false;
                }
                break;
            case SkillType.ExtraBlock: 
                int forwardPos = (pawn.currentPointIndex + 2) % 52;
                pawn.StartSlideEffect(forwardPos); 
                isTurnActive = true; eventProcessed = false;
                break;
            case SkillType.TeleportSafe: 
                pawn.StartSlideEffect(FindNearestSafeZone(pawn.currentPointIndex)); 
                isTurnActive = true; eventProcessed = false;
                break;
        }
    }

    void FinishSkillUsage()
    {
        if (skillUserIndex >= 0) skillInventory[skillUserIndex, selectedSlot] = SkillType.None;
        isTargetingMode = false;
        isSelectingPullAnchor = false;
        pullAnchorPawn = null;
        selectedSkill = SkillType.None;
        selectedSlot = -1;
        skillUserIndex = -1;
        UpdateAllSkillUI();
    }

    // ==========================================
    // 6. GAME LOOP (MOVE)
    // ==========================================
    void ExecuteMove(FollowPath pawn)
    {
        if (pawn == null) return;

        if (!pawn.isOut)
        {
            if (lastDiceValue == 6)
            {
                pawn.LeaveBase();
                isWaitingForMove = false;
                eventProcessed = false;
                Invoke(nameof(EndTurn), 0.5f);
            }
            else
            {
                isWaitingForMove = false;
                Invoke(nameof(EndTurn), 0.5f);
            }
        }
        else
        {
            if (pawn.CheckPossibleMove(lastDiceValue))
            {
                pawn.isRunningMove = (lastDiceValue >= 4);
                pawn.MoveSteps(lastDiceValue);
                isTurnActive = true;
                isWaitingForMove = false;
            }
        }
    }

    public void RollDice()
    {
        // 1. Cek Cooldown: Kalau sedang rolling, tolak input
        if (isDiceRolling) return;

        // --- Logic Khusus: Skill Choose Dice (Instan, tanpa delay 1 detik) ---
        if (isDicePickingMode)
        {
            // Opsional: Kalau mau ada suara klik kecil saat ganti angka
            // PlaySFX(clickSkillSound); 
            
            lastDiceValue++;
            if (lastDiceValue > 6) lastDiceValue = 1;
            UpdateDiceVisual(activePlayerIndex, lastDiceValue);
            return;
        }

        if (isTurnActive || isWaitingForMove) return;

        // 2. Mulai Proses Rolling dengan Delay
        StartCoroutine(RollDiceRoutine());
    }

    // [BARU] Coroutine untuk jeda 1 detik
    System.Collections.IEnumerator RollDiceRoutine()
    {
        isDiceRolling = true; // Kunci input
        
        // Matikan tombol biar kelihatan feedback visual (disable)
        SetAllDiceButtonsInteractable(false); 

        // Play Sound Kocok Dadu
        PlaySFX(diceRollSound);

        // --- ANIMASI VISUAL DADU ACAK (OPSIONAL) ---
        // Kode di bawah ini bikin gambar dadu gonta-ganti cepat selama 1 detik
        float elapsed = 0f;
        float duration = 1.0f; // Durasi sesuai sound
        while(elapsed < duration)
        {
            // Tampilkan angka acak visual saja
            int randomVisual = Random.Range(1, 7);
            UpdateDiceVisual(activePlayerIndex, randomVisual);
            
            elapsed += 0.1f; // Ganti gambar tiap 0.1 detik
            yield return new WaitForSeconds(0.1f);
        }
        // -------------------------------------------

        // Setelah 1 detik, baru hitung hasil dadu yang sebenarnya
        isTargetingMode = false;
        eventProcessed = false;
        
        // Hitung angka final
        lastDiceValue = GetWeightedDiceRoll(activePlayerIndex);
        
        // Tampilkan hasil final
        UpdateDiceVisual(activePlayerIndex, lastDiceValue);
        
        // Logika Game Selanjutnya
        List<FollowPath> validPawns = GetValidPawns(activePlayerIndex, lastDiceValue);

        if (validPawns.Count == 0) Invoke(nameof(EndTurn), 1.0f);
        else if (validPawns.Count == 1) ExecuteMove(validPawns[0]);
        else { isWaitingForMove = true; }

        isDiceRolling = false; // Buka kunci (meski tombol tetap mati krn isWaitingForMove)
    }

    int GetWeightedDiceRoll(int playerIdx)
    {
        int pawnsInBase = 0;
        if (players != null)
        {
            foreach (var p in players)
                if (p != null && p.ownerPlayerIndex == playerIdx && !p.isOut && !p.isFinished) pawnsInBase++;
        }
        float chanceForSix = 0.166f;
        if (pawnsInBase == 4) chanceForSix = 0.55f;
        else if (pawnsInBase == 3) chanceForSix = 0.40f;
        return (Random.value < chanceForSix) ? 6 : Random.Range(1, 7);
    }

    List<FollowPath> GetValidPawns(int playerIdx, int diceVal)
    {
        List<FollowPath> validList = new List<FollowPath>();
        if (players == null) return validList;

        foreach (var p in players)
        {
            if (p == null) continue;
            if (p.ownerPlayerIndex == playerIdx && !p.isFinished)
            {
                if (!p.isOut && diceVal == 6) validList.Add(p);
                else if (p.isOut && p.CheckPossibleMove(diceVal)) validList.Add(p);
            }
        }
        return validList;
    }

    void PerformSwap(FollowPath p1, FollowPath p2)
    {
        int i1 = p1.currentPointIndex; Vector3 v1 = p1.transform.position;
        int i2 = p2.currentPointIndex; Vector3 v2 = p2.transform.position;
        p1.TeleportToPosition(i2, v2);
        p2.TeleportToPosition(i1, v1);
    }

    void HandleTurnEvents()
    {
        if (eventProcessed || players == null) return;

        foreach (var p in players)
        {
            if (p != null && p.ownerPlayerIndex == activePlayerIndex && p.isOut && !p.isFinished && !p.hasEnteredHome && skillZones.Contains(p.currentPointIndex))
                GetRandomSkill(p);
        }

        bool captured = CheckAndCapture();
        eventProcessed = true;

        if (captured) Invoke(nameof(ResetTurnForBonus), 1.0f);
        else Invoke(nameof(EndTurn), 0.5f);
    }

    void ResetTurnForBonus()
    {
        isTurnActive = false;
        isWaitingForMove = false;
        eventProcessed = false;
        isTargetingMode = false;
        isDicePickingMode = false;
        UpdateDiceButtons();
        UpdateAllSkillUI();
    }

    bool CheckAndCapture()
    {
        bool captured = false;
        if (players == null) return false;

        foreach (FollowPath killer in players)
        {
            if (killer == null || killer.ownerPlayerIndex != activePlayerIndex || !killer.isOut || killer.hasEnteredHome) continue;

            foreach (FollowPath victim in players)
            {
                if (victim == null || victim.ownerPlayerIndex == activePlayerIndex || !victim.isOut || victim.hasEnteredHome || safeZones.Contains(victim.currentPointIndex)) continue;

                if (victim.currentPointIndex == killer.currentPointIndex)
                {
                    if (victim.hasShield)
                    {
                        victim.hasShield = false;
                    }
                    else
                    {
                        killer.PlayAttackAnimation();
                        // [AUDIO] Play Attack
                        PlaySFX(attackSound);

                        victim.PlayHurtAnimation();
                        victim.PlayDieAnimation();
                        victim.StartReverseEffect(victim.startIndex, true);
                        
                        // [AUDIO] Play Return Base
                        PlaySFX(returnToBaseSound);

                        captured = true;
                    }
                }
            }
        }
        return captured;
    }

    void EndTurn()
    {
        isTurnActive = false;
        isWaitingForMove = false;
        eventProcessed = false;
        isTargetingMode = false;
        isDicePickingMode = false;

        if (lastDiceValue == 6)
        {
            UpdateDiceButtons();
            return;
        }

        if (players != null)
        {
            foreach (var p in players)
            {
                if (p != null && p.ownerPlayerIndex == activePlayerIndex && p.isFrozen) p.isFrozen = false;
            }
        }

        if (storedTurnIndex != -1)
        {
            activePlayerIndex = storedTurnIndex;
            storedTurnIndex = -1;
            UpdateDiceButtons();
            UpdateAllSkillUI();
            return;
        }

        int attempts = 0;
        do
        {
            activePlayerIndex++;
            if (activePlayerIndex >= 4) activePlayerIndex = 0;
            attempts++;
        }
        while (IsPlayerFinished(activePlayerIndex) && attempts < 4);

        UpdateDiceButtons();
        UpdateAllSkillUI();
    }

    bool IsPlayerFinished(int pIdx)
    {
        int c = 0;
        if (players == null) return false;
        foreach (var p in players)
        {
            if (p != null && p.ownerPlayerIndex == pIdx && p.isFinished) c++;
        }
        return c == 4;
    }

    void FindButtonsByColor(string colorName, ref Button[] buttonArray)
    {
        List<Button> foundButtons = new List<Button>();
        string baseName = colorName + "_SkillButton";
        GameObject btn1 = GameObject.Find(baseName);
        if (btn1 != null) foundButtons.Add(btn1.GetComponent<Button>());
        for (int i = 1; i < 3; i++)
        {
            string nameWithIndex = baseName + " (" + i + ")";
            GameObject btnNext = GameObject.Find(nameWithIndex);
            if (btnNext != null) foundButtons.Add(btnNext.GetComponent<Button>());
        }
        buttonArray = foundButtons.ToArray();
    }

    int FindNearestSafeZone(int c)
    {
        foreach (int s in safeZones) if (s > c) return s;
        return safeZones[0];
    }

    void UpdateAllSkillUI()
    {
        void RefreshButtons(Button[] btns, int pIdx)
        {
            if (btns == null) return;
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null) continue;
                SkillType s = skillInventory[pIdx, i];
                Image btnImg = btns[i].GetComponent<Image>();
                if (btnImg != null)
                {
                    if (s == SkillType.None)
                    {
                        btnImg.sprite = null; 
                        btnImg.color = new Color(1, 1, 1, 0); 
                        btns[i].interactable = false;
                    }
                    else
                    {
                        btnImg.sprite = GetSkillSprite(s);
                        btnImg.color = Color.white; 
                        btns[i].interactable = true;
                    }
                }
            }
        }
        RefreshButtons(blueSkillButtons, 0);
        RefreshButtons(redSkillButtons, 1);
        RefreshButtons(greenSkillButtons, 2);
        RefreshButtons(yellowSkillButtons, 3);
    }

    Sprite GetSkillSprite(SkillType s)
    {
        switch (s)
        {
            case SkillType.Shield: return shieldIcon;
            case SkillType.ExtraRoll: return extraRollIcon;
            case SkillType.ChooseDice: return chooseDiceIcon;
            case SkillType.TeleportSafe: return teleportIcon;
            case SkillType.SwapPosition: return swapIcon;
            case SkillType.FreezeEnemy: return freezeIcon;
            case SkillType.PullEnemy: return pullIcon;
            case SkillType.ExtraBlock: return extraBlockIcon;
            default: return emptySlotSprite;
        }
    }

    void UpdateDiceButtons()
    {
        if (diceButtons == null) return;
        for (int i = 0; i < diceButtons.Length; i++)
        {
            if (diceButtons[i] == null) continue;
            bool myTurn = (i == activePlayerIndex && !IsPlayerFinished(i));
            diceButtons[i].interactable = myTurn;
            diceButtons[i].transform.localScale = myTurn ? Vector3.one * 1.1f : Vector3.one;
        }
    }

    void SetAllDiceButtonsInteractable(bool s)
    {
        if (diceButtons == null) return;
        foreach (var b in diceButtons)
            if (b) b.interactable = s;
    }

    // ==========================================
    // 7. AUDIO HELPER
    // ==========================================
    public void PlayBGM()
    {
        if (bgmSource != null && backgroundMusic != null)
        {
            bgmSource.clip = backgroundMusic;
            bgmSource.loop = true;
            bgmSource.Play();
        }
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    public void PlayMoveSound()
    {
        PlaySFX(moveStepSound);
    }

    public void PlayFinishedSound()
    {
        PlaySFX(finishedSound);
    }
}