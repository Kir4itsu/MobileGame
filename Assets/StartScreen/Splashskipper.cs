using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SplashSkipper — Attach ke GameObject di scene pertama (index 0 di Build Settings).
/// Scene ini harus INDEX 0, StartScreen INDEX 1, LoadingScreen INDEX 2, SampleScene INDEX 3.
///
/// Script ini langsung load StartScreen tanpa delay sehingga logo Unity 
/// tidak terasa "nyangkut" ke game.
///
/// Cara pakai:
///   1. Buat scene baru bernama "Boot" (kosong).
///   2. Tambahkan Empty GameObject, attach script ini.
///   3. Di Build Settings: Boot=0, StartScreen=1, LoadingScreen=2, SampleScene=3.
///   4. Di Project Settings → Player → Splash Screen: matikan "Show Unity Logo" 
///      atau set durasi ke minimum.
/// </summary>
public class SplashSkipper : MonoBehaviour
{
    [Tooltip("Scene name tujuan setelah boot (biasanya 'StartScreen')")]
    public string startScreenName = "StartScreen";

    [Tooltip("Delay kecil sebelum pindah (0 = langsung)")]
    public float delay = 0f;

    IEnumerator Start()
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        SceneManager.LoadScene(startScreenName);
    }
}