using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

/// <summary>
/// HUDButtonBuilder v4 — GTA V mobile style
/// Hand & Run icon diperbaiki: proporsi natural, pose dramatis.
/// </summary>
public class HUDButtonBuilder : MonoBehaviour
{
    static readonly Color C_BTN_BG    = new Color(0.08f, 0.08f, 0.09f, 0.82f);
    static readonly Color C_BTN_PRESS = new Color(0.22f, 0.22f, 0.24f, 0.92f);
    static readonly Color C_ICON      = new Color(1f, 1f, 1f, 0.95f);
    static readonly Color C_ICON_DIM  = new Color(1f, 1f, 1f, 0.38f);
    static readonly Color C_VEHICLE   = new Color(0.18f, 0.55f, 1.00f, 0.95f);
    static readonly Color C_BTN_VEH   = new Color(0.04f, 0.14f, 0.36f, 0.88f);

    [Header("Layout")]
    public float buttonSize    = 112f;
    public float buttonSpacing = 12f;
    public float marginRight   = 24f;
    [Range(0f,1f)]
    public float stackAnchorY  = 0.40f;

    private Canvas           _canvas;
    private PhoneManager     _phoneManager;
    private FloatingJoystick _joystick;

    [HideInInspector] public RectTransform rtPhone;
    [HideInInspector] public RectTransform rtCamera;
    [HideInInspector] public RectTransform rtInteract;
    [HideInInspector] public RectTransform rtRun;

    private Image     _imgPhone, _imgCamera, _imgInteract, _imgRun;
    private Text      _camBadgeText;
    private bool      _nearVehicle = false;
    private Transform _interactIconRoot;

    // ══════════════════════════════════════════════════════════════
    void Start()
    {
        _canvas   = FindOrGetCanvas();
        _joystick = FloatingJoystick.Instance;
        BuildAllButtons();
        _phoneManager = FindFirstObjectByType<PhoneManager>();
    }

    public void SetNearVehicle(bool near)
    {
        if (_nearVehicle == near) return;
        _nearVehicle = near;
        RefreshInteractIcon();
    }

    public void SetVisible(bool v)
    {
        if (rtPhone)    rtPhone.gameObject.SetActive(v);
        if (rtCamera)   rtCamera.gameObject.SetActive(v);
        if (rtInteract) rtInteract.gameObject.SetActive(v);
        if (rtRun)      rtRun.gameObject.SetActive(v);
    }

    Canvas FindOrGetCanvas()
    {
        Canvas c = FindFirstObjectByType<Canvas>();
        if (c != null) return c;
        var go = new GameObject("MainCanvas");
        c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    void BuildAllButtons()
    {
        var cont = new GameObject("HUDButtons");
        cont.transform.SetParent(_canvas.transform, false);
        var crt = cont.AddComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = new Vector2(1f, stackAnchorY);
        crt.pivot     = new Vector2(1f, 0.5f);
        crt.anchoredPosition = new Vector2(-marginRight, 0f);
        crt.sizeDelta = Vector2.zero;

        float step = buttonSize + buttonSpacing;
        rtRun      = BuildBtn(cont.transform, "HUD_Run",      step*0f, BuildRunIcon,    OnRunDown,      OnRunUp);
        rtInteract = BuildBtn(cont.transform, "HUD_Interact", step*1f, BuildHandIcon,   OnInteractDown, OnInteractUp);
        rtCamera   = BuildBtn(cont.transform, "HUD_Camera",   step*2f, BuildCameraIcon, OnCameraDown,   OnCameraUp);
        rtPhone    = BuildBtn(cont.transform, "HUD_Phone",    step*3f, BuildPhoneIcon,  OnPhoneDown,    OnPhoneUp);

        _imgRun      = rtRun.GetComponent<Image>();
        _imgCamera   = rtCamera.GetComponent<Image>();
        _imgInteract = rtInteract.GetComponent<Image>();
        _imgPhone    = rtPhone.GetComponent<Image>();

        if (_joystick != null)
        {
            _joystick.RegisterProtectedRect(rtPhone);
            _joystick.RegisterProtectedRect(rtCamera);
            _joystick.RegisterProtectedRect(rtInteract);
            _joystick.RegisterProtectedRect(rtRun);
        }
    }

    RectTransform BuildBtn(Transform parent, string name, float offsetY,
        System.Action<Transform,float> iconFn,
        System.Action onDown, System.Action onUp)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta        = new Vector2(buttonSize, buttonSize);
        rt.anchoredPosition = new Vector2(0f, offsetY);

        var img  = go.AddComponent<Image>();
        img.color  = C_BTN_BG;
        img.sprite = MakeCircleSprite(128);

        var iconRoot = new GameObject("Icon");
        iconRoot.transform.SetParent(go.transform, false);
        var irt = iconRoot.AddComponent<RectTransform>();
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
        irt.offsetMin = irt.offsetMax = Vector2.zero;

        if (name == "HUD_Interact") _interactIconRoot = iconRoot.transform;
        iconFn(iconRoot.transform, buttonSize);

        var et = go.AddComponent<EventTrigger>();
        AddTrigger(et, EventTriggerType.PointerDown, _ =>
        {
            img.color = C_BTN_PRESS;
            StartCoroutine(ScaleTo(rt, 0.88f, 0.07f));
            onDown?.Invoke();
        });
        AddTrigger(et, EventTriggerType.PointerUp, _ =>
        {
            img.color = (name == "HUD_Interact" && _nearVehicle) ? C_BTN_VEH : C_BTN_BG;
            StartCoroutine(ScaleTo(rt, 1.00f, 0.12f));
            onUp?.Invoke();
        });
        return rt;
    }

