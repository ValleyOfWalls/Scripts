using UnityEngine;
using System.Collections; // Needed for Coroutines

[RequireComponent(typeof(SpriteRenderer))]
public class PopupEffect : MonoBehaviour
{
    [Header("Shadow Settings")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.5f); // Default shadow color (semi-transparent black)
    public Vector3 shadowOffset = new Vector3(0.1f, -0.1f, 0f); // Default offset for the shadow (Changed Z to 0)
    public string shadowSortingLayerName = "Default"; // Sorting layer for the shadow
    public int shadowSortingOrderOffset = -1; // Offset from the parent's sorting order

    [Header("Popup Animation Settings")]
    public bool usePopupAnimation = true; // Enable scaling animation on start
    public float popupDuration = 0.2f; // Duration of the popup animation
    public Vector3 startScale = new Vector3(0.1f, 0.1f, 1f); // Initial scale for animation
    public float animationDelay = 0f; // Optional delay before animation starts

    private GameObject shadowGameObject;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer shadowSpriteRenderer;
    private Vector3 originalScale;
    private Coroutine popupCoroutine;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalScale = transform.localScale; // Store original scale

        // Check if shadow already exists (e.g., due to script reload in editor)
        Transform existingShadow = transform.Find("Shadow");
        if (existingShadow != null)
        {
            shadowGameObject = existingShadow.gameObject;
            shadowSpriteRenderer = shadowGameObject.GetComponent<SpriteRenderer>();
            if (shadowSpriteRenderer == null) // Should not happen if created by this script, but safety check
            {
                Destroy(shadowGameObject); // Destroy invalid shadow
                CreateShadowObject();
            }
        }
        else
        {
            CreateShadowObject();
        }

