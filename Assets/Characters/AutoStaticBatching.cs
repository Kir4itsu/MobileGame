using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// AutoStaticBatching — otomatis set object non-player/NPC jadi Static
/// 
/// Cara pakai:
/// 1. Attach script ini ke GameObject kosong di scene (misal "OptimizationManager")
/// 2. Isi daftar tag yang DIKECUALIKAN (Player, NPC, dll) di Inspector
/// 3. Klik kanan script di Inspector → "Apply Static Batching" untuk set di Editor
/// 4. Atau aktifkan "Auto Apply On Play" untuk runtime batching
/// </summary>
public class AutoStaticBatching : MonoBehaviour
{
    [Header("Exclusion Settings")]
    [Tooltip("Tag yang TIDAK akan di-set Static (player, NPC, object bergerak)")]
    public string[] excludedTags = { "Player", "OtherPlayer", "NPC", "Enemy", "Projectile" };

    [Tooltip("Nama GameObject yang TIDAK akan di-set Static (partial match)")]
    public string[] excludedNames = { "NPC", "T-Pose", "FloatingJoystick", "Canvas",
                                      "EventSystem", "Camera", "DialogueManager",
                                      "MinimapSystem", "SettingsMenu", "GraphicsSettings" };

    [Header("Runtime Batching")]
    [Tooltip("Jalankan StaticBatchingUtility.Combine() saat game start untuk batching dinamis.\n" +
             "Berguna kalau tidak bisa set Static flag di Editor (prefab dari Photon dll).")]
    public bool combineOnStart = true;

    [Header("Debug")]
    public bool showLog = true;

    // ──────────────────────────────────────────────
    void Start()
    {
        if (combineOnStart)
            ApplyRuntimeBatching();
    }

    // ──────────────────────────────────────────────
    //  RUNTIME BATCHING
    // ──────────────────────────────────────────────
    /// <summary>
    /// Kumpulkan semua MeshRenderer yang tidak bergerak lalu
    /// paksa Unity batch mereka via StaticBatchingUtility.Combine().
    /// Ini bekerja meski Static flag tidak di-set di Editor.
    /// </summary>
    public void ApplyRuntimeBatching()
    {
        // Kumpulkan semua renderer yang layak di-batch
        MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

        var batchTargets = new System.Collections.Generic.List<GameObject>();

        foreach (MeshRenderer r in allRenderers)
        {
            if (ShouldExclude(r.gameObject)) continue;
            batchTargets.Add(r.gameObject);
        }

        if (batchTargets.Count == 0)
        {
            if (showLog) Debug.Log("[AutoStaticBatching] Tidak ada object yang perlu di-batch.");
            return;
        }

        // Combine: Unity gabung mesh jadi sedikit draw call
        StaticBatchingUtility.Combine(batchTargets.ToArray(), gameObject);

        if (showLog)
            Debug.Log($"[AutoStaticBatching] ✅ Runtime batching selesai! {batchTargets.Count} object di-batch.");
    }

    // ──────────────────────────────────────────────
    //  HELPERS
    // ──────────────────────────────────────────────
    bool ShouldExclude(GameObject go)
    {
        // Cek tag
        foreach (string tag in excludedTags)
        {
            try { if (go.CompareTag(tag)) return true; }
            catch { /* tag tidak exist di project, skip */ }
        }

        // Cek nama (partial match, case-insensitive)
        string goName = go.name.ToLower();
        foreach (string n in excludedNames)
        {
            if (goName.Contains(n.ToLower())) return true;
        }

        // Cek apakah punya Rigidbody / CharacterController (berarti bergerak)
        if (go.GetComponentInParent<Rigidbody>()          != null) return true;
        if (go.GetComponentInParent<CharacterController>() != null) return true;
        if (go.GetComponentInParent<Animator>()            != null) return true;

        return false;
    }

// ──────────────────────────────────────────────
//  EDITOR TOOL
// ──────────────────────────────────────────────
#if UNITY_EDITOR
    [ContextMenu("Apply Static Flags (Editor Only)")]
    public void ApplyStaticFlagsInEditor()
    {
        MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

        int setCount  = 0;
        int skipCount = 0;

        foreach (MeshRenderer r in allRenderers)
        {
            GameObject go = r.gameObject;

            if (ShouldExclude(go))
            {
                if (showLog) Debug.Log($"[AutoStaticBatching] ⏭ Skip: {go.name}");
                skipCount++;
                continue;
            }

            // Set semua static flags
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.BatchingStatic       |
                StaticEditorFlags.OccludeeStatic       |
                StaticEditorFlags.OccluderStatic       |
                StaticEditorFlags.ContributeGI         |
                StaticEditorFlags.ReflectionProbeStatic);

            setCount++;
        }

        Debug.Log($"[AutoStaticBatching] ✅ Selesai! {setCount} object di-set Static, {skipCount} di-skip.");

        // Tandai scene sudah berubah supaya bisa disave
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    [ContextMenu("Remove All Static Flags (Undo)")]
    public void RemoveAllStaticFlags()
    {
        MeshRenderer[] allRenderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        foreach (MeshRenderer r in allRenderers)
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);

        Debug.Log("[AutoStaticBatching] 🗑 Semua static flags dihapus.");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
#endif
}