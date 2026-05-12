using UnityEngine;

public class AutoAddColliders : MonoBehaviour
{
    [Header("Settings")]
    public bool useMeshCollider = true; // True = Mesh Collider, False = Box Collider
    public bool convex = false; // Only for Mesh Collider
    
    [ContextMenu("Add Colliders to All Children")]
    public void AddCollidersToChildren()
    {
        // Get all MeshRenderers in children
        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
        
        int count = 0;
        
        foreach (MeshRenderer renderer in renderers)
        {
            GameObject obj = renderer.gameObject;
            
            // Skip if already has collider
            if (obj.GetComponent<Collider>() != null)
            {
                Debug.Log($"⏭️ {obj.name} already has collider, skipping...");
                continue;
            }
            
            if (useMeshCollider)
            {
                // Add Mesh Collider
                MeshCollider meshCollider = obj.AddComponent<MeshCollider>();
                meshCollider.convex = convex;
                Debug.Log($"✅ Added Mesh Collider to {obj.name}");
            }
            else
            {
                // Add Box Collider
                BoxCollider boxCollider = obj.AddComponent<BoxCollider>();
                Debug.Log($"✅ Added Box Collider to {obj.name}");
            }
            
            count++;
        }
        
        Debug.Log($"🎉 Done! Added {count} colliders.");
    }
    
    [ContextMenu("Remove All Colliders from Children")]
    public void RemoveAllColliders()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        
        foreach (Collider col in colliders)
        {
            DestroyImmediate(col);
        }
        
        Debug.Log($"🗑️ Removed {colliders.Length} colliders.");
    }
}