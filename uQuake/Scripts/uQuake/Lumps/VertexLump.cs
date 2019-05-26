using System.Text;

namespace SharpBSP
{
    public struct VertexLump
    {
        public Vertex[] verts;
        public int[] meshVerts;

        public override string ToString()
        {
            StringBuilder blob = new StringBuilder();
            for (int i = 0; i < verts.Length; i++)
            {
                blob.Append("Vertex " + i + " Pos: " + verts[i].position + " Normal: " + verts[i].normal + "\r\n");
            }

            return blob.ToString();
        }
    }
}