using System.Text;

namespace SharpBSP
{
    public struct FaceLump
    {
        public FaceLump(int faceCount)
        {
            faces = new Face[faceCount];
        }

        public Face[] faces;

        public string PrintInfo()
        {
            StringBuilder blob = new StringBuilder();
            int count = 0;
            foreach (Face face in faces)
            {
                blob.AppendLine("Face " + count + "\t Tex: " + face.texture + "\tType: " + face.type + "\tVertIndex: " +
                                face.vertex + "\tNumVerts: " + face.n_vertexes + "\tMeshVertIndex: " + face.meshvert +
                                "\tMeshVerts: " + face.n_meshverts + "\r\n");
                count++;
            }

            return blob.ToString();
        }
    }
}