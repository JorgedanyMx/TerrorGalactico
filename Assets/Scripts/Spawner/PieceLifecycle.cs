using System.Collections;
using UnityEngine;

/// <summary>
/// Added at runtime by CameraAreaSpawner to every dropped piece.
/// Scales it from 0 to its prefab scale, keeps it alive for a while, then scales it back to 0 and destroys it.
/// Dismiss() plays that same scale-out right away (used when the player collects the piece).
/// </summary>
public class PieceLifecycle : MonoBehaviour
{
    private Vector3 targetScale;
    private float scaleOutTime = 0.3f;
    private bool dismissed;

    private void Awake() => targetScale = transform.localScale;

    public void Begin(float lifetime, float scaleInTime, float scaleOutTime)
    {
        this.scaleOutTime = scaleOutTime;
        transform.localScale = Vector3.zero;
        StartCoroutine(Run(lifetime, scaleInTime));
    }

    /// <summary>Skips the remaining lifetime and shrinks to 0 from the current scale.</summary>
    public void Dismiss()
    {
        if (dismissed) return;

        dismissed = true;
        StopAllCoroutines();
        StartCoroutine(ScaleOutAndDestroy());
    }

    private IEnumerator Run(float lifetime, float scaleInTime)
    {
        yield return ScaleTo(Vector3.zero, targetScale, scaleInTime, EaseOutBack);
        yield return new WaitForSeconds(lifetime);
        dismissed = true;
        yield return ScaleOutAndDestroy();
    }

    private IEnumerator ScaleOutAndDestroy()
    {
        yield return ScaleTo(transform.localScale, Vector3.zero, scaleOutTime, EaseInQuad);
        Destroy(gameObject);
    }

    private IEnumerator ScaleTo(Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = Vector3.LerpUnclamped(from, to, ease(t / duration));
            yield return null;
        }
        transform.localScale = to;
    }

    // Slight overshoot, gives the "pop" feel.
    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private static float EaseInQuad(float x) => x * x;
}
