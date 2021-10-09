﻿using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace QuickEye.Editor
{
    [Serializable]
    public class EfficientScrollView
    {
        [SerializeField]
        private Vector2 scrollPos;

        public Rect ScrollViewRect { get; private set; }
        public float ElementHeight { get; set; }

        public Action<Rect, int> DrawElement { get; set; }
        public int rowCount;

        public void OnGUI()
        {
            ScrollViewRect =
                GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true),
                    GUILayout.ExpandWidth(true));
            var viewRect = new Rect(ScrollViewRect)
            {
                height = rowCount * ElementHeight,
                width = ScrollViewRect.width - GUI.skin.verticalScrollbar.fixedWidth
            };
            var visibleRowCount = Mathf.CeilToInt(ScrollViewRect.height / ElementHeight) + 2;
            var listIndex = Mathf.FloorToInt(scrollPos.y / ElementHeight);

            var elementSize = new Vector2(ScrollViewRect.width, ElementHeight);

            using (var s = new GUI.ScrollViewScope(ScrollViewRect, scrollPos, viewRect))
            using (new GUI.GroupScope(viewRect))
            {
                for (var i = 0; i < visibleRowCount && listIndex < rowCount; i++, listIndex++)
                {
                    var pos = new Vector2(0, ElementHeight * listIndex);
                    var elementRect = new Rect
                    {
                        position = pos,
                        size = elementSize
                    };
                    DrawElement?.Invoke(elementRect, listIndex);
                }

                scrollPos = s.scrollPosition;
            }

            // var debugText = $"{scrollPos} | visible: {listIndex + visibleRowCount}/{rowCount}/{visibleRowCount}";
            // GUI.Label(new Rect(0, 20, 1000, 20), debugText, EditorStyles.boldLabel);
        }
    }
}