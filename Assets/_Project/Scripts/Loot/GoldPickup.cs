using TinyDragon.Audio;
using TinyDragon.Config;
using TinyDragon.Data;
using TinyDragon.Shared.Unity;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class GoldPickup : MonoBehaviour
{
    private const float DropDuration = 0.18f;
    private const float Lifetime = 20f;
    private const float WorldScale = 1.3f;
    private const int PotentialPointsPerGold = 10;

    private static Material spriteDefaultMaterial;

    private int amount;
    private bool collected;
    private float age;
    private Vector3 startPosition;
    private Vector3 restingPosition;

    public static GoldPickup Create(Vector3 position, int amount, string spritePath)
    {
        GameObject pickupObject = new GameObject("Gold Pickup");
        pickupObject.transform.position = position;

        SpriteRenderer renderer = pickupObject.AddComponent<SpriteRenderer>();
        renderer.sprite = ResourceLoader.Load<Sprite>(spritePath);
        renderer.color = Color.white;
        renderer.sortingOrder = 30;
        SetUnlitSpriteMaterial(renderer);

        CircleCollider2D trigger = pickupObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = 0.28f;

        Rigidbody2D body = pickupObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        GoldPickup pickup = pickupObject.AddComponent<GoldPickup>();
        pickup.Initialize(amount, position);
        return pickup;
    }

    private void Initialize(int newAmount, Vector3 position)
    {
        amount = Mathf.Max(newAmount, 1);
        startPosition = position;
        restingPosition = position + new Vector3(Random.Range(-0.4f, 0.4f), -0.2f, 0f);
        transform.localScale = Vector3.one * WorldScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (age < DropDuration)
        {
            float progress = age / DropDuration;
            Vector3 arc = Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.3f);
            transform.position = Vector3.Lerp(startPosition, restingPosition, progress) + arc;
            return;
        }

        transform.position = restingPosition + Vector3.up * (Mathf.Sin((age - DropDuration) * 4f) * 0.035f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth == null || playerHealth.CurrentHealth <= 0)
        {
            return;
        }

        if (!TinyDragonSaveManager.Instance.TryAddGold(amount, out _))
        {
            return;
        }

        int gainedPotential = amount * PotentialPointsPerGold;
        TinyDragonSaveManager.Instance.TryAddPotentialPoints(gainedPotential, out _);
        UiSoundPlayer.PlayCoinPickup();

        collected = true;
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.enabled = false;
        }

        ShowPickupPopup(playerHealth);
        Destroy(gameObject);
    }

    private void ShowPickupPopup(PlayerHealth playerHealth)
    {
        string popupPath = TinyDragonRuntimeConfigProvider.Resolve(null).Resources.damagePopupPrefabPath;
        GameObject popupPrefab = ResourceLoader.Load<GameObject>(popupPath);
        if (popupPrefab == null)
        {
            return;
        }

        GameObject popupObject = Instantiate(
            popupPrefab,
            playerHealth.transform.position + Vector3.up * 1.25f,
            Quaternion.identity,
            RuntimeSceneRoot.GetChild("LootPopups")
        );
        popupObject.transform.localScale = Vector3.one * 1.35f;

        FloatingDamageText popup = popupObject.GetComponent<FloatingDamageText>();
        if (popup != null)
        {
            popup.Initialize($"+{amount} GOLD\n+{amount * PotentialPointsPerGold} tiềm năng", new Color(1f, 0.82f, 0.05f));
        }
    }

    private static void SetUnlitSpriteMaterial(SpriteRenderer renderer)
    {
        if (spriteDefaultMaterial == null)
        {
            Shader spriteShader = Shader.Find("Sprites/Default");
            if (spriteShader == null)
            {
                return;
            }

            spriteDefaultMaterial = new Material(spriteShader);
        }

        renderer.sharedMaterial = spriteDefaultMaterial;
    }
}
