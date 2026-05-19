using UnityEngine;

/// <summary>
/// MapCameraRenderer — berdasarkan script asli, fix: CloudOverlay/Rain tidak ikut ke-render
///
/// PERUBAHAN DARI VERSI ASLI:
/// - Timing auto-fit SAMA seperti asli (0.5 detik) → map langsung center dari awal
/// - Tambah excludeLayerNames: CloudOverlay/Rain yang di TransparentFX tidak ikut render
/// - Culling mask lebih ketat: hanya MapLayer, exclude weather layer
///
/// SETUP:
/// 1. Layer "MapLayer" → assign ke semua objek MAP
/// 2. CloudOverlay, Rain Particle, SplashParticle → set Layer ke "TransparentFX"
/// 3. Field "Exclude Layer Names" di Inspector → isi: TransparentFX, UI
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
    public float cameraHeight     = 200f;
    public float orthographicSize = 150f;

    [Tooltip("Nama Layer yang dirender kamera peta (harus sama dengan layer objek peta)")]
    public string mapLayerName = "MapLayer";

    [Header("Exclude dari Map (Weather, dll)")]
    [Tooltip("Layer-layer ini TIDAK dirender di map.\n" +
             "Isi dengan layer CloudOverlay/Rain/SplashParticle kamu.\n" +
             "Default: TransparentFX, UI")]
    public string[] excludeLayerNames = new string[] { "TransparentFX", "UI" };

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
        MapRenderTexture.antiAliasing = 1; // URP Mobile tidak support MSAA di RenderTexture
        MapRenderTexture.Create();
    }

    void SetupCamera()
    {
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

        // ── Culling Mask ──────────────────────────────────────────────────────
        int mapLayer = LayerMask.NameToLayer(mapLayerName);
        if (mapLayer < 0)
        {
            // Layer MapLayer tidak ditemukan → fallback: render semua KECUALI exclude list
            Debug.LogWarning($"[MapCameraRenderer] Layer '{mapLayerName}' tidak ditemukan! " +
                             "Fallback: render semua kecuali exclude layers. " +
                             "Buat layer 'MapLayer' di Edit > Project Settings > Tags and Layers.");
            _mapCamera.cullingMask = ~BuildExcludeMask();
        }
        else
        {
            // Layer MapLayer ditemukan → HANYA render MapLayer
            // CloudOverlay/Rain yang bukan MapLayer otomatis tidak muncul
            _mapCamera.cullingMask = 1 << mapLayer;
            Debug.Log($"[MapCameraRenderer] Culling mask: hanya layer '{mapLayerName}' " +
                      $"(index {mapLayer}). Weather/particles di layer lain tidak akan muncul.");
        }

        // Render ke texture, bukan ke layar
        _mapCamera.targetTexture   = MapRenderTexture;
        _mapCamera.clearFlags      = CameraClearFlags.SolidColor;
        _mapCamera.backgroundColor = new Color(0.05f, 0.08f, 0.05f, 1f);

        // Nonaktifkan audio listener agar tidak bentrok
        AudioListener al = camGO.GetComponent<AudioListener>();
        if (al != null) al.enabled = false;

        // Timing SAMA seperti script asli → map langsung center dari awal
        StartCoroutine(AutoFitToMapObjects());
    }

    /// <summary>
    /// Bangun exclude mask dari excludeLayerNames (untuk fallback mode saja).
    /// </summary>
    int BuildExcludeMask()
    {
        int mask = 0;
        foreach (string layerName in excludeLayerNames)
        {
            int l = LayerMask.NameToLayer(layerName);
            if (l >= 0)
                mask |= (1 << l);
        }
        return mask;
    }

    [ContextMenu("Auto Fit Camera To Map")]
    public void AutoFitCameraToMap()
    {
        if (_mapCamera == null) return;
        int mapLayer = LayerMask.NameToLayer(mapLayerName);
        if (mapLayer < 0) return;

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

        Vector3 center = bounds.center;
        _mapCamera.transform.position = new Vector3(center.x, center.y + cameraHeight, center.z);

        float aspect     = (float)renderWidth / renderHeight;
        float halfWidth  = bounds.extents.x + 5f;
        float halfHeight = bounds.extents.z + 5f;

        orthographicSize            = Mathf.Max(halfHeight, halfWidth / aspect);
        _mapCamera.orthographicSize = orthographicSize;
        _mapCamera.farClipPlane     = cameraHeight + bounds.extents.y * 2f + 50f;

        Debug.Log($"[MapCameraRenderer] Auto-fit OK: center=({center.x:F1},{center.z:F1}), " +
                  $"orthoSize={orthographicSize:F1}");
    }

    // Timing asli: 2 frame + 0.5 detik → map langsung center dari awal, tidak ada delay loncat
    System.Collections.IEnumerator AutoFitToMapObjects()
    {
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.5f);
        AutoFitCameraToMap();
    }

    public void FollowTarget(Transform target, bool followX = true, bool followZ = true)
    {
        if (_mapCamera == null || target == null) return;
        Vector3 pos = _mapCamera.transform.position;
        if (followX) pos.x = target.position.x;
        if (followZ) pos.z = target.position.z;
        _mapCamera.transform.position = pos;
    }

    public void SetOrthoSize(float size)
    {
        if (_mapCamera != null)
            _mapCamera.orthographicSize = Mathf.Clamp(size, 10f, 500f);
    }

    /// <summary>
    /// Panggil dari luar (misal setelah Photon spawn) untuk refresh fit.
    /// </summary>
    public void RefreshAndFit()
    {
        StartCoroutine(AutoFitToMapObjects());
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