using UnityEngine;
using UnityEngine.InputSystem; // <-- Pakai ini kalau pakai Input System baru

public class GameManager : MonoBehaviour
{
    [Header("Daftar Pemain")]
    public FollowPath[] players; // Array untuk menyimpan 4 pion (Merah, Hijau, Kuning, Biru)
    
    [Header("Status Game")]
    public int activePlayerIndex = 0; // 0=P1, 1=P2, 2=P3, 3=P4
    public bool isTurnActive = false; // Apakah sedang ada pion yang jalan?

    void Update()
    {
        // 1. Cek apakah giliran sedang berlangsung?
        if (isTurnActive)
        {
            // Cek apakah pion yang aktif SUDAH BERHENTI jalan?
            if (!players[activePlayerIndex].isMoving)
            {
                // Kalau sudah berhenti, oper giliran ke pemain berikutnya
                EndTurn();
            }
            return; // Jangan lakukan apa-apa lagi sampai giliran selesai
        }

        // 2. Input Lempar Dadu (Sementara pakai Spasi dulu)
        // Nanti ini bisa dipanggil lewat Tombol UI di layar
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RollDice();
        }
    }

    public void RollDice()
    {
        // Acak angka 1 sampai 6
        int diceValue = Random.Range(1, 7);
        Debug.Log($"Pemain {activePlayerIndex} dapat angka: {diceValue}");

        // Perintahkan pion aktif untuk jalan
        players[activePlayerIndex].MoveSteps(diceValue);
        
        // Tandai bahwa giliran sedang berjalan (tunggu sampai pion berhenti)
        isTurnActive = true;
    }

    void EndTurn()
    {
        Debug.Log("Giliran Selesai. Ganti Pemain.");
        isTurnActive = false;

        // Pindah index ke pemain berikutnya
        activePlayerIndex++;

        // Kalau sudah lewat pemain terakhir, balik ke pemain pertama (Looping 0-1-2-3 -> 0)
        if (activePlayerIndex >= players.Length)
        {
            activePlayerIndex = 0;
        }

        Debug.Log($"Sekarang Giliran Pemain Index: {activePlayerIndex}");
    }
}