using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CharacterSelectPanel : MonoBehaviour
{
    [Header("UI References")]
    public Transform  gridParent;
    public GameObject cardTemplate;
    public Button     closeButton;

    [Header("Selected Highlight")]
    public Color selectedColor   = new Color(0.2f, 0.8f, 0.4f, 1f);
    public Color unselectedColor = Color.white;

    private CharacterCard[] _cards;

    void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

        // Tunda 1 frame supaya CharacterSwitcher.Start() sudah selesai LoadCharacters()
        StartCoroutine(LateStart());
    }

    IEnumerator LateStart()
    {
        yield return null; // tunggu 1 frame

        // Kalau CharacterSwitcher masih belum ready, tunggu sampai 3 detik
        float waited = 0f;
        while (CharacterSwitcher.Instance == null && waited < 3f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (CharacterSwitcher.Instance == null)
        {
            Debug.LogError("[CharacterSelectPanel] CharacterSwitcher tidak ditemukan setelah 3 detik!");
            yield break;
        }

        BuildGrid();

        CharacterSwitcher.Instance.OnCharacterChanged += RefreshHighlight;
    }

    void OnDestroy()
    {
        if (CharacterSwitcher.Instance != null)
            CharacterSwitcher.Instance.OnCharacterChanged -= RefreshHighlight;
    }

    void OnEnable()
    {
        // Refresh highlight setiap kali panel dibuka
        if (_cards != null && CharacterSwitcher.Instance != null)
            RefreshHighlight(CharacterSwitcher.Instance.ActiveIndex);
    }

    void BuildGrid()
    {
        int count = CharacterSwitcher.Instance.CharacterCount;
        if (count == 0)
        {
            Debug.LogWarning("[CharacterSelectPanel] Tidak ada karakter — cek Resources/Characters/");
            return;
        }

        _cards = new CharacterCard[count];

        if (cardTemplate != null)
            cardTemplate.SetActive(false);

        for (int i = 0; i < count; i++)
        {
            var data   = CharacterSwitcher.Instance.GetCharacter(i);
            var cardGO = Instantiate(cardTemplate, gridParent);
            cardGO.SetActive(true);

            var card = cardGO.GetComponent<CharacterCard>();
            if (card == null) card = cardGO.AddComponent<CharacterCard>();

            int idx = i; // capture untuk closure
            card.Setup(data.characterName, data.thumbnail, "", () => OnCardClicked(idx));
            _cards[i] = card;
        }

        RefreshHighlight(CharacterSwitcher.Instance.ActiveIndex);
        Debug.Log($"[CharacterSelectPanel] Grid berhasil dibuat: {count} karakter.");
    }

    void OnCardClicked(int index)
    {
        if (CharacterSwitcher.Instance == null) return;
        CharacterSwitcher.Instance.SwitchTo(index);
        RefreshHighlight(index);
    }

    void RefreshHighlight(int activeIndex)
    {
        if (_cards == null) return;
        for (int i = 0; i < _cards.Length; i++)
            if (_cards[i] != null)
                _cards[i].SetSelected(i == activeIndex, selectedColor, unselectedColor);
    }
}