using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The 3 god reels The Jackpot spins on a Jackpot. Shows them at the top of the screen,
/// stops them one by one, then reports which god landed on each reel.
/// Added automatically by JackpotHammer.
/// </summary>
public class SlotReels : MonoBehaviour
{
    public float firstStopTime = 0.55f;
    public float timeBetweenStops = 0.3f;
    public float holdAfterStop = 0.9f;

    public bool IsSpinning { get; private set; }

    private RectTransform canvas;
    private CanvasGroup group;
    private Text[] reelTexts = new Text[3];
    private Image[] reelBacks = new Image[3];

    void Awake()
    {
        Build();
    }

    void OnDisable()
    {
        // Switching weapons mid-spin: stop cleanly so the reels work next time
        StopAllCoroutines();
        IsSpinning = false;
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    /// <summary>Spins the reels. rigged = all three land on the same god (Rigged Machine boon).</summary>
    public bool Spin(bool rigged, Action<God[]> onResult)
    {
        if (IsSpinning) return false;
        StartCoroutine(SpinRoutine(rigged, onResult));
        return true;
    }

    private IEnumerator SpinRoutine(bool rigged, Action<God[]> onResult)
    {
        IsSpinning = true;
        canvas.gameObject.SetActive(true);
        group.alpha = 1f;

        God[] result = new God[3];
        result[0] = GodEffects.All[UnityEngine.Random.Range(0, GodEffects.All.Length)];
        for (int i = 1; i < 3; i++)
            result[i] = rigged ? result[0] : GodEffects.All[UnityEngine.Random.Range(0, GodEffects.All.Length)];

        bool[] stopped = new bool[3];
        float t = 0f;
        float tick = 0f;
        int face = 0;

        while (!stopped[2])
        {
            t += Time.unscaledDeltaTime;
            tick += Time.unscaledDeltaTime;

            for (int i = 0; i < 3; i++)
            {
                if (!stopped[i] && t >= firstStopTime + i * timeBetweenStops)
                {
                    stopped[i] = true;
                    ShowGod(i, result[i], true);
                }
            }

            if (tick >= 0.06f)
            {
                tick = 0f;
                face++;
                for (int i = 0; i < 3; i++)
                    if (!stopped[i]) ShowGod(i, GodEffects.All[(face + i * 2) % GodEffects.All.Length], false);
            }
            yield return null;
        }

        onResult?.Invoke(result);

        yield return new WaitForSecondsRealtime(holdAfterStop);
        for (float f = 0f; f < 0.3f; f += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - f / 0.3f;
            yield return null;
        }
        canvas.gameObject.SetActive(false);
        IsSpinning = false;
    }

    private void ShowGod(int reel, God g, bool final)
    {
        Color c = GodEffects.Colour(g);
        reelTexts[reel].text = final
            ? $"<size=30><b>{GodEffects.Title(g)}</b></size>\n<size=18>{GodEffects.Blurb(g)}</size>"
            : $"<size=30>{GodEffects.Title(g)}</size>";
        reelTexts[reel].color = final ? c : new Color(c.r, c.g, c.b, 0.6f);
        reelBacks[reel].color = final ? new Color(0.25f, 0.2f, 0.08f, 0.95f) : new Color(0.1f, 0.09f, 0.08f, 0.9f);
        UIBuilder.SetBorder(reelBacks[reel], final ? UIBuilder.Gold : new Color(1f, 0.82f, 0.25f, 0.3f), final ? 4f : 2f);
    }

    private void Build()
    {
        canvas = UIBuilder.MakeCanvas("Slot Reels", transform, 20);
        group = canvas.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        for (int i = 0; i < 3; i++)
        {
            Image back = UIBuilder.Panel($"Reel {i}", canvas, new Vector2((i - 1) * 250f, -250f), new Vector2(230, 110), UIBuilder.CardColor);
            back.rectTransform.anchorMin = back.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            back.raycastTarget = false;
            reelBacks[i] = back;
            Text t = UIBuilder.Label("Text", back.transform, "", 28, UIBuilder.Marble);
            UIBuilder.Stretch(t.rectTransform, 6f);
            reelTexts[i] = t;
        }
        canvas.gameObject.SetActive(false);
    }
}
