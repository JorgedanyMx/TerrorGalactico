using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Smoothly scales a UI element up on hover (or keyboard/gamepad selection) and down while pressed.
/// Added automatically by MenuButtonsIntro, or put it on any button by hand.
/// </summary>
public class ButtonHoverScale : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float pressedScale = 0.95f;
    [Tooltip("Higher = snappier.")]
    [SerializeField] private float speed = 14f;
    [Tooltip("Also grow when selected with keyboard/gamepad. Off by default: the EventSystem starts with the first button selected, which would leave it big until you click elsewhere.")]
    [SerializeField] private bool scaleOnSelect = false;

    private Vector3 baseScale;
    private float target = 1f;
    private bool hovered;
    private bool pressed;
    private bool selected;

    private void Awake() => baseScale = transform.localScale;

    private void OnDisable()
    {
        hovered = pressed = selected = false;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        target = pressed ? pressedScale : (hovered || selected ? hoverScale : 1f);

        Vector3 goal = baseScale * target;
        if ((transform.localScale - goal).sqrMagnitude < 0.000001f) return;

        // Exponential smoothing: frame-rate independent and works while the game is paused.
        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, goal, k);
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;
    public void OnSelect(BaseEventData eventData) => selected = scaleOnSelect;
    public void OnDeselect(BaseEventData eventData) => selected = false;
}
