using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Button prompt QTE where player must press correct button after delay.
/// Displays button prompt and timer, evaluates correctness and timing.
/// Supports fakeout mode (player must NOT press button during fakeout).
/// Called by QTEManager with AttackMove parameters for button and timing setup.
/// </summary>
public class ButtonPromptQTE : MonoBehaviour
{
    /// <summary>Root gameObject containing all UI; shown during QTE, hidden otherwise.</summary>
    public GameObject uiRoot;
    /// <summary>Text display for prompt message (e.g., "PRESS SPACE!").</summary>
    public TMP_Text promptText;
    /// <summary>Image component showing timer countdown (radial fill).</summary>
    public Image timerImage;

    /// <summary>If true, fakeout mode active (player must NOT press button).</summary>
    public bool isFakeout = false;

    /// <summary>
    /// Synchronous fakeout evaluation (frames-based instead of coroutine).
    /// Checks if player presses any key during fakeout timeLimit.
    /// Returns FakeoutHit if pressed (bad), FakeoutAvoid if not pressed (good).
    /// Called for quick fakeout testing without coroutine overhead.
    /// </summary>
    public QTEResult RunFakeout(float timeLimit)
    {
        float timer = 0f;
        bool pressed = false;

        if (promptText != null)
            promptText.text = "FAKEOUT";

        while (timer < timeLimit)
        {
            if (Input.anyKeyDown)
            {
                pressed = true;
                break;
            }

            timer += Time.deltaTime;
        }

        return pressed ? QTEResult.FakeoutHit : QTEResult.FakeoutAvoid;
    }

    /// <summary>
    /// Executes button prompt QTE with pre-delay and answer window.
    /// If isFakeout is true: player must NOT press during timeLimit.
    /// If normal mode: waits preDelay seconds, then prompts for button, evaluates timing.
    /// Called by QTEManager with move parameters, button from AttackMove.GetRandomButton().
    /// Result varies from Perfect (early press) to Miss (no press).
    /// </summary>
    public IEnumerator Run(AttackMove move, float preDelay, KeyCode button, float timeLimit, Action<QTEResult> onComplete)
    {
        // ===== FAKEOUT MODE =====
        // Player must NOT press any button during timeLimit to succeed (reverse logic).
        // Pressing ANY key = FakeoutHit (failure), no press = FakeoutAvoid (success).
        // Used by deception-themed enemy moves to punish button-mashing tendencies.
        if (isFakeout)
        {
            if (promptText != null)
                promptText.text = "FAKEOUT";

            float timer = 0f;
            bool pressed = false;

            // Monitor for ANY key input during fakeout window
            while (timer < timeLimit)
            {
                if (Input.anyKeyDown)
                {
                    pressed = true;  // Player fell for the fake
                    break;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            onComplete?.Invoke(pressed ? QTEResult.FakeoutHit : QTEResult.FakeoutAvoid);
            yield break;
        }

        // ===== PHASE 1: WAIT PERIOD (DO NOT PRESS) =====
        // Before answer window, player must NOT press the target button.
        // Early press = Miss (failed to wait). This prevents button-mashing strategies.
        if (promptText != null)
            promptText.text = $"Wait to press {button}... ";

        if (timerImage != null)
            timerImage.fillAmount = 1f;

        float waitTimer = 0f;

        while (waitTimer < preDelay)
        {
            // Detect accidental/premature button press during wait period
            if (Input.GetKeyDown(button))
            {
                onComplete?.Invoke(QTEResult.Miss);  // Failed: pressed too early
                yield break;
            }

            waitTimer += Time.deltaTime;
            yield return null;
        }

        // ===== PHASE 2: ANSWER WINDOW (MUST PRESS WITHIN LIMIT) =====
        // After pre-delay expires, player must press target button within timeLimit window.
        // Timing evaluated against perfectThreshold = Mathf.Min(0.25f, timeLimit * 0.4f)
        // Perfect = press within first 25% or 0.25s window (whichever smaller)
        // Good = press within remaining time
        // Miss = no press or timeout
        if (promptText != null)
            promptText.text = $"PRESS {button}!";

        float t = 0f;
        bool pressedCorrect = false;
        float pressTime = -1f;

        while (t < timeLimit)
        {
            // Monitor for correct button press during answer window
            if (Input.GetKeyDown(button))
            {
                pressedCorrect = true;
                pressTime = t;  // Record press timing for quality evaluation
                break;
            }

            t += Time.deltaTime;

            // Update visual countdown: fillAmount = 1.0 (start) to 0.0 (timeout)
            // Provides player visual feedback on remaining time
            if (timerImage != null)
                timerImage.fillAmount = 1f - (t / timeLimit);

            yield return null;
        }

        // ===== RESULT EVALUATION =====
        // Determine result quality based on press timing vs perfectThreshold.
        // Formula: perfectThreshold = Min(0.25s, 40% of timeLimit)
        // Example: timeLimit=1.0s → perfectThreshold=0.25s (must press within 250ms for Perfect)
        QTEResult result = QTEResult.Miss;

        if (pressedCorrect)
        {
            float perfectThreshold = Mathf.Min(0.25f, timeLimit * 0.4f);
            result = pressTime <= perfectThreshold ? QTEResult.Perfect : QTEResult.Good;
        }

        // Display result briefly (0.15s) for player feedback
        if (promptText != null)
            promptText.text = result.ToString();

        yield return new WaitForSeconds(0.15f);
        onComplete?.Invoke(result);  // Return result to QTEManager for damage calculation
    }


    /// <summary>
    /// Sets custom text in the prompt display (optional utility).
    /// Called by QTEManager for special messages, debugging, or move-specific flavor text.
    /// </summary>
    public void SetCustomText(string text)
    {
        if (promptText != null)
            promptText.text = text;
    }
}
