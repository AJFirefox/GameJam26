using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The Altar of Tyche. After a wave is cleared it drops out of the sky into the arena and holds the next wave.
/// Walk up and press E (gamepad X) to open it: pick 1 of 3 boons, optionally switch weapons,
/// then press Next Wave. The altar rises back into the sky and the next wave starts.
/// Hold Tab any time to see the boons you have.
///
/// Setup: put this on an empty GameObject in the scene. Needs a SpawnManager and a BoonManager in the scene,
/// and a WeaponLoadout on the Player. Optional: an altar model prefab and a landing point.
/// </summary>
public class AltarOfTyche : MonoBehaviour
{
    [Header("When")]
    [Tooltip("On: altar after every wave. Off: only after boss waves.")]
    public bool afterEveryWave = true;
    public int boonChoices = 3;

    [Header("Where")]
    [Tooltip("Where it lands. Empty = the middle of the spawn points, dropped onto the floor.")]
    public Transform landingPoint;
    public float dropHeight = 40f;
    public float fallTime = 0.9f;
    public float riseTime = 1.4f;
    public float landingDelay = 1f;

    [Header("Look")]
    [Tooltip("Optional altar model. Empty = a placeholder stone pedestal with a golden orb.")]
    public GameObject altarPrefab;

    [Header("Interact")]
    public float interactRange = 4f;

    // ---------- References ----------
    private SpawnManager spawner;
    private WeaponLoadout loadout;
    private Transform player;
    private PlayerMovement playerMovement;

    // ---------- State ----------
    private enum State { Idle, Falling, Waiting, MenuOpen, Rising }
    private State state = State.Idle;
    private GameObject altar;
    private Transform orb;
    private List<BoonManager.Boon> offer = new List<BoonManager.Boon>();
    private int takenIndex = -1;
    private bool waitingForLuckyNumber;

    // ---------- UI ----------
    private RectTransform menuCanvas, promptCanvas, boonListCanvas;
    private Text subtitleText, promptText, boonListText;
    private Button[] boonCards;
    private Button[] weaponButtons;
    private RectTransform numberRow;
    private Text numberRowLabel;
    private Button nextWaveButton;

    // ======================================================================
    // Setup
    // ======================================================================

    void Awake()
    {
        UIBuilder.EnsureEventSystem();
        BuildMenu();
        BuildPromptAndBoonList();
    }

