using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections; // Added for Coroutines

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardData cardData { get; set; } // Set by GameManager when instantiated

    // --- ADDED: Hover effect fields ---
    public Vector3 originalPosition { get; set; }
    public Quaternion originalRotation { get; set; }
    public Vector3 originalScale { get; set; }
    private static readonly float hoverOffsetY = 30f;
    private static readonly float neighborOffsetX = 40f;
    private static readonly float hoverScaleFactor = 1.1f;
    private int originalSiblingIndex;
    private bool isHovering = false;
    // --- END ADDED ---

    // --- ADDED: Animation fields ---
    private static readonly float hoverAnimDuration = 0.15f; // Duration for hover animation
    private Coroutine currentPosScaleRotCoroutine = null;
    private Coroutine currentNeighborLeftCoroutine = null;
    private Coroutine currentNeighborRightCoroutine = null;
    // --- END ADDED ---

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 startPosition; // Screen position at drag start
    private Transform originalParent;
    private Canvas rootCanvas;
    private GameManager gameManager;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            Debug.LogWarning("CardDragHandler requires a CanvasGroup component on the card prefab root. Adding one.");
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        rootCanvas = GetComponentInParent<Canvas>();
        gameManager = FindFirstObjectByType<GameManager>();
        if (gameManager == null)
        {
            Debug.LogError("CardDragHandler could not find GameManager in the scene!");
        }

        // Ensure originalScale is initialized if Awake runs before first CombatManager layout
        originalScale = rectTransform.localScale;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (gameManager == null || !gameManager.IsPlayerTurn())
        {
            eventData.pointerDrag = null;
            return;
        }

        // --- ADDED: Stop animations and reset hover state if dragging starts mid-hover ---
        if (isHovering)
        {
            StopAllCardAnimations(); // Stop own animation & neighbor animations *started by this card*
            
            // Reset own visuals instantly before drag starts
            rectTransform.localPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
            rectTransform.localScale = originalScale;
            isHovering = false;
        }
         originalSiblingIndex = transform.GetSiblingIndex(); // Store index before reparenting
        // --- END ADDED ---

        startPosition = rectTransform.position;
        originalParent = transform.parent;
        originalScale = rectTransform.localScale; // Store scale just before drag

        transform.SetParent(rootCanvas.transform, true);
        transform.SetAsLastSibling();
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvas.transform as RectTransform, eventData.position, rootCanvas.worldCamera, out Vector2 localPoint);
        rectTransform.localPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"CardDragHandler.OnEndDrag fired. Pointer entered: {(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null")}");

        canvasGroup.blocksRaycasts = true;
        transform.SetParent(originalParent, true); // Return to hand panel first

        bool playedSuccessfully = false;
        if (eventData.pointerEnter != null && eventData.pointerEnter.TryGetComponent<CardDropZone>(out CardDropZone dropZone))
        {
            if (gameManager != null)
            {
                playedSuccessfully = gameManager.AttemptPlayCard(cardData, dropZone.targetType);
            }
        }

        if (!playedSuccessfully)
        {
            // --- MODIFIED: Snap back instantly, ensure correct sibling index ---
            StopAllCardAnimations(); // Ensure no lingering animations
            rectTransform.localPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
            rectTransform.localScale = originalScale;
            // Use the sibling index stored *before* the drag started
            if(originalParent != null && originalSiblingIndex >= 0 && originalSiblingIndex < originalParent.childCount)
            {
                 transform.SetSiblingIndex(originalSiblingIndex);
            }
            else
            {
                // Fallback if something went wrong with index
                 transform.SetAsLastSibling(); 
            }
            // --- END MODIFIED ---
        }
        // If played successfully, GameManager's UpdateHandUI will handle removal/re-layout
    }

    // --- ADDED: Public Hover Methods (called by HandPanelHoverManager) ---
    public void EnterHoverState()
    {
        if (canvasGroup.blocksRaycasts == false || isHovering) return;
        isHovering = true;
        originalSiblingIndex = transform.GetSiblingIndex();

        // --- MODIFIED: Use Animation --- 
        StopAllCardAnimations(); // Stop existing animations first
        
        // Calculate target state
        Vector3 targetPosition = originalPosition + Vector3.up * hoverOffsetY;
        Quaternion targetRotation = originalRotation; // Or Quaternion.identity if you want it straight
        Vector3 targetScale = originalScale * hoverScaleFactor;

        // Start animation for this card
        currentPosScaleRotCoroutine = StartCoroutine(AnimateTransformCoroutine(targetPosition, targetRotation, targetScale, true));
        
        // Start neighbor animations
        AffectNeighbors(neighborOffsetX, true);
        // --- END MODIFIED ---
    }

    public void ExitHoverState()
    {
        if (!isHovering || canvasGroup.blocksRaycasts == false) return;
        isHovering = false;

        // --- MODIFIED: Use Animation --- 
        StopAllCardAnimations(); // Stop existing animations first

        // Target state is the original state
        currentPosScaleRotCoroutine = StartCoroutine(AnimateTransformCoroutine(originalPosition, originalRotation, originalScale, false));

        // Start neighbor animations back to original
        AffectNeighbors(0f, false);
        // --- END MODIFIED ---
    }
    
    // --- ADDED: Coroutine for smooth transform animation ---
    private IEnumerator AnimateTransformCoroutine(Vector3 targetPosition, Quaternion targetRotation, Vector3 targetScale, bool bringToFront)
    {
        float elapsedTime = 0f;
        Vector3 startPosition = rectTransform.localPosition;
        Quaternion startRotation = rectTransform.localRotation;
        Vector3 startScale = rectTransform.localScale;

        // Bring to front immediately if entering hover
        if (bringToFront)
        {
            transform.SetAsLastSibling();
        }

        while (elapsedTime < hoverAnimDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / hoverAnimDuration);
            // Smoothstep for nicer easing (optional)
            t = t * t * (3f - 2f * t);

            rectTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
            rectTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        // Ensure final state is set precisely
        rectTransform.localPosition = targetPosition;
        rectTransform.localRotation = targetRotation;
        rectTransform.localScale = targetScale;

        // Restore original sibling index if exiting hover
        if (!bringToFront && originalParent != null && originalSiblingIndex >= 0 && originalSiblingIndex < originalParent.childCount)
        {
            // Check parent again, might have changed during animation?
            if (transform.parent == originalParent) {
                 transform.SetSiblingIndex(originalSiblingIndex);
            }
        }
        currentPosScaleRotCoroutine = null; // Mark as finished
    }
    // --- END ADDED ---
    
    // --- MODIFIED: AffectNeighbors to use animation and fix parent issue ---
    private void AffectNeighbors(float horizontalOffset, bool enteringHover)
    {
        CardDragHandler leftNeighbor, rightNeighbor;
        FindNeighbors(out leftNeighbor, out rightNeighbor);

        // Animate Left Neighbor
        if (leftNeighbor != null && !leftNeighbor.isHovering)
        {
            leftNeighbor.StopCoroutineIfRunning(ref leftNeighbor.currentPosScaleRotCoroutine); // Stop any running animation on the neighbor
            Vector3 targetPos = leftNeighbor.originalPosition + Vector3.left * horizontalOffset;
            // Start animation *on the neighbor*, store coroutine locally to stop it if *this* card exits hover
            currentNeighborLeftCoroutine = leftNeighbor.StartCoroutine(leftNeighbor.AnimateTransformCoroutine(targetPos, leftNeighbor.originalRotation, leftNeighbor.originalScale, false)); 
        }

        // Animate Right Neighbor
        if (rightNeighbor != null && !rightNeighbor.isHovering)
        {
            rightNeighbor.StopCoroutineIfRunning(ref rightNeighbor.currentPosScaleRotCoroutine);
            Vector3 targetPos = rightNeighbor.originalPosition + Vector3.right * horizontalOffset;
            currentNeighborRightCoroutine = rightNeighbor.StartCoroutine(rightNeighbor.AnimateTransformCoroutine(targetPos, rightNeighbor.originalRotation, rightNeighbor.originalScale, false));
        }
    }
    
    // --- ADDED: Helper to find neighbors --- 
    private void FindNeighbors(out CardDragHandler leftNeighbor, out CardDragHandler rightNeighbor)
    {   
        leftNeighbor = null;
        rightNeighbor = null;
        // --- MODIFIED: Use current transform.parent --- 
        Transform parent = transform.parent; 
        // --- END MODIFIED ---
        if (parent == null) return;

        int myIndex = transform.GetSiblingIndex(); // Use current index in the loop
        int count = parent.childCount;

        // Find immediate left neighbor (that's not the template)
        for (int i = myIndex - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child.gameObject.name != "CardTemplate" && child.gameObject.activeSelf)
            {
                leftNeighbor = child.GetComponent<CardDragHandler>();
                break;
            }
        }

        // Find immediate right neighbor (that's not the template)
        for (int i = myIndex + 1; i < count; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.gameObject.name != "CardTemplate" && child.gameObject.activeSelf)
            {
                rightNeighbor = child.GetComponent<CardDragHandler>();
                break;
            }
        }
    }
    // --- END ADDED ---
    
    // --- ADDED: Helper to stop coroutine ---
    private void StopCoroutineIfRunning(ref Coroutine coroutineRef)
    {
        if (coroutineRef != null)
        {
            StopCoroutine(coroutineRef);
            coroutineRef = null;
        }
    }
    
    // --- ADDED: Method to stop all animations on this card ---
    private void StopAllCardAnimations()
    {
        StopCoroutineIfRunning(ref currentPosScaleRotCoroutine);
        // Stop neighbor animations *started by this card*
        StopCoroutineIfRunning(ref currentNeighborLeftCoroutine);
        StopCoroutineIfRunning(ref currentNeighborRightCoroutine);
    }
    
    // --- ADDED: Method to reset card state instantly (e.g., if neighbor hover stops abruptly) ---
    public void ResetToOriginalStateInstantly()
    {
        StopAllCardAnimations(); // Stop any animations running ON this card
        isHovering = false; // Ensure hover state is off

        // Only reset if rectTransform exists and original values are plausible
        if (rectTransform != null && originalScale != Vector3.zero) 
        {
            rectTransform.localPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
            rectTransform.localScale = originalScale;

            // Attempt to restore sibling index if parent is valid
            Transform currentParent = transform.parent; // Use current parent
            if (currentParent != null && originalSiblingIndex >= 0 && originalSiblingIndex < currentParent.childCount)
            {
                transform.SetSiblingIndex(originalSiblingIndex);
            }
        }
    }
    // --- END ADDED ---
} 