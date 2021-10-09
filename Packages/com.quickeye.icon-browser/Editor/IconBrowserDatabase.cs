using System;
using System.Linq;
using UnityEngine;

namespace QuickEye.Editor
{
    public class IconBrowserDatabase
    {
        public Texture2D[] icons;
        public Texture2D[] searchResult;

        public IconBrowserDatabase()
        {
            GetIcons();
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

        public void SortByColor()
        {
            icons = (from icon in icons
                    let hsv = GetIconAverageHSV(icon)
                    orderby hsv.h, hsv.s, hsv.v
                    select icon
                ).ToArray();
        }

        public void SortByName()
        {
            icons = (from icon in icons
                    orderby icon.name
                    select icon
                ).ToArray();
        }

        public void UpdateBySearch(string searchString)
        {
            searchResult = (from icon in icons
                    let lowerName = icon.name.ToLower()
                    let lowerSearch = searchString.ToLower()
                    where lowerName.Contains(lowerSearch)
                    orderby lowerName.IndexOf(lowerSearch, StringComparison.Ordinal)
                    select icon
                ).ToArray();
        }

        private static (float h, float s, float v) GetIconAverageHSV(Texture2D icon)
        {
            var readableTexture = new Texture2D(icon.width, icon.height, icon.format, icon.mipmapCount > 1);
            Graphics.CopyTexture(icon, readableTexture);
            var averageColor = AverageColorFromTexture(readableTexture);
            Color.RGBToHSV(averageColor, out var h, out var s, out var v);
            UnityEngine.Object.DestroyImmediate(readableTexture);
            return (h, s, v);
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
    }
}