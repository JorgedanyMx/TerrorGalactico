using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the credits text from the sections below and scrolls it up the viewport.
/// Esc or the Back button returns to the menu; hold Space or the left mouse button to scroll faster.
/// Edit the names in the Inspector (Sections), no need to touch the scene layout.
/// </summary>
public class CreditsScroller : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        public string title;
        public string[] names;
    }

    [Header("Content")]
    [SerializeField] private Section[] sections;
    [SerializeField] private TMP_FontAsset titleFont;
    [Tooltip("Needs accents (á, é, ñ...).")]
    [SerializeField] private TMP_FontAsset nameFont;
    [SerializeField] private float titleSize = 64f;
    [SerializeField] private float nameSize = 44f;
    [SerializeField] private Color titleColor = new Color(1f, 0.98f, 0.9f);
    [SerializeField] private Color nameColor = Color.white;
    [Tooltip("Empty space before each section after the first.")]
    [SerializeField] private float sectionSpacing = 90f;

    [Header("Scroll")]
    [SerializeField] private RectTransform viewport;
    [Tooltip("Anchored top-stretch with a Vertical Layout Group and a Content Size Fitter.")]
    [SerializeField] private RectTransform content;
    [Tooltip("Canvas units per second.")]
    [SerializeField] private float speed = 70f;
    [SerializeField] private float fastMultiplier = 4f;
    [SerializeField] private float startDelay = 1f;
    [Tooltip("Repeat the credits when they finish. While on, the scene only exits with Esc or the Back button.")]
    [SerializeField] private bool loop = true;

    [Header("Exit")]
    [SerializeField] private string menuScene = "MainMenu";
    [Tooltip("Only used when Loop is off.")]
    [SerializeField] private bool returnWhenFinished = true;
    [SerializeField] private float endDelay = 1.5f;

    private bool leaving;

    private IEnumerator Start()
    {
        BuildContent();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // Anchored Y is the distance from the viewport top to the content top:
        // -viewport height = content just below the viewport, content height = content just above it.
        float startY = -viewport.rect.height;
        float endY = content.rect.height;
        content.anchoredPosition = new Vector2(0f, startY);

        yield return new WaitForSecondsRealtime(startDelay);

        while (true)
        {
            while (content.anchoredPosition.y < endY)
            {
                float multiplier = IsSpeedingUp() ? fastMultiplier : 1f;
                content.anchoredPosition += Vector2.up * (speed * multiplier * Time.unscaledDeltaTime);
                yield return null;
            }

            if (!loop) break;

            // Start over from below the viewport.
            yield return new WaitForSecondsRealtime(endDelay);
            content.anchoredPosition = new Vector2(0f, startY);
        }

        if (returnWhenFinished)
        {
            yield return new WaitForSecondsRealtime(endDelay);
            Back();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
    }

    /// <summary>Loads the menu scene (hooked to the Back button).</summary>
    public void Back()
    {
        if (leaving) return;

        leaving = true;
        SceneManager.LoadScene(menuScene);
    }

    private static bool IsSpeedingUp()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            || (Mouse.current != null && Mouse.current.leftButton.isPressed);
    }

    private void BuildContent()
    {
        for (int i = content.childCount - 1; i >= 0; i--) Destroy(content.GetChild(i).gameObject);
        if (sections == null) return;

        for (int s = 0; s < sections.Length; s++)
        {
            Section section = sections[s];
            if (s > 0) CreateSpacer(sectionSpacing);

            if (!string.IsNullOrEmpty(section.title))
                CreateLine(section.title, titleFont, titleSize, titleColor);

            if (section.names == null) continue;
            foreach (string name in section.names) CreateLine(name, nameFont, nameSize, nameColor);
        }
    }

    private void CreateLine(string text, TMP_FontAsset font, float size, Color color)
    {
        var go = new GameObject(text, typeof(RectTransform));
        go.transform.SetParent(content, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
    }

    private void CreateSpacer(float height)
    {
        var go = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
        go.transform.SetParent(content, false);
        go.GetComponent<LayoutElement>().preferredHeight = height;
    }
}
