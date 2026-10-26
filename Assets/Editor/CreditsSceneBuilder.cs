using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Tools > UI > Create Credits Scene: builds Assets/Scenes/Credits.unity (background, logo, scrolling credits, back button)
/// and adds it to the Build Settings. The names live in the CreditsScroller component, edit them there.
/// </summary>
public static class CreditsSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Credits.unity";
    private const string BackgroundPath = "Assets/Sprites/MainMenuBackground.png";
    private const string LogoPath = "Assets/Sprites/Logo_SantoReposo_Blanco.png";
    private const string TitleFontPath = "Assets/Fonts/WideNoise SDF.asset";
    private const string NameFontPath = "Assets/Fonts/iknowaghost SDF.asset";

    [MenuItem("Tools/UI/Create Credits Scene")]
    public static void Create()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Credits scene", $"{ScenePath} already exists. Replace it?", "Replace", "Cancel"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateEventSystem();
        RectTransform canvas = CreateCanvas();

        CreateBackground(canvas);
        CreateLogo(canvas);
        RectTransform viewport = CreateViewport(canvas, out RectTransform content);
        CreditsScroller scroller = CreateScroller(viewport, content);
        CreateBackButton(canvas, scroller);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log($"Credits scene created at {ScenePath}. Press Play to test it.");
    }

    private static void CreateCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private static RectTransform CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return go.GetComponent<RectTransform>();
    }

    private static void CreateBackground(RectTransform canvas)
    {
        Sprite sprite = LoadSprite(BackgroundPath);

        // Covers the whole screen without distortion; the sides are cropped on narrower screens.
        var bg = CreateImage("Background", canvas, sprite, Color.white);
        var fitter = bg.gameObject.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;

        var dim = CreateImage("Dim", canvas, null, new Color(0f, 0f, 0f, 0.6f));
        Stretch(dim.rectTransform);
    }

    private static void CreateLogo(RectTransform canvas)
    {
        var logo = CreateImage("Logo", canvas, LoadSprite(LogoPath), Color.white);
        logo.preserveAspect = true;

        RectTransform rt = logo.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(520f, 310f);
    }

    private static RectTransform CreateViewport(RectTransform canvas, out RectTransform content)
    {
        var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
        viewportGo.transform.SetParent(canvas, false);
        var viewport = viewportGo.GetComponent<RectTransform>();
        viewport.anchorMin = new Vector2(0.1f, 0.08f);
        viewport.anchorMax = new Vector2(0.9f, 0.66f);
        viewport.offsetMin = viewport.offsetMax = Vector2.zero;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(viewport, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 12f;

        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return viewport;
    }

    private static CreditsScroller CreateScroller(RectTransform viewport, RectTransform content)
    {
        var go = new GameObject("CreditsScroller");
        var scroller = go.AddComponent<CreditsScroller>();

        var so = new SerializedObject(scroller);
        so.FindProperty("viewport").objectReferenceValue = viewport;
        so.FindProperty("content").objectReferenceValue = content;
        so.FindProperty("titleFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        so.FindProperty("nameFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(NameFontPath);

        // Titles use the menu font in capitals without accents (it has no accented glyphs); names use a font with accents.
        SetSections(so.FindProperty("sections"),
            ("PROGRAMACION", new[] { "Juls Sánchez", "Jorge Daniel Guerrero", "Shige Sánchez", "Pablo Gutiérrez", "Oscar Vázquez Guerra" }),
            ("ARTE", new[] { "Abraham Calderón", "Karina Porras" }),
            ("AUDIO", new[] { "Erick González" }));
        so.ApplyModifiedPropertiesWithoutUndo();
        return scroller;
    }

    private static void SetSections(SerializedProperty sections, params (string title, string[] names)[] data)
    {
        sections.arraySize = data.Length;
        for (int i = 0; i < data.Length; i++)
        {
            SerializedProperty section = sections.GetArrayElementAtIndex(i);
            section.FindPropertyRelative("title").stringValue = data[i].title;

            SerializedProperty names = section.FindPropertyRelative("names");
            names.arraySize = data[i].names.Length;
            for (int n = 0; n < data[i].names.Length; n++)
                names.GetArrayElementAtIndex(n).stringValue = data[i].names[n];
        }
    }

    private static void CreateBackButton(RectTransform canvas, CreditsScroller scroller)
    {
        Image image = CreateImage("Back Button", canvas, null, new Color(1f, 1f, 1f, 0.15f));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        image.gameObject.AddComponent<ButtonHoverScale>();
        UnityEventTools.AddVoidPersistentListener(button.onClick, scroller.Back);

        RectTransform rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 40f);
        rt.sizeDelta = new Vector2(260f, 70f);

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(rt, false);
        Stretch(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "VOLVER";
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TitleFontPath);
        label.fontSize = 30f;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Sprite LoadSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogWarning($"Credits scene: sprite not found at {path}.");
        return sprite;
    }

    private static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.All(s => s.path != ScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        if (scenes.All(s => !s.path.EndsWith("/MainMenu.unity")))
            Debug.LogWarning("Credits scene: MainMenu is not in the Build Settings, so the Back button will not find it. Add it from File > Build Profiles.");
    }
}
