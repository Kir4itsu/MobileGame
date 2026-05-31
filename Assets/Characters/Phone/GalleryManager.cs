using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// GalleryManager — Galeri foto in-game, bisa dibuka dari Phone menu.
///
/// CARA PAKAI:
/// - Di-attach otomatis oleh PhoneUIBuilder
/// - CameraMode.TakePhoto() otomatis menambah foto ke sini via AddPhoto()
/// - Tap "Camera" → tap ikon galeri → OpenGallery()
/// - Di phone home menu, bisa juga tambahkan item "Gallery" yang panggil OpenGallery()
///
/// FITUR:
/// - Grid foto 2 kolom di dalam phone UI
/// - Tap foto → tampilkan fullscreen
/// - Foto disimpan selama session (tidak persist — untuk persist, pakai PlayerPrefs path)
/// - Scroll view
/// </summary>
public class GalleryManager : MonoBehaviour
{
    // ── Public refs ───────────────────────────────────────────────
    [HideInInspector] public PhoneManager   phoneManager;
    [HideInInspector] public PhoneNavigator phoneNavigator;

    // ── Data ──────────────────────────────────────────────────────
    private readonly List<PhotoEntry> _photos = new List<PhotoEntry>();

    // ── UI refs ───────────────────────────────────────────────────
    private GameObject _galleryPanel;   // panel di dalam phone
    private Transform  _gridContent;    // parent grid foto
    private GameObject _fullscreenView; // fullscreen preview
    private RawImage   _fullscreenImg;
    private Canvas     _canvas;
    private bool       _isOpen = false;

    // ── Warna ─────────────────────────────────────────────────────
    static readonly Color C_BG      = new Color(0.05f, 0.05f, 0.05f, 1f);
    static readonly Color C_PANEL   = new Color(0.08f, 0.08f, 0.08f, 1f);
    static readonly Color C_GREEN   = new Color(0.30f, 0.69f, 0.31f, 1f);
    static readonly Color C_WHITE   = new Color(0.87f, 0.87f, 0.87f, 1f);
    static readonly Color C_GRAY    = new Color(0.40f, 0.40f, 0.40f, 1f);
    static readonly Color C_OVERLAY = new Color(0f,    0f,    0f,    0.85f);