    void RefreshInteractIcon()
    {
        if (_interactIconRoot == null) return;
        for (int i = _interactIconRoot.childCount - 1; i >= 0; i--)
            Destroy(_interactIconRoot.GetChild(i).gameObject);

        if (_nearVehicle)
        {
            BuildSteeringIcon(_interactIconRoot, buttonSize);
            if (_imgInteract) _imgInteract.color = C_BTN_VEH;
        }
        else
        {
            BuildHandIcon(_interactIconRoot, buttonSize);
            if (_imgInteract) _imgInteract.color = C_BTN_BG;
        }
        StartCoroutine(PulseScale(rtInteract));
    }

    // ══════════════════════════════════════════════════════════════
    //  ICON BUILDERS
    // ══════════════════════════════════════════════════════════════

    // ── PHONE: handset klasik ────────────────────────────────────
    void BuildPhoneIcon(Transform root, float s)
    {
        var bot = R(root, "Bot", -s*0.06f, -s*0.14f, s*0.26f, s*0.20f, C_ICON, true);
        bot.localRotation = Quaternion.Euler(0,0, 35f);
        var top = R(root, "Top",  s*0.06f,  s*0.14f, s*0.26f, s*0.20f, C_ICON, true);
        top.localRotation = Quaternion.Euler(0,0, 35f);
        var mid = R(root, "Mid",  0f, 0f, s*0.12f, s*0.36f, C_ICON, false);
        mid.localRotation = Quaternion.Euler(0,0, 35f);
        C_(root, "Ear",    s*0.15f,  s*0.24f, s*0.14f, C_ICON);
        C_(root, "Mouth", -s*0.15f, -s*0.24f, s*0.14f, C_ICON);
    }

    // ── CAMERA: video camera chunky ──────────────────────────────
    void BuildCameraIcon(Transform root, float s)
    {
        R(root, "Body", -s*0.04f, s*0.02f, s*0.46f, s*0.28f, C_ICON, true);
        R(root, "View", -s*0.19f, s*0.13f, s*0.12f, s*0.10f, C_ICON, true);
        C_(root, "LensOut", s*0.08f, s*0.02f, s*0.22f, C_ICON);
        C_(root, "LensIn",  s*0.08f, s*0.02f, s*0.13f, new Color(0.08f,0.08f,0.10f,1f));
        C_(root, "LensHL",  s*0.10f, s*0.06f, s*0.04f, new Color(1f,1f,1f,0.50f));
        var badge = R(root, "Badge", -s*0.17f, -s*0.12f, s*0.22f, s*0.13f, new Color(0.04f,0.04f,0.06f,1f), true);
        _camBadgeText = Lbl(badge.transform, "TPP", s*0.095f, Color.white);
    }

