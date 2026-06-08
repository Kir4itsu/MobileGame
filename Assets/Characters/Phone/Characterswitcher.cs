using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CharacterSwitcher — Auto-setup karakter dari Resources folder.
///
/// SETUP SATU KALI:
/// 1. Pindahkan prefab karakter ke dalam folder Resources:
///    - Assets/Resources/Characters/MCT        (prefab male)
///    - Assets/Resources/Characters/FCT        (prefab female)
/// 2. Pindahkan sprite thumbnail ke:
///    - Assets/Resources/CharacterSprites/MCT  (sprite male)
///    - Assets/Resources/CharacterSprites/FCT  (sprite female)
///    PENTING: Texture Type harus "Sprite (2D and UI)", Sprite Mode = Single, lalu Apply!
/// 3. Buat GameObject kosong, attach script ini
/// 4. PINDAHKAN GameObject CharacterSwitcher ke titik spawn yang diinginkan di scene
/// 5. Play — karakter akan spawn di posisi CharacterSwitcher!
/// </summary>
public class CharacterSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class CharacterData
    {
        public string     characterName;
        public GameObject prefab;
        public Sprite     thumbnail;
    }

    [Header("Auto Setup")]
    [Tooltip("Folder di dalam Resources yang berisi prefab karakter")]
    public string prefabFolder  = "Characters";
    [Tooltip("Folder di dalam Resources yang berisi sprite thumbnail")]
    public string spriteFolder  = "CharacterSprites";

    [Header("Manual Override (opsional — kosongkan untuk auto)")]
    public CharacterData[] manualCharacters;

    private List<CharacterData> _characters = new List<CharacterData>();
    private GameObject _currentInstance;
    private int _activeIndex = 0;

    public static CharacterSwitcher Instance { get; private set; }
    public System.Action<int> OnCharacterChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        LoadCharacters();

        var existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null)
        {
            _currentInstance = existing;
            _activeIndex = 0;
        }
        else if (_characters.Count > 0)
        {
            SpawnCharacter(0, transform.position, transform.rotation);
        }
    }

    void LoadCharacters()
    {
        _characters.Clear();

        // ── Manual override diutamakan ──────────────────────────
        if (manualCharacters != null && manualCharacters.Length > 0)
        {
            foreach (var c in manualCharacters)
                if (c.prefab != null) _characters.Add(c);
            Debug.Log($"[CharacterSwitcher] Manual mode: {_characters.Count} karakter.");
            return;
        }

        // ── DEBUG: cek semua sprite yang berhasil di-load ───────
        var allSprites = Resources.LoadAll<Sprite>(spriteFolder);
        Debug.Log($"[CharacterSwitcher] Total sprite di Resources/{spriteFolder}/: {allSprites.Length}");
        foreach (var s in allSprites)
            Debug.Log($"  [Sprite] name='{s.name}'");

        // ── Auto-load dari Resources ────────────────────────────
        var prefabs = Resources.LoadAll<GameObject>(prefabFolder);
        Debug.Log($"[CharacterSwitcher] Total prefab di Resources/{prefabFolder}/: {prefabs.Length}");

        if (prefabs.Length == 0)
        {
            Debug.LogWarning($"[CharacterSwitcher] Tidak ada prefab di Resources/{prefabFolder}/\n" +
                             "Pastikan prefab sudah dipindah ke folder Resources!");
            return;
        }

        foreach (var prefab in prefabs)
        {
            Debug.Log($"[CharacterSwitcher] Cek prefab: '{prefab.name}' — punya PlayerMovement: {prefab.GetComponent<PlayerMovement>() != null}");

            if (prefab.GetComponent<PlayerMovement>() == null) continue;

            var data           = new CharacterData();
            data.prefab        = prefab;
            data.characterName = prefab.name;

            // Coba load sprite nama persis sama
            data.thumbnail = Resources.Load<Sprite>($"{spriteFolder}/{prefab.name}");
            Debug.Log($"[CharacterSwitcher] Load sprite '{spriteFolder}/{prefab.name}': {(data.thumbnail != null ? "OK" : "GAGAL")}");

            // Fallback: cari sprite yang namanya mengandung nama prefab
            if (data.thumbnail == null)
            {
                foreach (var s in allSprites)
                {
                    if (s.name.ToLower().Contains(prefab.name.ToLower()) ||
                        prefab.name.ToLower().Contains(s.name.ToLower()))
                    {
                        data.thumbnail = s;
                        Debug.Log($"[CharacterSwitcher] Fallback match: prefab='{prefab.name}' → sprite='{s.name}'");
                        break;
                    }
                }
            }

            if (data.thumbnail == null)
                Debug.LogWarning($"[CharacterSwitcher] SPRITE TIDAK DITEMUKAN untuk '{prefab.name}'!\n" +
                                 "Pastikan: 1) nama file sprite sama dengan nama prefab\n" +
                                 "          2) Texture Type = Sprite (2D and UI)\n" +
                                 "          3) Sprite Mode = Single\n" +
                                 "          4) Sudah klik Apply di Inspector");

            _characters.Add(data);
            Debug.Log($"[CharacterSwitcher] Loaded: '{data.characterName}' " +
                      $"| thumbnail: {(data.thumbnail != null ? data.thumbnail.name : "NULL")}");
        }

        // Urutkan: Male dulu, Female kedua
        _characters.Sort((a, b) => {
            bool aFemale = a.characterName.ToLower().Contains("female") ||
                           a.characterName.ToLower().Contains("fct");
            bool bFemale = b.characterName.ToLower().Contains("female") ||
                           b.characterName.ToLower().Contains("fct");
            return aFemale.CompareTo(bFemale);
        });

        Debug.Log($"[CharacterSwitcher] Total karakter siap: {_characters.Count}");
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= _characters.Count) return;
        if (index == _activeIndex && _currentInstance != null) return;

        Vector3    pos = _currentInstance != null ? _currentInstance.transform.position : transform.position;
        Quaternion rot = _currentInstance != null ? _currentInstance.transform.rotation : transform.rotation;

        if (_currentInstance != null)
            Destroy(_currentInstance);

        _activeIndex = index;
        SpawnCharacter(index, pos, rot);
        OnCharacterChanged?.Invoke(index);
    }

    void SpawnCharacter(int index, Vector3 pos, Quaternion rot)
    {
        if (_characters[index].prefab == null)
        {
            Debug.LogError($"[CharacterSwitcher] Prefab null untuk: {_characters[index].characterName}");
            return;
        }

        _currentInstance     = Instantiate(_characters[index].prefab, pos, rot);
        _currentInstance.tag = "Player";

        var pm     = _currentInstance.GetComponent<PlayerMovement>();
        Camera mainCam = Camera.main;
        if (mainCam != null && pm != null)
        {
            pm.cameraTransform = mainCam.transform;
            var camCtrl = mainCam.GetComponent<CameraController>();
            if (camCtrl != null)
            {
                camCtrl.target = _currentInstance.transform;
                camCtrl.RefreshCharacterScale(); // update FPP/TPP/Shoulder height sesuai karakter baru
            }
        }

        if (pm != null && pm.animator == null)
            pm.animator = _currentInstance.GetComponent<Animator>();

        Debug.Log($"[CharacterSwitcher] Spawned: '{_characters[index].characterName}' di {pos}");
    }

    void Update()
    {
        // Shortcut PC: panah kiri/kanan untuk switch karakter
        // Hanya aktif saat HP tertutup supaya tidak konflik dengan navigasi menu HP
        var phone = PhoneManager.Instance;
        if (phone != null && phone.IsPhoneOpen) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            int prev = (_activeIndex - 1 + _characters.Count) % _characters.Count;
            SwitchTo(prev);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            int next = (_activeIndex + 1) % _characters.Count;
            SwitchTo(next);
        }
    }

    public int           CharacterCount  => _characters.Count;
    public CharacterData GetCharacter(int i) => _characters[i];
    public int           ActiveIndex     => _activeIndex;
    public GameObject    CurrentInstance => _currentInstance;
}