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
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // When mouse leaves the panel area, stop hovering the current card
        CardDragHandler cardToExit = currentlyHoveredCard; // Store reference
        currentlyHoveredCard = null; // Clear reference *before* calling ExitHoverState

        if (cardToExit != null) 
        {
            cardToExit.ExitHoverState(); // Call exit on the stored reference
            // Debug.Log($"[HandPanelHoverManager] Exited hover on {currentlyHoveredCard.name} (Panel Exit)");
        }
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

    // --- ADDED: Getter for CombatManager --- 
    public CardDragHandler GetCurrentlyHoveredCard()
    {
        return currentlyHoveredCard;
    }
    // --- END ADDED ---
} 