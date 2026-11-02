using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// TEMPORARY test-scene HUD for the wave prototype. Not the game's UI (Ronit owns UI);
/// delete it once the real HUD listens to WaveEncounter's events.
/// Built at runtime with uGUI (built-in font, no extra assets). Layout is authored at 1920x1080.
/// Readability: draw it through a UI-only camera with post-processing off (or leave UI Camera empty for
/// Screen Space - Overlay), so anti-aliasing, bloom and vignette never touch the text. Text is pixel-snapped and
/// never scales below Minimum Scale, so it stays legible in a small Game view.
/// Hull/wave panel (top-left), wave banner (top-centre), controls (bottom-left), result panel (centre),
/// detailed counters behind Tab (top-right), red edge flash and pip pulse when the player is hit.
/// </summary>
public class EncounterDebugHUD : MonoBehaviour
{
    [SerializeField] private WaveEncounter encounter;
    [Tooltip("Optional placeholder player health; shows hull pips and hit feedback when assigned.")]
    [SerializeField] private TestPlayerHealth playerHealth;
    [Tooltip("UI-only camera (post-processing off) the HUD renders through. Empty = Screen Space - Overlay.")]
    [SerializeField] private Camera uiCamera;
    [SerializeField] private bool showDebugCounters = false;
    [Tooltip("The HUD never shrinks below this fraction of its 1920x1080 size (keeps text readable in small windows).")]
    [Range(0.4f, 1f)] [SerializeField] private float minimumScale = 0.6f;
    [Tooltip("Red screen-edge flash when the player is hit.")]
    [SerializeField] private bool damageFlash = true;

    [Header("Mission text (optional, for a demo)")]
    [Tooltip("Shown as the banner before wave 1 instead of the wave name. Empty = wave name.")]
    [SerializeField] private string missionTitle = "";
    [Tooltip("Shown under the hull/wave panel for the whole encounter. Empty = hidden.")]
    [SerializeField] private string objective = "";
    [SerializeField] private string clearedTitle = "ENCOUNTER CLEARED";
    [Tooltip("Extra line on the cleared panel (e.g. what happens next). Empty = none.")]
    [SerializeField] private string clearedMessage = "";

    public const float ReferenceWidth = 1920f;
    public const float ReferenceHeight = 1080f;

    static readonly Color PanelColour = new Color(0.015f, 0.025f, 0.06f, 0.82f);
    static readonly Color TextColour = new Color(0.96f, 0.98f, 1f);
    static readonly Color DimText = new Color(0.72f, 0.78f, 0.86f);
    static readonly Color PipFull = new Color(0.3f, 0.85f, 1f);
    static readonly Color PipEmpty = new Color(0.16f, 0.2f, 0.27f, 0.95f);
    static readonly Color Amber = new Color(1f, 0.78f, 0.3f);
    static readonly Color Good = new Color(0.5f, 0.95f, 0.6f);
    static readonly Color Bad = new Color(1f, 0.4f, 0.34f);

    private Font font;
    private GameObject canvasObject;
    private Canvas canvas;
    private CanvasScaler scaler;
    private int uiLayer;
    private Image[] pips = new Image[0];
    private Text hullLabel;
    private Text hullCount;
    private Text hitPopup;
    private Text waveText;
    private Text bannerText;
    private Text objectiveText;
    private GameObject debugPanel;
    private Text debugText;
    private GameObject resultPanel;
    private Text resultTitle;
    private Text resultBody;
    private RectTransform resultRect;
    private RawImage edgeFlash;
    private Texture2D edgeTexture;

    private float flashTimer;
    private float flashLength = 0.35f;
    private float flashStrength;
    private float pipPulse;
    private int pulsePip = -1;
    private float popupTimer;

