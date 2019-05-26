using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SharpBSP;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

// http://answers.unity3d.com/questions/126048/create-a-button-in-the-inspector.html#answer-360940
[CustomEditor(typeof(GenerateMap))]
internal class GenerateMapCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        GenerateMap script = (GenerateMap) target;
        if (GUILayout.Button("Generate Map"))
        {
            Debug.Log("Generate Map: " + script.mapName);
            ClearChildren(script);
            script.Run();
        }

        if (GUILayout.Button("Clear Map"))
        {
            ClearChildren(script);
        }
    }

    private static void ClearChildren(GenerateMap script)
    {
// http://forum.unity3d.com/threads/deleting-all-chidlren-of-an-object.92827/
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in script.gameObject.transform) children.Add(child.gameObject);
        children.ForEach(child => DestroyImmediate(child));
    }
}

public class GenerateMap : MonoBehaviour
{
    private static ProfilerMarker globalLoadMarker = new ProfilerMarker("MapLoading");
    private static ProfilerMarker fileLoadMarker = new ProfilerMarker("FileLoading");
    private static ProfilerMarker facesLoadMarker = new ProfilerMarker("FacesLoading");
    private static ProfilerMarker materialFetchMarker = new ProfilerMarker("MaterialFetching");
    private static ProfilerMarker generateObjMarker = new ProfilerMarker("GeneratePolygonObject");
    private static ProfilerMarker generateObjMeshMarker = new ProfilerMarker("GeneratePolygonMesh");
    private static ProfilerMarker generateBezMarker = new ProfilerMarker("GenerateBezierObject");
    private static ProfilerMarker generateBezMeshMarker = new ProfilerMarker("GenerateBezierMesh");

    public bool applyLightmaps;
    public bool generateAtRuntime = true;
    private BSPMap map;
    public bool mapIsInsidePK3;
    public string mapName;

    public Material fallbackMaterial;

    public int tessellations = 5;
    public bool useRippedTextures;

    public Material materialTemplate;
    public Material materialTemplateLightMap;
    public string lightMapProperty = "_LightMap";

    private static int lightMapPropertyId;

    private void Awake()
    {
        if (generateAtRuntime)
            Run();
    }

    public void Run()
    {
        using (globalLoadMarker.Auto())
        {
            Stopwatch s = Stopwatch.StartNew();
            lightMapPropertyId = Shader.PropertyToID(lightMapProperty);

            // Create a new BSPmap, which is an object that
            // represents the map and all its data as a whole
            using (fileLoadMarker.Auto())
            {
                if (mapIsInsidePK3)
                    map = new BSPMap(mapName, true);
                else
                    map = new BSPMap("Assets/baseq3/maps/" + mapName, false);

                s.Stop();
                Debug.Log($"Read map file in {s.ElapsedMilliseconds}ms");
            }

            s.Restart();
            using (facesLoadMarker.Auto())
            {
                // Each face is its own gameobject
                var groups = map.faceLump.faces.GroupBy(x => new {x.type, x.texture, x.lm_index});
                foreach (var group in groups)
                {
                    Face[] faces = group.ToArray();
                    if (faces.Length == 0) continue;

                    Material mat = useRippedTextures
                        ? FetchMaterial(map.textureLump.Textures[faces[0].texture].Name, faces[0].lm_index)
                        : fallbackMaterial;

                    switch (group.Key.type)
                    {
                        case 2:
                        {
                            GenerateBezObject(mat, faces);
                            break;
                        }

                        case 1:
                        case 3:
                        {
                            GeneratePolygonObject(mat, faces);
                            break;
                        }

                        default:
                            Debug.Log(
                                $"Skipped face because it was not a polygon, mesh, or bez patch ({group.Key.type}).");
                            break;
                    }
                }

                GC.Collect();
                s.Stop();
                Debug.Log($"Loaded map in {s.ElapsedMilliseconds}ms");
            }
        }
    }

    #region Material Generation

    // This returns a material with the correct texture for a given face
    private Material FetchMaterial(string texName, int lm_index)
    {
        using (materialFetchMarker.Auto())
        {
//            string texName = map.textureLump.Textures[face.texture].Name;

            // Load the primary texture for the face from the texture lump
            // The texture lump itself will have already looked over all
            // available .pk3 files and compiled a dictionary of textures for us.
            Texture2D tex;

            if (map.textureLump.ContainsTexture(texName))
                tex = map.textureLump.GetTexture(texName);
            else
            {
                Debug.Log($"Failed to find texture '{texName}'");
                return fallbackMaterial;
            }

            Material mat;
            // Lightmapping is on, so calc the lightmaps
            if (lm_index >= 0 && applyLightmaps)
            {
                // LM experiment
                Texture2D lmap = map.lightMapLump.lightMaps[lm_index];
                lmap.Compress(true);
                lmap.Apply();
                mat = Instantiate(materialTemplateLightMap);
                mat.mainTexture = tex;
                mat.SetTexture(lightMapPropertyId, lmap);
                return mat;
            }

            // Lightmapping is off, so don't.
            mat = Instantiate(materialTemplate);
            mat.mainTexture = tex;
            return mat;
        }
    }

