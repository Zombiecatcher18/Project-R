using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class QTEManager : MonoBehaviour
{
    public static QTEManager Instance;

    public static bool DebugQTE = false;   // MASTER DEBUG TOGGLE

    [Header("QTE Types")]
    public TimingQTE timingQTE;
    public ButtonPromptQTE buttonQTE;

    public event Action<QTEResult> OnQTECompleted;

    public enum QTEOwner { None, Player, Enemy }
    public QTEOwner CurrentOwner { get; private set; } = QTEOwner.None;

    public bool IsRunning { get; private set; }
    public bool isFakeout { get; private set; }

    private ExamplePlayerController cachedPlayerController;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            cachedPlayerController = player.GetComponent<ExamplePlayerController>();

        if (DebugQTE)
            Debug.Log("[QTEManager] Initialized.");
    }

    private void SetPlayerInputEnabled(bool enabled)
    {
        if (cachedPlayerController != null)
            cachedPlayerController.EnableInput(enabled);
    }

    // ===================================================
    // QTE Initialization
    // ===================================================
    public void StartQTEForMove(AttackMove move, QTEOwner owner)
    {
        if (DebugQTE)
            Debug.Log($"[QTEManager] Starting QTE for move: {move.moveName}, type={move.qteType}, speed={move.qteSpeed}");

        if (IsRunning)
        {
            if (DebugQTE)
                Debug.LogWarning("[QTEManager] QTE already running.");
            return;
        }

        if (move == null)
        {
            OnQTECompleted?.Invoke(QTEResult.Miss);
            return;
        }

        PrepareQTE(owner);

        if (move.qteType == QTEType.Timing)
            StartCoroutine(RunTiming(move));
        else
            StartCoroutine(RunButtonPrompt(move));
    }

    private void PrepareQTE(QTEOwner owner)
    {
        IsRunning = true;
        CurrentOwner = owner;

        isFakeout = false;
        timingQTE.isFakeout = false;
        buttonQTE.isFakeout = false;

        // Hide both UIs before showing the correct one
        timingQTE.uiRoot.SetActive(false);
        timingQTE.gameObject.SetActive(false);

        buttonQTE.uiRoot.SetActive(false);
        buttonQTE.gameObject.SetActive(false);

        if (timingQTE.resultText != null)
            timingQTE.resultText.text = "";

        if (buttonQTE.promptText != null)
            buttonQTE.promptText.text = "";

        SetPlayerInputEnabled(false);
    }

    // ===================================================
    // Fakeout QTE
    // ===================================================
    public void StartFakeoutQTE(AttackMove move)
    {
        isFakeout = true;
        timingQTE.isFakeout = true;
        buttonQTE.isFakeout = true;

        IsRunning = true;
        CurrentOwner = QTEOwner.Enemy;

        timingQTE.uiRoot.SetActive(false);
        buttonQTE.uiRoot.SetActive(false);

        if (move.qteType == QTEType.Timing)
            StartCoroutine(RunTiming(move));
        else
            StartCoroutine(RunButtonPrompt(move));
    }

    // ===================================================
    // End QTE
    // ===================================================
    public void EndQTE()
    {
        IsRunning = false;
        CurrentOwner = QTEOwner.None;
        isFakeout = false;

        timingQTE.isFakeout = false;
        buttonQTE.isFakeout = false;

        timingQTE.uiRoot.SetActive(false);
        timingQTE.gameObject.SetActive(false);

        buttonQTE.uiRoot.SetActive(false);
        buttonQTE.gameObject.SetActive(false);

        SetPlayerInputEnabled(true);

        if (DebugQTE)
            Debug.Log("[QTEManager] QTE Ended.");
    }

    public void ForceStopQTE()
    {
        if (!IsRunning)
            return;

        if (DebugQTE)
            Debug.Log("[QTEManager] ForceStopQTE called.");

        StopAllCoroutines();

        timingQTE.uiRoot.SetActive(false);
        timingQTE.gameObject.SetActive(false);

        buttonQTE.uiRoot.SetActive(false);
        buttonQTE.gameObject.SetActive(false);

        IsRunning = false;
        CurrentOwner = QTEOwner.None;
        isFakeout = false;

        SetPlayerInputEnabled(true);
    }

    // ===================================================
    // Timing QTE
    // ===================================================
    private IEnumerator RunTiming(AttackMove move)
    {
        if (BattleManager.Instances != null && BattleManager.Instances.battleOver)
            yield break;

        // Hide button QTE UI
        buttonQTE.uiRoot.SetActive(false);
        buttonQTE.gameObject.SetActive(false);

        // Show timing QTE UI
        timingQTE.uiRoot.SetActive(true);
        timingQTE.gameObject.SetActive(true);

        if (timingQTE.resultText != null)
            timingQTE.resultText.text = "";

        if (DebugQTE)
            Debug.Log("[QTEManager] RunTiming started");

        QTEResult result = QTEResult.Miss;

        yield return timingQTE.Run(move.qteSpeed, r => result = r);

        if (CurrentOwner != QTEOwner.None)
            OnQTECompleted?.Invoke(result);

        EndQTE();
    }

    // ===================================================
    // Button Prompt QTE
    // ===================================================
    private IEnumerator RunButtonPrompt(AttackMove move)
    {
        if (BattleManager.Instances != null && BattleManager.Instances.battleOver)
            yield break;

        // Hide timing QTE UI
        timingQTE.uiRoot.SetActive(false);
        timingQTE.gameObject.SetActive(false);

        // Show button QTE UI
        buttonQTE.uiRoot.SetActive(true);
        buttonQTE.gameObject.SetActive(true);

        KeyCode button = move.GetRandomButton();
        float timeLimit = move.GetTimeLimit();;
        float preDelay = move.preDelay;

        QTEResult result = QTEResult.Miss;

        yield return buttonQTE.Run(move, preDelay, button, timeLimit, r => result = r);

        if (CurrentOwner != QTEOwner.None)
            OnQTECompleted?.Invoke(result);

        EndQTE();
    }

    // ===================================================
    // Fakeout Coroutine (safe timeout)
    // ===================================================
    private IEnumerator RunFakeoutCoroutine(float timeLimit, Action<QTEResult> onComplete)
    {
        float timeout = timeLimit;
        bool pressed = false;

        while (timeout > 0f)
        {
            if (Input.anyKeyDown)
            {
                pressed = true;
                break;
            }

            timeout -= Time.deltaTime;
            yield return null;
        }

        QTEResult result = pressed ? QTEResult.FakeoutHit : QTEResult.FakeoutAvoid;
        onComplete?.Invoke(result);
    }
}
