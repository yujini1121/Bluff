using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds serialized, editable UGUI objects only in the dedicated copied scene.
public static class RadialBettingSetupEditor
{
    public const string ScenePath = "Assets/_Game/Scenes/UI_Yujin.unity";
    private static readonly Color Pink = new Color(1, .36f, .73f);
    private static readonly Color Lilac = new Color(.72f, .57f, 1);
    private static readonly Color White = new Color(.95f, .91f, 1);
    private static TMP_FontAsset font;

    [MenuItem("Tools/Bluff/Build UI_Yujin Radial Betting")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
        {
            if (!AssetDatabase.CopyAsset("Assets/_Game/Scenes/Dev_Yujin.unity", ScenePath))
                throw new InvalidOperationException("Could not copy Dev_Yujin.");
        }
        var previousScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
        bool openedForBuild = !scene.IsValid() || !scene.isLoaded;
        if (openedForBuild) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        if (scene.isDirty) throw new InvalidOperationException("UI_Yujin has unsaved edits. Save it before rebuilding.");
        var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameplayView>(true)).Single();
        if (view == null) throw new InvalidOperationException("GameplayView missing.");
        var properties = new SerializedObject(view);
        font = properties.FindProperty("uiFont").objectReferenceValue as TMP_FontAsset;
        var oldBar = (GameObject)properties.FindProperty("playerActionBar").objectReferenceValue;
        oldBar.SetActive(false);
        foreach (var root in scene.GetRootGameObjects().Where(r => r.name == "Player Test Area")) root.SetActive(false);
        var old = view.transform.Find("RadialBetting");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rootRect = Rect("RadialBetting", view.transform, Vector2.zero, new Vector2(360, 360));
        rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(1, 0);
        rootRect.anchoredPosition = new Vector2(-28, 28);
        var radial = rootRect.gameObject.AddComponent<RadialBettingView>();
        var animation = rootRect.gameObject.AddComponent<RadialBettingAnimator>();
        Arc("Quarter / smoked violet", rootRect, 320, 320, 90, 180, new Color(.055f, .025f, .10f, .86f));
        Arc("Outer rim", rootRect, 320, 1.4f, 90, 180, new Color(.68f, .34f, .9f, .55f));
        var rotor = Full("Transition rim", rootRect);
        Arc("Accent", rotor, 328, 2, 113, 154, Pink * new Color(1, 1, 1, .65f));
        var actions = Full("Actions", rootRect).gameObject.AddComponent<CanvasGroup>();
        var raise = Full("Raise selection", rootRect).gameObject.AddComponent<CanvasGroup>();
        raise.alpha = 0;
        raise.interactable = raise.blocksRaycasts = false;

        var fold = ArcButton("Fold", actions.transform, 96, 119, "FOLD", Lilac);
        var raiseButton = ArcButton("Raise", actions.transform, 122, 146, "RAISE", Pink);
        var call = ArcButton("Call", actions.transform, 149, 174, "CALL\n<size=14>0 CHIPS</size>", White);
        Label("Brand", actions.transform, "B L U F F", new Vector2(-102, 167), new Vector2(160, 24), 14, Lilac);
        var status = Label("Status", actions.transform, "YOUR MOVE", new Vector2(-110, 134), new Vector2(180, 32), 23, White);
        Label("Hint", actions.transform, "CHOOSE AN ACTION", new Vector2(-110, 104), new Vector2(180, 22), 11, Lilac);
        Arc("Inner contour", actions.transform, 209, 1, 96, 174, new Color(.6f, .4f, .8f, .25f));

        var track = Arc("ArcSlider", raise.transform, 326, 48, 92, 178, new Color(0, 0, 0, 0));
        track.raycastTarget = true;
        var slider = track.gameObject.AddComponent<BettingArcSlider>();
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.targetGraphic = track;
        Arc("Track", track.transform, 304, 4, 96, 174, new Color(.3f, .19f, .4f, 1));
        for (int i = 0; i <= 10; i++)
        {
            float angle = Mathf.Lerp(174, 96, i / 10f);
            Arc("Tick " + i, track.transform, 315, 5, angle - .15f, angle + .15f, new Color(.7f, .5f, .9f, .55f));
        }
        var fill = Arc("Selected arc", track.transform, 304, 4, 174, 174, Pink);
        var handle = Rect("Handle", track.transform, BettingArcGraphic.Point(174, 302), new Vector2(14, 14));
        handle.localRotation = Quaternion.Euler(0, 0, 45);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = White;
        handleImage.raycastTarget = false;
        Set(slider, "fill", fill, "handle", handle);

