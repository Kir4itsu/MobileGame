using UnityEngine;

public class KolamIkanTrigger : MonoBehaviour
{
    public FishMovement[] semuaIkan;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        foreach (var ikan in semuaIkan)
        {
            if (ikan == null) continue;
            ikan.playerTarget = other.transform;
            ikan.playerDalam = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        foreach (var ikan in semuaIkan)
        {
            if (ikan == null) continue;
            ikan.playerTarget = null;
            ikan.playerDalam = false;
        }
    }
}