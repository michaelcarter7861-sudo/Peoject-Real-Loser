using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering;

namespace RealLoserStudio.Tactical.Core
{
    /// <summary>
    /// Self-contained binary glTF 2.0 (.glb) and RIFF PCM (.wav) runtime loader.
    /// Reads binary headers (0x46546C67 'glTF', JSON chunk 0x4E4F534A, BIN chunk 0x004E4942),
    /// extracts POSITION, NORMAL, JOINTS_0, WEIGHTS_0, and INDICES buffers via BitConverter,
    /// constructs Unity Mesh, MeshFilter, MeshRenderer, BoxCollider/MeshCollider hierarchies,
    /// creates shader-safe URP/Standard fallback Materials, and decodes 16-bit PCM .wav AudioClips.
    /// </summary>
    public static class RuntimeGLBAndMeshLoader
    {
        public const uint GLTF_MAGIC = 0x46546C67;       // "glTF"
        public const uint CHUNK_JSON = 0x4E4F534A;       // "JSON"
        public const uint CHUNK_BIN  = 0x004E4942;       // "BIN\0"

        public const int COMPONENT_UNSIGNED_BYTE  = 5121;
        public const int COMPONENT_UNSIGNED_SHORT = 5123;
        public const int COMPONENT_UNSIGNED_INT   = 5125;
        public const int COMPONENT_FLOAT          = 5126;

        private static readonly Dictionary<string, GameObject> _prefabTemplateCache = new Dictionary<string, GameObject>();
        private static readonly Dictionary<string, AudioClip> _audioClipCache = new Dictionary<string, AudioClip>();
        private static Shader _cachedLitShader;
        private static readonly Dictionary<string, Texture2D> _proceduralSurfaceTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        [Serializable]
        private class GltfRoot
        {
            public List<GltfNode> nodes;
            public List<GltfMesh> meshes;
            public List<GltfAccessor> accessors;
            public List<GltfBufferView> bufferViews;
            public List<GltfMaterial> materials;
            public List<GltfSkin> skins;
        }

        [Serializable]
        private class GltfNode
        {
            public string name;
            public int mesh = -1;
            public int skin = -1;
            public float[] translation;
            public float[] rotation;
            public float[] scale;
            public int[] children;
        }

        [Serializable]
        private class GltfSkin
        {
            public string name;
            public int skeleton = -1;
            public int[] joints;
            public int inverseBindMatrices = -1;
        }

        [Serializable]
        private class GltfMesh
        {
            public string name;
            public List<GltfPrimitive> primitives;
        }

        [Serializable]
        private class GltfAttributes
        {
            public int POSITION = -1;
            public int NORMAL = -1;
            public int TEXCOORD_0 = -1;
            public int JOINTS_0 = -1;
            public int WEIGHTS_0 = -1;
        }

        [Serializable]
        private class GltfPrimitive
        {
            public GltfAttributes attributes;
            public int indices = -1;
            public int material = 0;
            public int mode = 4;
        }

        [Serializable]
        private class GltfAccessor
        {
            public int bufferView = -1;
            public int byteOffset = 0;
            public int componentType = 5126;
            public int count = 0;
            public string type = "SCALAR";
        }

        [Serializable]
        private class GltfBufferView
        {
            public int buffer = 0;
            public int byteOffset = 0;
            public int byteLength = 0;
            public int byteStride = 0;
        }

        [Serializable]
        private class GltfPbrMetallicRoughness
        {
            public float[] baseColorFactor;
            public float metallicFactor = 0.25f;
            public float roughnessFactor = 0.70f;
        }

        [Serializable]
        private class GltfMaterial
        {
            public string name;
            public GltfPbrMetallicRoughness pbrMetallicRoughness;
        }

        public static Shader GetSafeLitShader()
        {
            if (_cachedLitShader != null) return _cachedLitShader;

            bool isUrpActive = GraphicsSettings.currentRenderPipeline != null;
            string[] candidateShaders = isUrpActive
                ? new string[]
                {
                    "Universal Render Pipeline/Lit",
                    "Universal Render Pipeline/Simple Lit",
                    "Standard",
                    "Mobile/Diffuse",
                    "Legacy Shaders/Diffuse",
                    "Sprites/Default"
                }
                : new string[]
                {
                    "Standard",
                    "Mobile/Diffuse",
                    "Legacy Shaders/Diffuse",
                    "Universal Render Pipeline/Lit",
                    "Sprites/Default"
                };

            for (int i = 0; i < candidateShaders.Length; i++)
            {
                Shader s = Shader.Find(candidateShaders[i]);
                if (s != null)
                {
                    _cachedLitShader = s;
                    return s;
                }
            }
            return Shader.Find("Hidden/InternalErrorShader");
        }

