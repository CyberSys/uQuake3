namespace SharpBSP
{
    public class BSPDirectoryEntry
    {
        public BSPDirectoryEntry(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }

        public int Length { get; }

        public string Name { get; set; }

        public bool Validate()
        {
            if (Length % 4 == 0)
                return true;
            return false;
        }
    }
}