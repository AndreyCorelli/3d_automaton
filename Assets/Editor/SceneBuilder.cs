// Run via menu: ParticleAutomaton → Build Demo Scene
// Full automated setup: URP pipeline, scene, camera, lights, controller, bounds wireframe.

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ParticleAutomaton.Editor
{
    public static class SceneBuilder
    {
        const string k_ScenePath    = "Assets/Scenes/ParticleAutomatonDemo.unity";
        const string k_MatPath      = "Assets/Materials/ParticleMaterial.mat";
        const string k_BoundsPath   = "Assets/Materials/BoundsWireframe.mat";
        const string k_GroundPath   = "Assets/Materials/GroundPlane.mat";
        const string k_ComputePath  = "Assets/Shaders/ParticleAutomaton.compute";
        const string k_RendererPath = "Assets/Rendering/UniversalRendererData.asset";
        const string k_PipelinePath = "Assets/Rendering/UniversalRenderPipeline.asset";

        // ── Entry point ────────────────────────────────────────────────────────

        [MenuItem("ParticleAutomaton/Build Demo Scene", priority = 1)]
        static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolders();
            EnsureUrpPipeline();

            var particleMat  = EnsureParticleMaterial();
            var boundsMat    = EnsureBoundsMaterial();
            var groundMat    = EnsureGroundPlaneMaterial();
            var cs           = AssetDatabase.LoadAssetAtPath<ComputeShader>(k_ComputePath);

            if (cs == null)
                Debug.LogWarning("[SceneBuilder] Compute shader not found at " + k_ComputePath +
                                 " — assign it manually on ParticleAutomatonController after build.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureRenderSettings();
            CreateDirectionalLight();

            var cameraGO    = CreateCamera();
            var automatonGO = CreateAutomaton(cs, particleMat);
            CreateBoundsWireframe(automatonGO.transform, boundsMat);
            CreateGroundPlane(groundMat);
            LinkOrbitTarget(cameraGO, automatonGO.transform);

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, k_ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[SceneBuilder] Scene saved → " + k_ScenePath);
            EditorUtility.DisplayDialog("Scene Built",
                "ParticleAutomatonDemo saved.\n\nPress Play to run the simulation.", "OK");
        }

        [MenuItem("ParticleAutomaton/Build Demo Scene", validate = true)]
        static bool ValidateBuildScene() => !Application.isPlaying;

        // ── Folders ────────────────────────────────────────────────────────────

        static void EnsureFolders()
        {
            foreach (var (parent, child) in new[]
            {
                ("Assets", "Scenes"),
                ("Assets", "Materials"),
                ("Assets", "Rendering"),
                ("Assets", "Editor"),
            })
            {
                if (!AssetDatabase.IsValidFolder(parent + "/" + child))
                    AssetDatabase.CreateFolder(parent, child);
            }
        }

        // ── URP pipeline ───────────────────────────────────────────────────────

        static void EnsureUrpPipeline()
        {
            // If any quality level or global slot already has a pipeline, leave it alone.
            if (GraphicsSettings.defaultRenderPipeline != null) return;

            // Load or create renderer data.
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(k_RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, k_RendererPath);
            }

            // Load or create pipeline asset.
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(k_PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, k_PipelinePath);
            }

            // Assign as the global default render pipeline.
            // Unity automatically marks ProjectSettings/GraphicsSettings.asset as dirty
            // and persists the change on the next project save.
            GraphicsSettings.defaultRenderPipeline = pipeline;

            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] URP pipeline created and assigned → " + k_PipelinePath);
        }

        // ── Materials ──────────────────────────────────────────────────────────

        static Material EnsureParticleMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(k_MatPath);
            if (existing != null) return existing;

            // Custom shader compiles after first import; fall back to Lit if not ready yet.
            var shader = Shader.Find("ParticleAutomaton/ParticleInstanced")
                      ?? Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
            {
                Debug.LogError("[SceneBuilder] No shader found for ParticleMaterial — assign manually.");
                return null;
            }

            var mat = new Material(shader) { name = "ParticleMaterial" };
            AssetDatabase.CreateAsset(mat, k_MatPath);
            return mat;
        }

        static Material EnsureBoundsMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(k_BoundsPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Color");

            if (shader == null)
            {
                Debug.LogWarning("[SceneBuilder] No Unlit shader — bounds wireframe will be invisible.");
                return null;
            }

            var mat = new Material(shader) { name = "BoundsWireframe" };
            // _BaseColor = URP Unlit;  _Color = legacy Unlit — both set, harmless if absent.
            mat.SetColor("_BaseColor", new Color(0.30f, 0.75f, 1f, 1f));
            mat.SetColor("_Color",     new Color(0.30f, 0.75f, 1f, 1f));
            AssetDatabase.CreateAsset(mat, k_BoundsPath);
            return mat;
        }

        static Material EnsureGroundPlaneMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(k_GroundPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogWarning("[SceneBuilder] No Lit shader — ground plane will use default material.");
                return null;
            }

            float c   = 192f / 255f;
            var   mat = new Material(shader) { name = "GroundPlane" };
            mat.SetColor("_BaseColor", new Color(c, c, c, 1f));
            mat.SetColor("_Color",     new Color(c, c, c, 1f));
            AssetDatabase.CreateAsset(mat, k_GroundPath);
            return mat;
        }

        // ── Render settings ────────────────────────────────────────────────────

        static void ConfigureRenderSettings()
        {
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.18f, 0.18f, 0.18f, 1f);
            RenderSettings.skybox       = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.fog          = false;
        }

        // ── Directional light ─────────────────────────────────────────────────

        static void CreateDirectionalLight()
        {
            var go    = new GameObject("Directional Light");
            var light = go.AddComponent<Light>();
            light.type      = LightType.Directional;
            light.intensity = 0.4f;
            light.color     = Color.white;
            light.shadows   = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // ── Camera ─────────────────────────────────────────────────────────────

        static GameObject CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";

            var cam             = go.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.Skybox;
            cam.fieldOfView     = 60f;
            cam.nearClipPlane   = 0.3f;
            cam.farClipPlane    = 1000f;

            go.AddComponent<AudioListener>();
            go.AddComponent<OrbitCamera>();

            // OrbitCamera.Start() reads the initial transform to seed yaw/pitch/distance.
            go.transform.position = new Vector3(80f, 80f, -120f);
            go.transform.LookAt(Vector3.zero);

            return go;
        }

        // ── ParticleAutomaton ─────────────────────────────────────────────────

        static GameObject CreateAutomaton(ComputeShader cs, Material mat)
        {
            var go         = new GameObject("ParticleAutomaton");
            var controller = go.AddComponent<ParticleAutomatonController>();

            // Write into [SerializeField] private fields via SerializedObject.
            var so = new SerializedObject(controller);
            if (cs  != null) so.FindProperty("computeShader").objectReferenceValue    = cs;
            if (mat != null) so.FindProperty("particleMaterial").objectReferenceValue = mat;
            so.ApplyModifiedPropertiesWithoutUndo();

            return go;
        }

        // ── Ground plane ──────────────────────────────────────────────────────
        // Endless flat plane sitting at y = -50 (bottom face of the simulation volume).

        static void CreateGroundPlane(Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "GroundPlane";
            // Unity's Plane primitive is 10×10 units; scale ×1000 → 10,000×10,000 units.
            go.transform.position   = new Vector3(0f, -50f, 0f);
            go.transform.localScale = new Vector3(1000f, 1f, 1000f);

            if (mat != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ── Bounds wireframe ──────────────────────────────────────────────────
        // 12 line-segment edges of the simulation AABB rendered via MeshTopology.Lines.

        static void CreateBoundsWireframe(Transform parent, Material mat)
        {
            if (mat == null) return;

            var go = new GameObject("BoundsWireframe");
            go.transform.SetParent(parent, worldPositionStays: false);

            var mf            = go.AddComponent<MeshFilter>();
            var mr            = go.AddComponent<MeshRenderer>();
            mf.sharedMesh     = BuildWireBoxMesh(new(-50f, -50f, -50f), new(50f, 50f, 50f));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows    = false;
            mr.lightProbeUsage   = UnityEngine.Rendering.LightProbeUsage.Off;
        }

        static Mesh BuildWireBoxMesh(Vector3 min, Vector3 max)
        {
            var v = new[]
            {
                new Vector3(min.x, min.y, min.z), // 0 — left  bottom back
                new Vector3(max.x, min.y, min.z), // 1 — right bottom back
                new Vector3(max.x, min.y, max.z), // 2 — right bottom front
                new Vector3(min.x, min.y, max.z), // 3 — left  bottom front
                new Vector3(min.x, max.y, min.z), // 4 — left  top    back
                new Vector3(max.x, max.y, min.z), // 5 — right top    back
                new Vector3(max.x, max.y, max.z), // 6 — right top    front
                new Vector3(min.x, max.y, max.z), // 7 — left  top    front
            };

            var idx = new[]
            {
                0,1,  1,2,  2,3,  3,0,  // bottom ring
                4,5,  5,6,  6,7,  7,4,  // top ring
                0,4,  1,5,  2,6,  3,7,  // vertical pillars
            };

            var mesh = new Mesh { name = "BoundsWireframe" };
            mesh.vertices = v;
            mesh.SetIndices(idx, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ── Orbit camera wiring ────────────────────────────────────────────────

        static void LinkOrbitTarget(GameObject cameraGO, Transform target)
        {
            var orbit = cameraGO.GetComponent<OrbitCamera>();
            if (orbit == null) return;

            var so = new SerializedObject(orbit);
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
