namespace SharpBSP
{
    public struct EntityLump
    {
        public EntityLump(string lump)
        {
            EntityString = lump;
        }

        public string EntityString { get; }

        public override string ToString()
        {
            return EntityString;
        }
    }
}