        public static Material CreateSafeMaterial(string name, Color baseColor, float metallic = 0.2f, float smoothness = 0.35f)
        {
            Shader shader = GetSafeLitShader();
            Material mat = new Material(shader);
            mat.name = name;
            mat.color = baseColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            Texture2D surfaceTexture = GetProceduralSurfaceTexture(name);
            if (surfaceTexture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", surfaceTexture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", surfaceTexture);
            }
            string materialKey = ResolveAuthoredTextureKey(name);
            Texture2D normalTexture = Resources.Load<Texture2D>("PRLTextures/T_" + materialKey + "_Normal");
            if (normalTexture != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalTexture);
                mat.EnableKeyword("_NORMALMAP");
            }
            Texture2D maskTexture = Resources.Load<Texture2D>("PRLTextures/T_" + materialKey + "_Mask");
            if (maskTexture != null)
            {
                if (mat.HasProperty("_MetallicGlossMap")) { mat.SetTexture("_MetallicGlossMap", maskTexture); mat.EnableKeyword("_METALLICGLOSSMAP"); }
                if (mat.HasProperty("_OcclusionMap")) mat.SetTexture("_OcclusionMap", maskTexture);
            }
            return mat;
        }

        public static byte[] ReadStreamingOrProjectBytes(string relativeSubPath)
        {
            if (string.IsNullOrEmpty(relativeSubPath)) return null;

            // Resources TextAssets are the synchronous, Android-safe source of truth for runtime data.
            // StreamingAssets on Android lives inside the APK/JAR and must not be synchronously polled
            // with UnityWebRequest from the main thread (that pattern can freeze the game indefinitely).
            string resourcePath = Path.Combine("PRLRuntime", Path.ChangeExtension(relativeSubPath, null)).Replace('\\', '/');
            TextAsset embedded = Resources.Load<TextAsset>(resourcePath);
            if (embedded != null) return embedded.bytes;

            string streamingPath = Path.Combine(Application.streamingAssetsPath, relativeSubPath);
            if (streamingPath.Contains("://") || streamingPath.Contains(":///"))
            {
                // Procedural fallback handles unbundled environment/prop variations cleanly on Android without blocking UnityWebRequest.
                return null;
            }
            if (File.Exists(streamingPath)) return File.ReadAllBytes(streamingPath);

            string projectArtPath = Path.Combine(Application.dataPath, "_Project", relativeSubPath);
            if (File.Exists(projectArtPath))
            {
                return File.ReadAllBytes(projectArtPath);
            }

            string rootFallback = Path.Combine(Directory.GetCurrentDirectory(), relativeSubPath);
            if (File.Exists(rootFallback))
            {
                return File.ReadAllBytes(rootFallback);
            }

            return null;
        }

        public static string ReadStreamingOrProjectText(string relativeSubPath)
        {
            byte[] raw = ReadStreamingOrProjectBytes(relativeSubPath);
            if (raw != null && raw.Length > 0)
            {
                return Encoding.UTF8.GetString(raw);
            }
            return null;
        }

        public static GameObject InstantiateGLB(string relativeGlbPath, Vector3 worldPos, Quaternion worldRot, Transform parent = null, bool addCollider = true)
        {
            string cacheKey = relativeGlbPath.Replace('\\', '/') + (addCollider ? "|collider" : "|visual");
            GameObject template;
            if (!_prefabTemplateCache.TryGetValue(cacheKey, out template) || template == null)
            {
                byte[] glbBytes = ReadStreamingOrProjectBytes(relativeGlbPath);
                template = glbBytes != null && glbBytes.Length >= 20
                    ? ParseAndBuildGLBGameObject(Path.GetFileNameWithoutExtension(relativeGlbPath), glbBytes, addCollider)
                    : BuildProceduralFallbackObject(Path.GetFileNameWithoutExtension(relativeGlbPath), addCollider);
                template.name = "PRL_CACHED_" + Path.GetFileNameWithoutExtension(relativeGlbPath) + (addCollider ? "_ColliderTemplate" : "_VisualTemplate");
                template.transform.SetParent(null, false);
                template.transform.position = Vector3.zero;
                template.transform.rotation = Quaternion.identity;
                template.SetActive(false);
                template.hideFlags = HideFlags.HideInHierarchy;
                if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(template);
                _prefabTemplateCache[cacheKey] = template;
            }

            GameObject instance = UnityEngine.Object.Instantiate(template);
            instance.name = Path.GetFileNameWithoutExtension(relativeGlbPath);
            instance.hideFlags = HideFlags.None;
            instance.SetActive(true);
            if (parent != null) instance.transform.SetParent(parent, false);
            instance.transform.position = worldPos;
            instance.transform.rotation = worldRot;
            return instance;
        }

        public static GameObject ParseAndBuildGLBGameObject(string assetName, byte[] data, bool addCollider)
        {
            uint magic = BitConverter.ToUInt32(data, 0);
            uint version = BitConverter.ToUInt32(data, 4);
            uint totalLen = BitConverter.ToUInt32(data, 8);

            if (magic != GLTF_MAGIC || version != 2 || totalLen > data.Length)
            {
                return BuildProceduralFallbackObject(assetName, addCollider);
            }

            uint jsonLen = BitConverter.ToUInt32(data, 12);
            uint jsonType = BitConverter.ToUInt32(data, 16);
            if (jsonType != CHUNK_JSON || 20 + jsonLen > data.Length)
            {
                return BuildProceduralFallbackObject(assetName, addCollider);
            }

            string jsonText = Encoding.UTF8.GetString(data, 20, (int)jsonLen);
            int binHeaderOffset = 20 + (int)jsonLen;
            byte[] binBuffer = null;

            if (binHeaderOffset + 8 <= data.Length)
            {
                uint binLen = BitConverter.ToUInt32(data, binHeaderOffset);
                uint binType = BitConverter.ToUInt32(data, binHeaderOffset + 4);
                if (binType == CHUNK_BIN && binHeaderOffset + 8 + (int)binLen <= data.Length)
                {
                    binBuffer = new byte[binLen];
                    Buffer.BlockCopy(data, binHeaderOffset + 8, binBuffer, 0, (int)binLen);
                }
            }

            if (binBuffer == null)
            {
                return BuildProceduralFallbackObject(assetName, addCollider);
            }

            GltfRoot gltf = JsonUtility.FromJson<GltfRoot>(jsonText);
            if (gltf == null || gltf.meshes == null || gltf.meshes.Count == 0)
            {
                return BuildProceduralFallbackObject(assetName, addCollider);
            }

            GameObject rootObj = new GameObject(assetName);

            // Build materials from glTF PBR factors
            List<Material> unityMaterials = new List<Material>();
            if (gltf.materials != null && gltf.materials.Count > 0)
            {
                for (int m = 0; m < gltf.materials.Count; m++)
                {
                    var gm = gltf.materials[m];
                    Color col = new Color(0.45f, 0.48f, 0.46f, 1f);
                    float metallic = 0.25f;
                    float roughness = 0.70f;
                    if (gm.pbrMetallicRoughness != null)
                    {
                        if (gm.pbrMetallicRoughness.baseColorFactor != null && gm.pbrMetallicRoughness.baseColorFactor.Length >= 4)
                        {
                            col = new Color(
                                gm.pbrMetallicRoughness.baseColorFactor[0],
                                gm.pbrMetallicRoughness.baseColorFactor[1],
                                gm.pbrMetallicRoughness.baseColorFactor[2],
                                gm.pbrMetallicRoughness.baseColorFactor[3]
                            );
                        }
                        metallic = gm.pbrMetallicRoughness.metallicFactor;
                        roughness = gm.pbrMetallicRoughness.roughnessFactor;
                    }
                    unityMaterials.Add(CreateSafeMaterial(assetName + "_" + (gm.name ?? "Mat"), col, metallic, 1.0f - roughness));
                }
            }
            else
            {
                unityMaterials.Add(CreateSafeMaterial(assetName + "_DefaultMat", new Color(0.45f, 0.48f, 0.46f, 1f)));
            }

            // Resolve the skin used by each mesh before constructing geometry.
            int[] skinForMesh = new int[gltf.meshes.Count];
            for (int i = 0; i < skinForMesh.Length; i++) skinForMesh[i] = -1;
            if (gltf.nodes != null)
                for (int i = 0; i < gltf.nodes.Count; i++)
                    if (gltf.nodes[i].mesh >= 0 && gltf.nodes[i].mesh < skinForMesh.Length && gltf.nodes[i].skin >= 0)
                        skinForMesh[gltf.nodes[i].mesh] = gltf.nodes[i].skin;

            // Preserve every glTF primitive as a Unity submesh and preserve material slots.
            List<Mesh> builtMeshes = new List<Mesh>();
            List<Material[]> builtMeshMaterials = new List<Material[]>();
            for (int mi = 0; mi < gltf.meshes.Count; mi++)
            {
                var gMesh = gltf.meshes[mi];
                string mName = gMesh.name ?? (assetName + "_Mesh_" + mi);
                if (mName.EndsWith("_LOD1") || mName.EndsWith("_LOD2"))
                {
                    builtMeshes.Add(null);
                    builtMeshMaterials.Add(new Material[0]);
                    continue;
                }
                GltfSkin skin = skinForMesh[mi] >= 0 && gltf.skins != null && skinForMesh[mi] < gltf.skins.Count ? gltf.skins[skinForMesh[mi]] : null;
                int[] materialIndices;
                Mesh uMesh = BuildUnityMeshFromPrimitive(mName, gMesh, gltf.accessors, gltf.bufferViews, binBuffer, skin, out materialIndices);
                builtMeshes.Add(uMesh);
                Material[] slotMaterials = new Material[materialIndices != null ? materialIndices.Length : 1];
                for (int slot = 0; slot < slotMaterials.Length; slot++)
                {
                    int materialIndex = materialIndices != null ? materialIndices[slot] : 0;
                    slotMaterials[slot] = materialIndex >= 0 && materialIndex < unityMaterials.Count ? unityMaterials[materialIndex] : unityMaterials[0];
                }
                builtMeshMaterials.Add(slotMaterials);
            }

            // Instantiate Node Hierarchy (including real SkinnedMeshRenderer bindings for rigged characters).
            if (gltf.nodes != null && gltf.nodes.Count > 0)
            {
                Transform[] nodeTransforms = new Transform[gltf.nodes.Count];
                bool[] hasParent = new bool[gltf.nodes.Count];

                for (int ni = 0; ni < gltf.nodes.Count; ni++)
                {
                    var gNode = gltf.nodes[ni];
                    string nName = string.IsNullOrEmpty(gNode.name) ? ("Node_" + ni) : gNode.name;
                    // All generated character clips target a stable root path across the entire character library.
                    if ((gNode.children != null && gNode.children.Length > 0) && nName.StartsWith("chr_", StringComparison.OrdinalIgnoreCase) && gNode.mesh < 0)
                        nName = "RigRoot";
                    GameObject nObj = new GameObject(nName);
                    nodeTransforms[ni] = nObj.transform;

                    if (gNode.translation != null && gNode.translation.Length >= 3)
                    {
                        nObj.transform.localPosition = new Vector3(-gNode.translation[0], gNode.translation[1], gNode.translation[2]);
                    }
                    if (gNode.rotation != null && gNode.rotation.Length >= 4)
                    {
                        nObj.transform.localRotation = new Quaternion(gNode.rotation[0], -gNode.rotation[1], -gNode.rotation[2], gNode.rotation[3]);
                    }
                    if (gNode.scale != null && gNode.scale.Length >= 3)
                    {
                        nObj.transform.localScale = new Vector3(gNode.scale[0], gNode.scale[1], gNode.scale[2]);
                    }

                }

                for (int ni = 0; ni < gltf.nodes.Count; ni++)
                {
                    var gNode = gltf.nodes[ni];
                    if (gNode.children != null)
                    {
                        for (int ci = 0; ci < gNode.children.Length; ci++)
                        {
                            int childIdx = gNode.children[ci];
                            if (childIdx >= 0 && childIdx < nodeTransforms.Length && childIdx != ni)
                            {
                                nodeTransforms[childIdx].SetParent(nodeTransforms[ni], false);
                                hasParent[childIdx] = true;
                            }
                        }
                    }
                }

                for (int ni = 0; ni < nodeTransforms.Length; ni++)
                {
                    if (!hasParent[ni]) nodeTransforms[ni].SetParent(rootObj.transform, false);
                }

                for (int ni = 0; ni < nodeTransforms.Length; ni++)
                {
                    GltfNode gNode = gltf.nodes[ni];
                    if (gNode.mesh < 0 || gNode.mesh >= builtMeshes.Count || builtMeshes[gNode.mesh] == null) continue;
                    Mesh mesh = builtMeshes[gNode.mesh];
                    Material[] meshMaterials = builtMeshMaterials[gNode.mesh];
                    if (gNode.skin >= 0 && gltf.skins != null && gNode.skin < gltf.skins.Count && gltf.skins[gNode.skin].joints != null && gltf.skins[gNode.skin].joints.Length > 0)
                    {
                        GltfSkin skin = gltf.skins[gNode.skin];
                        SkinnedMeshRenderer smr = nodeTransforms[ni].gameObject.AddComponent<SkinnedMeshRenderer>();
                        smr.sharedMesh = mesh;
                        smr.sharedMaterials = meshMaterials;
                        Transform[] bones = new Transform[skin.joints.Length];
                        for (int bi = 0; bi < skin.joints.Length; bi++)
                        {
                            int jointNode = skin.joints[bi];
                            if (jointNode >= 0 && jointNode < nodeTransforms.Length) bones[bi] = nodeTransforms[jointNode];
                        }
                        smr.bones = bones;
                        if (skin.skeleton >= 0 && skin.skeleton < nodeTransforms.Length) smr.rootBone = nodeTransforms[skin.skeleton];
                        else smr.rootBone = bones[0];
                        smr.updateWhenOffscreen = true;
                    }
                    else
                    {
                        MeshFilter mf = nodeTransforms[ni].gameObject.AddComponent<MeshFilter>();
                        mf.sharedMesh = mesh;
                        MeshRenderer mr = nodeTransforms[ni].gameObject.AddComponent<MeshRenderer>();
                        mr.sharedMaterials = meshMaterials;
                    }
                }
            }

            if (addCollider)
            {
                Bounds combined = CalculateCombinedBounds(rootObj);
                BoxCollider bc = rootObj.AddComponent<BoxCollider>();
                bc.center = combined.center;
                bc.size = new Vector3(
                    Mathf.Max(0.2f, combined.size.x),
                    Mathf.Max(0.2f, combined.size.y),
                    Mathf.Max(0.2f, combined.size.z)
                );
            }

            return rootObj;
        }

        private static Mesh BuildUnityMeshFromPrimitive(
            string meshName,
            GltfMesh gMesh,
            List<GltfAccessor> accessors,
            List<GltfBufferView> bufferViews,
            byte[] binBuffer,
            GltfSkin skin,
            out int[] materialIndices)
        {
            materialIndices = null;
            if (gMesh.primitives == null || gMesh.primitives.Count == 0) return null;
            List<Vector3> allPositions = new List<Vector3>();
            List<Vector3> allNormals = new List<Vector3>();
            List<Vector2> allUvs = new List<Vector2>();
            List<BoneWeight> allBoneWeights = new List<BoneWeight>();
            List<int[]> submeshTriangles = new List<int[]>();
            materialIndices = new int[gMesh.primitives.Count];
            bool normalsComplete = true;
            bool skinComplete = skin != null;

            for (int pi = 0; pi < gMesh.primitives.Count; pi++)
            {
                GltfPrimitive prim = gMesh.primitives[pi];
                if (prim == null || prim.attributes == null || prim.attributes.POSITION < 0 || prim.attributes.POSITION >= accessors.Count)
                { submeshTriangles.Add(new int[0]); materialIndices[pi] = 0; continue; }
                Vector3[] positions = ReadVec3Accessor(accessors[prim.attributes.POSITION], bufferViews, binBuffer, true);
                if (positions == null || positions.Length == 0) { submeshTriangles.Add(new int[0]); materialIndices[pi] = prim.material; continue; }
                Vector3[] normals = prim.attributes.NORMAL >= 0 && prim.attributes.NORMAL < accessors.Count ? ReadVec3Accessor(accessors[prim.attributes.NORMAL], bufferViews, binBuffer, true) : null;
                if (normals == null || normals.Length != positions.Length) normalsComplete = false;
                Vector2[] uvs = prim.attributes.TEXCOORD_0 >= 0 && prim.attributes.TEXCOORD_0 < accessors.Count ? ReadVec2Accessor(accessors[prim.attributes.TEXCOORD_0], bufferViews, binBuffer) : null;
                if (uvs == null || uvs.Length != positions.Length) uvs = BuildPlanarUVs(positions);
                BoneWeight[] weights = null;
                if (skin != null && prim.attributes.JOINTS_0 >= 0 && prim.attributes.WEIGHTS_0 >= 0 && prim.attributes.JOINTS_0 < accessors.Count && prim.attributes.WEIGHTS_0 < accessors.Count)
                    weights = ReadBoneWeights(accessors[prim.attributes.JOINTS_0], accessors[prim.attributes.WEIGHTS_0], bufferViews, binBuffer, positions.Length, skin.joints != null ? skin.joints.Length : 0);
                if (weights == null || weights.Length != positions.Length) skinComplete = false;

                int offset = allPositions.Count;
                allPositions.AddRange(positions);
                for (int i = 0; i < positions.Length; i++)
                {
                    allNormals.Add(normals != null && normals.Length == positions.Length ? normals[i] : Vector3.zero);
                    allUvs.Add(uvs[i]);
                    allBoneWeights.Add(weights != null && weights.Length == positions.Length ? weights[i] : new BoneWeight { weight0 = 1f, boneIndex0 = 0 });
                }
                int[] indices = prim.indices >= 0 && prim.indices < accessors.Count ? ReadIndicesAccessor(accessors[prim.indices], bufferViews, binBuffer) : BuildSequentialIndices(positions.Length);
                for (int i = 0; i < indices.Length; i++) indices[i] += offset;
                submeshTriangles.Add(indices);
                materialIndices[pi] = prim.material;
            }

            if (allPositions.Count == 0) return null;
            Mesh mesh = new Mesh();
            mesh.name = meshName;
            if (allPositions.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(allPositions);
            mesh.SetUVs(0, allUvs);
            mesh.subMeshCount = submeshTriangles.Count;
            for (int i = 0; i < submeshTriangles.Count; i++) mesh.SetTriangles(submeshTriangles[i], i, false);
            if (normalsComplete && allNormals.Count == allPositions.Count) mesh.SetNormals(allNormals);
            else mesh.RecalculateNormals();
            if (skinComplete && allBoneWeights.Count == allPositions.Count)
            {
                mesh.boneWeights = allBoneWeights.ToArray();
                if (skin.inverseBindMatrices >= 0 && skin.inverseBindMatrices < accessors.Count)
                {
                    Matrix4x4[] bindposes = ReadMatrixAccessor(accessors[skin.inverseBindMatrices], bufferViews, binBuffer);
                    if (bindposes != null && bindposes.Length == skin.joints.Length) mesh.bindposes = bindposes;
                }
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int[] BuildSequentialIndices(int count)
        {
            int[] result = new int[count];
            for (int i = 0; i < count; i++) result[i] = i;
            return result;
        }

        private static BoneWeight[] ReadBoneWeights(GltfAccessor jointsAccessor, GltfAccessor weightsAccessor, List<GltfBufferView> views, byte[] bin, int vertexCount, int boneCount)
        {
            if (jointsAccessor.bufferView < 0 || weightsAccessor.bufferView < 0 || jointsAccessor.bufferView >= views.Count || weightsAccessor.bufferView >= views.Count) return null;
            var jv = views[jointsAccessor.bufferView];
            var wv = views[weightsAccessor.bufferView];
            int js = jv.byteStride > 0 ? jv.byteStride : (jointsAccessor.componentType == COMPONENT_UNSIGNED_SHORT ? 8 : 4);
            int ws = wv.byteStride > 0 ? wv.byteStride : 16;
            int jo = jv.byteOffset + jointsAccessor.byteOffset;
            int wo = wv.byteOffset + weightsAccessor.byteOffset;
            BoneWeight[] result = new BoneWeight[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                int jp = jo + i * js; int wp = wo + i * ws;
                if (jp + (jointsAccessor.componentType == COMPONENT_UNSIGNED_SHORT ? 8 : 4) > bin.Length || wp + 16 > bin.Length) return null;
                int[] ji = new int[4];
                for (int c = 0; c < 4; c++)
                {
                    if (jointsAccessor.componentType == COMPONENT_UNSIGNED_SHORT) ji[c] = BitConverter.ToUInt16(bin, jp + c * 2);
                    else ji[c] = bin[jp + c];
                    if (boneCount > 0 && ji[c] >= boneCount) ji[c] = 0;
                }
                float w0 = BitConverter.ToSingle(bin, wp), w1 = BitConverter.ToSingle(bin, wp + 4), w2 = BitConverter.ToSingle(bin, wp + 8), w3 = BitConverter.ToSingle(bin, wp + 12);
                float total = w0 + w1 + w2 + w3;
                if (total <= 0.0001f) { w0 = 1f; w1 = w2 = w3 = 0f; total = 1f; }
                result[i] = new BoneWeight { boneIndex0 = ji[0], boneIndex1 = ji[1], boneIndex2 = ji[2], boneIndex3 = ji[3], weight0 = w0 / total, weight1 = w1 / total, weight2 = w2 / total, weight3 = w3 / total };
            }
            return result;
        }

        private static Matrix4x4[] ReadMatrixAccessor(GltfAccessor acc, List<GltfBufferView> views, byte[] bin)
        {
            if (acc.bufferView < 0 || acc.bufferView >= views.Count || acc.type != "MAT4") return null;
            var bv = views[acc.bufferView];
            int stride = bv.byteStride > 0 ? bv.byteStride : 64;
            int start = bv.byteOffset + acc.byteOffset;
            Matrix4x4[] result = new Matrix4x4[acc.count];
            for (int i = 0; i < acc.count; i++)
            {
                int off = start + i * stride;
                if (off + 64 > bin.Length) return null;
                Matrix4x4 m = new Matrix4x4();
                for (int col = 0; col < 4; col++) for (int row = 0; row < 4; row++) m[row, col] = BitConverter.ToSingle(bin, off + (col * 4 + row) * 4);
                // Convert glTF right-handed X basis to the loader's mirrored-X Unity basis.
                for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) if ((row == 0) != (col == 0)) m[row, col] = -m[row, col];
                result[i] = m;
            }
            return result;
        }

        private static Vector2[] ReadVec2Accessor(GltfAccessor acc, List<GltfBufferView> views, byte[] bin)
        {
            if (acc.bufferView < 0 || acc.bufferView >= views.Count) return null;
            var bv = views[acc.bufferView];
            int stride = bv.byteStride > 0 ? bv.byteStride : 8;
            int baseOffset = bv.byteOffset + acc.byteOffset;
            Vector2[] result = new Vector2[acc.count];
            for (int i = 0; i < acc.count; i++)
            {
                int off = baseOffset + i * stride;
                if (off + 8 > bin.Length) return null;
                float u = BitConverter.ToSingle(bin, off);
                float v = BitConverter.ToSingle(bin, off + 4);
                result[i] = new Vector2(u, 1f - v);
            }
            return result;
        }

        private static Vector2[] BuildPlanarUVs(Vector3[] vertices)
        {
            if (vertices == null || vertices.Length == 0) return new Vector2[0];
            Bounds b = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Length; i++) b.Encapsulate(vertices[i]);
            float sx = Mathf.Max(0.01f, b.size.x);
            float sy = Mathf.Max(0.01f, b.size.y);
            float sz = Mathf.Max(0.01f, b.size.z);
            bool projectXY = b.size.y > b.size.z && b.size.y > b.size.x * 0.75f;
            Vector2[] uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                uv[i] = projectXY ? new Vector2((vertices[i].x - b.min.x) / sx, (vertices[i].y - b.min.y) / sy) : new Vector2((vertices[i].x - b.min.x) / sx, (vertices[i].z - b.min.z) / sz);
            return uv;
        }

        private static string ResolveAuthoredTextureKey(string materialName)
        {
            string n = (materialName ?? string.Empty).ToLowerInvariant();
            if (n.Contains("foliage") || n.Contains("leaf") || n.Contains("tree") || n.Contains("grass") || n.Contains("ghillie")) return "Foliage";
            if (n.Contains("desertcamo") || n.Contains("desert_uniform") || n.Contains("desert_soldier")) return "DesertCamo";
            if (n.Contains("urban_camo") || n.Contains("urban_uniform")) return "UrbanCamo";
            if (n.Contains("sand") || n.Contains("desert") || n.Contains("dune") || n.Contains("adobe")) return "DesertSand";
            if (n.Contains("fabric") || n.Contains("uniform") || n.Contains("character") || n.Contains("soldier") || n.Contains("body") || n.Contains("cloth") || n.StartsWith("chr_")) return "OliveFabric";
            if (n.Contains("wood") || n.Contains("crate") || n.Contains("plank")) return "WeatheredWood";
            if (n.Contains("rubber") || n.Contains("tyre") || n.Contains("tire")) return "Rubber";
            if (n.Contains("weapon") || n.Contains("rifle") || n.Contains("pistol") || n.Contains("barrel")) return "WeaponSteel";
            if (n.Contains("metal") || n.Contains("steel") || n.Contains("truck") || n.Contains("vehicle") || n.Contains("pipe")) return "BrushedSteel";
            return "Concrete";
        }

        private static Texture2D GetProceduralSurfaceTexture(string materialName)
        {
            string authoredKey = ResolveAuthoredTextureKey(materialName);
            Texture2D authored = Resources.Load<Texture2D>("PRLTextures/T_" + authoredKey + "_BaseColor");
            if (authored != null) return authored;
            string key = "generic";
            string n = (materialName ?? string.Empty).ToLowerInvariant();
            if (n.Contains("foliage") || n.Contains("leaf") || n.Contains("tree") || n.Contains("grass")) key = "foliage";
            else if (n.Contains("sand") || n.Contains("desert") || n.Contains("dune") || n.Contains("adobe")) key = "sand";
            else if (n.Contains("metal") || n.Contains("steel") || n.Contains("weapon") || n.Contains("truck") || n.Contains("vehicle") || n.Contains("pipe")) key = "metal";
            else if (n.Contains("wood") || n.Contains("crate") || n.Contains("door") || n.Contains("plank")) key = "wood";
            else if (n.Contains("fabric") || n.Contains("uniform") || n.Contains("character") || n.Contains("soldier") || n.Contains("body") || n.Contains("cloth") || n.StartsWith("chr_")) key = "fabric";
            else if (n.Contains("concrete") || n.Contains("wall") || n.Contains("building") || n.Contains("bldg") || n.Contains("floor") || n.Contains("stone") || n.Contains("bunker") || n.StartsWith("env_")) key = "concrete";
            Texture2D cached;
            if (_proceduralSurfaceTextures.TryGetValue(key, out cached) && cached != null) return cached;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false, false);
            tex.name = "PRL_Procedural_" + key;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 2;
            Color[] pixels = new Color[size * size];
            int seed = 17;
            for (int i = 0; i < key.Length; i++) seed = seed * 31 + key[i];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float wave = Mathf.Sin((x + seed % 19) * 0.19f) * Mathf.Cos((y + seed % 13) * 0.17f);
                    float fine = Mathf.PerlinNoise((x + seed % 37) * 0.11f, (y + seed % 29) * 0.11f) - 0.5f;
                    float pattern = 0f;
                    if (key == "metal") pattern = Mathf.Sin((y + seed % 7) * 0.95f) * 0.035f;
                    else if (key == "wood") pattern = Mathf.Sin((y + seed % 9) * 0.32f + Mathf.Sin(x * 0.08f)) * 0.075f;
                    else if (key == "sand") pattern = wave * 0.045f;
                    else if (key == "foliage") pattern = (Mathf.PerlinNoise(x * 0.07f, y * 0.07f) - 0.5f) * 0.15f;
                    else if (key == "fabric") pattern = Mathf.Sin((x + y) * 0.8f) * 0.025f;
                    else pattern = wave * 0.025f;
                    float value = Mathf.Clamp(0.88f + fine * 0.19f + pattern, 0.62f, 1.12f);
                    pixels[y * size + x] = new Color(value, value, value, 1f);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _proceduralSurfaceTextures[key] = tex;
            return tex;
        }

        private static Vector3[] ReadVec3Accessor(GltfAccessor acc, List<GltfBufferView> views, byte[] bin, bool flipX)
        {
            if (acc.bufferView < 0 || acc.bufferView >= views.Count) return null;
            var bv = views[acc.bufferView];
            int stride = bv.byteStride > 0 ? bv.byteStride : 12;
            int baseOffset = bv.byteOffset + acc.byteOffset;
            Vector3[] result = new Vector3[acc.count];

            for (int i = 0; i < acc.count; i++)
            {
                int off = baseOffset + i * stride;
                if (off + 12 > bin.Length) break;
                float vx = BitConverter.ToSingle(bin, off);
                float vy = BitConverter.ToSingle(bin, off + 4);
                float vz = BitConverter.ToSingle(bin, off + 8);
                result[i] = new Vector3(flipX ? -vx : vx, vy, vz);
            }
            return result;
        }

        private static int[] ReadIndicesAccessor(GltfAccessor acc, List<GltfBufferView> views, byte[] bin)
        {
            if (acc.bufferView < 0 || acc.bufferView >= views.Count) return null;
            var bv = views[acc.bufferView];
            int baseOffset = bv.byteOffset + acc.byteOffset;
            int[] raw = new int[acc.count];

            if (acc.componentType == COMPONENT_UNSIGNED_SHORT)
            {
                for (int i = 0; i < acc.count; i++)
                {
                    int off = baseOffset + i * 2;
                    if (off + 2 > bin.Length) break;
                    raw[i] = BitConverter.ToUInt16(bin, off);
                }
            }
            else if (acc.componentType == COMPONENT_UNSIGNED_INT)
            {
                for (int i = 0; i < acc.count; i++)
                {
                    int off = baseOffset + i * 4;
                    if (off + 4 > bin.Length) break;
                    raw[i] = (int)BitConverter.ToUInt32(bin, off);
                }
            }
            else if (acc.componentType == COMPONENT_UNSIGNED_BYTE)
            {
                for (int i = 0; i < acc.count; i++)
                {
                    int off = baseOffset + i;
                    if (off + 1 > bin.Length) break;
                    raw[i] = bin[off];
                }
            }

            // Flip triangle winding for left-handed X-axis reflection
            for (int t = 0; t + 2 < raw.Length; t += 3)
            {
                int tmp = raw[t + 1];
                raw[t + 1] = raw[t + 2];
                raw[t + 2] = tmp;
            }
            return raw;
        }

        private static Bounds CalculateCombinedBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(new Vector3(0f, 1.0f, 0f), new Vector3(1f, 2f, 1f));

            bool initialized = false;
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.one);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                Bounds world = renderer.bounds;
                Vector3 c = world.center;
                Vector3 e = world.extents;
                Vector3[] corners = new Vector3[]
                {
                    c + new Vector3(-e.x, -e.y, -e.z), c + new Vector3(-e.x, -e.y, e.z),
                    c + new Vector3(-e.x, e.y, -e.z), c + new Vector3(-e.x, e.y, e.z),
                    c + new Vector3(e.x, -e.y, -e.z), c + new Vector3(e.x, -e.y, e.z),
                    c + new Vector3(e.x, e.y, -e.z), c + new Vector3(e.x, e.y, e.z)
                };
                for (int k = 0; k < corners.Length; k++)
                {
                    Vector3 local = root.transform.InverseTransformPoint(corners[k]);
                    if (!initialized) { localBounds = new Bounds(local, Vector3.zero); initialized = true; }
                    else localBounds.Encapsulate(local);
                }
            }
            if (!initialized) return new Bounds(new Vector3(0f, 1.0f, 0f), new Vector3(1f, 2f, 1f));
            localBounds.size = new Vector3(Mathf.Max(0.2f, localBounds.size.x), Mathf.Max(0.2f, localBounds.size.y), Mathf.Max(0.2f, localBounds.size.z));
            return localBounds;
        }

        public static GameObject BuildProceduralFallbackObject(string assetName, bool addCollider)
        {
            string n = (assetName ?? "Object").ToLowerInvariant();
            GameObject root = new GameObject(assetName);

            Material oliveMat = CreateSafeMaterial(assetName + "_OliveFabric", new Color(0.26f, 0.33f, 0.24f, 1f), 0.15f, 0.30f);
            Material steelMat = CreateSafeMaterial(assetName + "_BrushedSteel", new Color(0.32f, 0.35f, 0.37f, 1f), 0.65f, 0.55f);
            Material woodMat = CreateSafeMaterial(assetName + "_WeatheredWood", new Color(0.46f, 0.34f, 0.22f, 1f), 0.05f, 0.20f);
            Material concreteMat = CreateSafeMaterial(assetName + "_Concrete", new Color(0.52f, 0.53f, 0.50f, 1f), 0.08f, 0.22f);
            Material sandMat = CreateSafeMaterial(assetName + "_DesertSand", new Color(0.68f, 0.58f, 0.42f, 1f), 0.05f, 0.18f);
            Material foliageMat = CreateSafeMaterial(assetName + "_Foliage", new Color(0.18f, 0.38f, 0.20f, 1f), 0.02f, 0.15f);
            Material rubberMat = CreateSafeMaterial(assetName + "_Rubber", new Color(0.12f, 0.13f, 0.14f, 1f), 0.10f, 0.20f);
            Material hazardMat = CreateSafeMaterial(assetName + "_Hazard", new Color(0.76f, 0.24f, 0.16f, 1f), 0.30f, 0.45f);

            if (n.Contains("truck_cargo"))
            {
                AddPart(root.transform, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0f), new Vector3(2.5f, 0.5f, 6.8f), steelMat);
                AddPart(root.transform, "Cab", PrimitiveType.Cube, new Vector3(0f, 1.85f, 2.2f), new Vector3(2.4f, 1.6f, 2.0f), oliveMat);
                AddPart(root.transform, "CargoBed", PrimitiveType.Cube, new Vector3(0f, 2.05f, -0.9f), new Vector3(2.5f, 2.0f, 4.2f), oliveMat);
                for (int side = -1; side <= 1; side += 2)
                {
                    AddPart(root.transform, "WheelF", PrimitiveType.Cylinder, new Vector3(side * 1.25f, 0.50f, 2.1f), new Vector3(0.95f, 0.25f, 0.95f), rubberMat, new Vector3(0f, 0f, 90f));
                    AddPart(root.transform, "WheelR", PrimitiveType.Cylinder, new Vector3(side * 1.25f, 0.50f, -1.8f), new Vector3(0.95f, 0.25f, 0.95f), rubberMat, new Vector3(0f, 0f, 90f));
                }
            }
            else if (n.Contains("jeep") || n.Contains("forklift"))
            {
                AddPart(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(1.9f, 0.8f, 3.6f), oliveMat);
                AddPart(root.transform, "Cabin", PrimitiveType.Cube, new Vector3(0f, 1.45f, -0.2f), new Vector3(1.75f, 0.75f, 1.8f), steelMat);
            }
            else if (n.Contains("chopper"))
            {
                AddPart(root.transform, "Fuselage", PrimitiveType.Capsule, new Vector3(0f, 1.8f, 0f), new Vector3(2.4f, 2.2f, 6.2f), oliveMat);
                AddPart(root.transform, "MainRotor", PrimitiveType.Cube, new Vector3(0f, 3.3f, 0f), new Vector3(7.5f, 0.08f, 0.35f), steelMat);
                AddPart(root.transform, "CrossRotor", PrimitiveType.Cube, new Vector3(0f, 3.3f, 0f), new Vector3(0.35f, 0.08f, 7.5f), steelMat);
            }
            else if (n.Contains("container_40ft"))
            {
                AddPart(root.transform, "Container40", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0f), new Vector3(2.45f, 2.9f, 12.0f), steelMat);
            }
            else if (n.Contains("container_20ft"))
            {
                AddPart(root.transform, "Container20", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0f), new Vector3(2.45f, 2.9f, 6.0f), steelMat);
            }
            else if (n.Contains("tree") || n.Contains("palm"))
            {
                AddPart(root.transform, "Trunk", PrimitiveType.Cylinder, new Vector3(0f, 1.8f, 0f), new Vector3(0.45f, 1.8f, 0.45f), woodMat);
                AddPart(root.transform, "Canopy", PrimitiveType.Sphere, new Vector3(0f, 4.4f, 0f), new Vector3(3.6f, 3.8f, 3.6f), foliageMat);
            }
            else if (n.Contains("bush") || n.Contains("scrub"))
            {
                AddPart(root.transform, "BushFoliage", PrimitiveType.Sphere, new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 1.2f, 1.8f), foliageMat);
            }
            else if (n.Contains("fuel_tank") || n.Contains("manifold"))
            {
                AddPart(root.transform, "TankCylinder", PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(2.2f, 1.6f, 2.2f), hazardMat);
                AddPart(root.transform, "PipeHeader", PrimitiveType.Cube, new Vector3(0f, 3.3f, 0f), new Vector3(2.4f, 0.35f, 0.45f), steelMat);
            }
            else if (n.Contains("relay") || n.Contains("antenna") || n.Contains("mast"))
            {
                AddPart(root.transform, "Base", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(2.6f, 1.2f, 2.6f), concreteMat);
                AddPart(root.transform, "Mast", PrimitiveType.Cylinder, new Vector3(0f, 6.5f, 0f), new Vector3(0.45f, 6.5f, 0.45f), steelMat);
                AddPart(root.transform, "Crossarm", PrimitiveType.Cube, new Vector3(0f, 11.5f, 0f), new Vector3(3.2f, 0.25f, 0.25f), hazardMat);
            }
            else if (n.Contains("sandbag") || n.Contains("hesko") || n.Contains("barrier"))
            {
                AddPart(root.transform, "BarrierWall", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(2.8f, 1.3f, 0.75f), sandMat);
            }
            else if (n.Contains("crate") || n.Contains("safe") || n.Contains("vault") || n.Contains("terminal") || n.Contains("desk") || n.Contains("rack"))
            {
                Material m = n.Contains("wood") || n.Contains("ammo") ? woodMat : (n.Contains("safe") || n.Contains("vault") || n.Contains("terminal") ? hazardMat : oliveMat);
                AddPart(root.transform, "BoxBody", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(1.3f, 1.3f, 1.1f), m);
            }
            else if (n.Contains("helipad") || n.Contains("extraction"))
            {
                AddPart(root.transform, "PadSlab", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(8.0f, 0.08f, 8.0f), concreteMat);
                AddPart(root.transform, "PadMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.17f, 0f), new Vector3(4.5f, 0.02f, 4.5f), hazardMat);
            }
            else
            {
                Material defaultMat = n.Contains("wall") || n.Contains("bldg") || n.Contains("bunker") ? concreteMat : (n.Contains("trap") ? hazardMat : steelMat);
                AddPart(root.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(1.4f, 1.5f, 1.4f), defaultMat);
            }

            if (addCollider)
            {
                Bounds combined = CalculateCombinedBounds(root);
                BoxCollider bc = root.AddComponent<BoxCollider>();
                bc.center = combined.center;
                bc.size = new Vector3(
                    Mathf.Max(0.3f, combined.size.x),
                    Mathf.Max(0.3f, combined.size.y),
                    Mathf.Max(0.3f, combined.size.z)
                );
            }
            return root;
        }

        private static GameObject AddPart(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 localScale, Material mat, Vector3 localEuler = default(Vector3))
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Collider col = part.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = Quaternion.Euler(localEuler);
            part.transform.localScale = localScale;
            MeshRenderer mr = part.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = mat;
            return part;
        }

        /// <summary>
        /// Decodes a 16-bit PCM RIFF .wav file from StreamingAssets/Audio into a playable Unity AudioClip.
        /// </summary>
        public static AudioClip LoadWavAudioClip(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;
            if (_audioClipCache.TryGetValue(eventId, out AudioClip cached) && cached != null)
            {
                return cached;
            }

            string relPath = Path.Combine("Audio", eventId.EndsWith(".wav") ? eventId : (eventId + ".wav"));
            byte[] wavBytes = ReadStreamingOrProjectBytes(relPath);
            if (wavBytes == null || wavBytes.Length < 44) return null;

            int channels = BitConverter.ToInt16(wavBytes, 22);
            int sampleRate = BitConverter.ToInt32(wavBytes, 24);
            int bitsPerSample = BitConverter.ToInt16(wavBytes, 34);
            if (channels <= 0) channels = 1;
            if (sampleRate <= 0) sampleRate = 22050;

            int dataOffset = 12;
            int dataSize = 0;
            while (dataOffset + 8 <= wavBytes.Length)
            {
                string chunkId = Encoding.ASCII.GetString(wavBytes, dataOffset, 4);
                int chunkSize = BitConverter.ToInt32(wavBytes, dataOffset + 4);
                if (chunkId == "data")
                {
                    dataOffset += 8;
                    dataSize = Mathf.Min(chunkSize, wavBytes.Length - dataOffset);
                    break;
                }
                dataOffset += 8 + chunkSize;
            }

            if (dataSize <= 0 || bitsPerSample != 16) return null;

            int sampleCount = dataSize / 2;
            float[] floatSamples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short pcm = BitConverter.ToInt16(wavBytes, dataOffset + i * 2);
                floatSamples[i] = pcm / 32768.0f;
            }

            AudioClip clip = AudioClip.Create(eventId, sampleCount / channels, channels, sampleRate, false);
            clip.SetData(floatSamples, 0);
            _audioClipCache[eventId] = clip;
            return clip;
        }
    }
}