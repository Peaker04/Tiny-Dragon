using UnityEngine;

public class FloatingDamageText : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float riseSpeed = 1.2f;
    [SerializeField] private float horizontalDrift = 0.25f;

    private TextMesh textMesh;
    private Color startColor;
    private float age;
    private Vector3 velocity;

    public void Initialize(string text, Color color)
    {
        textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.color = color;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.04f;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.sortingOrder = 1000;

        startColor = color;
        velocity = new Vector3(Random.Range(-horizontalDrift, horizontalDrift), riseSpeed, 0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.position += velocity * Time.deltaTime;

        float fade = Mathf.Clamp01(1f - age / lifetime);
        textMesh.color = new Color(startColor.r, startColor.g, startColor.b, fade);

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}
