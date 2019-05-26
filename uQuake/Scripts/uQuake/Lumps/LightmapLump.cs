using UnityEngine;

namespace SharpBSP
{
    public struct LightmapLump
    {
        public LightmapLump(int lightmapCount)
        {
            lightMaps = new Texture2D[lightmapCount];
        }

        public Texture2D[] lightMaps;

        private static byte CalcLight(byte color)
        {
            int icolor = color;
            //icolor += 200;

            if (icolor > 255) icolor = 255;

            return (byte) icolor;
        }

        public static Texture2D CreateLightMap(byte[] rgb)
        {
            Texture2D tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            Color32[] colors = new Color32[128 * 128];
            int j = 0;
            for (int i = 0; i < 128 * 128; i++)
                colors[i] = new Color32(CalcLight(rgb[j++]), CalcLight(rgb[j++]), CalcLight(rgb[j++]), (byte) 1f);
            tex.SetPixels32(colors);
            tex.Apply();
            return tex;
        }
    }
}