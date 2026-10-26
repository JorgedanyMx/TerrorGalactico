using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Entrance animation for the menu: each element fades in while sliding to its place, one after another.
/// Put it on the panel that holds the buttons. It plays every time the panel is enabled
/// (when the scene starts and when coming back from the credits panel).
/// Also adds a hover/press scale effect to the buttons (ButtonHoverScale).
/// Only works for elements that are placed by hand, not inside a Layout Group.
/// </summary>
public class MenuButtonsIntro : MonoBehaviour
{
    [Tooltip("Elements to animate, in order. Empty = every Button under this object.")]
    [SerializeField] private RectTransform[] targets;
    [Tooltip("Where each element starts, relative to its final position.")]
    [SerializeField] private Vector2 slideOffset = new Vector2(-160f, 0f);
    [SerializeField] private float duration = 0.5f;
    [Tooltip("Delay between one element and the next.")]
    [SerializeField] private float stagger = 0.12f;
    [SerializeField] private float startDelay = 0.2f;
    [SerializeField] private bool addHoverEffect = true;

    private RectTransform[] items;
    private Vector2[] basePositions;
    private CanvasGroup[] groups;

    private void Awake()
    {
        items = targets != null && targets.Length > 0
            ? targets
            : GetComponentsInChildren<Button>(true).Select(b => (RectTransform)b.transform).ToArray();

        basePositions = items.Select(i => i.anchoredPosition).ToArray();
        groups = items.Select(i => i.TryGetComponent(out CanvasGroup g) ? g : i.gameObject.AddComponent<CanvasGroup>()).ToArray();

        if (!addHoverEffect) return;
        foreach (Button button in GetComponentsInChildren<Button>(true))
            if (!button.TryGetComponent(out ButtonHoverScale _)) button.gameObject.AddComponent<ButtonHoverScale>();
    }

    private void OnEnable()
    {
        Apply(0f, 0f);
        StartCoroutine(Play());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator Play()
    {
        yield return new WaitForSecondsRealtime(startDelay);

        float total = Mathf.Max(0, items.Length - 1) * stagger + duration;
        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            Apply(t, 1f);
            yield return null;
        }
        Apply(total, 1f);
    }

    /// <summary>Puts every element at the point of the animation for time t (progress 0 for all when active = 0).</summary>
    private void Apply(float t, float active)
    {
        for (int i = 0; i < items.Length; i++)
        {
            float progress = active > 0f ? Mathf.Clamp01((t - i * stagger) / duration) : 0f;
            float eased = 1f - Mathf.Pow(1f - progress, 3f); // ease out: fast start, soft landing

            items[i].anchoredPosition = basePositions[i] + slideOffset * (1f - eased);
            groups[i].alpha = eased;
        }
    }
}
