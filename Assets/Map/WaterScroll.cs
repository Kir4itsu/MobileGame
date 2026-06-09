using UnityEngine;

public class WaterScroll : MonoBehaviour
{
    public float scrollX = 0.05f;
    public float scrollY = 0.03f;

    private Material mat;

    void Start()
    {
        mat = GetComponent<Renderer>().material;
    }

    void Update()
    {
        float x = Mathf.Repeat(Time.time * scrollX, 1);
        float y = Mathf.Repeat(Time.time * scrollY, 1);
        mat.mainTextureOffset = new Vector2(x, y);
    }
}