using UnityEngine;
using Photon.Pun;

/// <summary>
/// Attach ke GameObject mobil.
/// - Android : pakai tombol INTERACT (hijau) yang sudah ada di FloatingJoystick
/// - PC      : tekan E
/// Tidak perlu tombol baru sama sekali.
/// </summary>
[RequireComponent(typeof(VehicleController))]
public class VehicleEntry : MonoBehaviour
{
    [Header("Entry Settings")]
    public float     entryDistance = 2.5f;

    [Tooltip("Buat Empty GameObject di sisi kanan mobil, assign di sini. Kosongkan = pakai pusat mobil.")]
    public Transform entryPoint;

    // Runtime
    private VehicleController vehicle;
    private Transform          localPlayer;
    private bool               playerInside = false;
    private bool               playerNearby = false;

    void Awake()
    {
        vehicle = GetComponent<VehicleController>();
    }

    void Update()
    {
        if (localPlayer == null)
            FindLocalPlayer();

        if (localPlayer == null) return;

        if (!playerInside)
            CheckProximity();

        HandleInput();
    }

    // ─────────────────────────────────────────────
    //  FIND PLAYER
    // ─────────────────────────────────────────────
    void FindLocalPlayer()
    {
        if (PhotonNetwork.IsConnected)
        {
            foreach (var pv in PhotonNetwork.PhotonViewCollection)
            {
                if (pv.IsMine && pv.gameObject.CompareTag("Player"))
                {
                    localPlayer = pv.transform;
                    return;
                }
            }
        }
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) localPlayer = p.transform;
    }

    // ─────────────────────────────────────────────
    //  PROXIMITY — tampilkan label di tombol INTERACT
    // ─────────────────────────────────────────────
    void CheckProximity()
    {
        Vector3 origin = entryPoint != null ? entryPoint.position : transform.position;
        float dist = Vector3.Distance(localPlayer.position, origin);
        bool  near = dist <= entryDistance;

        if (near == playerNearby) return;
        playerNearby = near;

        // Ganti label tombol INTERACT jadi "NAIK" saat dekat, balik lagi saat menjauh
        SetInteractLabel(near ? "NAIK" : "INTERACT");
    }

    // ─────────────────────────────────────────────
    //  INPUT
    // ─────────────────────────────────────────────
    void HandleInput()
    {
        // PENTING: Hanya proses input kendaraan jika player memang dekat atau di dalam.
        // Tanpa guard ini, ConsumeInteract() akan menghabiskan input milik NPC
        // setiap frame, sehingga tombol INTERACT di NPC tidak pernah jalan di Android.
        if (!playerNearby && !playerInside) return;

        // Cek tombol INTERACT (Android) atau E (PC)
        bool interactPressed = Input.GetKeyDown(KeyCode.E);

        // Consume interact dari FloatingJoystick hanya saat kita eligible
        if (!interactPressed && FloatingJoystick.Instance != null)
            interactPressed = FloatingJoystick.Instance.ConsumeInteract();

        if (!interactPressed) return;

        if (!playerInside && playerNearby)
            TryEnter();
        else if (playerInside)
            TryExit();
    }

    // ─────────────────────────────────────────────
    //  ENTER / EXIT
    // ─────────────────────────────────────────────
    void TryEnter()
    {
        if (playerInside || localPlayer == null) return;

        Vector3 origin = entryPoint != null ? entryPoint.position : transform.position;
        float dist = Vector3.Distance(localPlayer.position, origin);
        if (dist > entryDistance + 1f) return;

        playerInside = true;
        playerNearby = false;

        // Ganti label tombol jadi "KELUAR"
        SetInteractLabel("KELUAR");

        var cc = localPlayer.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        var pm = localPlayer.GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = false;

        vehicle.EnterVehicle(localPlayer);

        // Minimap track mobil, bukan player
        if (MinimapSystem.Instance != null)
            MinimapSystem.Instance.SetTrackedTarget(this.transform);

        Debug.Log("[VehicleEntry] Masuk mobil!");
    }

    void TryExit()
    {
        if (!playerInside || localPlayer == null) return;

        playerInside = false;

        // Kembalikan label tombol ke default
        SetInteractLabel("INTERACT");

        vehicle.ExitVehicle(localPlayer);

        // Kembalikan minimap ke player
        if (MinimapSystem.Instance != null)
            MinimapSystem.Instance.ResetTrackedTarget();

        var pm = localPlayer.GetComponent<PlayerMovement>();
        if (pm != null) pm.enabled = true;

        StartCoroutine(ReenableCC());
        Debug.Log("[VehicleEntry] Keluar mobil!");
    }

    System.Collections.IEnumerator ReenableCC()
    {
        yield return null;
        if (localPlayer == null) yield break;
        var cc = localPlayer.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = true;
    }

    // ─────────────────────────────────────────────
    //  HELPER — ganti label tombol INTERACT
    // ─────────────────────────────────────────────
    void SetInteractLabel(string label)
    {
        // Cari tombol INTERACT di canvas FloatingJoystick
        // FloatingJoystick buat tombolnya dengan nama "InteractButton"
        var interactGO = GameObject.Find("InteractButton");
        if (interactGO == null) return;

        var txt = interactGO.GetComponentInChildren<UnityEngine.UI.Text>();
        if (txt != null) txt.text = label;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = entryPoint != null ? entryPoint.position : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, entryDistance);

        if (entryPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(entryPoint.position, 0.2f);
        }
    }
}