    // ── HAND: stop-hand GTA style, jari rapat & natural ─────────
    //
    //  Prinsip:
    //  - 4 jari berdekatan (gap kecil), lebar 11px @ s=112
    //  - Tinggi: pinky < ring < middle > index (kurva natural)
    //  - Telapak solid menyambung ke bawah jari
    //  - Ibu jari di kiri-bawah, miring 28° keluar
    //
    void BuildHandIcon(Transform root, float s)
    {
        // Semua koordinat dalam skala s (buttonSize)
        // Center button = (0,0). Positif Y = atas.

        float fw   = s * 0.110f;   // lebar tiap jari
        float gap  = s * 0.004f;   // celah antar jari (nyaris nempel)
        float unit = fw + gap;      // pitch

        // Posisi X tengah tiap jari (pinky=kiri, index=kanan)
        float xPinky  = -unit * 1.5f;
        float xRing   = -unit * 0.5f;
        float xMiddle =  unit * 0.5f;
        float xIndex  =  unit * 1.5f;

        // Tinggi tiap jari
        float hPinky  = s * 0.27f;
        float hRing   = s * 0.33f;
        float hMiddle = s * 0.36f;
        float hIndex  = s * 0.31f;

        // Ujung atas jari semua rata di Y = baseTop
        // jari pendek: anchor lebih bawah, jari panjang: anchor lebih atas
        // Kita set posisi CENTER jari, jadi cy = baseTop - h/2
        float baseTop = s * 0.30f; // Y ujung atas jari tengah

        R(root,"Pinky",  xPinky,  baseTop - hPinky*0.5f  + (hMiddle-hPinky)*0.5f,   fw, hPinky,  C_ICON, true);
        R(root,"Ring",   xRing,   baseTop - hRing*0.5f   + (hMiddle-hRing)*0.5f,    fw, hRing,   C_ICON, true);
        R(root,"Middle", xMiddle, baseTop - hMiddle*0.5f,                            fw, hMiddle, C_ICON, true);
        R(root,"Index",  xIndex,  baseTop - hIndex*0.5f  + (hMiddle-hIndex)*0.5f,   fw, hIndex,  C_ICON, true);

        // Telapak: lebar pas nutupin 4 jari + sedikit keluar tiap sisi
        float palmW = unit * 4f + s * 0.04f;
        float palmH = s * 0.20f;
        // Atas telapak harus menyambung ke bawah jari
        // Bawah jari terpendek (pinky) ada di baseTop - hMiddle + hPinky dari top jari tengah
        // Tapi lebih simpel: set palm Y center sehingga top palm = baseTop - hMiddle + sedikit overlap
        float palmCy = baseTop - hMiddle - palmH * 0.5f + s * 0.04f;
        R(root,"Palm", -s*0.002f, palmCy, palmW, palmH, C_ICON, true);

        // Ibu jari: di sisi kiri, miring ke luar
        // Pivot center, rotate 28° CCW
        var thumb = R(root,"Thumb", xPinky - fw*0.8f, palmCy + palmH*0.1f, fw, s*0.20f, C_ICON, true);
        thumb.localRotation = Quaternion.Euler(0f, 0f, 28f);
    }

    // ── STEERING WHEEL ───────────────────────────────────────────
    void BuildSteeringIcon(Transform root, float s)
    {
        float outerD = s * 0.68f;
        float ringW  = s * 0.095f;
        C_(root,"RingOut", 0f,0f, outerD,         C_VEHICLE);
        C_(root,"RingIn",  0f,0f, outerD-ringW*2f, new Color(0.04f,0.14f,0.36f,1f));
        float spokeLen = outerD*0.5f - ringW;
        float spokeW   = s * 0.11f;
        for (int i=0;i<3;i++)
        {
            float angle = i*120f;
            float rad   = angle*Mathf.Deg2Rad;
            var sp = R(root,"Sp"+i, Mathf.Sin(rad)*spokeLen*0.5f, Mathf.Cos(rad)*spokeLen*0.5f,
                        spokeW, spokeLen, C_VEHICLE, true);
            sp.localRotation = Quaternion.Euler(0,0,angle);
        }
        C_(root,"Hub",   0f,0f, s*0.165f, C_VEHICLE);
        C_(root,"HubIn", 0f,0f, s*0.075f, new Color(0.04f,0.14f,0.36f,1f));
    }

