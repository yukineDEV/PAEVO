using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Pengaturan Scene")]
    // Pastikan nama ini SAMA PERSIS dengan nama scene game kamu
    public string gameSceneName = "SampleScene"; 

    [Header("Audio Settings")]
    public AudioSource audioSource;   // Drag komponen AudioSource ke sini
    public AudioClip menuBGM;         // Drag lagu background menu ke sini
    public AudioClip buttonClickSFX;  // Drag suara klik tombol ke sini (opsional)

    void Start()
    {
        // Jalankan Musik Background saat Menu dimulai
        if (audioSource != null && menuBGM != null)
        {
            audioSource.clip = menuBGM;
            audioSource.loop = true; // Agar lagu mengulang terus
            audioSource.Play();
        }
    }

    // Fungsi untuk memulai game
    public void PlayGame()
    {
        PlayClickSound(); // Bunyikan suara klik
        Debug.Log("🚀 Memulai Permainan...");
        SceneManager.LoadScene(gameSceneName);
    }

    // Fungsi untuk keluar game
    public void QuitGame()
    {
        PlayClickSound(); // Bunyikan suara klik
        Debug.Log("🚪 Keluar Game");
        Application.Quit();
    }

    // Fungsi tambahan: Panggil ini di Inspector Button -> On Click() jika ingin suara saja tanpa pindah scene
    public void PlayClickSound()
    {
        if (audioSource != null && buttonClickSFX != null)
        {
            audioSource.PlayOneShot(buttonClickSFX);
        }
    }
}