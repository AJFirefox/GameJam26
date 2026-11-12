using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Small helpers for building placeholder UI in code (Altar menu, Game Over screen).
/// Everything made here is a normal UI object, so it can be restyled later.
/// </summary>
public static class UIBuilder
{
    public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);
    public static readonly Color Marble = new Color(0.96f, 0.94f, 0.9f);
    public static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.06f, 0.92f);
    public static readonly Color CardColor = new Color(0.18f, 0.16f, 0.13f, 1f);
    public static readonly Color LockedColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);

    private static Font font;
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    /// <summary>Makes sure clicks reach the UI (needs an EventSystem in the scene).</summary>
    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static RectTransform MakeCanvas(string name, Transform parent, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        if (parent != null) go.transform.SetParent(parent, false);
        Canvas c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;
        CanvasScaler s = go.GetComponent<CanvasScaler>();
        s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        s.referenceResolution = new Vector2(1920, 1080);
        s.matchWidthOrHeight = 0.5f;
        return go.GetComponent<RectTransform>();
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchoredPos, Vector2 size, Vector2? anchor = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = a;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return rt;
    }

    public static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        RectTransform rt = Rect(name, parent, pos, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        return img;
    }

    public static void Stretch(RectTransform rt, float padding = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }

    public static Text Label(string name, Transform parent, string text, int size, Color color,
                             TextAnchor align = TextAnchor.MiddleCenter, Vector2? pos = null, Vector2? box = null)
    {
        RectTransform rt = Rect(name, parent, pos ?? Vector2.zero, box ?? new Vector2(600, 60));
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Outline o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.7f);
        o.effectDistance = new Vector2(1.5f, -1.5f);
        return t;
    }

    /// <summary>A button with a background and a label filling it. Returns the Button; its label is button.GetComponentInChildren&lt;Text&gt;().</summary>
    public static Button Button(string name, Transform parent, string text, int fontSize, Vector2 pos, Vector2 size, Action onClick)
    {
        Image bg = Panel(name, parent, pos, size, CardColor);
        Button b = bg.gameObject.AddComponent<Button>();
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.92f, 0.7f);
        cb.pressedColor = new Color(0.85f, 0.75f, 0.5f);
        cb.selectedColor = new Color(1f, 0.92f, 0.7f);
        cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
        b.colors = cb;
        if (onClick != null) b.onClick.AddListener(() => onClick());

        Text label = Label("Label", bg.transform, text, fontSize, Marble);
        Stretch(label.rectTransform, 12f);

        // Gold border
        Outline border = bg.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
        border.effectDistance = new Vector2(2f, -2f);
        return b;
    }

    public static void SetBorder(Component c, Color color, float thickness = 2f)
    {
        Outline o = c.GetComponent<Outline>();
        if (o == null) o = c.gameObject.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(thickness, -thickness);
    }
}