    // ── RUN: pose lari dramatis, arm-swing jelas ─────────────────
    //
    //  Prinsip GTA:
    //  - Kepala BESAR & clear di atas (tidak tenggelam)
    //  - Badan miring ke depan ~15°
    //  - Lengan swing berlawanan dengan kaki (lengan kanan maju = kaki kiri maju)
    //  - Kaki: satu ke depan, satu kick back ke atas
    //  - Speed lines di kiri, 3 garis
    //
    void BuildRunIcon(Transform root, float s)
    {
        float lw = s * 0.095f;  // limb width tebal
        // Geser figure sedikit ke kanan agar ada ruang speed lines
        float ox = s * 0.08f;   // offset X center figure
        float oy = s * 0.02f;   // offset Y (turun sedikit)

        // Kepala — besar, clear
        C_(root,"Head", ox - s*0.01f, oy + s*0.28f, s*0.135f, C_ICON);

        // Badan — miring ke depan
        var torso = R(root,"Torso", ox, oy+s*0.08f, lw, s*0.21f, C_ICON, true);
        torso.localRotation = Quaternion.Euler(0,0,-15f);

        // Lengan KANAN: ke depan-atas (naik tinggi = arm swing natural)
        // Pivot di bahu atas
        var armR = R(root,"ArmR", ox+s*0.10f, oy+s*0.16f, lw, s*0.20f, C_ICON, true);
        armR.localRotation = Quaternion.Euler(0,0,-55f);

        // Lengan KIRI: ke belakang-bawah
        var armL = R(root,"ArmL", ox-s*0.13f, oy+s*0.08f, lw, s*0.19f, C_ICON, true);
        armL.localRotation = Quaternion.Euler(0,0,38f);

        // Kaki KIRI: stride ke depan
        var legL = R(root,"LegL", ox-s*0.07f, oy-s*0.10f, lw, s*0.26f, C_ICON, true);
        legL.localRotation = Quaternion.Euler(0,0,-35f);

        // Kaki KANAN: kick back ke atas (lebih dramatis)
        var legR = R(root,"LegR", ox+s*0.10f, oy-s*0.04f, lw, s*0.24f, C_ICON, true);
        legR.localRotation = Quaternion.Euler(0,0,48f);

        // Speed lines — 3 garis, makin ke bawah makin pendek & transparan
        float lx = ox - s*0.34f;
        float[] ww = { s*0.19f, s*0.14f, s*0.09f };
        float[] yy = { oy+s*0.08f, oy-s*0.01f, oy-s*0.10f };
        float[] aa = { 0.60f, 0.40f, 0.24f };
        for (int i=0;i<3;i++)
            R(root,"Line"+i, lx - ww[i]*0.4f, yy[i], ww[i], lw*0.55f,
              new Color(1f,1f,1f,aa[i]), true);
    }

    // ══════════════════════════════════════════════════════════════
    //  CALLBACKS
    // ══════════════════════════════════════════════════════════════
    void OnPhoneDown()
    {
        if (_phoneManager == null) _phoneManager = FindFirstObjectByType<PhoneManager>();
        _phoneManager?.TogglePhone();
    }
    void OnPhoneUp() {}

    void OnCameraDown()
    {
        var cam = FindFirstObjectByType<CameraController>();
        if (cam == null) return;
        cam.CycleMode();
        if (_camBadgeText != null)
            _camBadgeText.text = cam.cameraMode switch
            {
                CameraController.CameraMode.FPP      => "FPP",
                CameraController.CameraMode.Shoulder => "SHLD",
                _                                     => "TPP"
            };
    }
    void OnCameraUp() {}

    void OnInteractDown()
    {
        if (_joystick == null) _joystick = FloatingJoystick.Instance;
        _joystick?.SetInteractPressed();
    }
    void OnInteractUp() {}

    void OnRunDown()
    {
        if (_joystick == null) _joystick = FloatingJoystick.Instance;
        _joystick?.SetSprintHeld(true);
    }
    void OnRunUp()
    {
        if (_joystick == null) _joystick = FloatingJoystick.Instance;
        _joystick?.SetSprintHeld(false);
    }

