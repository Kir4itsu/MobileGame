using UnityEngine;

public class TriggerVisited : MonoBehaviour
{
    public ExploreLocation location;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            location?.MarkVisited();
            Debug.Log($"[TriggerVisited] Player masuk zona '{location?.locationName}'");
        }
    }
}