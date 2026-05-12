using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class PlayerNameTag : MonoBehaviourPun
{
    [Header("UI References")]
    public Text nameText;
    public GameObject nameTagCanvas;
    public float heightOffset = 2f;
    
    private Transform mainCamera;

    void Start()
    {
        if (nameTagCanvas == null)
        {
            Debug.LogWarning("NameTag Canvas not assigned!");
            return;
        }

        // Set nama player
        if (nameText != null)
        {
            nameText.text = photonView.Owner.NickName;
        }

        // Hide nametag untuk player sendiri
        if (photonView.IsMine)
        {
            nameTagCanvas.SetActive(false);
        }
        
        // Get main camera reference
        mainCamera = Camera.main?.transform;
    }

    void LateUpdate()
    {
        if (nameTagCanvas == null || !nameTagCanvas.activeSelf || mainCamera == null)
            return;

        // Nametag selalu ngadep ke camera
        nameTagCanvas.transform.LookAt(mainCamera);
        nameTagCanvas.transform.Rotate(0, 180, 0); // Flip agar teksnya tidak terbalik
        
        // Posisi nametag di atas kepala player
        nameTagCanvas.transform.position = transform.position + Vector3.up * heightOffset;
    }

    // Method untuk update nama (bisa dipanggil dari script lain)
    public void UpdateName(string newName)
    {
        if (nameText != null)
        {
            nameText.text = newName;
        }
    }
}