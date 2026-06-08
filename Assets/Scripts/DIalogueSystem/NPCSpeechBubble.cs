using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// NPCSpeechBubble — Upgrade Persona 5 Style
/// 
/// JARAK JAUH  (dist > detectionRange)  : Whisper floating text naik ke atas lalu fade
/// JARAK DEKAT (dist <= detectionRange) : Bubble "..." animasi + scale up/down (interactable indicator)
/// SAAT DIALOG (CycleDialogue aktif)    : Bubble teks dialog biasa
/// </summary>
public class NPCSpeechBubble : MonoBehaviour
{
    [Header("=== Teks Dialog ===")]
    public List<string> dialogueLines = new List<string>()
    {
        "Hei, apa kabar?",
        "Cuaca hari ini bagus ya!",
        "Mau ngobrol?"
    };
    public float displayDuration   = 3f;
    public float pauseBetweenLines = 0.8f;

    [Header("=== Fitur On/Off ===")]
    [Tooltip("NPC punya dialog bubble saat player dekat")]
    public bool enableDialogBubble = true;
    [Tooltip("Bubble ... muncul saat player dekat (penanda bisa diinteract)")]
    public bool enableDotAnimation = true;
    [Tooltip("Teks whisper mengambang saat player agak jauh")]
    public bool enableWhisper      = true;

    [Header("=== Deteksi Player ===")]
    public float detectionRange  = 5f;
    public string playerTag      = "Player";

    [Header("=== Posisi Bubble ===")]
    public Vector3 bubbleOffset  = new Vector3(0f, 5.47f, 0f);

    [Header("=== Skala Canvas ===")]
    public float canvasScale     = 0.025f;

    [Header("=== Tampilan ===")]
    public Color bubbleBgColor     = new Color(0.05f, 0.05f, 0.05f, 0.92f); // hitam gelap
    public Color bubbleTextColor   = new Color(1f, 1f, 1f, 1f);             // putih
    public Color bubbleShadowColor = new Color(0f, 1f, 0.5f, 0.25f);        // hijau aksen
    public float fontSize          = 20f;
    public Vector2 bubblePadding   = new Vector2(24f, 14f);
    public int   cornerRadius      = 22;

    [Header("=== Dot Animasi ===")]
    public float dotBounceHeight   = 8f;
    public float dotBounceSpeed    = 2.5f;
    public float bubblePulseMin    = 0.92f;
    public float bubblePulseMax    = 1.06f;
    public float bubblePulseSpeed  = 1.8f;

    [Header("=== Whisper ===")]
    [Tooltip("Jarak player mulai muncul whisper (harus lebih besar dari detectionRange)")]
    public float whisperRange    = 15f;
    public List<string> whisperWords = new List<string>()
    {
        "Whisper...", "Murmur...", "Psst...", "Hey...",
        "...", "Hmm...", "Eh...", "Ngobrolin lo tuh"
    };
    public float whisperSpawnInterval = 1.4f;
    public float whisperRiseDistance  = 80f;
    public float whisperDuration      = 2.2f;
    public float whisperFontSize      = 28f;
    // Warna putih dengan outline hitam supaya keliatan di background apapun
    public Color whisperTextColor    = new Color(1f, 1f, 1f, 0.95f);
    public Color whisperOutlineColor = new Color(0f, 0f, 0f, 1f);
    public float whisperOutlineWidth = 0.3f; // 0.0 - 0.5, makin besar makin tebal outline

    // ── private ──────────────────────────────────────────────────────────
    private Canvas          _canvas;
    private GameObject      _bubbleRoot;
    private RectTransform   _bubbleRT;
    private GameObject      _boxGO;
    private Image           _bgImage;
    private Image           _shadowImage;
    private RectTransform   _shadowRT;
    private GameObject      _tailGO;
    private Image           _tailImage;
    private GameObject      _tailShadowGO;
    private Image           _tailShadowImage;
    private TextMeshProUGUI _tmp;
    private RectTransform   _textRT;
    private CanvasGroup     _cg;

