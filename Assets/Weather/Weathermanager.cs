using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// WeatherManager - GTA4-style, fixed velocity curve error
/// Rain Particle harus jadi CHILD of Main Camera
/// Position Rain Particle: X:0, Y:8, Z:5
/// Rotation Rain Particle: X:90, Y:0, Z:0
/// Scale Rain Particle: X:1, Y:1, Z:1
/// Keyboard test: 1=Sunny, 2=Cloudy, 3=Rainy
/// </summary>
public class WeatherManager : MonoBehaviour
{
    public enum WeatherType { Sunny, Cloudy, Rainy }
    public enum GraphicsQuality { Low, Medium, High }

    [Header("=== AUTO WEATHER ===")]
    public bool autoChangeWeather = true;
    public float weatherChangeDuration = 60f;
    public float transitionDuration = 4f;

    [Header("=== GRAFIK QUALITY ===")]
    public GraphicsQuality graphicsQuality = GraphicsQuality.High;

    [Header("=== REFERENSI OBJECT ===")]
    public Light sunLight;
    public ParticleSystem rainParticle;
    public ParticleSystem splashParticle;
    public GameObject cloudOverlay;
    public AudioSource rainAudio;
    public AudioSource thunderAudio;
    public AudioClip[] thunderClips;

    [Header("=== WIND ===")]
    public Transform[] windObjects;

    [Header("=== STATUS ===")]
    public WeatherType currentWeather = WeatherType.Sunny;

    // Private
    private float weatherTimer = 0f;
    private bool isTransitioning = false;
    private float windStrength = 0f;
    private float targetWindStrength = 0f;
    private Coroutine thunderCoroutine;
    private float currentWindX = 0f;
    private float targetWindX = 0f;

    private float startLightIntensity, targetLightIntensity;
    private Color startLightColor, targetLightColor;
    private float startFogDensity, targetFogDensity;
    private Color startFogColor, targetFogColor;
    private float startAmbient, targetAmbient;

    // =============================================
    // PRESET CUACA
    // =============================================
    struct WeatherPreset {
        public float lightIntensity;
        public Color lightColor;
        public float fogDensity;
        public Color fogColor;
        public float ambientIntensity;
        public float wind;
    }

    WeatherPreset presetSunny = new WeatherPreset {
        lightIntensity   = 1.3f,
        lightColor       = new Color(1f, 0.96f, 0.82f),
        fogDensity       = 0.002f,
        fogColor         = new Color(0.82f, 0.91f, 1f),
        ambientIntensity = 1.1f,
        wind             = 0f
    };
    WeatherPreset presetCloudy = new WeatherPreset {
        lightIntensity   = 0.55f,
        lightColor       = new Color(0.82f, 0.86f, 0.95f),
        fogDensity       = 0.018f,
        fogColor         = new Color(0.62f, 0.65f, 0.7f),
        ambientIntensity = 0.45f,
        wind             = 0.3f
    };
    WeatherPreset presetRainy = new WeatherPreset {
        lightIntensity   = 0.25f,
        lightColor       = new Color(0.68f, 0.74f, 0.88f),
        fogDensity       = 0.055f,
        fogColor         = new Color(0.42f, 0.46f, 0.54f),
        ambientIntensity = 0.28f,
        wind             = 1f
    };

    // =============================================
    // PRESET GRAFIK
    // =============================================
    struct GfxPreset {
        public int maxParticles;
        public float emissionRate;
        public float startSize;
        public bool useSplash;
        public bool useThunder;
        public bool useWind;
    }

    GfxPreset gfxLow    = new GfxPreset { maxParticles = 500,  emissionRate = 80f,  startSize = 0.06f, useSplash = false, useThunder = false, useWind = false };
    GfxPreset gfxMedium = new GfxPreset { maxParticles = 1500, emissionRate = 200f, startSize = 0.07f, useSplash = true,  useThunder = true,  useWind = true  };
    GfxPreset gfxHigh   = new GfxPreset { maxParticles = 3500, emissionRate = 450f, startSize = 0.08f, useSplash = true,  useThunder = true,  useWind = true  };

    // =============================================
    // START
    // =============================================
    void Start()
    {
        RenderSettings.fog     = true;
        RenderSettings.fogMode = FogMode.Exponential;

        SetupRainParticle();

        if (splashParticle != null) splashParticle.Stop();
        if (cloudOverlay   != null) cloudOverlay.SetActive(false);

        ApplyWeatherImmediate(currentWeather);
    }

    // =============================================
    // UPDATE
    // =============================================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ChangeWeather(WeatherType.Sunny);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ChangeWeather(WeatherType.Cloudy);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ChangeWeather(WeatherType.Rainy);

        if (autoChangeWeather && !isTransitioning)
        {
            weatherTimer += Time.deltaTime;
            if (weatherTimer >= weatherChangeDuration)
            {
                weatherTimer = 0f;
                PickNextWeather();
            }
        }

