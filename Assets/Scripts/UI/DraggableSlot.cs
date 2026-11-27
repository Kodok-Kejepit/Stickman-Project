using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Stickman.UI
{
    /// <summary>Drag handle component used inside <see cref="LayoutEditorOverlay"/>.</summary>
    public sealed class DraggableSlot : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public int Index;
        public RectTransform Target;
        public Action<int> OnSelected;
        public Action<int, Vector2> OnMoved;

        Vector2 _startAnchored;
        Vector2 _startLocal;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnSelected?.Invoke(Index);
            if (Target == null) return;
            _startAnchored = Target.anchoredPosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target.parent as RectTransform, eventData.position, eventData.pressEventCamera, out _startLocal);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target.parent as RectTransform, eventData.position, eventData.pressEventCamera, out Vector2 curLocal))
            {
                Vector2 next = _startAnchored + (curLocal - _startLocal);
                Target.anchoredPosition = next;
                OnMoved?.Invoke(Index, next);
            }
        }
    }
}
