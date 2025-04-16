using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CardData cardData { get; set; } // Set by GameManager when instantiated

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector3 startPosition;
    private Transform originalParent;
    private Canvas rootCanvas; // To ensure correct drag positioning
    private GameManager gameManager; // To call PlayCard

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
             Debug.LogWarning("CardDragHandler requires a CanvasGroup component on the card prefab root. Adding one.");
             canvasGroup = gameObject.AddComponent<CanvasGroup>(); // Need this to ignore raycasts while dragging
        }
        rootCanvas = GetComponentInParent<Canvas>(); // Find the root canvas for positioning
        gameManager = FindObjectOfType<GameManager>(); // Find the GameManager
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (gameManager == null || !gameManager.IsPlayerTurn()) // Basic check if it's okay to drag (add energy check later)
        {
            eventData.pointerDrag = null; // Cancel drag if not allowed
            return;
        }
        
        startPosition = rectTransform.position;
        originalParent = transform.parent;

        // Reparent to canvas root for consistent drag visuals above other UI
        transform.SetParent(rootCanvas.transform, true); 
        transform.SetAsLastSibling(); // Ensure it renders on top

        canvasGroup.blocksRaycasts = false; // Allows raycasts to hit drop zones underneath
        // Optional: Add visual feedback like slight scaling or highlight
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (rectTransform == null) return;
        // Convert screen position to local position within the canvas
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvas.transform as RectTransform, eventData.position, rootCanvas.worldCamera, out Vector2 localPoint);
        rectTransform.localPosition = localPoint;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // <<--- ADDED LOGGING START --->>
        Debug.Log($"CardDragHandler.OnEndDrag fired. Pointer entered: {(eventData.pointerEnter != null ? eventData.pointerEnter.name : "null")}");
        // <<--- ADDED LOGGING END --->>

        canvasGroup.blocksRaycasts = true; // Re-enable raycasting for the card itself
        transform.SetParent(originalParent, true); // Return to original parent (hand) initially

        // Check if we dropped onto a valid CardDropZone
        if (eventData.pointerEnter != null && eventData.pointerEnter.TryGetComponent<CardDropZone>(out CardDropZone dropZone))
        {
            // Attempt to play the card via GameManager
            if (gameManager != null)
            {
                bool playedSuccessfully = gameManager.AttemptPlayCard(cardData, dropZone.targetType); 
                if (playedSuccessfully)
                {
                    // Card was played, GameManager handles destroying/moving it
                    // No need to return to hand visually
                    return; 
                }
            }
        }

        // If not dropped on a valid zone or play failed, return to original position smoothly (or instantly)
        rectTransform.position = startPosition; 
        // TODO: Could add a smooth tween back to position
    }
} 