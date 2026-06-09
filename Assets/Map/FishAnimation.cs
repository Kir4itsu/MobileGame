using UnityEngine;

public class FishAnimation : MonoBehaviour
{
    [Header("Wiggle Settings")]
    public float wiggleSpeed = 2f;
    public float wiggleAmount = 12f;

    [HideInInspector] public float currentSpeed = 1f;

    private float wiggleTimer;
    private Quaternion baseRotation;

    void Start()
    {
        baseRotation = transform.localRotation;
    }

    void Update()
    {
        wiggleTimer += Time.deltaTime * wiggleSpeed * currentSpeed;

        float wiggle = Mathf.Sin(wiggleTimer) * wiggleAmount;

        // Wiggle ditambahkan di atas rotasi awal
        transform.localRotation = baseRotation * Quaternion.Euler(
            0,
            wiggle,
            Mathf.Sin(wiggleTimer * 0.5f) * 2f
        );
    }
}