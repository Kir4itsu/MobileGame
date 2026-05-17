using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;
using System.Collections.Generic;

// ══════════════════════════════════════════════════════════════════
//  1. SONG LIST ITEM
// ══════════════════════════════════════════════════════════════════
public class SongListItem : MonoBehaviour
{
    [Header("UI References")]
    public Text  songNameText;
    public Text  artistNameText;
    public Image backgroundImage;

    [Header("Colors")]
    public Color normalColor   = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    public Color selectedColor = new Color(0.2f, 0.6f, 0.2f, 0.9f);

    private Button _button;
    private Action _onClickCallback;

    void Awake() { _button = GetComponent<Button>(); }

    public void Setup(string songName, string artistName, Action onClick)
    {
        if (songNameText   != null) songNameText.text   = songName;
        if (artistNameText != null) artistNameText.text = artistName;
        _onClickCallback = onClick;
        if (_button != null)
            _button.onClick.AddListener(() => _onClickCallback?.Invoke());
    }

    public void SetSelected(bool selected)
    {
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
    }
}

// ══════════════════════════════════════════════════════════════════
//  2. SLIDER DRAG EVENTS
// ══════════════════════════════════════════════════════════════════
public class SliderDragEvents : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public event Action onDragStart;
    public event Action onDragEnd;

    public void OnPointerDown(PointerEventData eventData) => onDragStart?.Invoke();
    public void OnPointerUp(PointerEventData eventData)   => onDragEnd?.Invoke();
}

// ══════════════════════════════════════════════════════════════════
//  3. PHONE NAVIGATOR
//  Navigasi antar panel di HP — pakai List bukan Stack
// ══════════════════════════════════════════════════════════════════
public class PhoneNavigator : MonoBehaviour
{
    [Header("Panels")]
    public GameObject homePanel;

    [Header("Navigation")]
    public Button backButton;

    private GameObject       _currentPanel;
    private List<GameObject> _panelHistory = new List<GameObject>(); // ← List, bukan Stack

    void Start()
    {
        if (backButton != null)
            backButton.onClick.AddListener(GoBack);

        if (homePanel != null)
            OpenPanel(homePanel, isHome: true);
    }

    public void OpenPanel(GameObject panel, bool isHome = false)
    {
        if (panel == null) return;

        if (_currentPanel != null)
        {
            _currentPanel.SetActive(false);
            if (!isHome)
                _panelHistory.Add(_currentPanel); // ← Add, bukan Push
        }

        _currentPanel = panel;
        _currentPanel.SetActive(true);

        if (backButton != null)
            backButton.gameObject.SetActive(_panelHistory.Count > 0);
    }

    public void GoBack()
    {
        if (_panelHistory.Count == 0) return;

        if (_currentPanel != null)
            _currentPanel.SetActive(false);

        // Ambil panel terakhir dari list (simulasi Pop)
        int last = _panelHistory.Count - 1;
        _currentPanel = _panelHistory[last];
        _panelHistory.RemoveAt(last);

        _currentPanel.SetActive(true);

        if (backButton != null)
            backButton.gameObject.SetActive(_panelHistory.Count > 0);
    }

    public void GoHome()
    {
        _panelHistory.Clear();
        OpenPanel(homePanel, isHome: true);
    }
}

// ══════════════════════════════════════════════════════════════════
//  4. PHONE MENU BUTTON
// ══════════════════════════════════════════════════════════════════
public class PhoneMenuButton : MonoBehaviour
{
    [Header("Navigation")]
    public GameObject    targetPanel;
    public PhoneNavigator navigator;

    private Button _button;

    void Start()
    {
        _button = GetComponent<Button>();
        if (_button != null)
            _button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (navigator != null && targetPanel != null)
            navigator.OpenPanel(targetPanel);
        else
            Debug.LogWarning("[PhoneMenuButton] navigator atau targetPanel belum di-assign!");
    }
}