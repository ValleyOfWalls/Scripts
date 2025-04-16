using UnityEngine;
using UnityEngine.EventSystems;

public enum DropZoneTargetType
{
    OwnPet,       // Target is the player's own pet
    OpponentPet   // Target is the opponent's pet
    // Add other types if needed (e.g., PlayerSelf, SpecificEnemy)
}

public class CardDropZone : MonoBehaviour, IDropHandler
{
    public DropZoneTargetType targetType; // Set this in the Inspector for each drop zone

    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log($"OnDrop detected on {gameObject.name} (Type: {targetType})");
        // The logic is mostly handled in CardDragHandler's OnEndDrag
        // This script primarily acts as a marker and provides the targetType.

        // We could add visual feedback here (e.g., highlight when card hovers over)
        // using IPointerEnterHandler and IPointerExitHandler
    }
} 