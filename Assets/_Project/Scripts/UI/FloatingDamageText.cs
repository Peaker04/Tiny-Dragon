using UnityEngine;

[RequireComponent(typeof(TextMesh))]
public class FloatingDamageText : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.8f;
    [SerializeField] private float riseSpeed = 1.2f;
    [SerializeField] private float horizontalDrift = 0.25f;

    private TextMesh textMesh;
    private Color startColor;
    private float age;
    private Vector3 velocity;
    private System.Action<FloatingDamageText> releaseToPool;

    private void Awake()
    {
        textMesh = GetComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = 48;
        textMesh.characterSize = 0.04f;

        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 1000;
            if (textMesh.font == null)
            {
                textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            if (meshRenderer.sharedMaterial == null && textMesh.font != null)
            {
                meshRenderer.sharedMaterial = textMesh.font.material;
            }
        }
    }

    public void Initialize(string text, Color color, System.Action<FloatingDamageText> releaseHandler = null)
    {
        age = 0f;
        releaseToPool = releaseHandler;
        textMesh.text = text;
        textMesh.color = color;

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
            Release();
        }
    }

    private void Release()
    {
        if (releaseToPool != null)
        {
            releaseToPool(this);
            return;
        }

        Destroy(gameObject);
    }
}
