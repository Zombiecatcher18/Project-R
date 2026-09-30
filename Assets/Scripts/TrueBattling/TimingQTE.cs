using System;
using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Timing-based QTE mechanic where player must press key while bar is in success zones.
/// Animates shrinking outline rect and evaluates hit zones (Perfect, Good, OK, Miss).
/// Supports fakeout mode where player must avoid pressing button (reverse logic).
/// Called by QTEManager during move execution for damage multiplier effects.
/// </summary>
public class TimingQTE : MonoBehaviour
{
    /// <summary>Root gameObject containing all UI elements; shown/hidden for QTE activation.</summary>
    public GameObject uiRoot;
    /// <summary>RectTransform for barely zone indicator (visual feedback for timing windows).</summary>
    public RectTransform barelyZone;
    /// <summary>RectTransform for white zone background (visual reference).</summary>
    public RectTransform whiteZoneRect;
    /// <summary>RectTransform for animated outline that shrinks during timing window.</summary>
    public RectTransform outlineRect;
    /// <summary>RectTransform for perfect zone indicator (highest damage bonus area).</summary>
    public RectTransform perfectZoneRect;
    /// <summary>Text display for showing result (Perfect/Good/OK/Miss/Fakeout).</summary>
    public TMP_Text resultText;

    /// <summary>Key to press for successful QTE (typically Space).</summary>
    public KeyCode hitKey = KeyCode.Space;

    /// <summary>Cached starting size for outline rect for reset each run.</summary>
    private Vector2 startSize;
    /// <summary>If true, reverses logic: player must NOT press button to succeed (fakeout mechanic).</summary>
    public bool isFakeout = false;

    /// <summary>Cached corner positions of rect zones for accurate hit detection.</summary>
    private readonly Vector3[] outlineCorners = new Vector3[4];
    /// <summary>Cached perfect zone corners for hit evaluation.</summary>
    private readonly Vector3[] perfectCorners = new Vector3[4];
    /// <summary>Cached good zone corners for hit evaluation.</summary>
    private readonly Vector3[] goodCorners = new Vector3[4];
    /// <summary>Cached ok zone corners for hit evaluation.</summary>
    private readonly Vector3[] okCorners = new Vector3[4];

    /// <summary>
    /// Caches rect sizes on scene load for efficient resets.
    /// Called by Unity on component initialization.
    /// </summary>
    private void Awake()
    {
        startSize = outlineRect.sizeDelta;
    }

    /// <summary>
    /// Executes timing QTE with specified animation speed.
    /// Animates shrinking outline and evaluates player key press timing against zones.
    /// Supports fakeout mode (player must avoid pressing) and normal mode (press in zone).
    /// Called by QTEManager with move's qteSpeed parameter.
    /// Invokes callback with result (Perfect/Good/OK/Miss/Fakeout variants).
    /// </summary>
    public IEnumerator Run(float speed, Action<QTEResult> callback)
    {
        uiRoot.SetActive(true);

        float t = 0f;
        bool pressed = false;

        outlineRect.sizeDelta = startSize;

        // ===== FAKEOUT MODE =====
        // Player must NOT press button to succeed (visual shrinking but no zone evaluation)
        if (isFakeout)
        {
            if (resultText != null)
                resultText.text = "FAKEOUT";

            // Animate shrinking outline (visual only, no zone evaluation)
            while (t < 1f)
            {
                if (Input.anyKeyDown)
                {
                    pressed = true;
                    callback?.Invoke(QTEResult.FakeoutHit);
                    yield break;
                }

                // Animate outline shrink without evaluating hit zones
                t += Time.deltaTime * speed;
                outlineRect.sizeDelta = startSize * Mathf.Lerp(1f, 0f, t);

                yield return null;
            }

            // No key pressed during fakeout → player successfully avoided attack
            callback?.Invoke(QTEResult.FakeoutAvoid);
            yield break;
        }

        // ===== NORMAL TIMING QTE =====

        QTEResult result = QTEResult.Miss;

        while (t < 1f)
        {
            if (Input.GetKeyDown(hitKey))
            {
                pressed = true;
                result = EvaluateHit();
                break;
            }

            // Animate shrinking outline for visual timing reference
            t += Time.deltaTime * speed;
            outlineRect.sizeDelta = startSize * Mathf.Lerp(1f, 0f, t);

            yield return null;
        }

        // If no key pressed by end of timing window, result is Miss
        if (!pressed)
            result = QTEResult.Miss;

        callback?.Invoke(result);
    }


    private QTEResult EvaluateHit()
    {
        Rect outR = GetWorldRect(outlineRect, outlineCorners);
        Rect perfR = GetWorldRect(perfectZoneRect, perfectCorners);
        Rect goodR = GetWorldRect(whiteZoneRect, goodCorners);
        Rect okR = GetWorldRect(barelyZone, okCorners);

        if (perfR.Contains(outR.min) && perfR.Contains(outR.max))
            return QTEResult.Perfect;

        if (goodR.Contains(outR.min) && goodR.Contains(outR.max))
            return QTEResult.Good;

        if (okR.Contains(outR.min) && okR.Contains(outR.max))
            return QTEResult.Ok;

        return QTEResult.Miss;
    }

    private Rect GetWorldRect(RectTransform rt, Vector3[] buffer)
    {
        rt.GetWorldCorners(buffer);
        return new Rect(buffer[0], buffer[2] - buffer[0]);
    }

    public void SetFakeoutText()
    {
        if (resultText != null)
            resultText.text = "FAKEOUT";
    }
}
