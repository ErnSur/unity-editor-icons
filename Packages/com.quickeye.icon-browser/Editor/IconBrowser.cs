using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static UnityEngine.GUILayout;
using Random = UnityEngine.Random;

namespace QuickEye.Editor
{
    // copy PNG to clipboard option
    // context click to context menu:
    // Copy: Name,FileID,PNG,GUIContent Expression
    public class IconBrowser : EditorWindow, IHasCustomMenu
    {
        private const string EditorPrefsKey = "quickeye.icon-browser/browser-state";

        private static string[] iconBlacklist =
        {
            "StateMachineEditor.Background",
            "scene-template-empty-scene",
            "scene-template-2d-scene",
        };

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

        [SerializeField]
        private string searchString;

        [SerializeField]
        private Sorting sortingMode;

        [SerializeField]
        private Layout layout;

        [SerializeField]
        private bool debugMode;

        [SerializeField]
        private EfficientScrollView listView = new EfficientScrollView();

        private SearchField searchField;
        private IconBrowserDatabase database;
        private Rect sortingButtonRect;
        private float iconSize = 40;
        private readonly (float min, float max) iconSizeLimit = (16, 60);
        private int elementsInRow;
        private float elementWidth;

        private bool HasSearch => !string.IsNullOrWhiteSpace(searchString);
        private Texture2D[] Icons => HasSearch ? database.searchResult : database.icons;

        private void OnEnable()
        {
            database = new IconBrowserDatabase();
            searchField = new SearchField();
            Sort(sortingMode);
            UpdateLayout();
        }

        private void OnGUI()
        {
            if (Event.current.alt)
            {
                EditorGUI.DrawRect(new Rect(Vector2.zero, position.size), AlternativeSkinBackgroundColor);
            }

            DrawToolbar();
            DrawIcons(Icons);

            DrawDebugView();
        }

        private void DrawListElement(Rect rect, int index)
        {
            var icon = Icons[index];
            var iconContent = new GUIContent(icon);
            var textContent = new GUIContent(icon.name);
            using (KeepIconAspectRatio(icon, new Vector2(iconSize, iconSize)))
            {
                var iconRect = new Rect(rect) { size = new Vector2(iconSize + 4, iconSize + 4) };
                GUI.Label(iconRect, iconContent);
                var labelRect = new Rect(rect)
                {
                    xMin = iconRect.xMax
                };
                var labelStyle = new GUIStyle(EditorStyles.largeLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                };

                GUI.Label(labelRect, textContent, labelStyle);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                {
                    OnIconClick(icon);
                }
            }
        }

        private void DrawGridElement(Rect rect, int rowIndex)
        {
            var iconCount = Icons.Length;
            var style = new GUIStyle("label");
            elementWidth = iconSize + style.padding.horizontal + 1; // + style.margin.right;
            var eInRow = rect.width / elementWidth;
            elementsInRow = Mathf.FloorToInt(eInRow);

            var index = rowIndex * elementsInRow;

            Debug.Log($"MES: {rect.width}/{elementWidth}={eInRow} === {index}");
            for (var j = 0; j < elementsInRow && index < iconCount; j++, index++)
            {
                var icon = Icons[index];
                var content = new GUIContent(icon, icon.name);

                using (KeepIconAspectRatio(icon, new Vector2(iconSize, iconSize)))
                {
                    var buttonRect = new Rect();
                    buttonRect.width = buttonRect.height = elementWidth;
                    buttonRect.y = rect.y;
                    buttonRect.x = j * elementWidth;
                    if (GUI.Button(buttonRect, content, style))
                        OnIconClick(icon);
                }
            }
        }

        private int GetGridRowCount()
        {
            var iconCount = Icons.Length;
            var style = new GUIStyle("label");
            elementWidth = iconSize + style.padding.horizontal + 1; // + style.margin.right;
            elementsInRow = Mathf.FloorToInt(listView.ScrollViewRect.width / elementWidth);
            var x = Mathf.CeilToInt((float)iconCount / elementsInRow);
            return x;
        }

        private void DrawDebugView()
        {
            if (!debugMode)
                return;
            for (int i = 0; i < elementsInRow; i++)
            {
                var pos = new Vector2(i * elementWidth, 0);
                EditorGUI.DrawRect(new Rect(pos, new Vector2(elementWidth, 5)), Random.ColorHSV());
            }
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
                        database.UpdateBySearch(searchString);
                }

                FlexibleSpace();
                iconSize = HorizontalSlider(iconSize, iconSizeLimit.min, iconSizeLimit.max, MaxWidth(100),
                    MinWidth(55));
            }
        }

        private void ViewModeButton()
        {
            var icon = layout == Layout.Grid ? "GridLayoutGroup Icon" : "VerticalLayoutGroup Icon";
            if (Button(EditorGUIUtility.IconContent(icon), EditorStyles.toolbarButton, Width(30)))
            {
                layout = layout == Layout.Grid ? Layout.List : Layout.Grid;
                UpdateLayout();
            }
        }

        private void SortingButton()
        {
            if (Button("Sorting", EditorStyles.toolbarDropDown, Width(60)))
            {
                var menu = new GenericMenu();

                menu.AddItem(new GUIContent("Name"), sortingMode == Sorting.Name, () => Sort(Sorting.Name));
                menu.AddItem(new GUIContent("Color"), sortingMode == Sorting.Color, () => Sort(Sorting.Color));

                menu.DropDown(sortingButtonRect);
            }

            if (Event.current.type == EventType.Repaint)
                sortingButtonRect = GUILayoutUtility.GetLastRect();
        }

        private void Sort(Sorting newSorting)
        {
            sortingMode = newSorting;
            switch (sortingMode)
            {
                case Sorting.Name:
                    database.SortByName();
                    break;
                case Sorting.Color:
                    database.SortByColor();
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void DrawIcons(Texture2D[] icons)
        {
            if (Event.current.type == EventType.Layout)
                listView.rowCount = layout == Layout.List ? icons.Length : GetGridRowCount();
            var style = EditorStyles.label;
            listView.ElementHeight = iconSize + style.padding.vertical + style.margin.vertical;
            listView.OnGUI();
        }

        private void UpdateLayout()
        {
            listView.DrawElement = layout == Layout.Grid ? DrawGridElement : DrawListElement;
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

        [Serializable]
        private enum Sorting
        {
            Name,
            Color
        }

        [Serializable]
        private enum Layout
        {
            Grid,
            List
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Debug Mode"), debugMode, () => debugMode ^= true);
        }
    }
}