    void Start()
    {
        spawner = FindAnyObjectByType<SpawnManager>();
        if (spawner != null) spawner.OnWaveCleared += HandleWaveCleared;
        else Debug.LogWarning("AltarOfTyche: no SpawnManager in the scene, so the altar never appears.");

        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerMovement = p.GetComponent<PlayerMovement>();
            loadout = p.GetComponent<WeaponLoadout>();
        }
        if (loadout == null) Debug.LogWarning("AltarOfTyche: add a WeaponLoadout to the Player to switch weapons at the altar.");
        if (BoonManager.Instance == null) Debug.LogWarning("AltarOfTyche: add a BoonManager to the scene for boons to work.");
    }

    void OnDestroy()
    {
        if (spawner != null) spawner.OnWaveCleared -= HandleWaveCleared;
        if (state == State.MenuOpen) Time.timeScale = 1f;
    }

    // ======================================================================
    // Flow
    // ======================================================================

    private void HandleWaveCleared(int wave)
    {
        if (!afterEveryWave && !spawner.IsBossWave) return;
        if (state != State.Idle) return;

        spawner.RequestHold();
        StartCoroutine(Descend());
    }

    private IEnumerator Descend()
    {
        state = State.Falling;
        yield return new WaitForSeconds(landingDelay);

        Vector3 ground = FindLandingSpot();
        altar = altarPrefab != null ? Instantiate(altarPrefab) : BuildPlaceholderAltar();
        orb = altar.transform.Find("Orb");
        Vector3 sky = ground + Vector3.up * dropHeight;
        altar.transform.position = sky;

        // Fall, speeding up like it's dropped
        for (float t = 0f; t < fallTime; t += Time.deltaTime)
        {
            float k = t / fallTime;
            altar.transform.position = Vector3.Lerp(sky, ground, k * k);
            yield return null;
        }
        altar.transform.position = ground;

        // Landing: flash, thunder, shake
        JackpotEffect fx = FindAnyObjectByType<JackpotEffect>();
        if (fx != null) fx.Play(0.6f, false);

        // Knock the player out of the way if it landed on them
        if (player != null && playerMovement != null)
        {
            Vector3 away = player.position - ground;
            away.y = 0f;
            if (away.magnitude < 2.5f) playerMovement.Dash(away.sqrMagnitude > 0.01f ? away : Vector3.forward, 12f, 0.25f);
        }

        offer = BoonManager.Instance != null ? BoonManager.Instance.GetOffer(boonChoices) : new List<BoonManager.Boon>();
        takenIndex = -1;
        waitingForLuckyNumber = false;
        state = State.Waiting;
    }

    private void OpenMenu()
    {
        state = State.MenuOpen;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        RefreshMenu();
        menuCanvas.gameObject.SetActive(true);
        promptCanvas.gameObject.SetActive(false);
    }

    private void CloseMenu()
    {
        menuCanvas.gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // Resume next frame so the click that closed the menu doesn't also swing the sword
        StartCoroutine(ResumeNextFrame());
    }

    private IEnumerator ResumeNextFrame()
    {
        yield return null;
        Time.timeScale = 1f;
        if (state == State.MenuOpen) state = State.Waiting;
    }

    private void NextWave()
    {
        if (takenIndex < 0 || waitingForLuckyNumber) return;
        CloseMenu();
        state = State.Rising;
        StartCoroutine(Ascend());
    }

    private IEnumerator Ascend()
    {
        yield return null;   // let time resume first
        Vector3 start = altar.transform.position;
        Vector3 sky = start + Vector3.up * dropHeight;
        for (float t = 0f; t < riseTime; t += Time.deltaTime)
        {
            float k = t / riseTime;
            altar.transform.position = Vector3.Lerp(start, sky, k * k);
            yield return null;
        }
        Destroy(altar);
        altar = null;
        state = State.Idle;
        if (spawner != null) spawner.ReleaseHold();
    }

    // ======================================================================
    // Per-frame
    // ======================================================================

    void Update()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        // Tab: boon list (not while the altar menu is open)
        bool showList = state != State.MenuOpen && kb != null && kb.tabKey.isPressed;
        if (showList != boonListCanvas.gameObject.activeSelf)
        {
            if (showList) RefreshBoonList();
            boonListCanvas.gameObject.SetActive(showList);
        }

        // Idle animation for the orb
        if (orb != null) orb.localPosition = new Vector3(0f, 3.5f + Mathf.Sin(Time.unscaledTime * 2f) * 0.15f, 0f);

        if (state == State.Waiting && player != null && altar != null)
        {
            float dist = Vector3.Distance(Flat(player.position), Flat(altar.transform.position));
            bool near = dist <= interactRange;
            promptCanvas.gameObject.SetActive(true);
            promptText.text = near
                ? "<b>[E]</b>  Approach the Altar of Tyche"
                : (takenIndex >= 0 ? "Return to the Altar to begin the next wave" : "The Altar of Tyche has descended");

            bool interact = (kb != null && kb.eKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            if (near && interact) OpenMenu();
        }
        else if (state != State.MenuOpen && promptCanvas.gameObject.activeSelf)
        {
            promptCanvas.gameObject.SetActive(false);
        }

        if (state == State.MenuOpen)
        {
            bool back = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame);
            if (back && !waitingForLuckyNumber) CloseMenu();
        }
    }

    // ======================================================================
    // Choices
    // ======================================================================

    private void TakeBoon(int index)
    {
        if (takenIndex >= 0 || index >= offer.Count || BoonManager.Instance == null) return;
        takenIndex = index;
        BoonManager.Boon boon = offer[index];
        BoonManager.Instance.Grant(boon);
        waitingForLuckyNumber = boon.needsNumberPick;
        RefreshMenu();
    }

    private void PickLuckyNumber(int number)
    {
        if (LuckSystem.Instance != null) LuckSystem.Instance.SetLuckyNumber(number);
        waitingForLuckyNumber = false;
        RefreshMenu();
    }

    private void PickWeapon(int index)
    {
        if (loadout != null && loadout.Equip(index)) RefreshMenu();
    }

    // ======================================================================
    // UI
    // ======================================================================

    private void RefreshMenu()
    {
        int wave = spawner != null ? spawner.CurrentWave : 0;
        subtitleText.text = takenIndex < 0
            ? $"Wave {wave} cleared. Choose a blessing."
            : waitingForLuckyNumber ? "Name your Lucky Number." : "Tyche smiles on you. Change weapons, or begin the next wave.";

        // Boon cards
        for (int i = 0; i < boonCards.Length; i++)
        {
            Button card = boonCards[i];
            bool has = i < offer.Count;
            card.gameObject.SetActive(has);
            if (!has) continue;

            BoonManager.Boon b = offer[i];
            card.GetComponentInChildren<Text>().text =
                $"<size=18><color=#FFD040>{b.god.ToUpper()}</color></size>\n<size=30><b>{b.name}</b></size>\n\n<size=20>{b.description}</size>";
            card.interactable = takenIndex < 0;
            bool chosen = i == takenIndex;
            UIBuilder.SetBorder(card, chosen ? UIBuilder.Gold : new Color(1f, 0.82f, 0.25f, 0.35f), chosen ? 5f : 2f);
            card.GetComponent<Image>().color = chosen ? new Color(0.35f, 0.28f, 0.12f) : UIBuilder.CardColor;
        }

        // Lucky Number picker (Oracle's Decree)
        bool showNumbers = takenIndex >= 0 && takenIndex < offer.Count && offer[takenIndex].needsNumberPick;
        numberRow.gameObject.SetActive(showNumbers);
        if (showNumbers) BuildNumberButtons();

        // Weapons
        for (int i = 0; i < weaponButtons.Length; i++)
        {
            Button wb = weaponButtons[i];
            bool exists = loadout != null && i < loadout.slots.Length;
            wb.gameObject.SetActive(exists);
            if (!exists) continue;

            WeaponLoadout.WeaponSlot slot = loadout.slots[i];
            bool unlocked = loadout.IsUnlocked(i);
            bool equipped = loadout.CurrentIndex == i;
            string status = !unlocked ? "<color=#888888>LOCKED</color>" : equipped ? "<color=#FFD040>EQUIPPED</color>" : "Take up";
            wb.GetComponentInChildren<Text>().text = $"<size=24><b>{slot.name}</b></size>\n<size=16>{slot.description}</size>\n<size=18>{status}</size>";
            wb.interactable = unlocked && !equipped && !waitingForLuckyNumber;
            UIBuilder.SetBorder(wb, equipped ? UIBuilder.Gold : new Color(1f, 0.82f, 0.25f, 0.35f), equipped ? 5f : 2f);
            wb.GetComponent<Image>().color = unlocked ? UIBuilder.CardColor : UIBuilder.LockedColor;
        }

        nextWaveButton.interactable = takenIndex >= 0 && !waitingForLuckyNumber;
        nextWaveButton.GetComponentInChildren<Text>().text = takenIndex < 0 ? "Choose a blessing first" : "NEXT WAVE  >";
    }

    private void BuildNumberButtons()
    {
        for (int i = numberRow.childCount - 1; i >= 0; i--)
            if (numberRow.GetChild(i) != numberRowLabel.transform) Destroy(numberRow.GetChild(i).gameObject);

        LuckSystem luck = LuckSystem.Instance;
        if (luck == null) return;

        int min = luck.MinLuckyNumber, max = luck.MaxLuckyNumber;
        int count = max - min + 1;
        float width = Mathf.Min(78f, 1300f / count);
        for (int n = min; n <= max; n++)
        {
            int number = n;
            float x = (n - min - (count - 1) * 0.5f) * width;
            float odds = luck.ChanceOfTotal(n) * 100f;
            Button b = UIBuilder.Button($"Number {n}", numberRow, $"<size=26><b>{n}</b></size>\n<size=14>{odds:0.#}%</size>",
                                        20, new Vector2(x, -20f), new Vector2(width - 6f, 70f), () => PickLuckyNumber(number));
            bool current = luck.LuckyNumber == n;
            b.GetComponent<Image>().color = current ? new Color(0.35f, 0.28f, 0.12f) : UIBuilder.CardColor;
            UIBuilder.SetBorder(b, current ? UIBuilder.Gold : new Color(1f, 0.82f, 0.25f, 0.35f), current ? 4f : 2f);
        }
        numberRowLabel.text = waitingForLuckyNumber
            ? "Choose your Lucky Number  <size=16>(chance to roll it)</size>"
            : $"Lucky Number: {luck.LuckyNumber}  <size=16>(click to change)</size>";
    }

    private void RefreshBoonList()
    {
        LuckSystem luck = LuckSystem.Instance;
        string text = "<size=30><color=#FFD040><b>BLESSINGS</b></color></size>\n";
        if (luck != null)
            text += $"<size=18>Lucky Number {luck.LuckyNumber}   ·   {luck.DiceCount}d6   ·   Jackpot chance {luck.JackpotChance * 100f:0.#}%</size>\n\n";

        if (BoonManager.Instance == null || BoonManager.Instance.Owned.Count == 0)
            text += "<size=20><i>None yet. Clear a wave to visit the Altar of Tyche.</i></size>";
        else
            foreach (BoonManager.Boon b in BoonManager.Instance.Owned)
                text += $"<size=22><b>{b.god}: {b.name}</b></size>\n<size=17>{b.description}</size>\n\n";

        boonListText.text = text;
    }

    private void BuildMenu()
    {
        menuCanvas = UIBuilder.MakeCanvas("Altar Menu", transform, 40);

        Image dim = UIBuilder.Panel("Dim", menuCanvas, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.6f));
        UIBuilder.Stretch(dim.rectTransform);

        Image panel = UIBuilder.Panel("Panel", menuCanvas, Vector2.zero, new Vector2(1440, 900), UIBuilder.PanelColor);
        UIBuilder.SetBorder(panel, UIBuilder.Gold, 3f);
        Transform p = panel.transform;

        UIBuilder.Label("Title", p, "ALTAR OF TYCHE", 54, UIBuilder.Gold, TextAnchor.MiddleCenter, new Vector2(0, 395), new Vector2(1200, 70));
        subtitleText = UIBuilder.Label("Subtitle", p, "", 22, UIBuilder.Marble, TextAnchor.MiddleCenter, new Vector2(0, 345), new Vector2(1200, 34));

        UIBuilder.Label("Blessings Header", p, "BLESSINGS", 22, UIBuilder.Gold, TextAnchor.MiddleLeft, new Vector2(-560, 300), new Vector2(300, 30));
        boonCards = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            boonCards[i] = UIBuilder.Button($"Boon {i}", p, "", 20, new Vector2((i - 1) * 450f, 170f), new Vector2(420, 230), () => TakeBoon(index));
            Text t = boonCards[i].GetComponentInChildren<Text>();
            t.alignment = TextAnchor.UpperCenter;
            t.rectTransform.offsetMin = new Vector2(18, 14);
            t.rectTransform.offsetMax = new Vector2(-18, -16);
        }

        // Lucky Number row (only for Oracle's Decree)
        numberRow = UIBuilder.Rect("Lucky Number Row", p, new Vector2(0, 2f), new Vector2(1340, 110));
        numberRowLabel = UIBuilder.Label("Label", numberRow, "", 22, UIBuilder.Gold, TextAnchor.MiddleCenter, new Vector2(0, 40), new Vector2(1200, 30));
        numberRow.gameObject.SetActive(false);

        UIBuilder.Label("Weapons Header", p, "WEAPONS", 22, UIBuilder.Gold, TextAnchor.MiddleLeft, new Vector2(-560, -85), new Vector2(300, 30));
        weaponButtons = new Button[4];
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            weaponButtons[i] = UIBuilder.Button($"Weapon {i}", p, "", 18, new Vector2((i - 1.5f) * 335f, -200f), new Vector2(320, 190), () => PickWeapon(index));
            Text t = weaponButtons[i].GetComponentInChildren<Text>();
            t.alignment = TextAnchor.UpperCenter;
            t.rectTransform.offsetMin = new Vector2(14, 12);
            t.rectTransform.offsetMax = new Vector2(-14, -14);
        }

        nextWaveButton = UIBuilder.Button("Next Wave", p, "NEXT WAVE  >", 30, new Vector2(0, -370f), new Vector2(420, 76), NextWave);
        UIBuilder.Label("Hint", p, "Esc: step away from the altar", 16, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight, new Vector2(560, -370f), new Vector2(300, 30));

        menuCanvas.gameObject.SetActive(false);
    }

    private void BuildPromptAndBoonList()
    {
        promptCanvas = UIBuilder.MakeCanvas("Altar Prompt", transform, 15);
        promptText = UIBuilder.Label("Prompt", promptCanvas, "", 28, UIBuilder.Gold, TextAnchor.MiddleCenter, new Vector2(0, 180), new Vector2(1000, 50));
        promptText.rectTransform.anchorMin = promptText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        promptCanvas.gameObject.SetActive(false);

        boonListCanvas = UIBuilder.MakeCanvas("Boon List", transform, 30);
        Image panel = UIBuilder.Panel("Panel", boonListCanvas, Vector2.zero, new Vector2(560, 700), UIBuilder.PanelColor);
        panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        panel.rectTransform.pivot = new Vector2(0f, 0.5f);
        panel.rectTransform.anchoredPosition = new Vector2(40, 0);
        panel.raycastTarget = false;
        UIBuilder.SetBorder(panel, UIBuilder.Gold, 2f);
        boonListText = UIBuilder.Label("Text", panel.transform, "", 20, UIBuilder.Marble, TextAnchor.UpperLeft);
        UIBuilder.Stretch(boonListText.rectTransform, 24f);
        boonListCanvas.gameObject.SetActive(false);
    }

    // ======================================================================
    // Placement and placeholder model
    // ======================================================================

    private Vector3 FindLandingSpot()
    {
        Vector3 spot;
        if (landingPoint != null) spot = landingPoint.position;
        else if (spawner != null && spawner.spawnPoints != null && spawner.spawnPoints.Length > 0)
        {
            spot = Vector3.zero;
            foreach (Transform t in spawner.spawnPoints) spot += t.position;
            spot /= spawner.spawnPoints.Length;
        }
        else spot = player != null ? player.position + player.forward * 6f : Vector3.zero;

        // Drop it onto whatever floor is below (ignoring triggers, the player and enemies)
        RaycastHit[] hits = Physics.RaycastAll(spot + Vector3.up * 60f, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 ground = spot;
        foreach (RaycastHit h in hits)
        {
            Transform root = h.collider.transform.root;
            if (root.CompareTag("Player") || root.CompareTag("Enemy")) continue;
            if (h.distance < best) { best = h.distance; ground = h.point; }
        }
        return ground;
    }

    private GameObject BuildPlaceholderAltar()
    {
        GameObject root = new GameObject("AltarOfTyche (placeholder)");
        Color stone = new Color(0.82f, 0.8f, 0.75f);
        Color gold = UIBuilder.Gold;

        Part(PrimitiveType.Cylinder, root.transform, "Base", new Vector3(0, 0.2f, 0), new Vector3(2.4f, 0.2f, 2.4f), stone, false);
        Part(PrimitiveType.Cylinder, root.transform, "Step", new Vector3(0, 0.55f, 0), new Vector3(1.9f, 0.15f, 1.9f), stone, false);
        Part(PrimitiveType.Cylinder, root.transform, "Column", new Vector3(0, 1.6f, 0), new Vector3(0.9f, 0.9f, 0.9f), stone, false);
        Part(PrimitiveType.Cylinder, root.transform, "Top", new Vector3(0, 2.6f, 0), new Vector3(1.6f, 0.12f, 1.6f), gold, false);
        GameObject orbGO = Part(PrimitiveType.Sphere, root.transform, "Orb", new Vector3(0, 3.5f, 0), Vector3.one * 0.75f, gold, true);
        Destroy(orbGO.GetComponent<Collider>());

        Light glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(orbGO.transform, false);
        glow.type = LightType.Point;
        glow.color = gold;
        glow.intensity = 4f;
        glow.range = 10f;

        return root;
    }

    private static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color color, bool glow)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;

        Material m = go.GetComponent<Renderer>().material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (glow && m.HasProperty("_EmissionColor"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
        }
        return go;
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
