using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PhoneManager - Mengontrol buka/tutup HP in-game
/// Cara pakai:
/// 1. Buat GameObject kosong, attach script ini
/// 2. Assign phoneUI (Panel HP), phoneButton (tombol floating), openSound, closeSound
/// 3. Untuk PC: tekan panah atas untuk toggle HP
/// 4. Untuk Android: tap tombol floating, atau tekan Back 2x (Back pertama = ke Home, Back kedua = tutup HP)
///
/// FIX:
/// - HideMobileUI / ShowMobileUI sekarang dipanggil LANGSUNG di dalam TogglePhone()
///   dan ClosePhone(), bukan lewat polling Update() di PhoneVisibilityHook.
///   Ini memastikan semua tombol HUD (Phone, TPP, INTERACT, RUN) langsung
///   hilang pada frame yang sama saat HP dibuka — tidak ada delay 1 frame.
/// - PhoneVisibilityHook tetap ada sebagai fallback, tapi tidak lagi jadi
///   satu-satunya mekanisme hide/show.
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
    public KeyCode pcOpenKey = KeyCode.PageUp; // Tombol PC untuk buka/tutup HP

    private bool isPhoneOpen = false;
    private bool _isInVehicle = false;  // diset oleh VehicleController
    private float _lastToggleTime = -999f;
    private const float TOGGLE_COOLDOWN = 0.3f; // detik minimum antar toggle

    public static PhoneManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

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
        // PC: PageUp untuk toggle HP
        if (Input.GetKeyDown(pcOpenKey))
            TogglePhone();

        // Android Back Button / PC Escape → delegasi ke PhoneNavigator
        // GoBack() di PhoneNavigator sudah handle:
        //   - Jika di sub-panel → balik ke Home
        //   - Jika di Home      → tutup HP (panggil ClosePhone)
        if (isPhoneOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            var nav = FindFirstObjectByType<PhoneNavigator>();
            if (nav != null)
                nav.GoBack();
            else
                ClosePhone(); // fallback jika PhoneNavigator tidak ada
        }
    }

    /// <summary>Dipanggil VehicleController — blok phone saat berkendara.</summary>
    public void SetInVehicle(bool inVehicle)
    {
        _isInVehicle = inVehicle;
        // Paksa tutup phone jika sedang terbuka saat masuk kendaraan
        if (inVehicle && isPhoneOpen)
            ClosePhone();
    }

    public void TogglePhone()
    {
        // Jangan buka phone saat di dalam kendaraan
        if (_isInVehicle) return;

        // Debounce — abaikan jika dipanggil terlalu cepat (double-trigger guard)
        float now = Time.unscaledTime;
        if (now - _lastToggleTime < TOGGLE_COOLDOWN) return;
        _lastToggleTime = now;

        isPhoneOpen = !isPhoneOpen;

        if (phoneUI != null)
        {
            phoneUI.SetActive(isPhoneOpen);

            // Mainkan animasi jika ada Animator
            if (phoneAnimator != null)
                phoneAnimator.SetBool("IsOpen", isPhoneOpen);
        }

        // ── FIX: Langsung hide/show semua tombol HUD di frame yang sama ──
        // Tidak menunggu PhoneVisibilityHook polling di Update() berikutnya.
        ApplyHUDVisibility(isPhoneOpen);

        // Mainkan suara
        if (audioSource != null)
        {
            AudioClip clip = isPhoneOpen ? openSound : closeSound;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        Debug.Log($"[PhoneManager] HP {(isPhoneOpen ? "dibuka" : "ditutup")}");
    }

    public void OpenPhone()
    {
        if (!isPhoneOpen)
            TogglePhone();
    }

    public void ClosePhone()
    {
        if (isPhoneOpen)
            TogglePhone();
    }

    /// <summary>
    /// Hide semua tombol HUD saat HP buka, show kembali saat HP tutup.
    /// Dipanggil langsung dari TogglePhone() supaya sinkron di frame yang sama.
    /// </summary>
    void ApplyHUDVisibility(bool phoneIsOpen)
    {
        if (FloatingJoystick.Instance == null)
        {
            Debug.LogWarning("[PhoneManager] FloatingJoystick.Instance null — tombol HUD tidak bisa di-hide.");
            return;
        }

        if (phoneIsOpen)
            FloatingJoystick.Instance.HideMobileUI();
        else
            FloatingJoystick.Instance.ShowMobileUI();
    }

    public bool IsPhoneOpen => isPhoneOpen;
}