    #endregion

    #region Object Generation

    // This makes gameobjects for every bez patch in a face
    // they are tessellated according to the "tessellations" field
    // in the editor
    private void GenerateBezObject(Material material, params Face[] faces)
    {
        if (faces == null || faces.Length == 0)
            return;

        using (generateBezMarker.Auto())
        {
            int[] numPatches = new int[faces.Length];
            int totalPatches = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                int patches = (faces[i].size[0] - 1) / 2 * ((faces[i].size[1] - 1) / 2);
                numPatches[i] = patches;
                totalPatches += patches;
            }

            CombineInstance[] combine = new CombineInstance[totalPatches];
            int index = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                for (int n = 0; n < numPatches[i]; n++)
                {
                    combine[index].mesh = GenerateBezMesh(faces[i], n);
                    index++;
                }
            }

            int p = (faces[0].size[0] - 1) / 2 * ((faces[0].size[1] - 1) / 2);
            CombineInstance[] c = new CombineInstance[p];
            for (int i = 0; i < p; i++)
            {
                c[i].mesh = GenerateBezMesh(faces[0], i);
            }


            Mesh mesh = new Mesh();
            mesh.CombineMeshes(combine, true, false, false);
//            mesh.CombineMeshes(c, true, false, false);

            GameObject bezObj = new GameObject();
            bezObj.name = "Bezier";
            bezObj.transform.SetParent(transform);
            bezObj.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer meshRenderer = bezObj.AddComponent<MeshRenderer>();
            //bezObject.AddComponent<MeshCollider>();
            meshRenderer.sharedMaterial = material;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                bezObj.isStatic = true;
#endif
        }
    }


    // This takes one face and generates a gameobject complete with
    // mesh, renderer, material with texture, and collider.
    private void GeneratePolygonObject(Material material, params Face[] faces)
    {
        if (faces == null || faces.Length == 0)
        {
            Debug.LogWarning("Failed to create polygon object because there are no faces");
            return;
        }

        using (generateObjMarker.Auto())
        {
            GameObject obj = new GameObject();
            obj.name = "Mesh";
            obj.transform.SetParent(transform);
            // Our GeneratePolygonMesh will optimze and add the UVs for us
            CombineInstance[] combine = new CombineInstance[faces.Length];
            for (var i = 0; i < combine.Length; i++)
                combine[i].mesh = GeneratePolygonMesh(faces[i]);

            var mesh = new Mesh();
            mesh.CombineMeshes(combine, true, false, false);

            obj.AddComponent<MeshFilter>().mesh = mesh;
            MeshRenderer meshRenderer = obj.AddComponent<MeshRenderer>();
            //faceObject.AddComponent<MeshCollider>();
            meshRenderer.sharedMaterial = material;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                obj.isStatic = true;
            obj.hideFlags = HideFlags.DontSave;
#endif
        }
    }

    #endregion

    #region Mesh Generation

    // This forms a mesh from a bez patch of your choice
    // from the face of your choice.
    // It's ready to render with tex coords and all.
    private Mesh GenerateBezMesh(Face face, int patchNumber)
    {
        using (generateBezMeshMarker.Auto())
        {
            //Calculate how many patches there are using size[]
            //There are n_patchesX by n_patchesY patches in the grid, each of those
            //starts at a vert (i,j) in the overall grid
            //We don't actually need to know how many are on the Y length
            //but the forumla is here for historical/academic purposes
            int n_patchesX = (face.size[0] - 1) / 2;
            //int n_patchesY = ((face.size[1]) - 1) / 2;


            //Calculate what [n,m] patch we want by using an index
            //called patchNumber  Think of patchNumber as if you 
            //numbered the patches left to right, top to bottom on
            //the grid in a piece of paper.
            int pxStep = 0;
            int pyStep = 0;
            for (int i = 0; i < patchNumber; i++)
            {
                pxStep++;
                if (pxStep == n_patchesX)
                {
                    pxStep = 0;
                    pyStep++;
                }
            }

            //Create an array the size of the grid, which is given by
            //size[] on the face object.
            Vertex[,] vertGrid = new Vertex[face.size[0], face.size[1]];

            //Read the verts for this face into the grid, making sure
            //that the final shape of the grid matches the size[] of
            //the face.
            int gridXstep = 0;
            int gridYstep = 0;
            int vertStep = face.vertex;
            for (int i = 0; i < face.n_vertexes; i++)
            {
                vertGrid[gridXstep, gridYstep] = map.vertexLump.verts[vertStep];
                vertStep++;
                gridXstep++;
                if (gridXstep == face.size[0])
                {
                    gridXstep = 0;
                    gridYstep++;
                }
            }

            //We now need to pluck out exactly nine vertexes to pass to our
            //teselate function, so lets calculate the starting vertex of the
            //3x3 grid of nine vertexes that will make up our patch.
            //we already know how many patches are in the grid, which we have
            //as n and m.  There are n by m patches.  Since this method will
            //create one gameobject at a time, we only need to be able to grab
            //one.  The starting vertex will be called vi,vj think of vi,vj as x,y
            //coords into the grid.
            int vi = 2 * pxStep;
            int vj = 2 * pyStep;
            //Now that we have those, we need to get the vert at [vi,vj] and then
            //the two verts at [vi+1,vj] and [vi+2,vj], and then [vi,vj+1], etc.
            //the ending vert will at [vi+2,vj+2]

            int capacity = 3 * 3;
            List<Vector3> bverts = new List<Vector3>(capacity);

            //read texture/lightmap coords while we're at it
            //they will be tessellated as well.
            List<Vector2> uv = new List<Vector2>(capacity);
            List<Vector2> uv2 = new List<Vector2>(capacity);

            //Top row
            bverts.Add(vertGrid[vi, vj].position);
            bverts.Add(vertGrid[vi + 1, vj].position);
            bverts.Add(vertGrid[vi + 2, vj].position);

            uv.Add(vertGrid[vi, vj].texcoord);
            uv.Add(vertGrid[vi + 1, vj].texcoord);
            uv.Add(vertGrid[vi + 2, vj].texcoord);

            uv2.Add(vertGrid[vi, vj].lmcoord);
            uv2.Add(vertGrid[vi + 1, vj].lmcoord);
            uv2.Add(vertGrid[vi + 2, vj].lmcoord);

            //Middle row
            bverts.Add(vertGrid[vi, vj + 1].position);
            bverts.Add(vertGrid[vi + 1, vj + 1].position);
            bverts.Add(vertGrid[vi + 2, vj + 1].position);

            uv.Add(vertGrid[vi, vj + 1].texcoord);
            uv.Add(vertGrid[vi + 1, vj + 1].texcoord);
            uv.Add(vertGrid[vi + 2, vj + 1].texcoord);

            uv2.Add(vertGrid[vi, vj + 1].lmcoord);
            uv2.Add(vertGrid[vi + 1, vj + 1].lmcoord);
            uv2.Add(vertGrid[vi + 2, vj + 1].lmcoord);

            //Bottom row
            bverts.Add(vertGrid[vi, vj + 2].position);
            bverts.Add(vertGrid[vi + 1, vj + 2].position);
            bverts.Add(vertGrid[vi + 2, vj + 2].position);

            uv.Add(vertGrid[vi, vj + 2].texcoord);
            uv.Add(vertGrid[vi + 1, vj + 2].texcoord);
            uv.Add(vertGrid[vi + 2, vj + 2].texcoord);

            uv2.Add(vertGrid[vi, vj + 2].lmcoord);
            uv2.Add(vertGrid[vi + 1, vj + 2].lmcoord);
            uv2.Add(vertGrid[vi + 2, vj + 2].lmcoord);

            //Now that we have our control grid, it's business as usual
            Mesh bezMesh = new Mesh();
            bezMesh.name = "BSPfacemesh (bez)";
            BezierMesh bezPatch = new BezierMesh(tessellations, bverts, uv, uv2);
            return bezPatch.Mesh;
        }
    }

    // Generate a mesh for a simple polygon/mesh face
    // It's ready to render with tex coords and all.
    private Mesh GeneratePolygonMesh(Face face)
    {
        using (generateObjMeshMarker.Auto())
        {
            Mesh mesh = new Mesh();
            mesh.name = "BSPface (poly/mesh)";

            // Rip verts, uvs, and normals
            int vertexCount = face.n_vertexes;
            Vector3[] verts = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            Vector2[] uv2 = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            int[] indicies = new int[face.n_meshverts];
            int vertIndex = 0;
            int faceIndex = 0;

            int vstep = face.vertex;
            for (int n = 0; n < face.n_vertexes; n++)
            {
                verts[vertIndex] = map.vertexLump.verts[vstep].position;
                uv[vertIndex] = map.vertexLump.verts[vstep].texcoord;
                uv2[vertIndex] = map.vertexLump.verts[vstep].lmcoord;
                normals[vertIndex] = map.vertexLump.verts[vstep].normal;
                vstep++;
                vertIndex++;
            }

            // Rip meshverts / triangles
            int mstep = face.meshvert;
            for (int n = 0; n < face.n_meshverts; n++)
            {
                indicies[faceIndex] = map.vertexLump.meshVerts[mstep];
                mstep++;
                faceIndex++;
            }

            // add the verts, uvs, and normals we ripped to the gameobjects mesh filter
            mesh.vertices = verts;
            mesh.normals = normals;

            // Add the texture co-ords (or UVs) to the face/mesh
            mesh.uv = uv;
            mesh.uv2 = uv2;

            // add the meshverts to the object being built
            mesh.triangles = indicies;

            // Let Unity do some heavy lifting for us
            mesh.RecalculateBounds();
//            mesh.RecalculateNormals();
//            mesh.Optimize();

            return mesh;
        }
    }

    #endregion
}