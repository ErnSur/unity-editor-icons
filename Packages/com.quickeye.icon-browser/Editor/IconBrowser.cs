using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static UnityEngine.GUILayout;
using Random = UnityEngine.Random;

namespace QuickEye.Editor
{
// add list view and grid view
// copy PNG to clipboard option
// context click to context menu:
// Copy: Name,FileID,PNG,GUIContent Expression
    public class IconBrowser : EditorWindow, IHasCustomMenu
    {
        private const string EditorPrefsKey = "quickeye.icon-browser/browser-state";

        [MenuItem("Window/Icon Browser")]
        private static void OpenWindow()
        {
            var w = GetWindow<IconBrowser>("Icon Browser");
            w.titleContent.image = EditorGUIUtility.IconContent("Search Icon").image;
        }

        private static readonly Color LightSkinColor = new Color32(194, 194, 194, 255);
        private static readonly Color DarkSkinColor = new Color32(56, 56, 56, 255);

        private static Color AlternativeSkinBackgroundColor =>
            EditorGUIUtility.isProSkin ? LightSkinColor : DarkSkinColor;

        [SerializeField] private Sorting sortingMode;

        [SerializeField] private ViewMode viewMode;

        [SerializeField] private bool debugMode;

        private string[] iconBlacklist =
        {
            "StateMachineEditor.Background",
            "scene-template-empty-scene",
            "scene-template-2d-scene",
        };

        private Texture2D[] icons;
        private float iconSize = 40;
        private readonly (float min, float max) iconSizeLimit = (16, 60);
        private Vector2 scrollPos;
        private SearchField searchField;
        private Texture2D[] searchResult;
        private string searchString;

        private Rect sortingButtonRect;
        private bool HasSearch => !string.IsNullOrWhiteSpace(searchString);

        private void OnEnable()
        {
            Debug.Log("onEnable");
            LoadFromPrefs();
            searchField = new SearchField();
            GetIcons();
            Sort();
        }

