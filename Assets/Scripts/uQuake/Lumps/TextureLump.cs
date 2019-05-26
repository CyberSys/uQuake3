using System.Collections.Generic;
using System.IO;
using System.Text;
using Ionic.Crc;
using Ionic.Zip;
using UnityEngine;

namespace SharpBSP
{
    public class TextureLump
    {
        private readonly Dictionary<string, Texture2D> readyTextures = new Dictionary<string, Texture2D>();

        public TextureLump(int textureCount)
        {
            Textures = new Texture[textureCount];
        }

        public Texture[] Textures { get; set; }

        public int TextureCount => Textures.Length;

        public bool ContainsTexture(string textureName)
        {
            return readyTextures.ContainsKey(textureName);
        }

        public Texture2D GetTexture(string textureName)
        {
            return readyTextures[textureName];
        }

        public void PullInTextures(string pakName)
        {
            using (ZipFile pak = ZipFile.Read(Path.Combine(Application.streamingAssetsPath, pakName)))
            {
                LoadJPGTextures(pak);
                LoadTGATextures(pak);
            }
        }

        private void LoadJPGTextures(ZipFile pk3)
        {
            foreach (Texture tex in Textures)
                // The size of the new Texture2D object doesn't matter. It will be replaced (including its size) with the data from the .jpg texture that's getting pulled from the pk3 file.
                if (pk3.ContainsEntry(tex.Name + ".jpg"))
                {
                    Texture2D readyTex = new Texture2D(4, 4);
                    ZipEntry entry = pk3[tex.Name + ".jpg"];
                    using (CrcCalculatorStream stream = entry.OpenReader())
                    {
                        MemoryStream ms = new MemoryStream();
                        entry.Extract(ms);
                        readyTex.LoadImage(ms.GetBuffer());
                    }

                    readyTex.name = tex.Name;
                    readyTex.filterMode = FilterMode.Trilinear;
                    readyTex.Compress(true);

                    if (readyTextures.ContainsKey(tex.Name))
                    {
                        Debug.Log("Updating texture with name " + tex.Name);
                        readyTextures[tex.Name] = readyTex;
                    }
                    else
                    {
                        readyTextures.Add(tex.Name, readyTex);
                    }
                }
        }

        private void LoadTGATextures(ZipFile pk3)
        {
            foreach (Texture tex in Textures)
                // The size of the new Texture2D object doesn't matter. It will be replaced (including its size) with the data from the texture that's getting pulled from the pk3 file.
                if (pk3.ContainsEntry(tex.Name + ".tga"))
                {
                    Texture2D readyTex = new Texture2D(4, 4);
                    ZipEntry entry = pk3[tex.Name + ".tga"];
                    using (CrcCalculatorStream stream = entry.OpenReader())
                    {
                        MemoryStream ms = new MemoryStream();
                        entry.Extract(ms);
                        readyTex = TGALoader.LoadTGA(ms);
                    }

                    readyTex.name = tex.Name;
                    readyTex.filterMode = FilterMode.Trilinear;
                    readyTex.Compress(true);

                    if (readyTextures.ContainsKey(tex.Name))
                    {
                        Debug.Log("Updating texture with name " + tex.Name + ".tga");
                        readyTextures[tex.Name] = readyTex;
                    }
                    else
                    {
                        readyTextures.Add(tex.Name, readyTex);
                    }
                }
        }


        public string PrintInfo()
        {
            StringBuilder blob = new StringBuilder();
            int count = 0;
            foreach (Texture tex in Textures)
                blob.Append("Texture " + count++ + " Name: " + tex.Name.Trim() + "\tFlags: " + tex.Flags +
                            "\tContents: " + tex.Contents + "\r\n");
            return blob.ToString();
        }
    }
}