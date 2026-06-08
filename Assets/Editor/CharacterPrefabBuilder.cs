#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// CharacterPrefabBuilder — Auto bikin prefab karakter dari FBX.
///
/// CARA PAKAI:
/// 1. Taruh script ini di folder Assets/Editor/
/// 2. Di Unity menu bar → Tools → Character → Build Character Prefabs
/// 3. Semua FBX di folder Characters/MCT dan Characters/FemaleMC/FCT
///    otomatis dijadikan prefab lengkap dengan:
///    - PlayerMovement
///    - CharacterController
///    - Animator (dengan controller yang sesuai)
///    - Rigidbody (kinematic)
///    - Tag "Player"
///    - Layer "Character" (auto-create kalau belum ada)
/// 4. Prefab disimpan di Assets/Resources/Characters/
/// </summary>
public class CharacterPrefabBuilder : EditorWindow
{
    // ── Config path — sesuaikan kalau folder kamu beda ──────────
    private string _maleFBXPath    = "Assets/Characters/MCT.fbx";
    private string _femaleFBXPath  = "Assets/Characters/FemaleMC/FCT.fbx";
    private string _maleController = "Assets/Characters/PlayerAnimator.controller";
    private string _femaleController = "Assets/Characters/FemaleMC/FemalePlayerAnimator.controller";
    private string _outputFolder   = "Assets/Resources/Characters";
    private string _spriteFolder   = "Assets/Resources/CharacterSprites";
    private string _maleSpriteSearch   = "MaleMC";
    private string _femaleSpriteSearch = "PlayerSprite";

    private Vector2 _scroll;
    private string _log = "";

    [MenuItem("Tools/Character/Build Character Prefabs")]
    public static void OpenWindow()
    {
        var win = GetWindow<CharacterPrefabBuilder>("Character Prefab Builder");
        win.minSize = new Vector2(420f, 500f);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Character Prefab Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("FBX Sources", EditorStyles.boldLabel);
        _maleFBXPath     = EditorGUILayout.TextField("Male FBX Path",     _maleFBXPath);
        _femaleFBXPath   = EditorGUILayout.TextField("Female FBX Path",   _femaleFBXPath);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Animator Controllers", EditorStyles.boldLabel);
        _maleController   = EditorGUILayout.TextField("Male Controller",   _maleController);
        _femaleController = EditorGUILayout.TextField("Female Controller", _femaleController);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        _outputFolder  = EditorGUILayout.TextField("Prefab Output Folder", _outputFolder);
        _spriteFolder  = EditorGUILayout.TextField("Sprite Output Folder", _spriteFolder);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Sprite Search Keywords", EditorStyles.boldLabel);
        _maleSpriteSearch   = EditorGUILayout.TextField("Male Sprite Keyword",   _maleSpriteSearch);
        _femaleSpriteSearch = EditorGUILayout.TextField("Female Sprite Keyword", _femaleSpriteSearch);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("▶  Build All Prefabs", GUILayout.Height(40)))
        {
            _log = "";
            BuildAll();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Log:", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void BuildAll()
    {
        Log("=== Building Character Prefabs ===");

        // Pastikan output folder ada
        EnsureFolder(_outputFolder);
        EnsureFolder(_spriteFolder);

        // Build male
        BuildPrefab(
            fbxPath:        _maleFBXPath,
            controllerPath: _maleController,
            prefabName:     "MCT",
            spriteKeyword:  _maleSpriteSearch
        );

        // Build female
        BuildPrefab(
            fbxPath:        _femaleFBXPath,
            controllerPath: _femaleController,
            prefabName:     "FCT",
            spriteKeyword:  _femaleSpriteSearch
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Log("\n✅ Selesai! Cek folder: " + _outputFolder);
    }

    void BuildPrefab(string fbxPath, string controllerPath, string prefabName, string spriteKeyword)
    {
        Log($"\n--- Building: {prefabName} ---");

        // Load FBX
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null)
        {
            Log($"❌ FBX tidak ditemukan: {fbxPath}");
            return;
        }
        Log($"✅ FBX loaded: {fbx.name}");

        // Load Animator Controller
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
        if (controller == null)
            Log($"⚠️  Controller tidak ditemukan: {controllerPath} (Animator tidak akan di-assign)");
        else
            Log($"✅ Controller loaded: {controller.name}");

        // Instantiate FBX ke scene sementara
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = prefabName;
        go.tag  = "Player";

        // Set layer (buat kalau belum ada)
        int layer = EnsureLayer("Character");
        SetLayerRecursive(go, layer);

        // ── CharacterController ──────────────────────────────────
        var cc = go.GetComponent<CharacterController>();
        if (cc == null) cc = go.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 1f, 0f);
        cc.height = 1.8f;
        cc.radius = 0.3f;
        Log("✅ CharacterController added");

        // ── Rigidbody (kinematic) ────────────────────────────────
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
        Log("✅ Rigidbody (kinematic) added");

        // ── Animator ────────────────────────────────────────────
        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = go.AddComponent<Animator>();
        if (controller != null)
            anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        Log("✅ Animator configured");

        // ── PlayerMovement ───────────────────────────────────────
        var pm = go.GetComponent<PlayerMovement>();
        if (pm == null) pm = go.AddComponent<PlayerMovement>();
        pm.animator = anim;
        Log("✅ PlayerMovement added");

        // ── Cari dan copy sprite ke Resources/CharacterSprites ───
        CopySprite(spriteKeyword, prefabName);

        // ── Simpan sebagai prefab ────────────────────────────────
        string prefabPath = $"{_outputFolder}/{prefabName}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        if (prefab != null)
            Log($"✅ Prefab saved: {prefabPath}");
        else
            Log($"❌ Gagal simpan prefab: {prefabPath}");
    }

