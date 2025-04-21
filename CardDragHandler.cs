using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections; // Added for Coroutines
using System.Collections.Generic;
using System.Linq;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardData cardData { get; set; } // Set by GameManager when instantiated
    public int cardHandIndex { get; set; } = -1; // Track position in hand list to differentiate identical cards
    public bool isDiscarding { get; set; } = false; // ADDED: Flag for discard animation

    // --- ADDED: Hover effect fields ---
    public Vector3 originalPosition { get; set; }
    public Quaternion originalRotation { get; set; }
    public Vector3 originalScale { get; set; }
    private static readonly float hoverOffsetY = 30f;
    private static readonly float neighborOffsetX = 40f;
    private static readonly float hoverScaleFactor = 1.1f;
    private int originalSiblingIndex = -1; // Initialize to -1
    private bool isHovering = false;
    // --- END ADDED ---

    // --- ADDED: Animation fields ---
    private static readonly float hoverAnimDuration = 0.15f; // Duration for hover animation
    private Coroutine animationCoroutine = null; // Single coroutine for self-animation
    // --- END ADDED ---

    // --- ADDED: Target State Fields ---
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private Vector3 targetScale;
    private Vector3 currentNeighborOffset = Vector3.zero; // Offset applied by neighbors
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

        // --- ADDED: Initialize target state to current state ---
        targetPosition = rectTransform.localPosition;
        targetRotation = rectTransform.localRotation;
        targetScale = rectTransform.localScale;
        currentNeighborOffset = Vector3.zero;
        // --- END ADDED ---
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
            // Reset state instantly, neighbours will be reset by HoverManager potentially
            ResetToOriginalStateInstantly(false); // false = don't tell neighbors 
            isHovering = false;
        }
        
        // Store index *after* potential reset and *before* reparenting
        originalSiblingIndex = transform.GetSiblingIndex(); 

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
        //Debug.Log($"CardDragHandler.OnEndDrag fired. Pointer entered: {(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null")}");

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
            StopCoroutineIfRunning(ref animationCoroutine); // Stop self-animation
            rectTransform.localPosition = originalPosition;
            rectTransform.localRotation = originalRotation;
            rectTransform.localScale = originalScale;

            // Reset target state as well
            targetPosition = originalPosition;
            targetRotation = originalRotation;
            targetScale = originalScale;
            currentNeighborOffset = Vector3.zero;
            
            // --- REMOVED Sibling Index Restoration from Drag End --- 
            /*
            if(originalParent != null && originalSiblingIndex >= 0 && originalSiblingIndex < originalParent.childCount)
            {
                transform.SetSiblingIndex(originalSiblingIndex);
            }
            else
            {
                // If index is invalid, maybe just set as last for safety?
                if(originalParent != null) transform.SetAsLastSibling();
            }
            */
            // --- END REMOVED ---
        }
        // If played successfully, GameManager's UpdateHandUI will handle removal/re-layout
    }

    // --- ADDED: Public Hover Methods (called by HandPanelHoverManager) ---
    public void EnterHoverState()
    {
        if (canvasGroup.blocksRaycasts == false || isHovering) return;
        //Debug.Log($"[{Time.frameCount}] EnterHoverState START: {name}");
        isHovering = true;

        // Store index only if not already stored (might happen if quickly re-hovering)
        if(originalSiblingIndex < 0) 
        {
            originalSiblingIndex = transform.GetSiblingIndex();
           // Debug.Log($"[{Time.frameCount}] EnterHoverState: Stored originalSiblingIndex = {originalSiblingIndex}");
        }

        // --- MODIFIED: Use Animation --- 
        currentNeighborOffset = Vector3.zero; // Reset any neighbor offset
        
        // Calculate target state
        targetPosition = originalPosition + Vector3.up * hoverOffsetY;
        targetRotation = originalRotation; // Keep original rotation
        targetScale = originalScale * hoverScaleFactor;

        // Start animation towards target & bring to front
        transform.SetAsLastSibling(); // Bring to front immediately
        UpdateAnimationTargetAndStart(); // Start animation
        
        // Start neighbor animations
        AffectNeighbors(neighborOffsetX, true);
    }

    public void ExitHoverState()
    {
        if (!isHovering || canvasGroup.blocksRaycasts == false) return;
        //Debug.Log($"[{Time.frameCount}] ExitHoverState START: {name}");
        isHovering = false;

        // --- MODIFIED: Use Animation --- 
        currentNeighborOffset = Vector3.zero; // Reset any neighbor offset

        // Target state is the original state
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;

        // Start animation back towards target & restore layer order
        UpdateAnimationTargetAndStart(); // Just animate, layer order restored at end of coroutine

        // Start neighbor animations back to original
        AffectNeighbors(0f, false);
        // --- END MODIFIED ---
        
        // originalSiblingIndex = -1; // <-- MOVE THIS LINE
    }
    
    // --- ADDED: Helper to manage animation start/stop ---
    private void UpdateAnimationTargetAndStart()
    {
        //Debug.Log($"[{Time.frameCount}] UpdateAnimationTargetAndStart: Stopping existing coroutine for {name}");
        // Stop existing animation
        StopCoroutineIfRunning(ref animationCoroutine);
        // Start new one
        //Debug.Log($"[{Time.frameCount}] UpdateAnimationTargetAndStart: Starting AnimateTransformCoroutine for {name}. TargetPos={targetPosition}, TargetScale={targetScale}");
        animationCoroutine = StartCoroutine(AnimateTransformCoroutine());
    }
    
    // --- MODIFIED: Coroutine animates towards target fields ---
    private IEnumerator AnimateTransformCoroutine()
    {
        float elapsedTime = 0f;
        Vector3 startPosition = rectTransform.localPosition;
        Quaternion startRotation = rectTransform.localRotation;
        Vector3 startScale = rectTransform.localScale;

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

        // --- MODIFIED: Layering Handling at End of Animation ---
        // Layering is now handled by HandPanelHoverManager.ResortSiblingIndices()
        // Coroutine just ensures the final transform state is set.
        
        animationCoroutine = null; // Mark as finished
        // --- END MODIFIED ---
    }
    
    // --- MODIFIED: AffectNeighbors calls ApplyNeighborOffset ---
    private void AffectNeighbors(float horizontalOffset, bool enteringHover)
    {
        CardDragHandler leftNeighbor, rightNeighbor;
        FindNeighbors(out leftNeighbor, out rightNeighbor);

        // Animate Left Neighbor
        if (leftNeighbor != null)
        {
            // Apply offset (positive offset means move left)
            leftNeighbor.ApplyNeighborOffset(Vector3.left * horizontalOffset);
        }

        // Animate Right Neighbor
        if (rightNeighbor != null)
        {
             // Apply offset (positive offset means move right)
             rightNeighbor.ApplyNeighborOffset(Vector3.right * horizontalOffset);
        }
    }
    
    // --- ADDED: Method for neighbors to apply offset ---
    public void ApplyNeighborOffset(Vector3 offset)
    {
        // Don't apply if currently hovering/being dragged
        if (isHovering || canvasGroup.blocksRaycasts == false) return;

        currentNeighborOffset = offset;
        // Recalculate target position based on original + offset
        targetPosition = originalPosition + currentNeighborOffset;
        // Rotation and Scale remain original when offset by neighbor
        targetRotation = originalRotation;
        targetScale = originalScale;

        // Start animation towards the new target position
        UpdateAnimationTargetAndStart(); // Don't bring neighbor to front
    }
    
    // --- MODIFIED: FindNeighbors based on original X position --- 
    private void FindNeighbors(out CardDragHandler leftNeighbor, out CardDragHandler rightNeighbor)
    {   
        leftNeighbor = null;
        rightNeighbor = null;
        Transform parent = transform.parent;

        if (parent == null) 
        {   
            Debug.LogWarning($"FindNeighbors ({name}): Parent is null!");
            return;
        }

        // 1. Get all active CardDragHandler siblings
        List<CardDragHandler> siblings = parent.GetComponentsInChildren<CardDragHandler>(false) // false = don't include inactive
                                              .Where(h => h != null && h.gameObject.activeSelf && h.gameObject.name != "CardTemplate")
                                              .ToList();

        if (siblings.Count <= 1) return; // No neighbors if only one card

        // 2. Sort siblings based on their calculated original X position
        siblings.Sort((a, b) => a.originalPosition.x.CompareTo(b.originalPosition.x));

        // 3. Find the index of this card in the sorted list
        int mySortedIndex = -1;
        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i] == this)
            {
                mySortedIndex = i;
                break;
            }
        }

        if (mySortedIndex == -1)
        {
            Debug.LogError($"FindNeighbors ({name}): Could not find self in sorted sibling list!");
            return;
        }

        // Debug.Log($"FindNeighbors ({name}): Sorted Index={mySortedIndex}, Count={siblings.Count}");

        // 4. Identify neighbors from the sorted list
        if (mySortedIndex > 0)
        {
            leftNeighbor = siblings[mySortedIndex - 1];
            // Debug.Log($"  - Found Left Neighbor: {leftNeighbor?.name} at sorted index {mySortedIndex - 1}");
        }

        if (mySortedIndex < siblings.Count - 1)
        {
            rightNeighbor = siblings[mySortedIndex + 1];
            // Debug.Log($"  - Found Right Neighbor: {rightNeighbor?.name} at sorted index {mySortedIndex + 1}");
        }
    }
    
    // --- ADDED: Helper to stop coroutine ---
    private void StopCoroutineIfRunning(ref Coroutine coroutineRef)
    {
        // Check if the coroutine is still running on this MonoBehaviour
        if (coroutineRef != null && this != null && this.enabled)
        {
            StopCoroutine(coroutineRef);
            coroutineRef = null;
        }
    }
    
    // --- MODIFIED: Method to reset card state instantly ---
    public void ResetToOriginalStateInstantly(bool affectNeighbors = true)
    {
        // Debug.Log($"ResetInstant: {name}");
        StopCoroutineIfRunning(ref animationCoroutine); // Stop self-animation
        isHovering = false; // Ensure hover state is off

        // Restore original transform values
        if (rectTransform != null && originalScale != Vector3.zero) {
             rectTransform.localPosition = originalPosition;
             rectTransform.localRotation = originalRotation;
             rectTransform.localScale = originalScale;
        }

        // Reset target state
        targetPosition = originalPosition;
        targetRotation = originalRotation;
        targetScale = originalScale;
        currentNeighborOffset = Vector3.zero;

        // Optionally tell neighbours to also reset (e.g., called from HoverManager exit)
        if (affectNeighbors) {
             // Find neighbors first before potentially resetting them
             CardDragHandler leftNeighbor, rightNeighbor;
             FindNeighbors(out leftNeighbor, out rightNeighbor);
             if(leftNeighbor != null) leftNeighbor.ApplyNeighborOffset(Vector3.zero);
             if(rightNeighbor != null) rightNeighbor.ApplyNeighborOffset(Vector3.zero);
        }
        originalSiblingIndex = -1; // Reset stored index
    }
} 