    // ══════════════════════════════════════════════════════════════
    //  ANIMATIONS
    // ══════════════════════════════════════════════════════════════
    IEnumerator ScaleTo(RectTransform rt, float target, float dur)
    {
        if (rt==null) yield break;
        Vector3 from=rt.localScale, to=Vector3.one*target;
        float t=0f;
        while(t<1f){ t+=Time.unscaledDeltaTime/dur; rt.localScale=Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,t)); yield return null; }
        rt.localScale=to;
    }

    IEnumerator PulseScale(RectTransform rt)
    {
        if (rt==null) yield break;
        yield return ScaleTo(rt,1.10f,0.08f);
        yield return ScaleTo(rt,1.00f,0.14f);
    }

    // ══════════════════════════════════════════════════════════════
    //  SHAPE HELPERS (short alias)
    // ══════════════════════════════════════════════════════════════
    RectTransform R(Transform parent, string name,
                    float x, float y, float w, float h,
                    Color col, bool round=false)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        var rt=go.AddComponent<RectTransform>();
        rt.anchorMin=rt.anchorMax=new Vector2(0.5f,0.5f);
        rt.pivot=new Vector2(0.5f,0.5f);
        rt.sizeDelta=new Vector2(w,h);
        rt.anchoredPosition=new Vector2(x,y);
        var img=go.AddComponent<Image>();
        img.color=col; img.raycastTarget=false;
        if(round) img.sprite=MakeRoundedSprite(64,0.40f);
        return rt;
    }

    RectTransform C_(Transform parent, string name,
                     float x, float y, float d, Color col)
    {
        var go=new GameObject(name); go.transform.SetParent(parent,false);
        var rt=go.AddComponent<RectTransform>();
        rt.anchorMin=rt.anchorMax=new Vector2(0.5f,0.5f);
        rt.pivot=new Vector2(0.5f,0.5f);
        rt.sizeDelta=new Vector2(d,d);
        rt.anchoredPosition=new Vector2(x,y);
        var img=go.AddComponent<Image>();
        img.color=col; img.sprite=MakeCircleSprite(128); img.raycastTarget=false;
        return rt;
    }

    Text Lbl(Transform parent, string text, float size, Color col)
    {
        var go=new GameObject("Lbl"); go.transform.SetParent(parent,false);
        var rt=go.AddComponent<RectTransform>();
        rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
        rt.offsetMin=rt.offsetMax=Vector2.zero;
        var t=go.AddComponent<Text>();
        t.text=text; t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize=Mathf.RoundToInt(size); t.fontStyle=FontStyle.Bold;
        t.color=col; t.alignment=TextAnchor.MiddleCenter; t.raycastTarget=false;
        return t;
    }

    void AddTrigger(EventTrigger et, EventTriggerType type, System.Action<BaseEventData> action)
    {
        var e=new EventTrigger.Entry{eventID=type};
        e.callback.AddListener(d=>action(d));
        et.triggers.Add(e);
    }

    // ══════════════════════════════════════════════════════════════
    //  SPRITE GENERATORS
    // ══════════════════════════════════════════════════════════════
    Sprite MakeCircleSprite(int res)
    {
        var tex=new Texture2D(res,res,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear;
        var ctr=new Vector2(res/2f,res/2f); float r=res/2f;
        for(int y=0;y<res;y++) for(int x=0;x<res;x++)
            tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1f-(Vector2.Distance(new Vector2(x,y),ctr)-(r-2f))/2f)));
        tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,res,res),new Vector2(0.5f,0.5f),res);
    }

    Sprite MakeRoundedSprite(int res, float cr)
    {
        var tex=new Texture2D(res,res,TextureFormat.RGBA32,false);
        tex.filterMode=FilterMode.Bilinear;
        float corner=res*cr;
        for(int y=0;y<res;y++) for(int x=0;x<res;x++){
            float cx2=Mathf.Clamp(x,corner,res-corner), cy2=Mathf.Clamp(y,corner,res-corner);
            float d=Mathf.Sqrt((x-cx2)*(x-cx2)+(y-cy2)*(y-cy2));
            tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(1f-(d-(corner-1f))/1.5f)));
        }
        tex.Apply();
        return Sprite.Create(tex,new Rect(0,0,res,res),new Vector2(0.5f,0.5f),res);
    }
}