    void CopySprite(string keyword, string outputName)
    {
        // Cari sprite di seluruh project yang namanya mengandung keyword
        string[] guids = AssetDatabase.FindAssets($"t:Sprite {keyword}");
        if (guids.Length == 0)
        {
            // Fallback: cari tanpa filter type
            guids = AssetDatabase.FindAssets(keyword);
        }

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;
            if (!sprite.name.ToLower().Contains(keyword.ToLower())) continue;

            // Copy ke Resources/CharacterSprites dengan nama = prefabName
            string destPath = $"{_spriteFolder}/{outputName}.png";

            // Kalau sudah ada, skip
            if (AssetDatabase.LoadAssetAtPath<Sprite>(destPath) != null)
            {
                Log($"✅ Sprite sudah ada: {destPath}");
                return;
            }

            // Tandai texture sebagai readable
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            // Load texture dan simpan copy
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Log($"⚠️  Gagal load texture dari: {path}");
                return;
            }

            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(Application.dataPath + destPath.Replace("Assets", ""), bytes);
            AssetDatabase.ImportAsset(destPath);

            // Set sebagai Sprite
            var destImporter = AssetImporter.GetAtPath(destPath) as TextureImporter;
            if (destImporter != null)
            {
                destImporter.textureType = TextureImporterType.Sprite;
                destImporter.SaveAndReimport();
            }

            Log($"✅ Sprite copied: {path} → {destPath}");
            return;
        }

        Log($"⚠️  Sprite tidak ditemukan untuk keyword: '{keyword}'");
    }

    // ── Helpers ──────────────────────────────────────────────────

    void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string folder = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folder);
        Log($"📁 Folder dibuat: {path}");
    }

    int EnsureLayer(string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1) return layer;

        // Cari slot kosong di layer 8-31
        var tagManager = new UnityEditor.SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            var el = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(el.stringValue))
            {
                el.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Log($"✅ Layer '{layerName}' dibuat di slot {i}");
                return i;
            }
        }
        Log($"⚠️  Tidak bisa buat layer '{layerName}' — semua slot penuh");
        return 0;
    }

    void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    void Log(string msg)
    {
        _log += msg + "\n";
        Debug.Log("[CharacterPrefabBuilder] " + msg);
        Repaint();
    }
}
#endif