    // --- Dot "..." ---
    private GameObject      _dotRoot;
    private RectTransform   _dotRootRT;
    private CanvasGroup     _dotCG;
    private RectTransform[] _dotRTs = new RectTransform[3];
    private bool            _dotVisible = false;
    private Coroutine       _dotShowCR;

    // --- Whisper ---
    private GameObject      _whisperContainer;
    private bool            _whisperActive = false;
    private Coroutine       _whisperCR;

    private Transform       _playerTF;
    private Camera          _cam;

    private bool      _dialogVisible = false;
    private int       _idx     = 0;
    private Coroutine _cycleCR;

    private enum NPCState { Idle, Whisper, Dot, Dialog }
    private NPCState _state = NPCState.Idle;

    const float TAIL_H = 16f;
    const float TAIL_W = 24f;

    // ═════════════════════════════════════════════════════════════════════
    void Start()
    {
        _cam = Camera.main;
        TryFindPlayer();
        if (!BuildUI()) { Debug.LogError("[NPCSpeechBubble] BuildUI GAGAL — " + gameObject.name); enabled = false; return; }
        _bubbleRoot.SetActive(false);
        _dotRoot.SetActive(false);
    }

    void Update()
    {
        if (_playerTF == null) { TryFindPlayer(); return; }
        if (_cam == null) _cam = Camera.main;

        float dist = Vector3.Distance(transform.position, _playerTF.position);

        if (_cam != null)
        {
            Quaternion rot = _cam.transform.rotation;
            if (_bubbleRoot       != null) { _bubbleRoot.transform.position       = transform.position + bubbleOffset; _bubbleRoot.transform.rotation       = rot; }
            if (_dotRoot          != null) { _dotRoot.transform.position          = transform.position + bubbleOffset; _dotRoot.transform.rotation          = rot; }
            if (_whisperContainer != null) { _whisperContainer.transform.position = transform.position + bubbleOffset; _whisperContainer.transform.rotation = rot; }
        }

        NPCState newState;
        // Sembunyikan semua bubble saat DialogueManager sedang menampilkan dialog
        bool dialogueManagerActive = DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive();
        if      (dialogueManagerActive)                                                    newState = NPCState.Idle;
        else if (dist <= detectionRange && enableDialogBubble && dialogueLines.Count > 0) newState = NPCState.Dialog;
        else if (dist <= detectionRange && enableDotAnimation)                             newState = NPCState.Dot;
        else if (dist <= whisperRange   && enableWhisper)                                 newState = NPCState.Whisper;
        else                                                                               newState = NPCState.Idle;

        if (newState != _state) ApplyState(newState);

        if ((_state == NPCState.Dot || _state == NPCState.Dialog) && _dotVisible)
            AnimateDots();

        if (_state == NPCState.Dot && _dotRoot.activeSelf)
            PulseDotBubble();
    }

    void TryFindPlayer()
    {
        GameObject p = GameObject.FindWithTag(playerTag);
        if (p != null) _playerTF = p.transform;
    }

    void ApplyState(NPCState newState)
    {
        _state = newState;

        if (_cycleCR  != null) { StopCoroutine(_cycleCR);  _cycleCR  = null; }
        if (_dotShowCR!= null) { StopCoroutine(_dotShowCR);_dotShowCR= null; }
        if (_whisperCR!= null) { StopCoroutine(_whisperCR);_whisperCR= null; }

        switch (newState)
        {
            case NPCState.Idle:
                StartCoroutine(FadeHideAll());
                break;

            case NPCState.Whisper:
                StartCoroutine(FadeHideBubble());
                StartCoroutine(FadeHideDot());
                _whisperCR = StartCoroutine(WhisperLoop());
                break;

            case NPCState.Dot:
                StartCoroutine(FadeHideBubble());
                StopWhispers();
                _dotShowCR = StartCoroutine(ShowDotBubble());
                break;

            case NPCState.Dialog:
                StopWhispers();
                StartCoroutine(FadeHideDot());
                _dialogVisible = true;
                _bubbleRoot.SetActive(true);
                _cycleCR = StartCoroutine(CycleDialogue());
                break;
        }
    }

