using UnityEngine;

public class RainBlocker : MonoBehaviour
{
    public WeatherManager weatherManager;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            weatherManager.SetRainVisible(false);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            weatherManager.SetRainVisible(true);
    }
}