    // ─────────────────────────────────────────────────────────────
    class PhotoEntry
    {
        public Texture2D texture;
        public string    fileName;
        public string    timestamp;
    }

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        _canvas = FindFirstObjectByType<Canvas>();
    }

    // ═════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ═════════════════════════════════════════════════════════════

    /// <summary>Tambah foto baru ke galeri in-game. Dipanggil oleh CameraMode.</summary>
    public void AddPhoto(Texture2D tex, string fileName)
    {
        _photos.Add(new PhotoEntry
        {
            texture   = tex,
            fileName  = fileName,
            timestamp = System.DateTime.Now.ToString("dd MMM yyyy HH:mm")
        });

        // Refresh grid jika galeri sedang terbuka
        if (_isOpen)
            RefreshGrid();

        Debug.Log("[GalleryManager] Foto ditambahkan: " + fileName + " (total: " + _photos.Count + ")");
    }

    /// <summary>Buka panel galeri di dalam phone.</summary>
    public void OpenGallery()
    {
        if (_galleryPanel == null)
            BuildGalleryPanel();

        // Buka phone dulu jika belum
        if (phoneManager != null && !phoneManager.IsPhoneOpen)
            phoneManager.OpenPhone();

        // Navigasi ke panel galeri
        if (phoneNavigator != null)
            phoneNavigator.OpenPanel(_galleryPanel);

        _isOpen = true;
        RefreshGrid();
    }

    /// <summary>Tutup galeri, kembali ke home phone.</summary>
    public void CloseGallery()
    {
        _isOpen = false;
        if (phoneNavigator != null)
            phoneNavigator.GoBack();
    }

    // ═════════════════════════════════════════════════════════════
    //  BUILD GALLERY PANEL (di dalam phone screen)
    // ═════════════════════════════════════════════════════════════
    void BuildGalleryPanel()
    {
        // Cari PhoneScreen sebagai parent
        var phoneScreen = GameObject.Find("PhoneScreen");
        Transform screenParent = phoneScreen != null
            ? phoneScreen.transform
            : _canvas.transform;

        _galleryPanel = new GameObject("GalleryPanel");
        _galleryPanel.transform.SetParent(screenParent, false);

        var rt = _galleryPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(0f,  50f);  // atas NavBar
        rt.offsetMax = new Vector2(0f, -36f);  // bawah StatusBar
        _galleryPanel.AddComponent<Image>().color = C_PANEL;

        // ── Header ───────────────────────────────────────────────
        var header = new GameObject("Header");
        header.transform.SetParent(_galleryPanel.transform, false);
        var hRT = header.AddComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0f, 1f); hRT.anchorMax = new Vector2(1f, 1f);
        hRT.pivot     = new Vector2(0.5f, 1f);
        hRT.anchoredPosition = Vector2.zero;
        hRT.sizeDelta = new Vector2(0f, 50f);
        header.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.07f, 1f);

        var hTxt = MakeText(header.transform, "GALERI", 25, C_GREEN, TextAnchor.MiddleLeft, FontStyle.Bold);
        var hTxtRT = hTxt.GetComponent<RectTransform>();
        hTxtRT.anchorMin = Vector2.zero; hTxtRT.anchorMax = Vector2.one;
        hTxtRT.offsetMin = new Vector2(14f, 0f); hTxtRT.offsetMax = Vector2.zero;

        // Jumlah foto
        var countGO = MakeText(header.transform, "0 foto", 18, C_GRAY, TextAnchor.MiddleRight);
        var cRT     = countGO.GetComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = Vector2.zero; cRT.offsetMax = new Vector2(-14f, 0f);
        countGO.name = "PhotoCount";

        // ── Empty state label ─────────────────────────────────────
        var emptyGO = MakeText(_galleryPanel.transform, "Belum ada foto.\nAmbil foto dulu!", 22,
            C_GRAY, TextAnchor.MiddleCenter);
        var eRT = emptyGO.GetComponent<RectTransform>();
        eRT.anchorMin = Vector2.zero; eRT.anchorMax = Vector2.one;
        eRT.offsetMin = eRT.offsetMax = Vector2.zero;
        emptyGO.name = "EmptyLabel";

        // ── Scroll view grid ──────────────────────────────────────
        var scrollGO = new GameObject("GalleryScroll");
        scrollGO.transform.SetParent(_galleryPanel.transform, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 0f);
        scrollRT.anchorMax = new Vector2(1f, 1f);
        scrollRT.offsetMin = new Vector2(0f,  0f);
        scrollRT.offsetMax = new Vector2(0f, -50f); // bawah header
        scrollGO.AddComponent<Image>().color = Color.clear;

        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical   = true;

        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var vpRT = viewportGO.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = vpRT.offsetMax = Vector2.zero;
        viewportGO.AddComponent<Image>().color = Color.clear;
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;

        // Content (grid)
        var contentGO = new GameObject("GridContent");
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 0f); // tinggi diatur ContentSizeFitter

        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(120f, 120f);
        grid.spacing         = new Vector2(4f, 4f);
        grid.padding         = new RectOffset(6, 6, 6, 6);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment  = TextAnchor.UpperCenter;

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content  = contentRT;
        scroll.viewport = vpRT;

        _gridContent = contentGO.transform;

        // ── Fullscreen preview ────────────────────────────────────
        BuildFullscreenPreview();

        _galleryPanel.SetActive(false);
    }

    void BuildFullscreenPreview()
    {
        _fullscreenView = new GameObject("FullscreenPreview");
        _fullscreenView.transform.SetParent(_galleryPanel.transform, false);

        // Isi seluruh galeri panel
        var rt = _fullscreenView.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        _fullscreenView.AddComponent<Image>().color = C_OVERLAY;

        // Foto fullscreen
        var imgGO = new GameObject("FullImg");
        imgGO.transform.SetParent(_fullscreenView.transform, false);
        var iRT = imgGO.AddComponent<RectTransform>();
        iRT.anchorMin = new Vector2(0f, 0.1f);
        iRT.anchorMax = new Vector2(1f, 0.9f);
        iRT.offsetMin = iRT.offsetMax = Vector2.zero;
        _fullscreenImg = imgGO.AddComponent<RawImage>();
        _fullscreenImg.color = Color.white;

        // Tombol tutup
        var closeGO = new GameObject("ClosePreview");
        closeGO.transform.SetParent(_fullscreenView.transform, false);
        var cRT = closeGO.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(1f, 1f); cRT.anchorMax = new Vector2(1f, 1f);
        cRT.pivot     = new Vector2(1f, 1f);
        cRT.anchoredPosition = new Vector2(-10f, -10f);
        cRT.sizeDelta = new Vector2(60f, 50f);
        var cImg = closeGO.AddComponent<Image>();
        cImg.color = new Color(0f, 0f, 0f, 0.6f);
        var cBtn = closeGO.AddComponent<Button>();
        cBtn.targetGraphic = cImg;
        cBtn.onClick.AddListener(() => _fullscreenView.SetActive(false));

        var cTxt = MakeText(closeGO.transform, "✕", 26, C_WHITE, TextAnchor.MiddleCenter);
        var ctRT = cTxt.GetComponent<RectTransform>();
        ctRT.anchorMin = Vector2.zero; ctRT.anchorMax = Vector2.one;
        ctRT.offsetMin = ctRT.offsetMax = Vector2.zero;

        _fullscreenView.SetActive(false);
    }

    // ═════════════════════════════════════════════════════════════
    //  GRID REFRESH
    // ═════════════════════════════════════════════════════════════
    void RefreshGrid()
    {
        if (_gridContent == null) return;

        // Hapus item lama
        for (int i = _gridContent.childCount - 1; i >= 0; i--)
            Destroy(_gridContent.GetChild(i).gameObject);

        // Update label kosong
        var emptyLabel = _galleryPanel.transform.Find("EmptyLabel");
        if (emptyLabel != null)
            emptyLabel.gameObject.SetActive(_photos.Count == 0);

        // Update jumlah foto
        var countTxt = _galleryPanel.transform
            .Find("Header/PhotoCount")?.GetComponent<Text>();
        if (countTxt != null)
            countTxt.text = _photos.Count + " foto";

        // Buat thumbnail untuk setiap foto (urutan terbaru di atas)
        for (int i = _photos.Count - 1; i >= 0; i--)
        {
            int idx = i; // capture untuk closure
            var entry = _photos[i];

            var cell = new GameObject("Photo_" + i);
            cell.transform.SetParent(_gridContent, false);

            var img = cell.AddComponent<RawImage>();
            img.texture = entry.texture;
            img.color   = Color.white;

            // Tombol tap untuk fullscreen
            var btn = cell.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.7f);
            cb.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
            btn.colors = cb;
            btn.onClick.AddListener(() => ShowFullscreen(idx));

            // Timestamp kecil di bawah thumbnail
            var tsGO = new GameObject("Timestamp");
            tsGO.transform.SetParent(cell.transform, false);
            var tsRT = tsGO.AddComponent<RectTransform>();
            tsRT.anchorMin = new Vector2(0f, 0f);
            tsRT.anchorMax = new Vector2(1f, 0f);
            tsRT.pivot     = new Vector2(0.5f, 0f);
            tsRT.anchoredPosition = Vector2.zero;
            tsRT.sizeDelta = new Vector2(0f, 20f);
            var tsBG = tsGO.AddComponent<Image>();
            tsBG.color = new Color(0f, 0f, 0f, 0.6f);

            var tsTxt = MakeText(tsGO.transform, entry.timestamp, 10, C_WHITE, TextAnchor.MiddleCenter);
            var tsTxtRT = tsTxt.GetComponent<RectTransform>();
            tsTxtRT.anchorMin = Vector2.zero; tsTxtRT.anchorMax = Vector2.one;
            tsTxtRT.offsetMin = tsTxtRT.offsetMax = Vector2.zero;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_gridContent as RectTransform);
    }

    void ShowFullscreen(int idx)
    {
        if (idx < 0 || idx >= _photos.Count) return;
        _fullscreenImg.texture = _photos[idx].texture;
        _fullscreenView.SetActive(true);
    }

    // ═════════════════════════════════════════════════════════════
    //  HELPERS
    // ═════════════════════════════════════════════════════════════
    GameObject MakeText(Transform parent, string text, int size, Color color,
        TextAnchor anchor, FontStyle style = FontStyle.Normal)
    {
        var go  = new GameObject("Txt_" + text.Substring(0, Mathf.Min(text.Length, 10)));
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var t         = go.AddComponent<Text>();
        t.text        = text;
        t.font        = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize    = size;
        t.color       = color;
        t.alignment   = anchor;
        t.fontStyle   = style;
        t.raycastTarget = false;
        return go;
    }
}