        // Smooth wind
        windStrength = Mathf.Lerp(windStrength, targetWindStrength, Time.deltaTime * 2f);
        currentWindX = Mathf.Lerp(currentWindX, targetWindX, Time.deltaTime * 1.5f);
        ApplyWind();
    }

    // =============================================
    // SETUP RAIN PARTICLE
    // =============================================
    void SetupRainParticle()
    {
        if (rainParticle == null) return;

        GfxPreset gfx = GetGfxPreset();

        // MAIN MODULE
        var main             = rainParticle.main;
        main.maxParticles    = gfx.maxParticles;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(18f, 26f);
        main.startSize       = new ParticleSystem.MinMaxCurve(gfx.startSize * 0.7f, gfx.startSize);
        main.gravityModifier = 0.6f;
        // LOCAL = ikut kamera (karena child of camera)
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor      = new Color(0.78f, 0.85f, 0.97f, 0.55f);

        // EMISSION
        var emission          = rainParticle.emission;
        emission.enabled      = true;
        emission.rateOverTime = gfx.emissionRate;

        // SHAPE — Box lebar di atas kamera
        var shape        = rainParticle.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Box;
        shape.scale      = new Vector3(30f, 1f, 30f);

        // VELOCITY OVER LIFETIME
        // PENTING: Semua axis harus mode yang sama (Constant) — ini fix error!
        var vel          = rainParticle.velocityOverLifetime;
        vel.enabled      = true;
        vel.space        = ParticleSystemSimulationSpace.Local;
        // Gunakan MinMaxCurve constant mode untuk semua axis
        vel.x            = new ParticleSystem.MinMaxCurve(0f); // angin diatur lewat Update
        vel.y            = new ParticleSystem.MinMaxCurve(0f);
        vel.z            = new ParticleSystem.MinMaxCurve(0f);

        // RENDERER — Stretch supaya terlihat garis hujan jatuh
        var rend                  = rainParticle.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            rend.renderMode        = ParticleSystemRenderMode.Stretch;
            rend.lengthScale       = 2.5f;
            rend.velocityScale     = 0.15f;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows    = false;
        }

        rainParticle.Stop();
    }

    // =============================================
    // GANTI GRAFIK (panggil dari UI)
    // =============================================
    public void SetGraphicsQuality(GraphicsQuality quality)
    {
        graphicsQuality = quality;
        SetupRainParticle();
        if (currentWeather == WeatherType.Rainy) { rainParticle.Stop(); rainParticle.Play(); }
        Debug.Log("[WeatherManager] Grafik: " + quality);
    }
    public void SetQualityLow()    => SetGraphicsQuality(GraphicsQuality.Low);
    public void SetQualityMedium() => SetGraphicsQuality(GraphicsQuality.Medium);
    public void SetQualityHigh()   => SetGraphicsQuality(GraphicsQuality.High);

    // =============================================
    // RainVisible (dipanggil dari RainBlocker)
    // =============================================
    public void SetRainVisible(bool visible)
    {
        if (rainParticle == null) return;
        var emission = rainParticle.emission;
        emission.enabled = visible;
        if (visible && currentWeather == WeatherType.Rainy)
            rainParticle.Play();
        else
            rainParticle.Stop();
    }

    // =============================================
    // CHANGE WEATHER
    // =============================================
    public void ChangeWeather(WeatherType newWeather)
    {
        if (isTransitioning || newWeather == currentWeather) return;

        if (sunLight != null) { startLightIntensity = sunLight.intensity; startLightColor = sunLight.color; }
        startFogDensity = RenderSettings.fogDensity;
        startFogColor   = RenderSettings.fogColor;
        startAmbient    = RenderSettings.ambientIntensity;

        WeatherPreset t      = GetPreset(newWeather);
        targetLightIntensity = t.lightIntensity;
        targetLightColor     = t.lightColor;
        targetFogDensity     = t.fogDensity;
        targetFogColor       = t.fogColor;
        targetAmbient        = t.ambientIntensity;
        targetWindStrength   = t.wind;
        targetWindX          = t.wind * -4f; // Miring ke satu arah

        currentWeather = newWeather;
        StartCoroutine(TransitionCoroutine(newWeather));
    }

    IEnumerator TransitionCoroutine(WeatherType target)
    {
        isTransitioning = true;
        float elapsed   = 0f;

        if (target == WeatherType.Rainy) StartRain();
        else StopRain();

        if (cloudOverlay != null)
            cloudOverlay.SetActive(target != WeatherType.Sunny);

        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

            if (sunLight != null)
            {
                sunLight.intensity = Mathf.Lerp(startLightIntensity, targetLightIntensity, t);
                sunLight.color     = Color.Lerp(startLightColor, targetLightColor, t);
            }
            RenderSettings.fogDensity       = Mathf.Lerp(startFogDensity, targetFogDensity, t);
            RenderSettings.fogColor         = Color.Lerp(startFogColor, targetFogColor, t);
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbient, targetAmbient, t);

            yield return null;
        }

        ApplyWeatherImmediate(target);
        isTransitioning = false;
        Debug.Log("[WeatherManager] Cuaca: " + target);
    }

    void ApplyWeatherImmediate(WeatherType weather)
    {
        WeatherPreset p = GetPreset(weather);
        if (sunLight != null) { sunLight.intensity = p.lightIntensity; sunLight.color = p.lightColor; }
        RenderSettings.fogDensity       = p.fogDensity;
        RenderSettings.fogColor         = p.fogColor;
        RenderSettings.ambientIntensity = p.ambientIntensity;
        targetWindStrength              = p.wind;
        targetWindX                     = p.wind * -4f;

        if (weather == WeatherType.Rainy) StartRain();
        else StopRain();

        if (cloudOverlay != null)
            cloudOverlay.SetActive(weather != WeatherType.Sunny);
    }

    // =============================================
    // START / STOP RAIN
    // =============================================
    void StartRain()
    {
        if (rainParticle == null) return;
        GfxPreset gfx         = GetGfxPreset();
        var emission           = rainParticle.emission;
        emission.enabled       = true;
        emission.rateOverTime  = gfx.emissionRate;
        if (!rainParticle.isPlaying) rainParticle.Play();

        if (splashParticle != null && gfx.useSplash && !splashParticle.isPlaying)
            splashParticle.Play();
        if (rainAudio != null && !rainAudio.isPlaying)
            rainAudio.Play();
        if (gfx.useThunder && thunderCoroutine == null)
            thunderCoroutine = StartCoroutine(ThunderCoroutine());
    }

    void StopRain()
    {
        if (rainParticle != null)
        {
            var emission     = rainParticle.emission;
            emission.enabled = false;
            rainParticle.Stop();
        }
        if (splashParticle != null && splashParticle.isPlaying) splashParticle.Stop();
        if (rainAudio != null && rainAudio.isPlaying) rainAudio.Stop();
        if (thunderCoroutine != null) { StopCoroutine(thunderCoroutine); thunderCoroutine = null; }
    }

    // =============================================
    // PETIR
    // =============================================
    IEnumerator ThunderCoroutine()
    {
        while (currentWeather == WeatherType.Rainy)
        {
            yield return new WaitForSeconds(Random.Range(8f, 25f));
            if (currentWeather != WeatherType.Rainy) break;
            StartCoroutine(LightningFlash());
            if (thunderAudio != null && thunderClips != null && thunderClips.Length > 0)
            {
                thunderAudio.clip = thunderClips[Random.Range(0, thunderClips.Length)];
                thunderAudio.Play();
            }
        }
        thunderCoroutine = null;
    }

    IEnumerator LightningFlash()
    {
        if (sunLight == null) yield break;
        float original     = sunLight.intensity;
        sunLight.intensity = 2.5f;
        yield return new WaitForSeconds(0.05f);
        sunLight.intensity = original;
        yield return new WaitForSeconds(0.08f);
        sunLight.intensity = 3f;
        yield return new WaitForSeconds(0.07f);
        sunLight.intensity = original;
    }

    // =============================================
    // WIND
    // =============================================
    void ApplyWind()
    {
        // Update velocity partikel hujan secara smooth (fix error mode curve)
        if (rainParticle != null && rainParticle.isPlaying)
        {
            var vel  = rainParticle.velocityOverLifetime;
            vel.x    = new ParticleSystem.MinMaxCurve(currentWindX);
            vel.y    = new ParticleSystem.MinMaxCurve(0f);
            vel.z    = new ParticleSystem.MinMaxCurve(0f);
        }

        if (windObjects == null) return;
        if (!GetGfxPreset().useWind) return;
        foreach (Transform obj in windObjects)
        {
            if (obj == null) continue;
            float sway        = Mathf.Sin(Time.time * 2f + obj.position.x) * windStrength * 3f;
            obj.localRotation = Quaternion.Euler(sway, 0f, sway * 0.5f);
        }
    }

    // =============================================
    // HELPERS
    // =============================================
    void PickNextWeather()
    {
        WeatherType next;
        do { next = (WeatherType)Random.Range(0, 3); } while (next == currentWeather);
        ChangeWeather(next);
    }

    WeatherPreset GetPreset(WeatherType w)
    {
        switch (w)
        {
            case WeatherType.Sunny:  return presetSunny;
            case WeatherType.Cloudy: return presetCloudy;
            case WeatherType.Rainy:  return presetRainy;
            default:                 return presetSunny;
        }
    }

    GfxPreset GetGfxPreset()
    {
        switch (graphicsQuality)
        {
            case GraphicsQuality.Low:    return gfxLow;
            case GraphicsQuality.Medium: return gfxMedium;
            case GraphicsQuality.High:   return gfxHigh;
            default:                     return gfxMedium;
        }
    }

    public void SetSunny()  => ChangeWeather(WeatherType.Sunny);
    public void SetCloudy() => ChangeWeather(WeatherType.Cloudy);
    public void SetRainy()  => ChangeWeather(WeatherType.Rainy);
}