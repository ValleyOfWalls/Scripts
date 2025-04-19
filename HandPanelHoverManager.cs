using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

public class HandPanelHoverManager : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    private List<CardDragHandler> cardsInHand = new List<CardDragHandler>();
    private CardDragHandler currentlyHoveredCard = null;
    private RectTransform panelRectTransform;

    void Awake()
    {
        panelRectTransform = GetComponent<RectTransform>();
    }

    // Called by CombatManager whenever the hand UI is updated
    public void UpdateCardReferences()
    {
        cardsInHand.Clear();
        foreach (Transform child in transform)
        {
            // Ignore the template card
            if (child.gameObject.name != "CardTemplate" && child.gameObject.activeSelf)
            {
                CardDragHandler handler = child.GetComponent<CardDragHandler>();
                if (handler != null)
                {
                    cardsInHand.Add(handler);
                }
            }
        }
        // Debug.Log($"[HandPanelHoverManager] Updated card references. Count: {cardsInHand.Count}");
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (cardsInHand.Count == 0) return;

        CardDragHandler closestCard = FindClosestCard(eventData.position);

        if (closestCard != currentlyHoveredCard)
        {
            if (currentlyHoveredCard != null)
            {
                currentlyHoveredCard.ExitHoverState();
                // Debug.Log($"[HandPanelHoverManager] Exited hover on {currentlyHoveredCard.name}");
            }

            currentlyHoveredCard = closestCard;

            if (currentlyHoveredCard != null)
            {
                currentlyHoveredCard.EnterHoverState();
                // Debug.Log($"[HandPanelHoverManager] Entered hover on {currentlyHoveredCard.name}");
            }
        }
        
        // --- ADDED: Resort sibling indices after hover state change ---
        ResortSiblingIndices();
        // --- END ADDED ---
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // When mouse leaves the panel area, stop hovering the current card
        CardDragHandler cardToExit = currentlyHoveredCard; // Store reference
        currentlyHoveredCard = null; // Clear reference *before* calling ExitHoverState

        if (cardToExit != null) 
        {
            cardToExit.ExitHoverState(); // Call exit on the stored reference
            // Debug.Log($"[HandPanelHoverManager] Exited hover on {cardToExit.name} (Panel Exit)"); // Use cardToExit here
        }
        
        // --- ADDED: Resort sibling indices after hover state change ---
        ResortSiblingIndices();
        // --- END ADDED ---
    }

    private CardDragHandler FindClosestCard(Vector2 screenPosition)
    {
        CardDragHandler closest = null;
        float minDistanceSqr = float.MaxValue;

        foreach (CardDragHandler card in cardsInHand)
        {
            if (card == null || !card.gameObject.activeSelf) continue;

            // Use RectTransformUtility to convert screen point to card's local space if needed,
            // but comparing distances in screen space to the card's screen position is simpler here.
            Vector2 cardScreenPosition = RectTransformUtility.WorldToScreenPoint(null, card.transform.position); // Use null for camera if Canvas is ScreenSpaceOverlay
            
            // Alternative: Use card's pivot point projected to screen space
            // Vector2 cardPivotScreenPosition = RectTransformUtility.WorldToScreenPoint(null, card.GetComponent<RectTransform>().position); 

            float distanceSqr = (screenPosition - cardScreenPosition).sqrMagnitude;

            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                closest = card;
            }
        }

        // --- MODIFIED: Add a maximum distance threshold ---
        float maxDistanceThreshold = 150f; // Max distance in pixels to consider hovering
        float maxDistanceSqr = maxDistanceThreshold * maxDistanceThreshold;

        if (minDistanceSqr > maxDistanceSqr) 
        {
            return null; // Mouse is too far from the center of the closest card
        }
        // --- END MODIFIED ---

        return closest;
    }

    // --- ADDED: Function to resort sibling indices ---
    private void ResortSiblingIndices()
    {
        // 1. Get all active CardDragHandler siblings from the panel
        //    Use GetComponentsInChildren as UpdateCardReferences might not be perfectly synced
        List<CardDragHandler> activeCards = GetComponentsInChildren<CardDragHandler>(false) // false = don't include inactive
                                                .Where(h => h != null && h.gameObject.activeSelf && h.gameObject.name != "CardTemplate")
                                                .ToList();

        if (activeCards.Count <= 1) return; // No sorting needed for 0 or 1 card

        // 2. Sort cards based on their original X position (left to right)
        activeCards.Sort((a, b) => a.originalPosition.x.CompareTo(b.originalPosition.x));

        // 3. Set sibling indices based on sorted order
        for (int i = 0; i < activeCards.Count; i++)
        {
            // Set the sibling index. Lower index = rendered first (behind)
            // Higher index = rendered later (in front)
            activeCards[i].transform.SetSiblingIndex(i);
        }

        // 4. Ensure the currently hovered card is always rendered last (on top)
        if (currentlyHoveredCard != null && currentlyHoveredCard.gameObject.activeSelf)
        {
            // Check if it's still in the active list (it should be)
            if (activeCards.Contains(currentlyHoveredCard))
            {
                currentlyHoveredCard.transform.SetAsLastSibling();
                // Debug.Log($"[HandPanelHoverManager] ResortSiblingIndices: Set {currentlyHoveredCard.name} as last sibling.");
            }
            else
            {
                Debug.LogWarning($"[HandPanelHoverManager] ResortSiblingIndices: currentlyHoveredCard '{currentlyHoveredCard.name}' not found in active children during resort!");
            }
        }
        // else
        // {
            // Debug.Log($"[HandPanelHoverManager] ResortSiblingIndices: No card hovered.");
        // }
    }
    // --- END ADDED ---

    // --- ADDED: Getter for CombatManager --- 
    public CardDragHandler GetCurrentlyHoveredCard()
    {
        return currentlyHoveredCard;
    }
    // --- END ADDED ---
} 