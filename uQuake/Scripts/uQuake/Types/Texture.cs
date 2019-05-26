namespace SharpBSP
{
    public class Texture
    {
        public Texture(string rawName, int flags, int contents)
        {
            //The string is read as 64 characters, which includes a bunch of null bytes.  We strip them to avoid oddness when printing and using the texture names.
            Name = rawName.Replace("\0", string.Empty);
            Flags = flags;
            Contents = contents;

            // Remove some common shader modifiers to get normal
            // textures instead. This is kind of a hack, and could
            // bit you if a texture just happens to have any of these
            // in its name but isn't actually a shader texture.
            Name = Name.Replace("_hell", string.Empty);
            Name = Name.Replace("_trans", string.Empty);
            Name = Name.Replace("flat_400", string.Empty);
            Name = Name.Replace("_750", string.Empty);
        }

        public string Name { get; }

        public int Flags { get; }

        public int Contents { get; }
    }
}