    // ─── DOT BUBBLE ANIMASI ───────────────────────────────────────────────
    IEnumerator ShowDotBubble()
    {
        _dotRoot.SetActive(true);
        _dotCG.alpha = 0f;
        _dotVisible  = true;
        _dotRootRT.localScale = Vector3.one * bubblePulseMin;
        yield return StartCoroutine(FadeGroup(_dotCG, 0f, 1f, 0.2f));
    }

    void AnimateDots()
    {
        float t = Time.time * dotBounceSpeed;
        for (int i = 0; i < 3; i++)
        {
            if (_dotRTs[i] == null) continue;
            float phase = t + i * (Mathf.PI * 2f / 3f);
            float yOff  = (Mathf.Sin(phase) + 1f) * 0.5f * dotBounceHeight;
            _dotRTs[i].anchoredPosition = new Vector2(_dotRTs[i].anchoredPosition.x, yOff);
        }
    }

    void PulseDotBubble()
    {
        float s = Mathf.Lerp(bubblePulseMin, bubblePulseMax,
                  (Mathf.Sin(Time.time * bubblePulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f);
        _dotRootRT.localScale = Vector3.one * s;
    }

    IEnumerator FadeHideDot()
    {
        _dotVisible = false;
        if (_dotCG != null) yield return StartCoroutine(FadeGroup(_dotCG, _dotCG.alpha, 0f, 0.18f));
        if (_dotRoot != null) _dotRoot.SetActive(false);
    }

    // ─── WHISPER FLOATING TEXT ────────────────────────────────────────────
    IEnumerator WhisperLoop()
    {
        _whisperActive = true;
        _whisperContainer.SetActive(true);

        yield return new WaitForSeconds(0.2f);

        while (_whisperActive)
        {
            string word = whisperWords[Random.Range(0, whisperWords.Count)];
            StartCoroutine(SpawnWhisper(word));
            yield return new WaitForSeconds(whisperSpawnInterval);
        }
    }

    IEnumerator SpawnWhisper(string text)
    {
        GameObject go = new GameObject("Whisper_" + text, typeof(RectTransform));
        go.transform.SetParent(_whisperContainer.transform, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text             = text;
        tmp.fontSize         = whisperFontSize;
        tmp.color            = whisperTextColor;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.fontStyle        = FontStyles.Bold;

        // Outline hitam tebal supaya teks keliatan di atas background apapun
        tmp.outlineColor = whisperOutlineColor;
        tmp.outlineWidth = whisperOutlineWidth;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(300f, 50f);  // lebih lebar buat teks lebih panjang

        float startX = Random.Range(-30f, 30f);
        float startY = Random.Range(10f, 40f);
        rt.anchoredPosition = new Vector2(startX, startY);

        float elapsed = 0f;
        Color c = whisperTextColor;

        while (elapsed < whisperDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / whisperDuration;

            float y = startY + t * whisperRiseDistance;
            rt.anchoredPosition = new Vector2(startX, y);

            // Fade in 20% pertama, tetap penuh sampai 60%, fade out sisanya
            float alpha;
            if      (t < 0.2f) alpha = t / 0.2f;
            else if (t > 0.6f) alpha = 1f - ((t - 0.6f) / 0.4f);
            else               alpha = 1f;

            c.a = alpha * whisperTextColor.a;
            tmp.color = c;

            // Outline tetap solid hitam (alpha tidak ikut fade biar tetap kontras)
            Color oc = whisperOutlineColor;
            oc.a = alpha;
            tmp.outlineColor = oc;

            yield return null;
        }

        Destroy(go);
    }

    void StopWhispers()
    {
        _whisperActive = false;
        if (_whisperContainer != null)
        {
            foreach (Transform child in _whisperContainer.transform)
                Destroy(child.gameObject);
            _whisperContainer.SetActive(false);
        }
    }

    // ─── DIALOG CYCLE ────────────────────────────────────────────────────
    IEnumerator CycleDialogue()
    {
        while (true)
        {
            if (_tmp == null || dialogueLines.Count == 0) yield break;
            _tmp.text = dialogueLines[_idx % dialogueLines.Count];
            yield return null;
            ResizeBubble();
            yield return StartCoroutine(FadeGroup(_cg, 0f, 1f, 0.18f));
            yield return new WaitForSeconds(displayDuration);
            yield return StartCoroutine(FadeGroup(_cg, 1f, 0f, 0.18f));
            yield return new WaitForSeconds(pauseBetweenLines);
            _idx = (_idx + 1) % dialogueLines.Count;
        }
    }

    IEnumerator FadeHideBubble()
    {
        if (_cg != null) yield return StartCoroutine(FadeGroup(_cg, _cg.alpha, 0f, 0.18f));
        if (_bubbleRoot != null) _bubbleRoot.SetActive(false);
        _dialogVisible = false;
    }

    IEnumerator FadeHideAll()
    {
        yield return StartCoroutine(FadeHideBubble());
        yield return StartCoroutine(FadeHideDot());
        StopWhispers();
    }

    void ResizeBubble()
    {
        if (_tmp == null || _bubbleRT == null) return;

        float w = Mathf.Max(_tmp.preferredWidth  + bubblePadding.x * 2f, 80f);
        float h = Mathf.Max(_tmp.preferredHeight + bubblePadding.y * 2f, 40f);

        _bubbleRT.sizeDelta = new Vector2(w, h);

        int iw = Mathf.Max(4, Mathf.RoundToInt(w));
        int ih = Mathf.Max(4, Mathf.RoundToInt(h));
        int r  = Mathf.Clamp(cornerRadius, 2, Mathf.Min(iw, ih) / 2);

        if (_bgImage     != null) _bgImage.sprite     = MakeRoundedSprite(iw, ih, r);
        if (_shadowImage != null) { _shadowImage.sprite = MakeRoundedSprite(iw + 8, ih + 8, r); _shadowRT.sizeDelta = new Vector2(w + 8, h + 8); }
        if (_tailImage       != null) _tailImage.sprite       = MakeTriangleSprite(Mathf.RoundToInt(TAIL_W),     Mathf.RoundToInt(TAIL_H));
        if (_tailShadowImage != null) _tailShadowImage.sprite = MakeTriangleSprite(Mathf.RoundToInt(TAIL_W + 6), Mathf.RoundToInt(TAIL_H + 4));
        if (_textRT != null) { _textRT.offsetMin = new Vector2(bubblePadding.x, bubblePadding.y); _textRT.offsetMax = new Vector2(-bubblePadding.x, -bubblePadding.y); }
    }

    IEnumerator FadeGroup(CanvasGroup cg, float from, float to, float dur)
    {
        if (cg == null) yield break;
        float t = 0f; cg.alpha = from;
        while (t < dur) { t += Time.deltaTime; cg.alpha = Mathf.Lerp(from, to, t / dur); yield return null; }
        cg.alpha = to;
    }

    // ─── BUILD UI ────────────────────────────────────────────────────────
    bool BuildUI()
    {
        try
        {
            // ── Canvas ──
            GameObject canvasGO = new GameObject("SpeechBubbleCanvas_" + gameObject.name);
            canvasGO.transform.SetParent(null);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.WorldSpace;
            canvas.sortingOrder = 15;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            RectTransform cRT = canvasGO.GetComponent<RectTransform>();
            cRT.sizeDelta  = new Vector2(800f, 400f);
            cRT.localScale = Vector3.one * canvasScale;
            _canvas = canvas;

            // ── Dialog Bubble Root ──
            _bubbleRoot = new GameObject("BubbleRoot", typeof(RectTransform));
            _bubbleRoot.transform.SetParent(canvasGO.transform, false);
            _cg = _bubbleRoot.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            RectTransform rootRT = _bubbleRoot.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0.5f, 0.5f); rootRT.anchorMax = new Vector2(0.5f, 0.5f);
            rootRT.pivot     = new Vector2(0.5f, 0f);   rootRT.anchoredPosition = Vector2.zero;
            rootRT.sizeDelta = new Vector2(200f, 80f);

            // Tail shadow
            _tailShadowGO = new GameObject("TailShadow", typeof(RectTransform));
            _tailShadowGO.transform.SetParent(_bubbleRoot.transform, false);
            _tailShadowImage = _tailShadowGO.AddComponent<Image>();
            _tailShadowImage.color = bubbleShadowColor;
            _tailShadowImage.sprite = MakeTriangleSprite(Mathf.RoundToInt(TAIL_W + 6), Mathf.RoundToInt(TAIL_H + 4));
            RectTransform tsRT = _tailShadowGO.GetComponent<RectTransform>();
            tsRT.anchorMin = new Vector2(0.5f, 0f); tsRT.anchorMax = new Vector2(0.5f, 0f);
            tsRT.pivot = new Vector2(0.5f, 1f); tsRT.anchoredPosition = new Vector2(2f, TAIL_H - 2f);
            tsRT.sizeDelta = new Vector2(TAIL_W + 6, TAIL_H + 4);

            // Tail
            _tailGO = new GameObject("Tail", typeof(RectTransform));
            _tailGO.transform.SetParent(_bubbleRoot.transform, false);
            _tailImage = _tailGO.AddComponent<Image>();
            _tailImage.color = bubbleBgColor;
            _tailImage.sprite = MakeTriangleSprite(Mathf.RoundToInt(TAIL_W), Mathf.RoundToInt(TAIL_H));
            RectTransform tailRT = _tailGO.GetComponent<RectTransform>();
            tailRT.anchorMin = new Vector2(0.5f, 0f); tailRT.anchorMax = new Vector2(0.5f, 0f);
            tailRT.pivot = new Vector2(0.5f, 1f); tailRT.anchoredPosition = new Vector2(0f, TAIL_H - 1f);
            tailRT.sizeDelta = new Vector2(TAIL_W, TAIL_H);

            // Box shadow
            GameObject shadowGO = new GameObject("BoxShadow", typeof(RectTransform));
            shadowGO.transform.SetParent(_bubbleRoot.transform, false);
            _shadowImage = shadowGO.AddComponent<Image>();
            _shadowImage.color = bubbleShadowColor;
            _shadowImage.sprite = MakeRoundedSprite(168, 68, cornerRadius);
            _shadowRT = shadowGO.GetComponent<RectTransform>();
            _shadowRT.anchorMin = new Vector2(0.5f, 0f); _shadowRT.anchorMax = new Vector2(0.5f, 0f);
            _shadowRT.pivot = new Vector2(0.5f, 0f); _shadowRT.anchoredPosition = new Vector2(0f, TAIL_H - 1f);
            _shadowRT.sizeDelta = new Vector2(168f, 68f);

            // Box bubble
            _boxGO = new GameObject("Box", typeof(RectTransform));
            _boxGO.transform.SetParent(_bubbleRoot.transform, false);
            _bgImage = _boxGO.AddComponent<Image>();
            _bgImage.color = bubbleBgColor; _bgImage.sprite = MakeRoundedSprite(160, 60, cornerRadius);
            _bubbleRT = _boxGO.GetComponent<RectTransform>();
            _bubbleRT.anchorMin = new Vector2(0.5f, 0f); _bubbleRT.anchorMax = new Vector2(0.5f, 0f);
            _bubbleRT.pivot = new Vector2(0.5f, 0f); _bubbleRT.anchoredPosition = new Vector2(0f, TAIL_H - 1f);
            _bubbleRT.sizeDelta = new Vector2(160f, 60f);

            // Teks dialog
            GameObject textGO = new GameObject("BubbleText", typeof(RectTransform));
            textGO.transform.SetParent(_boxGO.transform, false);
            _tmp = textGO.AddComponent<TextMeshProUGUI>();
            if (_tmp == null) return false;
            _tmp.text = ""; _tmp.fontSize = fontSize; _tmp.color = bubbleTextColor;
            _tmp.alignment = TextAlignmentOptions.Center;
            _tmp.textWrappingMode = TextWrappingModes.NoWrap;
            _tmp.overflowMode = TextOverflowModes.Overflow;
            _textRT = textGO.GetComponent<RectTransform>();
            _textRT.anchorMin = Vector2.zero; _textRT.anchorMax = Vector2.one;
            _textRT.offsetMin = new Vector2(bubblePadding.x, bubblePadding.y);
            _textRT.offsetMax = new Vector2(-bubblePadding.x, -bubblePadding.y);

            // ── Dot Bubble ──
            _dotRoot = new GameObject("DotRoot", typeof(RectTransform));
            _dotRoot.transform.SetParent(canvasGO.transform, false);
            _dotCG = _dotRoot.AddComponent<CanvasGroup>();
            _dotCG.alpha = 0f;
            _dotRootRT = _dotRoot.GetComponent<RectTransform>();
            _dotRootRT.anchorMin = new Vector2(0.5f, 0.5f); _dotRootRT.anchorMax = new Vector2(0.5f, 0.5f);
            _dotRootRT.pivot = new Vector2(0.5f, 0f); _dotRootRT.anchoredPosition = Vector2.zero;
            _dotRootRT.sizeDelta = new Vector2(80f, 55f);

            // Dot tail shadow
            GameObject dotTSGO = new GameObject("DotTailShadow", typeof(RectTransform));
            dotTSGO.transform.SetParent(_dotRoot.transform, false);
            Image dotTSImg = dotTSGO.AddComponent<Image>();
            dotTSImg.color = bubbleShadowColor;
            dotTSImg.sprite = MakeTriangleSprite(20, 14);
            RectTransform dotTSRT = dotTSGO.GetComponent<RectTransform>();
            dotTSRT.anchorMin = new Vector2(0.3f, 0f); dotTSRT.anchorMax = new Vector2(0.3f, 0f);
            dotTSRT.pivot = new Vector2(0.5f, 1f); dotTSRT.anchoredPosition = new Vector2(2f, 12f);
            dotTSRT.sizeDelta = new Vector2(20f, 14f);

            // Dot tail
            GameObject dotTailGO = new GameObject("DotTail", typeof(RectTransform));
            dotTailGO.transform.SetParent(_dotRoot.transform, false);
            Image dotTailImg = dotTailGO.AddComponent<Image>();
            dotTailImg.color = bubbleBgColor;
            dotTailImg.sprite = MakeTriangleSprite(16, 12);
            RectTransform dotTailRT = dotTailGO.GetComponent<RectTransform>();
            dotTailRT.anchorMin = new Vector2(0.3f, 0f); dotTailRT.anchorMax = new Vector2(0.3f, 0f);
            dotTailRT.pivot = new Vector2(0.5f, 1f); dotTailRT.anchoredPosition = new Vector2(0f, 12f);
            dotTailRT.sizeDelta = new Vector2(16f, 12f);

            // Dot box shadow
            GameObject dotShadowGO = new GameObject("DotBoxShadow", typeof(RectTransform));
            dotShadowGO.transform.SetParent(_dotRoot.transform, false);
            Image dotShadowImg = dotShadowGO.AddComponent<Image>();
            dotShadowImg.color = bubbleShadowColor;
            dotShadowImg.sprite = MakeRoundedSprite(72, 48, 18);
            RectTransform dotShadowRT = dotShadowGO.GetComponent<RectTransform>();
            dotShadowRT.anchorMin = new Vector2(0.5f, 0f); dotShadowRT.anchorMax = new Vector2(0.5f, 0f);
            dotShadowRT.pivot = new Vector2(0.5f, 0f); dotShadowRT.anchoredPosition = new Vector2(2f, 11f);
            dotShadowRT.sizeDelta = new Vector2(72f, 48f);

            // Dot box
            GameObject dotBoxGO = new GameObject("DotBox", typeof(RectTransform));
            dotBoxGO.transform.SetParent(_dotRoot.transform, false);
            Image dotBoxImg = dotBoxGO.AddComponent<Image>();
            dotBoxImg.color = bubbleBgColor;
            dotBoxImg.sprite = MakeRoundedSprite(68, 44, 18);
            RectTransform dotBoxRT = dotBoxGO.GetComponent<RectTransform>();
            dotBoxRT.anchorMin = new Vector2(0.5f, 0f); dotBoxRT.anchorMax = new Vector2(0.5f, 0f);
            dotBoxRT.pivot = new Vector2(0.5f, 0f); dotBoxRT.anchoredPosition = new Vector2(0f, 12f);
            dotBoxRT.sizeDelta = new Vector2(68f, 44f);

            // 3 titik
            float[] dotX = { -14f, 0f, 14f };
            for (int i = 0; i < 3; i++)
            {
                GameObject dotGO = new GameObject("Dot" + i, typeof(RectTransform));
                dotGO.transform.SetParent(dotBoxGO.transform, false);
                Image dotImg = dotGO.AddComponent<Image>();
                dotImg.color = bubbleTextColor;
                dotImg.sprite = MakeCircleSprite(12, 12);
                RectTransform dRT = dotGO.GetComponent<RectTransform>();
                dRT.anchorMin = new Vector2(0.5f, 0.5f); dRT.anchorMax = new Vector2(0.5f, 0.5f);
                dRT.pivot = new Vector2(0.5f, 0.5f);
                dRT.anchoredPosition = new Vector2(dotX[i], 0f);
                dRT.sizeDelta = new Vector2(10f, 10f);
                _dotRTs[i] = dRT;
            }

            // ── Whisper Container ──
            _whisperContainer = new GameObject("WhisperContainer", typeof(RectTransform));
            _whisperContainer.transform.SetParent(canvasGO.transform, false);
            RectTransform wcRT = _whisperContainer.GetComponent<RectTransform>();
            wcRT.anchorMin = new Vector2(0.5f, 0.5f); wcRT.anchorMax = new Vector2(0.5f, 0.5f);
            wcRT.pivot = new Vector2(0.5f, 0f); wcRT.anchoredPosition = Vector2.zero;
            wcRT.sizeDelta = new Vector2(400f, 300f);
            _whisperContainer.SetActive(false);

            _bubbleRoot.transform.position       = transform.position + bubbleOffset;
            _dotRoot.transform.position          = transform.position + bubbleOffset;
            _whisperContainer.transform.position = transform.position + bubbleOffset;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[NPCSpeechBubble] Exception: " + e.Message + "\n" + e.StackTrace);
            return false;
        }
    }

    // ─── SPRITE GENERATORS ────────────────────────────────────────────────
    Sprite MakeRoundedSprite(int w, int h, int r)
    {
        w = Mathf.Max(w, 4); h = Mathf.Max(h, 4);
        r = Mathf.Clamp(r, 2, Mathf.Min(w, h) / 2);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = SdfRoundRect(x + 0.5f, y + 0.5f, w, h, r);
                float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(d + 0.5f));
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    Sprite MakeTriangleSprite(int w, int h)
    {
        w = Mathf.Max(w, 2); h = Mathf.Max(h, 2);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        float cx = w * 0.5f;
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            float half = cx * t;
            for (int x = 0; x < w; x++)
            {
                float fx   = x + 0.5f;
                float edge = Mathf.Min(fx - (cx - half), (cx + half) - fx);
                float a    = Mathf.Clamp01(edge);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1f), 100f, 0, SpriteMeshType.FullRect);
    }

    Sprite MakeCircleSprite(int w, int h)
    {
        w = Mathf.Max(w, 2); h = Mathf.Max(h, 2);
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color[] px = new Color[w * h];
        float cx = w * 0.5f, cy = h * 0.5f, r = Mathf.Min(cx, cy) - 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                float a  = Mathf.Clamp01(r - d + 0.5f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    float SdfRoundRect(float px, float py, float w, float h, float r)
    {
        float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r);
        float qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
        return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f))
               + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, whisperRange);
    }

    void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }
}