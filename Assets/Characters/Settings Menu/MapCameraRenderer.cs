using UnityEngine;
using System.Collections;

/// <summary>
/// MapCameraRenderer — STATIC MAP: Render sekali, texture dipakai terus
///
/// Cocok untuk map statis (gedung tidak bergerak).
/// Map Camera hanya render 1x saat pertama dibuka, hasilnya disimpan di RenderTexture.
/// Tidak ada render ulang → performa paling ringan.
///
/// SETUP:
/// 1. Skripsi_Kampus_3 → layer "Default"
/// 2. CloudOverlay, Rain, Splash → layer "TransparentFX"
/// 3. Panggil MapCameraRenderer.Instance.SetMinimapActive(true) saat buka minimap
/// </summary>
public class MapCameraRenderer : MonoBehaviour
{
    public static MapCameraRenderer Instance { get; private set; }

    [Header("Render Texture")]
    [Tooltip("Resolusi minimap. Rekomendasi mobile: 1024x512")]
    public int renderWidth  = 1024;
    public int renderHeight = 512;

    [Header("Camera Settings")]
    public float cameraHeight     = 200f;
    public float orthographicSize = 150f;

    [Header("Layer Settings")]
    [Tooltip("Layer yang TIDAK dirender di minimap (weather, UI, dll)")]
    public string[] excludeLayerNames = new string[]
    {
        "TransparentFX",
        "UI",
    };

    [Header("Main Camera")]
    public bool   autoExcludeMapLayerFromMainCam = true;
    public string mainCameraTag = "MainCamera";

    public RenderTexture MapRenderTexture { get; private set; }

    private Camera _mapCamera;
    private bool   _hasRendered = false; // Flag: sudah render atau belum

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupRenderTexture();
        SetupCamera();

        if (autoExcludeMapLayerFromMainCam)
            ExcludeMapLayerFromMainCamera();
    }

    void SetupRenderTexture()
    {
        MapRenderTexture = new RenderTexture(renderWidth, renderHeight, 16, RenderTextureFormat.ARGB32);
        MapRenderTexture.filterMode   = FilterMode.Bilinear;
        MapRenderTexture.antiAliasing = 1;
        MapRenderTexture.Create();
    }

    void SetupCamera()
    {
        GameObject camGO = new GameObject("_MapCameraInternal");
        camGO.transform.SetParent(transform);
        DontDestroyOnLoad(camGO);

        _mapCamera = camGO.AddComponent<Camera>();

        camGO.transform.position = new Vector3(0f, cameraHeight, 0f);
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        _mapCamera.orthographic     = true;
        _mapCamera.orthographicSize = orthographicSize;
        _mapCamera.nearClipPlane    = 0.1f;
        _mapCamera.farClipPlane     = cameraHeight + 50f;
        _mapCamera.cullingMask      = ~BuildExcludeMask();
        _mapCamera.targetTexture    = MapRenderTexture;
        _mapCamera.clearFlags       = CameraClearFlags.SolidColor;
        _mapCamera.backgroundColor  = new Color(0.05f, 0.08f, 0.05f, 1f);

        // Camera di-disable — tidak render otomatis sama sekali
        _mapCamera.enabled = false;

        AudioListener al = camGO.GetComponent<AudioListener>();
        if (al != null) al.enabled = false;

        StartCoroutine(AutoFitThenRender());
    }

    /// <summary>
    /// Dipanggil saat minimap dibuka.
    /// Render hanya dilakukan SEKALI — setelah itu texture langsung dipakai terus.
    /// </summary>
    public void SetMinimapActive(bool active)
    {
        if (active && !_hasRendered)
        {
            RenderOnce();
        }
        // Tidak ada render ulang walau dipanggil berkali-kali
    }

    /// <summary>
    /// Render tepat 1 frame ke RenderTexture, lalu stop.
    /// </summary>
    void RenderOnce()
    {
        if (_mapCamera == null) return;
        _mapCamera.Render();
        _hasRendered = true;
        Debug.Log("[MapCameraRenderer] Map dirender 1x. Texture siap dipakai.");
    }

    /// <summary>
    /// Paksa render ulang (misal setelah scene berubah / ada renovasi gedung).
    /// Panggil manual: MapCameraRenderer.Instance.ForceRerender();
    /// </summary>
    public void ForceRerender()
    {
        _hasRendered = false;
        RenderOnce();
        Debug.Log("[MapCameraRenderer] Force re-render dilakukan.");
    }

    IEnumerator AutoFitThenRender()
    {
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.5f);
        AutoFitCameraToMap();

        // Render sekali setelah auto-fit selesai
        // Texture langsung siap walau minimap belum dibuka
        RenderOnce();
    }

    [ContextMenu("Auto Fit Camera To Map")]
    public void AutoFitCameraToMap()
    {
        if (_mapCamera == null) return;

        int excludeMask = BuildExcludeMask();
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool found = false;

        foreach (Renderer r in allRenderers)
        {
            if ((excludeMask & (1 << r.gameObject.layer)) != 0) continue;
            if (r.gameObject.layer == LayerMask.NameToLayer("UI")) continue;

            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!found) { Debug.LogWarning("[MapCameraRenderer] Tidak ada objek!"); return; }

        Vector3 center = bounds.center;
        float   aspect = (float)renderWidth / renderHeight;

        _mapCamera.transform.position = new Vector3(center.x, center.y + cameraHeight, center.z);
        orthographicSize              = Mathf.Max(bounds.extents.z + 5f, (bounds.extents.x + 5f) / aspect);
        _mapCamera.orthographicSize   = orthographicSize;
        _mapCamera.farClipPlane       = cameraHeight + bounds.extents.y * 2f + 50f;

        Debug.Log($"[MapCameraRenderer] Auto-fit OK: center=({center.x:F1},{center.z:F1}), orthoSize={orthographicSize:F1}");
    }

    void ExcludeMapLayerFromMainCamera()
    {
        int mapLayerIdx = LayerMask.NameToLayer("MapLayer");
        if (mapLayerIdx < 0) return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
            mainCam = GameObject.FindGameObjectWithTag(mainCameraTag)?.GetComponent<Camera>();

        if (mainCam == null)
        {
            Debug.LogWarning("[MapCameraRenderer] Main Camera tidak ditemukan!");
            return;
        }

        mainCam.cullingMask &= ~(1 << mapLayerIdx);
        Debug.Log("[MapCameraRenderer] Main Camera: MapLayer di-exclude.");
    }

    int BuildExcludeMask()
    {
        int mask = 0;
        foreach (string layerName in excludeLayerNames)
        {
            int l = LayerMask.NameToLayer(layerName);
            if (l >= 0) mask |= (1 << l);
        }
        return mask;
    }

    // FollowTarget tidak perlu render ulang — map statis
    public void FollowTarget(Transform target, bool followX = true, bool followZ = true)
    {
        if (_mapCamera == null || target == null) return;
        Vector3 pos = _mapCamera.transform.position;
        if (followX) pos.x = target.position.x;
        if (followZ) pos.z = target.position.z;
        _mapCamera.transform.position = pos;
        // Tidak render ulang — hanya geser posisi kamera di texture yang sudah ada
    }

    public void SetOrthoSize(float size)
    {
        if (_mapCamera != null)
            _mapCamera.orthographicSize = Mathf.Clamp(size, 10f, 500f);
    }

    public void RefreshAndFit()
    {
        StartCoroutine(AutoFitThenRender());
        _hasRendered = false;
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