using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PhoneManager - Mengontrol buka/tutup HP in-game
/// Cara pakai:
/// 1. Buat GameObject kosong, attach script ini
/// 2. Assign phoneUI (Panel HP), phoneButton (tombol floating), openSound, closeSound
/// 3. Untuk PC: tekan panah atas untuk toggle HP
/// 4. Untuk Android: tap tombol floating
/// </summary>
public class PhoneManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject phoneUI;           // Panel utama UI HP
    public Button phoneButton;           // Floating button di layar
    public Animator phoneAnimator;       // (Opsional) Animator untuk animasi HP muncul

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    [Header("Settings")]
    public KeyCode pcOpenKey = KeyCode.UpArrow; // Tombol PC untuk buka HP

    private bool isPhoneOpen = false;

    void Start()
    {
        // Pastikan HP tertutup di awal
        if (phoneUI != null)
            phoneUI.SetActive(false);

        // Daftarkan tombol floating
        if (phoneButton != null)
            phoneButton.onClick.AddListener(TogglePhone);
    }

    void Update()
    {
        // Input PC: tekan panah atas
        if (Input.GetKeyDown(pcOpenKey))
        {
            TogglePhone();
        }
    }

    public void TogglePhone()
    {
        isPhoneOpen = !isPhoneOpen;

        if (phoneUI != null)
        {
            phoneUI.SetActive(isPhoneOpen);

            // Mainkan animasi jika ada Animator
            if (phoneAnimator != null)
                phoneAnimator.SetBool("IsOpen", isPhoneOpen);
        }

        // Mainkan suara
        if (audioSource != null)
        {
            AudioClip clip = isPhoneOpen ? openSound : closeSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        Debug.Log($"[PhoneManager] HP {(isPhoneOpen ? "dibuka" : "ditutup")}");
    }

    public void ClosePhone()
    {
        if (isPhoneOpen)
            TogglePhone();
    }

    public bool IsPhoneOpen => isPhoneOpen;
}