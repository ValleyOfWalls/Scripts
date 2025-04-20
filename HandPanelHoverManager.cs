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

    // Called by CombatUIManager whenever the hand UI is updated
    public void UpdateCardReferences(List<GameObject> cardObjects)
    {
        cardsInHand.Clear();
        foreach (GameObject cardGO in cardObjects)
        {
            if (cardGO != null && cardGO.activeSelf)
            {
                CardDragHandler handler = cardGO.GetComponent<CardDragHandler>();
                if (handler != null)
                {
                    cardsInHand.Add(handler);
                }
                else
                {
                     Debug.LogWarning($"[HandPanelHoverManager] Card GameObject {cardGO.name} is missing CardDragHandler.");
                }
            }
        }
        // Reset hovered card reference if the previously hovered card is no longer in the list
        if (currentlyHoveredCard != null && !cardsInHand.Contains(currentlyHoveredCard))
        {
            currentlyHoveredCard = null;
        }
        // Debug.Log($"[HandPanelHoverManager] Updated card references from list. Count: {cardsInHand.Count}");
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
        // Use the cached cardsInHand list which is updated by UpdateCardReferences
        if (cardsInHand.Count <= 1) return; // No sorting needed for 0 or 1 card

        // Create a temporary list to sort, preserving the original order of cardsInHand if needed elsewhere
        List<CardDragHandler> sortedCards = new List<CardDragHandler>(cardsInHand);

        // 2. Sort cards based on their original X position (left to right)
        sortedCards.Sort((a, b) => {
            // Null checks for safety
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            return a.originalPosition.x.CompareTo(b.originalPosition.x);
        });

        // 3. Set sibling indices based on sorted order
        for (int i = 0; i < sortedCards.Count; i++)
        {
            if (sortedCards[i] != null && sortedCards[i].transform != null) // Ensure handler and transform are valid
            { 
                // Set the sibling index. Lower index = rendered first (behind)
                // Higher index = rendered later (in front)
                sortedCards[i].transform.SetSiblingIndex(i);
            }
        }

        // 4. Ensure the currently hovered card is always rendered last (on top)
        if (currentlyHoveredCard != null && currentlyHoveredCard.gameObject.activeSelf)
        {
            // Check if it's still in the list (it should be if UpdateCardReferences ran correctly)
            if (cardsInHand.Contains(currentlyHoveredCard))
            {
                currentlyHoveredCard.transform.SetAsLastSibling();
            }
        }
    }
    // --- END ADDED ---

    // --- ADDED: Getter for CombatManager --- 
    public CardDragHandler GetCurrentlyHoveredCard()
    {
        return currentlyHoveredCard;
    }
    // --- END ADDED ---
} 