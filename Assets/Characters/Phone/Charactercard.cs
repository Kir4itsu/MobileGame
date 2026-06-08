using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CharacterCard — Komponen satu card di grid pilih karakter.
/// Attach ke prefab card, assign field via Inspector atau otomatis dicari by name.
/// </summary>
public class CharacterCard : MonoBehaviour
{
    [Header("Child References (auto-find jika kosong)")]
    public Image     thumbnailImage;
    public TMP_Text  nameText;
    public TMP_Text  descText;
    public GameObject selectedBadge;   // Highlight/centang saat terpilih
    public Image     cardBackground;   // Background card untuk warna highlight

    private Button _button;

    void Awake()
    {
        // Auto-find child components jika belum di-assign
        if (thumbnailImage == null)
        {
            var t = transform.Find("Thumbnail");
            if (t != null) thumbnailImage = t.GetComponent<Image>();
        }
        if (nameText == null)
        {
            var t = transform.Find("CharName");
            if (t != null) nameText = t.GetComponent<TMP_Text>();
        }
        if (descText == null)
        {
            var t = transform.Find("Description");
            if (t != null) descText = t.GetComponent<TMP_Text>();
        }
        if (selectedBadge == null)
        {
            var t = transform.Find("SelectedBadge");
            if (t != null) selectedBadge = t.gameObject;
        }
        if (cardBackground == null)
            cardBackground = GetComponent<Image>();

        _button = GetComponent<Button>();
    }

    public void Setup(string charName, Sprite thumbnail, string desc, System.Action onClick)
    {
        if (nameText != null)       nameText.text = charName;
        if (descText != null)       descText.text = desc;
        if (thumbnailImage != null && thumbnail != null)
            thumbnailImage.sprite = thumbnail;

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke());
        }

        if (selectedBadge != null)  selectedBadge.SetActive(false);
    }

    public void SetSelected(bool selected, Color onColor, Color offColor)
    {
        if (cardBackground != null)
            cardBackground.color = selected ? onColor : offColor;

        if (selectedBadge != null)
            selectedBadge.SetActive(selected);
    }
}