        UpdateShadowProperties();
    }

    void Start()
    {
        if (usePopupAnimation && Application.isPlaying)
        {
            // Start animation slightly delayed if needed
            if (animationDelay > 0)
            {
                transform.localScale = startScale; // Set initial scale immediately if delaying
                if (shadowGameObject != null) shadowGameObject.transform.localScale = Vector3.one; // Keep shadow scale relative
                Invoke(nameof(StartPopupAnimation), animationDelay);
            }
            else
            {
                StartPopupAnimation();
            }
        }
        else
        {
            // Ensure original scale is set if not animating
            transform.localScale = originalScale;
            if (shadowGameObject != null) shadowGameObject.transform.localScale = Vector3.one;
        }
    }

    void OnEnable()
    {
        // Option: Trigger popup also on re-enable? Uncomment if needed.
        // if (usePopupAnimation && Application.isPlaying && gameObject.activeInHierarchy) // Check activeInHierarchy to avoid triggering on initial Awake/Start sequence
        // {
             // Reset scale and start animation again
        //     if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        //     StartPopupAnimation();
        // }
    }

    void OnDisable()
    {
        // Ensure coroutine stops if object is disabled mid-animation
        if (popupCoroutine != null)
        {
            StopCoroutine(popupCoroutine);
            popupCoroutine = null;
            // Optionally reset scale immediately when disabled
            // transform.localScale = originalScale;
            // if (shadowGameObject != null) shadowGameObject.transform.localScale = Vector3.one;
        }
    }

    void StartPopupAnimation()
    {
        if (popupCoroutine != null) StopCoroutine(popupCoroutine);
        popupCoroutine = StartCoroutine(AnimatePopup());
    }

    IEnumerator AnimatePopup()
    {
        transform.localScale = startScale;
        if (shadowGameObject != null) shadowGameObject.transform.localScale = Vector3.one; // Shadow scale is relative to parent

        float elapsedTime = 0f;
        Vector3 targetScale = originalScale;

        while (elapsedTime < popupDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / popupDuration);
            // Optional: Add easing (e.g., SmoothStep)
            // t = Mathf.SmoothStep(0f, 1f, t);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            if (shadowGameObject != null) shadowGameObject.transform.localScale = Vector3.one; // Keep shadow scale correct

            yield return null;
        }

        transform.localScale = targetScale; // Ensure final scale is exact
        if (shadowGameObject != null) shadowGameObject.transform.localScale = Vector3.one;
        popupCoroutine = null;
    }

    void OnValidate()
    {
        // Update shadow properties if they are changed in the Inspector during edit mode
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        // Ensure shadow components exist before updating
        if (shadowGameObject != null && shadowSpriteRenderer != null && spriteRenderer != null)
        {
            UpdateShadowProperties();
        }
        else if (transform.Find("Shadow") != null)
        {
            // Attempt to find components again if they became null
            shadowGameObject = transform.Find("Shadow").gameObject;
            shadowSpriteRenderer = shadowGameObject.GetComponent<SpriteRenderer>();
            if (shadowSpriteRenderer != null && spriteRenderer != null) {
                 UpdateShadowProperties();
            }
        }
         // Optional: If needed, could recreate shadow here in editor if missing,
         // but might be annoying if user intentionally deleted it.
    }


    void CreateShadowObject()
    {
        shadowGameObject = new GameObject("Shadow");
        shadowGameObject.transform.SetParent(transform); // Make it a child
        shadowGameObject.transform.localScale = Vector3.one; // Ensure scale is reset relative to parent

        shadowSpriteRenderer = shadowGameObject.AddComponent<SpriteRenderer>();
        shadowSpriteRenderer.sprite = spriteRenderer.sprite; // Use the same sprite
        shadowGameObject.transform.hideFlags = HideFlags.HideInHierarchy; // Optionally hide shadow from hierarchy view

        // Match other potentially relevant properties
        shadowSpriteRenderer.flipX = spriteRenderer.flipX;
        shadowSpriteRenderer.flipY = spriteRenderer.flipY;
        shadowSpriteRenderer.material = spriteRenderer.material; // Use the same material initially

        // Set initial properties
        UpdateShadowProperties();
    }

    void UpdateShadowProperties()
    {
        if (spriteRenderer == null)
        {
            // Try to get component again if null (e.g., after script reload)
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) {
                 Debug.LogWarning("PopupEffect: Missing SpriteRenderer component.", this);
                 return;
            }
            originalScale = transform.localScale; // Re-capture original scale if needed
        }

        // Ensure shadow components exist before accessing
         if (shadowGameObject == null || shadowSpriteRenderer == null)
         {
             // Attempt to find/recreate shadow if missing during validation
             Transform existingShadow = transform.Find("Shadow");
             if (existingShadow != null)
             {
                 shadowGameObject = existingShadow.gameObject;
                 shadowSpriteRenderer = shadowGameObject.GetComponent<SpriteRenderer>();
             }

             // If still null (maybe deleted or not created yet in editor), exit validation
             if (shadowGameObject == null || shadowSpriteRenderer == null)
             {
                 // Don't log warning constantly in OnValidate, just skip update
                 return;
             }
         }

        // Original UpdateShadowProperties logic starts here
        // Update shadow position relative to parent
        shadowGameObject.transform.localPosition = shadowOffset;

        // Update visual properties
        shadowSpriteRenderer.sprite = spriteRenderer.sprite; // Ensure sprite is up-to-date
        shadowSpriteRenderer.color = shadowColor;

        // Update sorting order
        shadowSpriteRenderer.sortingLayerName = shadowSortingLayerName;
        // If using default layer, calculate order based on parent
        if (shadowSortingLayerName == spriteRenderer.sortingLayerName || shadowSortingLayerName == "Default")
        {
            shadowSpriteRenderer.sortingOrder = spriteRenderer.sortingOrder + shadowSortingOrderOffset;
        } else {
             // If using a different layer, you might want a specific order (e.g., 0)
             // For now, still base it off offset, assuming layer change is intentional
             shadowSpriteRenderer.sortingOrder = shadowSortingOrderOffset;
        }

        // Match other potentially relevant properties if they change
        shadowSpriteRenderer.flipX = spriteRenderer.flipX;
        shadowSpriteRenderer.flipY = spriteRenderer.flipY;
        // Note: You might want a different material for the shadow if using custom shaders
        // shadowSpriteRenderer.material = spriteRenderer.material;
    }

    // Optional: Add methods for the pop-up animation later
    // public void AnimatePopup() { ... }
} 