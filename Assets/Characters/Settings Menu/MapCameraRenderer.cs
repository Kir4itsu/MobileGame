using UnityEngine;

/// <summary>
/// MapCameraRenderer
/// Attach ke GameObject baru (misal "MapCamera") di scene.
/// Script ini membuat Camera overhead yang hanya nge-render
/// GameObject ber-tag "Map" ke sebuah RenderTexture.
///
/// SETUP DI UNITY:
/// 1. Buat Layer baru bernama "MapLayer" (Edit > Project Settings > Tags and Layers)
/// 2. Tandai semua objek peta kamu (tag "Map") ke layer "MapLayer"
/// 3. Attach script ini ke GameObject kosong "MapCamera"
/// 4. Pastikan SettingsMenu.cs sudah diupdate (pakai BuildMapContent versi baru)
/// </summary>
public class MapCameraRenderer : MonoBehaviour
{
    public static MapCameraRenderer Instance { get; private set; }

    [Header("Render Texture")]
    [Tooltip("Resolusi RenderTexture — makin besar makin tajam tapi makin berat")]
    public int renderWidth  = 1920;
    public int renderHeight = 1080;

    [Header("Camera Settings")]
    [Tooltip("Tinggi kamera dari atas (orthographic size)")]
    public float cameraHeight    = 200f;
    public float orthographicSize = 150f;

    [Tooltip("Nama Layer yang dirender kamera peta (harus sama dengan layer objek peta)")]
    public string mapLayerName = "MapLayer";

    // RenderTexture yang bisa diambil SettingsMenu
    public RenderTexture MapRenderTexture { get; private set; }

    private Camera _mapCamera;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupRenderTexture();
        SetupCamera();
    }

    void SetupRenderTexture()
    {
        MapRenderTexture = new RenderTexture(renderWidth, renderHeight, 16, RenderTextureFormat.ARGB32);
        MapRenderTexture.filterMode = FilterMode.Bilinear;
        MapRenderTexture.antiAliasing = 2;
        MapRenderTexture.Create();
    }

    void SetupCamera()
    {
        // Buat GameObject kamera
        GameObject camGO = new GameObject("_MapCameraInternal");
        camGO.transform.SetParent(transform);
        DontDestroyOnLoad(camGO);

        _mapCamera = camGO.AddComponent<Camera>();

        // Posisi: tinggi dari atas, menghadap ke bawah
        camGO.transform.position = new Vector3(0f, cameraHeight, 0f);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        // Orthographic agar tidak ada distorsi perspektif
        _mapCamera.orthographic     = true;
        _mapCamera.orthographicSize = orthographicSize;
        _mapCamera.nearClipPlane    = 0.1f;
        _mapCamera.farClipPlane     = cameraHeight + 50f;

        // Hanya render layer MapLayer
        int mapLayer = LayerMask.NameToLayer(mapLayerName);
        if (mapLayer < 0)
        {
            Debug.LogWarning($"[MapCameraRenderer] Layer '{mapLayerName}' tidak ditemukan! " +
                             "Buat layer baru di Edit > Project Settings > Tags and Layers.");
            _mapCamera.cullingMask = ~(LayerMask.GetMask("UI"));
        }
        else
        {
            _mapCamera.cullingMask = 1 << mapLayer;
        }

        // Render ke texture, bukan ke layar
        _mapCamera.targetTexture = MapRenderTexture;

        // Background gelap
        _mapCamera.clearFlags       = CameraClearFlags.SolidColor;
        _mapCamera.backgroundColor  = new Color(0.05f, 0.08f, 0.05f, 1f);

        // Nonaktifkan audio listener agar tidak bentrok
        AudioListener al = camGO.GetComponent<AudioListener>();
        if (al != null) al.enabled = false;

        // Auto-fit kamera ke semua objek di MapLayer saat Start
        StartCoroutine(AutoFitToMapObjects());
    }

    /// <summary>
    /// Auto-fit kamera overhead agar semua objek di MapLayer terlihat penuh,
    /// ter-center, dan tidak ada area hitam yang tidak perlu.
    /// Dipanggil otomatis saat Awake; bisa juga dipanggil manual lewat inspector.
    /// </summary>
    [ContextMenu("Auto Fit Camera To Map")]
    public void AutoFitCameraToMap()
    {
        if (_mapCamera == null) return;
        int mapLayer = LayerMask.NameToLayer(mapLayerName);
        if (mapLayer < 0) return;

        // Kumpulkan semua Renderer di layer MapLayer
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;
        foreach (Renderer r in allRenderers)
        {
            if (r.gameObject.layer == mapLayer)
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
        }

        if (!found)
        {
            Debug.LogWarning("[MapCameraRenderer] Tidak ada objek di layer MapLayer!");
            return;
        }

        // Posisikan kamera di center XZ dari bounds, tinggi = cameraHeight
        Vector3 center = bounds.center;
        _mapCamera.transform.position = new Vector3(center.x, center.y + cameraHeight, center.z);

        // Hitung orthographic size agar seluruh bounds muat
        // Aspect ratio RenderTexture = width / height
        float aspect     = (float)renderWidth / renderHeight;
        float halfWidth  = bounds.extents.x + 5f;  // +5 padding
        float halfHeight = bounds.extents.z + 5f;  // Z karena kamera top-down

        // Pilih ukuran yang cukup untuk cover kedua dimensi
        float sizeFromHeight = halfHeight;
        float sizeFromWidth  = halfWidth / aspect;
        orthographicSize = Mathf.Max(sizeFromHeight, sizeFromWidth);
        _mapCamera.orthographicSize = orthographicSize;
        _mapCamera.farClipPlane     = cameraHeight + bounds.extents.y * 2f + 50f;

        Debug.Log($"[MapCameraRenderer] Auto-fit: center={center}, orthoSize={orthographicSize:F1}, bounds={bounds.size}");
    }

    System.Collections.IEnumerator AutoFitToMapObjects()
    {
        // Tunggu 1 frame agar semua objek sudah di-spawn/loaded
        yield return null;
        AutoFitCameraToMap();
    }

    /// <summary>
    /// Ikuti posisi target (misal: player) — opsional.
    /// Panggil dari luar: MapCameraRenderer.Instance.FollowTarget(playerTransform);
    /// </summary>
    public void FollowTarget(Transform target, bool followX = true, bool followZ = true)
    {
        if (_mapCamera == null || target == null) return;
        Vector3 pos = _mapCamera.transform.position;
        if (followX) pos.x = target.position.x;
        if (followZ) pos.z = target.position.z;
        _mapCamera.transform.position = pos;
    }

    /// <summary>
    /// Ubah ketinggian/zoom kamera (orthographic size).
    /// Nilai kecil = zoom in, nilai besar = zoom out.
    /// </summary>
    public void SetOrthoSize(float size)
    {
        if (_mapCamera != null)
            _mapCamera.orthographicSize = Mathf.Clamp(size, 10f, 500f);
    }

    void OnDestroy()
    {
        if (MapRenderTexture != null)
        {
            MapRenderTexture.Release();
            Destroy(MapRenderTexture);
        }
    }
}