        Label("Raise caption", raise.transform, "SET YOUR RAISE", new Vector2(-106, 237), new Vector2(182, 24), 13, Lilac);
        var amount = Label("Raise amount", raise.transform, "RAISE <color=#FF80CF>+1</color>", new Vector2(-120, 203), new Vector2(218, 42), 30, White);
        var total = Label("Total payment", raise.transform, "TOTAL  1  CHIPS", new Vector2(-120, 166), new Vector2(210, 28), 18, White);
        var range = Label("Limits", raise.transform, "MIN 1    /    MAX 20", new Vector2(-120, 137), new Vector2(205, 22), 12, Lilac);
        var confirm = Button("Confirm", raise.transform, "CONFIRM RAISE", new Vector2(-120, 98), new Vector2(198, 44), Pink);
        var back = Button("Back", raise.transform, "BACK", new Vector2(-173, 48), new Vector2(92, 36), Lilac);
        var max = Button("Max", raise.transform, "MAX", new Vector2(-67, 48), new Vector2(92, 36), Pink);
        Label("Input hint", raise.transform, "DRAG ARC  /  SCROLL ±1", new Vector2(-127, 15), new Vector2(228, 20), 10, Lilac);
        Set(animation, "actions", actions, "raise", raise, "rotor", rotor);
        Set(radial, "animator", animation, "slider", slider,
            "callButton", call, "raiseButton", raiseButton, "foldButton", fold,
            "confirmButton", confirm, "backButton", back, "maxButton", max,
            "callText", call.GetComponentInChildren<TMP_Text>(), "amountText", amount,
            "totalText", total, "rangeText", range, "confirmText", confirm.GetComponentInChildren<TMP_Text>(), "statusText", status);
        Set(view, "radialBettingView", radial);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        // The existing Restart button reloads by build index.
        if (!EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
        if (openedForBuild)
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previousScene.IsValid()) UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
        }
        Debug.Log("UI_Yujin radial betting UI built and wired. Dev_Yujin is untouched.");
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.gameObject.layer = 5;
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = new Vector2(1, 0);
        r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = position;
        r.sizeDelta = size;
        return r;
    }
    private static RectTransform Full(string name, Transform parent)
    {
        var r = Rect(name, parent, Vector2.zero, new Vector2(360, 360));
        r.pivot = new Vector2(1, 0);
        return r;
    }
    private static BettingArcGraphic Arc(string name, Transform parent, float radius, float width, float start, float end, Color color)
    {
        var graphic = Full(name, parent).gameObject.AddComponent<BettingArcGraphic>();
        graphic.color = color;
        graphic.raycastTarget = false;
        graphic.Configure(radius, width, start, end);
        return graphic;
    }
    private static TMP_Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        var t = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }
    private static Button ArcButton(string name, Transform parent, float start, float end, string title, Color color)
    {
        var g = Arc(name, parent, 308, 82, start, end, new Color(.13f, .065f, .21f, .97f));
        g.raycastTarget = true;
        var b = g.gameObject.AddComponent<Button>();
        Style(b, g);
        Arc("Edge", g.transform, 308, 2, start + 1, end - 1, color);
        Label("Label", g.transform, title, BettingArcGraphic.Point((start + end) * .5f, 265), new Vector2(110, 52), 21, color);
        return b;
    }
    private static Button Button(string name, Transform parent, string title, Vector2 position, Vector2 size, Color accent)
    {
        var r = Rect(name, parent, position, size);
        var g = r.gameObject.AddComponent<Image>();
        g.color = new Color(.18f, .08f, .25f, 1);
        var b = r.gameObject.AddComponent<Button>();
        Style(b, g);
        var edge = Rect("Accent", r, new Vector2(-size.x / 2, 1), new Vector2(size.x, 2)).gameObject.AddComponent<Image>();
        edge.color = accent;
        edge.raycastTarget = false;
        Label("Label", r, title, new Vector2(-size.x / 2, size.y / 2), size, 15, White);
        return b;
    }
    private static void Style(Button b, Graphic graphic)
    {
        b.targetGraphic = graphic;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = b.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.5f, 1.8f, 1);
        colors.pressedColor = new Color(2, 1.2f, 1.6f, 1);
        colors.disabledColor = new Color(.5f, .5f, .5f, .55f);
        colors.fadeDuration = .07f;
        b.colors = colors;
    }
    private static void Set(Object target, params object[] pairs)
    {
        var s = new SerializedObject(target);
        for (int i = 0; i < pairs.Length; i += 2)
            s.FindProperty((string)pairs[i]).objectReferenceValue = (Object)pairs[i + 1];
        s.ApplyModifiedPropertiesWithoutUndo();
    }
}