    public bool DebugVisible => showDebugCounters;
    public bool ResultVisible => resultPanel != null && resultPanel.activeSelf;
    public string ResultTitle => resultTitle != null ? resultTitle.text : "";
    public string ObjectiveShown => objectiveText != null ? objective : "";
    public string BannerShown => bannerText != null && bannerText.gameObject.activeSelf ? bannerText.text : "";
    public float DamageFlashAlpha => edgeFlash != null ? edgeFlash.color.a : 0f;
    public float CurrentScale => scaler != null ? scaler.scaleFactor : 1f;
    public Canvas HudCanvas => canvas;

    public void SetDebugVisible(bool visible)
    {
        showDebugCounters = visible;
        if (debugPanel != null)
        {
            debugPanel.SetActive(visible);
        }
    }

    void Start()
    {
        BuildLayout();
        SetDebugVisible(showDebugCounters);
        if (playerHealth != null)
        {
            playerHealth.onDamaged.AddListener(HandleDamaged);
            playerHealth.onDefeated.AddListener(HandleDefeated);
        }
    }

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.onDamaged.RemoveListener(HandleDamaged);
            playerHealth.onDefeated.RemoveListener(HandleDefeated);
        }
        if (canvasObject != null)
        {
            Destroy(canvasObject);
        }
        if (edgeTexture != null)
        {
            Destroy(edgeTexture);
        }
    }

    private void HandleDamaged()
    {
        StartFlash(0.45f, 0.3f);
        pulsePip = playerHealth.HitsRemaining; // the pip that was just lost
        pipPulse = 1f;
        popupTimer = 0.8f;
        hitPopup.text = "HIT  -1";
    }

    private void HandleDefeated()
    {
        StartFlash(0.55f, 0.5f);
        pulsePip = 0;
        pipPulse = 1f;
        popupTimer = 0f;
    }

    private void StartFlash(float strength, float seconds)
    {
        if (!damageFlash)
        {
            return;
        }
        flashStrength = strength;
        flashLength = seconds;
        flashTimer = seconds;
    }

    void Update()
    {
        if (canvasObject == null || encounter == null)
        {
            return;
        }
        UpdateScale();
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            SetDebugVisible(!showDebugCounters);
        }
        UpdateFeedback();
        RefreshHull();
        RefreshWave();
        RefreshResult();
        if (showDebugCounters)
        {
            debugText.text =
                $"State  {encounter.State}  ({encounter.StateTimer:F1}s)\n" +
                $"Alive  {encounter.AliveCount}\n" +
                $"Spawned  {encounter.SpawnedCount}\n" +
                $"Destroyed  {encounter.DestroyedCount}\n" +
                $"Escaped  {encounter.EscapedCount}\n" +
                $"Timed out  {encounter.TimedOutCount}\n" +
                $"Time  {encounter.ElapsedTime:F1}s" +
                (encounter.ConfigErrorCount > 0 ? $"\nCONFIG ERRORS  {encounter.ConfigErrorCount}" : "");
        }
    }

    // Scale with the smaller of width/height ratios, but never below minimumScale.
    private void UpdateScale()
    {
        float w = uiCamera != null ? uiCamera.pixelWidth : Screen.width;
        float h = uiCamera != null ? uiCamera.pixelHeight : Screen.height;
        float scale = Mathf.Min(w / ReferenceWidth, h / ReferenceHeight);
        scaler.scaleFactor = Mathf.Max(minimumScale, scale);
    }

    private void UpdateFeedback()
    {
        // Unscaled, so feedback timing does not change if a test or menu changes Time.timeScale.
        float dt = Time.unscaledDeltaTime;
        if (flashTimer > 0f)
        {
            flashTimer = Mathf.Max(0f, flashTimer - dt);
        }
        float k = flashLength > 0f ? flashTimer / flashLength : 0f;
        edgeFlash.color = new Color(1f, 0.12f, 0.08f, flashStrength * k * k);
        edgeFlash.enabled = flashTimer > 0f;

        pipPulse = Mathf.Max(0f, pipPulse - dt / 0.45f);
        popupTimer = Mathf.Max(0f, popupTimer - dt);
        hitPopup.gameObject.SetActive(popupTimer > 0f);
        if (popupTimer > 0f)
        {
            Color c = Bad;
            c.a = Mathf.Clamp01(popupTimer / 0.3f);
            hitPopup.color = c;
        }
    }

    private void RefreshHull()
    {
        if (playerHealth == null)
        {
            return;
        }
        bool blink = playerHealth.IsInvulnerable && Mathf.Repeat(Time.time * 8f, 1f) > 0.5f;
        for (int i = 0; i < pips.Length; i++)
        {
            bool full = i < playerHealth.HitsRemaining;
            Color colour = full ? (blink && i == playerHealth.HitsRemaining - 1 ? Amber : PipFull) : PipEmpty;
            float scale = 1f;
            if (i == pulsePip && pipPulse > 0f)
            {
                // The lost pip flashes white and pops, then settles as empty.
                colour = Color.Lerp(colour, Color.white, pipPulse);
                scale = 1f + 0.45f * pipPulse;
            }
            pips[i].color = colour;
            pips[i].rectTransform.localScale = Vector3.one * scale;
        }
        bool low = playerHealth.HitsRemaining <= 1;
        hullLabel.color = low ? Bad : DimText;
        hullCount.text = $"{playerHealth.HitsRemaining}/{playerHealth.MaxHits}";
        hullCount.color = low ? Bad : TextColour;
    }

    private void RefreshWave()
    {
        EnemyWave wave = encounter.CurrentWave;
        string name = wave != null ? ShortName(wave.waveName) : "-";
        waveText.text = $"<b>WAVE {Mathf.Max(1, encounter.CurrentWaveIndex + 1)}/{encounter.WaveCount}</b>   <color=#B8C6D8>{name}</color>";

        bool incoming = encounter.State == WaveEncounter.EncounterState.WaitingForWave && wave != null;
        bannerText.gameObject.SetActive(incoming);
        if (incoming)
        {
            bannerText.text = encounter.CurrentWaveIndex == 0 && !string.IsNullOrEmpty(missionTitle)
                ? missionTitle.ToUpperInvariant()
                : $"WAVE {encounter.CurrentWaveIndex + 1}  -  {name.ToUpperInvariant()}";
            Color c = Amber;
            c.a = 0.75f + 0.25f * Mathf.Sin(Time.time * 5f);
            bannerText.color = c;
        }
    }

    private void RefreshResult()
    {
        bool done = encounter.IsCompleted || encounter.IsFailed;
        resultPanel.SetActive(done);
        if (!done)
        {
            return;
        }
        const string restart = "\n\n<color=#FFFFFF><b>R</b></color>  <color=#B8C6D8>restart</color>";
        if (encounter.IsCompleted)
        {
            resultTitle.text = clearedTitle;
            resultTitle.color = Good;
            string verdict = encounter.AllEnemiesDestroyed
                ? "<color=#80F29A>ALL ENEMIES DESTROYED</color>"
                : "<color=#B8C6D8>Some enemies got past (no penalty yet)</color>";
            if (!string.IsNullOrEmpty(clearedMessage))
            {
                verdict += "\n" + clearedMessage;
            }
            resultBody.text =
                $"Destroyed   {encounter.DestroyedCount} / {encounter.SpawnedCount}\n" +
                $"Escaped   {encounter.EscapedCount}" + (encounter.TimedOutCount > 0 ? $"     Timed out   {encounter.TimedOutCount}" : "") + "\n" +
                $"Time   {encounter.ElapsedTime:F1} s\n\n" + verdict + restart;
        }
        else
        {
            resultTitle.text = "SHIP DESTROYED";
            resultTitle.color = Bad;
            resultBody.text =
                $"Reached wave   {encounter.CurrentWaveIndex + 1} / {encounter.WaveCount}\n" +
                $"Destroyed   {encounter.DestroyedCount}\n" +
                $"Escaped   {encounter.EscapedCount}" + restart;
        }
        // Fit the panel to its text so short results do not leave an empty box.
        resultRect.sizeDelta = new Vector2(820f, 132f + resultBody.preferredHeight + 40f);
    }

    private static string ShortName(string waveName)
    {
        // "Wave 2 - V Formation" -> "V Formation"
        int dash = waveName.IndexOf(" - ");
        return dash >= 0 ? waveName.Substring(dash + 3) : waveName;
    }

    // ---- Layout ----

    private void BuildLayout()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer < 0)
        {
            uiLayer = 5; // Unity's built-in UI layer
        }

        canvasObject = new GameObject("PrototypeHUD_Canvas", typeof(RectTransform));
        canvasObject.layer = uiLayer;
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        if (uiCamera != null)
        {
            // Screen Space - Camera through a UI-only camera: crisp, and still included in camera captures.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCamera;
            canvas.planeDistance = 1f;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.pixelPerfect = true; // snap glyphs to whole pixels
        scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; // factor set in UpdateScale
        scaler.referencePixelsPerUnit = 100f;
        UpdateScale();
        RectTransform root = (RectTransform)canvasObject.transform;

        // Full-screen red edge flash (behind everything else).
        edgeTexture = EdgeTexture();
        var flashGo = new GameObject("DamageFlash", typeof(RectTransform), typeof(RawImage));
        flashGo.layer = uiLayer;
        var flashRect = (RectTransform)flashGo.transform;
        flashRect.SetParent(root, false);
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;
        edgeFlash = flashGo.GetComponent<RawImage>();
        edgeFlash.texture = edgeTexture;
        edgeFlash.raycastTarget = false;
        edgeFlash.color = new Color(1f, 0.12f, 0.08f, 0f);
        edgeFlash.enabled = false;

        // Top-left: hull + wave.
        RectTransform hud = Panel(root, "StatusPanel", new Vector2(0f, 1f), new Vector2(32f, -28f), new Vector2(560f, 128f), PanelColour);
        hullLabel = Label(hud, "HULL", 30, TextAnchor.MiddleLeft, DimText, new Vector2(24f, -14f), new Vector2(110f, 44f), bold: true);
        int maxHits = playerHealth != null ? playerHealth.MaxHits : 0;
        pips = new Image[maxHits];
        for (int i = 0; i < maxHits; i++)
        {
            RectTransform pip = Panel(hud, "Pip" + i, new Vector2(0f, 1f), new Vector2(138f + i * 78f, -22f), new Vector2(66f, 28f), PipFull);
            pip.pivot = new Vector2(0.5f, 0.5f);
            pip.anchoredPosition = new Vector2(138f + i * 78f + 33f, -36f);
            pips[i] = pip.GetComponent<Image>();
        }
        hullCount = Label(hud, "", 30, TextAnchor.MiddleLeft, TextColour, new Vector2(146f + maxHits * 78f, -14f), new Vector2(90f, 44f), bold: true);
        if (maxHits == 0)
        {
            hullLabel.text = "";
        }
        waveText = Label(hud, "", 30, TextAnchor.MiddleLeft, TextColour, new Vector2(24f, -70f), new Vector2(520f, 44f));
        hitPopup = Label(root, "", 34, TextAnchor.MiddleLeft, Bad, new Vector2(608f, -42f), new Vector2(240f, 48f), bold: true, outline: true);
        hitPopup.gameObject.SetActive(false);

        // Optional objective line, bottom-left above the controls (kept clear of the top edge where enemies enter).
        if (!string.IsNullOrEmpty(objective))
        {
            RectTransform objectivePanel = Panel(root, "ObjectivePanel", new Vector2(0f, 0f), new Vector2(32f, 94f), new Vector2(560f, 52f), PanelColour);
            objectiveText = Label(objectivePanel, $"<color=#FFC74D><b>OBJECTIVE</b></color>   {objective}", 28, TextAnchor.MiddleLeft, TextColour,
                new Vector2(24f, -4f), new Vector2(1400f, 44f));
            objectivePanel.sizeDelta = new Vector2(objectiveText.preferredWidth + 48f, 52f);
        }

        // Top-centre banner (only while a wave is incoming).
        bannerText = Label(root, "", 52, TextAnchor.MiddleCenter, Amber, Vector2.zero, new Vector2(1200f, 72f), bold: true, outline: true);
        Anchor(bannerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f));

        // Bottom-left controls on their own panel, sized to the text.
        RectTransform controlsPanel = Panel(root, "ControlsPanel", new Vector2(0f, 0f), new Vector2(32f, 24f), new Vector2(1000f, 58f), PanelColour);
        Text controls = Label(controlsPanel, Keys("WASD / Arrows", "move") + "     " + Keys("Space / Left mouse", "fire") + "     " +
                                             Keys("R", "restart") + "     " + Keys("Tab", "debug"),
            28, TextAnchor.MiddleLeft, TextColour, new Vector2(22f, -7f), new Vector2(1400f, 44f));
        controlsPanel.sizeDelta = new Vector2(controls.preferredWidth + 44f, 58f);

        // Top-right debug counters.
        RectTransform dbg = Panel(root, "DebugPanel", new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(390f, 318f), PanelColour);
        Label(dbg, "DEBUG (Tab)", 26, TextAnchor.UpperLeft, Amber, new Vector2(20f, -14f), new Vector2(350f, 34f), bold: true);
        debugText = Label(dbg, "", 26, TextAnchor.UpperLeft, TextColour, new Vector2(20f, -54f), new Vector2(350f, 250f));
        debugText.lineSpacing = 1.05f;
        debugPanel = dbg.gameObject;

        // Centre results.
        RectTransform result = Panel(root, "ResultPanel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820f, 440f), new Color(0.015f, 0.025f, 0.06f, 0.9f));
        resultTitle = Label(result, "", 56, TextAnchor.MiddleCenter, Good, new Vector2(0f, -30f), new Vector2(780f, 80f), true, bold: true);
        resultBody = Label(result, "", 30, TextAnchor.UpperCenter, TextColour, new Vector2(0f, -132f), new Vector2(780f, 300f), true);
        resultPanel = result.gameObject;
        resultRect = result;
        resultPanel.SetActive(false);
    }

    private static string Keys(string keys, string action) => $"<b>{keys}</b> <color=#B8C6D8>{action}</color>";

    private RectTransform Panel(RectTransform parent, string panelName, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
    {
        var go = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        go.layer = uiLayer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = size;
        Anchor(rt, anchor, position);
        var image = go.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return rt;
    }

    private Text Label(RectTransform parent, string content, int size, TextAnchor align, Color colour, Vector2 position, Vector2 boxSize,
        bool centred = false, bool bold = false, bool outline = false)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.layer = uiLayer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.sizeDelta = boxSize;
        Anchor(rt, centred ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f), position);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.alignment = align;
        text.color = colour;
        text.text = content;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (outline)
        {
            // Only for text drawn straight over the scene; panel text needs no edge effect.
            var edge = go.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.85f);
            edge.effectDistance = new Vector2(2f, -2f);
        }
        return text;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
    }

    // Transparent centre fading to opaque at the screen edges (rounded corners).
    private static Texture2D EdgeTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TEST_HUD_EdgeFlash", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                float v = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                float d = Mathf.Pow(Mathf.Pow(u, 4f) + Mathf.Pow(v, 4f), 0.25f);
                float a = Mathf.Clamp01((d - 0.72f) / 0.28f); // only the outer band: the action stays clear
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return tex;
    }
}
