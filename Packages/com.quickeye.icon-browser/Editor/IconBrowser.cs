using System;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using static UnityEngine.GUILayout;
using Random = UnityEngine.Random;

namespace QuickEye.Editor
{
    public class IconBrowser : EditorWindow, IHasCustomMenu
    {
        [MenuItem("Window/Icon Browser")]
        private static void OpenWindow()
        {
            var w = GetWindow<IconBrowser>("Icon Browser");
            w.titleContent.image = EditorGUIUtility.IconContent("Search Icon").image;
        }

        private static readonly Color LightSkinColor = new Color32(194, 194, 194, 255);
        private static readonly Color DarkSkinColor = new Color32(56, 56, 56, 255);
        private static readonly Color SelectedColor = new Color32(44, 93, 135, 255);

        private static Color BackgroundColor =>
            EditorGUIUtility.isProSkin ? DarkSkinColor : LightSkinColor;

        private static Color AlternativeSkinBackgroundColor =>
            EditorGUIUtility.isProSkin ? LightSkinColor : DarkSkinColor;

        private Color SelectedBackgroundColor =>
            drawAlternativeBackground ? AlternativeSkinBackgroundColor : BackgroundColor;

        [SerializeField]
        private string searchString = "";

        [SerializeField]
        private Sorting sortingMode;

        [SerializeField]
        private Layout layout;

        [SerializeField]
        private bool debugMode;

        [SerializeField]
        private bool drawAlternativeBackground;

        [SerializeField]
        private EfficientScrollView listView = new EfficientScrollView();

        private SearchField searchField;
        private IconBrowserDatabase database;
        private Rect sortingButtonRect;
        private float iconSize = 40;
        private readonly (float min, float max) iconSizeLimit = (16, 60);
        private int elementsInRow;
        private float elementWidth;
        private SelectedItem selectedItem;
        private bool HasSearch => !string.IsNullOrWhiteSpace(searchString);
        private Texture2D[] Icons => HasSearch ? database.SearchResult : database.Icons;

        private void OnEnable()
        {
            searchField = new SearchField();
            database = new IconBrowserDatabase(searchString);
            Sort(sortingMode);
            UpdateLayout();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawIcons();
            DrawFooter();
            DrawDebugView();
        }

        private void OnIconClick(Texture2D icon)
        {
            selectedItem = new SelectedItem(icon);
        }

        private void DrawFooter()
        {
            if (selectedItem == null)
                return;
            using (new HorizontalScope("In BigTitle", Height(40)))
            using (KeepIconAspectRatio(selectedItem.icon, new Vector2(40, 40)))
            {
                Label(selectedItem.icon, Width(40));
                using (new VerticalScope())
                {
                    Field("Name", selectedItem.name);
                    Field("File ID", selectedItem.fileId.ToString());
                }

                using (new VerticalScope(Width(50)))
                {
                    if (Button("Save"))
                        ExportSelectedIcon();
                    if (Button("Icon Content"))
                        CopyToClipboard("Icon Content", $"EditorGUIUtility.IconContent(\"{selectedItem.name}\")");
                }
            }


            void Field(string label, string value)
            {
                using (new HorizontalScope())
                {
                    Label($"{label}: ", Width(45));
                    if (Button(GUIContent.none, "label", Height(15)))
                        CopyToClipboard(label, value);

                    var rect = GUILayoutUtility.GetLastRect();
                    GUI.Label(rect, value);
                }
            }
        }

        private void CopyToClipboard(string valueName, string value)
        {
            ShowNotification(new GUIContent($"Copied {valueName}"), .2f);
            GUIUtility.systemCopyBuffer = value;
        }

        private void ExportSelectedIcon()
        {
            var path = EditorUtility.SaveFilePanel("Save icon", "Assets", selectedItem.name, "png");
            if (string.IsNullOrEmpty(path))
                return;
            TextureUtils.ExportIconToPath(path, selectedItem.icon);
            if (path.StartsWith(Application.dataPath))
            {
                path = path.Remove(0, Application.dataPath.Length);
                AssetDatabase.ImportAsset($"Assets{path}");
            }
        }

        private void DrawListElement(Rect rect, int index)
        {
            var icon = Icons[index];
            var iconContent = new GUIContent(icon);
            var textContent = new GUIContent(icon.name);
            using (KeepIconAspectRatio(icon, new Vector2(iconSize, iconSize)))
            {
                var iconRect = new Rect(rect) { size = new Vector2(iconSize + 4, iconSize + 4) };
                DrawSelectedBox(rect, icon);
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

        private void DrawSelectedBox(Rect rect, Texture2D icon)
        {
            if (selectedItem?.icon == icon)
            {
                EditorGUI.DrawRect(rect, SelectedColor);
                var mRect = new Rect(rect);
                mRect.max -= new Vector2(2, 2);
                mRect.min += new Vector2(2, 2);
                EditorGUI.DrawRect(mRect, SelectedBackgroundColor);
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

            for (var j = 0; j < elementsInRow && index < iconCount; j++, index++)
            {
                var icon = Icons[index];
                var content = new GUIContent(icon);

                using (KeepIconAspectRatio(icon, new Vector2(iconSize, iconSize)))
                {
                    var buttonRect = new Rect();
                    buttonRect.width = buttonRect.height = elementWidth;
                    buttonRect.y = rect.y;
                    buttonRect.x = j * elementWidth;
                    DrawSelectedBox(buttonRect, icon);

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
                var (lightOn, lightOff) = EditorGUIUtility.isProSkin
                    ? ("SceneViewLighting On", "SceneViewLighting")
                    : ("SceneViewLighting", "SceneViewLighting On");
                var icon = drawAlternativeBackground ? lightOn : lightOff;
                if (Button(EditorGUIUtility.IconContent(icon), EditorStyles.toolbarButton, Width(30)))
                {
                    drawAlternativeBackground = !drawAlternativeBackground;
                }

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

        private void DrawIcons()
        {
            listView.RowCount = layout == Layout.List ? Icons.Length : GetGridRowCount();
            var style = EditorStyles.label;
            listView.ElementHeight = iconSize + style.padding.vertical + style.margin.vertical;
            if (drawAlternativeBackground)
                EditorGUI.DrawRect(listView.ScrollViewRect, AlternativeSkinBackgroundColor);
            listView.OnGUI();
        }

        private void UpdateLayout()
        {
            listView.DrawElement = layout == Layout.Grid ? DrawGridElement : DrawListElement;
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

        [Serializable]
        private class SelectedItem
        {
            public string name;
            public long fileId;
            public Texture2D icon;

            public SelectedItem(Texture2D icon)
            {
                name = icon.name;
                fileId = AssetDatabaseUtil.GetFileId(icon);
                this.icon = icon;
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Debug Mode"), debugMode, () => debugMode ^= true);
        }
    }
}