        private void LoadFromPrefs()
        {
            if (!EditorPrefs.HasKey(EditorPrefsKey))
                return;
            try
            {
                JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(EditorPrefsKey), this);
            }
            catch
            {
            }
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(this));
        }

        private void OnGUI()
        {
            if (Event.current.alt)
            {
                EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), AlternativeSkinBackgroundColor);
            }

            DrawToolbar();

            for (int i = 0; i < rowSize; i++)
            {
                var pos = new Vector2(i * elementWidth, 0);
                EditorGUI.DrawRect(new Rect(pos, new Vector2(elementWidth, 5)), Random.ColorHSV());
            }

            var collection = HasSearch ? searchResult : icons;

            if (viewMode == ViewMode.Grid)
                DrawGridIcons(collection);
            else
                DrawListIcons(collection);
        }


        private void DrawToolbar()
        {
            using (new HorizontalScope(EditorStyles.toolbar, ExpandWidth(true)))
            {
                SortingButton();
                ViewModeButton();
                using (var s = new EditorGUI.ChangeCheckScope())
                {
                    searchString = searchField.OnToolbarGUI(searchString, MaxWidth(200));
                    if (s.changed)
                        UpdateBySearch();
                }

                if (Button("deb", EditorStyles.toolbarButton))
                {
                    Debug.Log($"Size: {position.size}, Row: {rowSize},eleW: {elementWidth}");
                }

                FlexibleSpace();
                iconSize = HorizontalSlider(iconSize, iconSizeLimit.min, iconSizeLimit.max, MaxWidth(100),
                    MinWidth(55));
            }
        }

        private void ViewModeButton()
        {
            var icon = viewMode == ViewMode.Grid ? "GridLayoutGroup Icon" : "VerticalLayoutGroup Icon";
            if (Button(EditorGUIUtility.IconContent(icon), EditorStyles.toolbarButton, Width(30)))
            {
                viewMode = viewMode == ViewMode.Grid ? ViewMode.List : ViewMode.Grid;
            }
        }

        private void SortingButton()
        {
            if (Button("Sorting", EditorStyles.toolbarDropDown, Width(60)))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Name"), sortingMode == Sorting.Name, SortByName);
                menu.AddItem(new GUIContent("Color"), sortingMode == Sorting.Color, SortByColor);

                menu.DropDown(sortingButtonRect);
            }

            if (Event.current.type == EventType.Repaint)
                sortingButtonRect = GUILayoutUtility.GetLastRect();
        }

        private void GetIcons()
        {
            var allIcons = AssetDatabaseUtil.GetAllEditorIcons();
            var doubles = (from icon in allIcons
                where icon.name.EndsWith("@2x")
                where icon.name.StartsWith("d_")
                select icon).ToArray();
            // foreach (var d in doubles)
            // {
            //     Debug.Log($"{d.name}");
            // }

            var doubleNames = doubles.Select(d => d.name.Replace("@2x", "").Replace("d_", "")).ToArray();
            icons = (from icon in AssetDatabaseUtil.GetAllEditorIcons()
                    //where !icon.name.EndsWith("@2x")
                    //where !icon.name.StartsWith("d_")
                    //where !doubleNames.Contains(icon.name)
                    select icon
                ).ToArray()
                //.Concat(doubles).ToArray();
                ;
        }

        private void Sort()
        {
            switch (sortingMode)
            {
                case Sorting.Name:
                    SortByName();
                    break;
                case Sorting.Color:
                    SortByColor();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void SortByColor()
        {
            sortingMode = Sorting.Color;
            icons = (from icon in icons
                    let hsv = GetIconAverageHSV(icon)
                    orderby hsv.h, hsv.s, hsv.v
                    select icon
                ).ToArray();
        }

        private void SortByName()
        {
            sortingMode = Sorting.Name;
            icons = (from icon in icons
                    orderby icon.name
                    select icon
                ).ToArray();
        }

        private void UpdateBySearch()
        {
            searchResult = (from icon in icons
                    let lowerName = icon.name.ToLower()
                    let lowerSearch = searchString.ToLower()
                    where lowerName.Contains(lowerSearch)
                    orderby lowerName.IndexOf(lowerSearch)
                    select icon
                ).ToArray();
        }

        private static (float h, float s, float v) GetIconAverageHSV(Texture2D icon)
        {
            var readableTexture = new Texture2D(icon.width, icon.height, icon.format, icon.mipmapCount > 1);
            Graphics.CopyTexture(icon, readableTexture);
            var averageColor = AverageColorFromTexture(readableTexture);
            Color.RGBToHSV(averageColor, out var h, out var s, out var v);
            DestroyImmediate(readableTexture);
            return (h, s, v);
        }

        private void DrawListIcons(Texture2D[] icons)
        {
            var len = icons.Length;
            var style = EditorStyles.label;
            var elementWidth = iconSize + style.padding.horizontal + style.margin.horizontal;
            using (var s = new ScrollViewScope(scrollPos))
            {
                for (var i = 0; i < len; i++)
                    using (new HorizontalScope(Height(iconSize)))
                    {
                        Space(5);
                        var icon = icons[i];
                        var iconContent = new GUIContent(icon);
                        var textContent = new GUIContent(icon.name);
                        using (KeepIconAspectRatio(icon, new Vector2(iconSize, iconSize)))
                        {
                            Label(iconContent, Width(iconSize + 4));
                            // if (Button(iconContent, "label", ExpandHeight(true)))
                            // {
                            //    OnIconClick(icon);
                            // }
                        }

                        Space(5);
                        Label(textContent, EditorStyles.largeLabel);
                    }

                scrollPos = s.scrollPosition;
            }
        }

        private int rowSize;
        private float elementWidth;

        private void DrawGridIcons(Texture2D[] collection)
        {
            var len = collection.Length;
            GUIStyle style = "label";
            elementWidth = iconSize + style.padding.horizontal + 1; // + style.margin.right;
            rowSize = Mathf.FloorToInt((position.width - 12) / elementWidth);
            using (var s = new ScrollViewScope(scrollPos))
            {
                for (var i = 0; i < len; i += rowSize)
                    using (new HorizontalScope(Height(iconSize)))
                    {
                        for (var j = 0; j < rowSize && i + j < len; j++)
                        {
                            var icon = collection[i + j];
                            var content = new GUIContent(icon, icon.name);

                            using (KeepIconAspectRatio(icon, new Vector2(iconSize, iconSize)))
                            {
                                if (Button(content, style, MaxWidth(iconSize), Height(iconSize), ExpandWidth(true)))
                                    OnIconClick(icon);
                            }
                        }
                    }

                scrollPos = s.scrollPosition;
            }
        }

        private static void OnIconClick(Texture2D icon)
        {
            GUIUtility.systemCopyBuffer = icon.name;

            Debug.Log($"Icon Name: <b>{icon.name}</b> FileID: <b>{AssetDatabaseUtil.GetFileId(icon)}</b>");
        }

        private static EditorGUIUtility.IconSizeScope KeepIconAspectRatio(Texture icon, Vector2 size)
        {
            if (icon.width > icon.height)
            {
                var r = icon.width / size.x;
                size.y = icon.height / r;
            }
            else
            {
                var r = icon.height / size.y;
                size.x = icon.width / r;
            }

            return new EditorGUIUtility.IconSizeScope(size);
        }

        private static Color32 AverageColorFromTexture(Texture2D tex)
        {
            var texColors = tex.GetPixels32();
            var total = texColors.Length;

            float r = 0;
            float g = 0;
            float b = 0;

            for (var i = 0; i < total; i++)
            {
                r += texColors[i].r;
                g += texColors[i].g;
                b += texColors[i].b;
            }

            return new Color32((byte)(r / total), (byte)(g / total), (byte)(b / total), 255);
        }

        [Serializable]
        private enum Sorting
        {
            Name,
            Color
        }

        [Serializable]
        private enum ViewMode
        {
            Grid,
            List
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Debug Mode"), debugMode, () => debugMode ^= true);
            menu.AddItem(new GUIContent("Reset Settings"), false, ResetSettings);
        }

        private void ResetSettings()
        {
            var defaultState = CreateInstance<IconBrowser>();
            Debug.Log("default created23 ");
            //JsonUtility.FromJsonOverwrite(EditorPrefs.GetString(EditorPrefsKey), this);
        }
    }
}