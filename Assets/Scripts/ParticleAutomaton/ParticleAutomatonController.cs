using UnityEngine;

namespace ParticleAutomaton
{
    [AddComponentMenu("Particle Automaton/Controller")]
    public class ParticleAutomatonController : MonoBehaviour
    {
        [Header("Assets")]
        [SerializeField] private ComputeShader computeShader;
        [SerializeField] private Mesh          particleMesh;     // assign low-poly sphere; fallback = octahedron
        [SerializeField] private Material      particleMaterial;

        [Header("Config")]
        [SerializeField] private ParticleAutomatonConfig config = new();

        [Header("Debug")]
        [SerializeField] private bool showDebugOverlay = true;

        private GpuParticleSimulation   _sim;
        private ParticleAutomatonRenderer _renderer;
        private int   _frame;
        private float _fps;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (!Validate()) return;

            if (particleMesh == null)
                particleMesh = CreateOctahedronMesh();

            _sim      = new GpuParticleSimulation(computeShader, config);
            _renderer = new ParticleAutomatonRenderer(particleMesh, particleMaterial, config);
        }

        private void OnDisable()
        {
            _sim?.Dispose();
            _renderer?.Dispose();
            _sim      = null;
            _renderer = null;
        }

        private void Update()
        {
            if (_sim == null) return;

            _sim.Step(Time.deltaTime * config.timeScale);

            var center  = (config.boundsMin + config.boundsMax) * 0.5f;
            var size    = config.boundsMax - config.boundsMin;
            var bounds  = new Bounds(center, size);
            _renderer.Draw(_sim.ParticlesRead, config, bounds);

            // Overflow readback every 30 frames to avoid GPU stall every frame.
            _frame++;
            if (_frame % 30 == 0)
                _sim.ReadbackOverflow();

            _fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-6f);
        }

        // ── Runtime buttons ────────────────────────────────────────────────────

        [ContextMenu("Reset Particles")]
        public void ResetParticles() => _sim?.InitParticles();

        [ContextMenu("Randomize Matrix")]
        public void RandomizeMatrix()
        {
            int n = config.classes.Length;
            config.interactionMatrix = new float[n * n];
            for (int i = 0; i < n * n; i++)
                config.interactionMatrix[i] = Random.Range(-20f, 20f);
            _sim?.UpdateInteractionMatrix();
        }

        [ContextMenu("Randomize Classes")]
        public void RandomizeClasses()
        {
            for (int i = 0; i < config.classes.Length; i++)
            {
                config.classes[i] = new ParticleClassConfig
                {
                    color  = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.8f, 1f),
                    weight = Random.Range(0.5f, 2f),
                };
            }
            _renderer?.UpdateClassColors(config);
        }

        // ── Validation ─────────────────────────────────────────────────────────

        private bool Validate()
        {
            bool ok = true;

            if (computeShader == null)
            { Debug.LogError("[ParticleAutomaton] computeShader not assigned.", this); ok = false; }

            if (particleMaterial == null)
            { Debug.LogError("[ParticleAutomaton] particleMaterial not assigned.", this); ok = false; }

            if (config.particleCount <= 0)
            { Debug.LogError("[ParticleAutomaton] particleCount must be > 0.", this); ok = false; }

            if (config.cellSize <= 0)
            { Debug.LogError("[ParticleAutomaton] cellSize must be > 0.", this); ok = false; }

            if (config.maxParticlesPerCell <= 0)
            { Debug.LogError("[ParticleAutomaton] maxParticlesPerCell must be > 0.", this); ok = false; }

            if (config.classes == null || config.classes.Length == 0)
            { Debug.LogError("[ParticleAutomaton] at least one class required.", this); ok = false; }
            else if (config.interactionMatrix == null ||
                     config.interactionMatrix.Length != config.classes.Length * config.classes.Length)
            { Debug.LogError($"[ParticleAutomaton] interactionMatrix must be {config.classes.Length}².", this); ok = false; }

            if (ok)
            {
                if (config.interactionRadius > config.cellSize)
                    Debug.LogWarning("[ParticleAutomaton] interactionRadius > cellSize: neighbors in cells 2+ away are missed.", this);

                if (config.maxParticlesPerCell >= 500)
                    Debug.LogWarning("[ParticleAutomaton] maxParticlesPerCell ≥ 500 wastes GPU memory for 10,000 particles.", this);

                Vector3 vol = config.boundsMax - config.boundsMin;
                if (vol.x <= 0 || vol.y <= 0 || vol.z <= 0)
                { Debug.LogError("[ParticleAutomaton] boundsMax must be greater than boundsMin on all axes.", this); ok = false; }
            }

            return ok;
        }

        // ── Debug overlay ──────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!showDebugOverlay || _sim == null) return;

            var (gs, tc) = _sim.GetGridInfo();
            long mem     = _sim.EstimateGpuBytes();

            var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(10, 10, 420, 250),
                $"Particles:    {config.particleCount:N0}\n" +
                $"Classes:      {config.classes.Length}\n" +
                $"Grid:         {gs.x}×{gs.y}×{gs.z}\n" +
                $"Total cells:  {tc:N0}\n" +
                $"Max/cell:     {config.maxParticlesPerCell}\n" +
                $"GPU memory:   {mem / 1048576.0:F2} MiB\n" +
                $"Overflow:     {_sim.LastOverflow}\n" +
                $"FPS:          {_fps:F1}",
                style);
        }

        // ── Editor gizmos ──────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
            var center = (config.boundsMin + config.boundsMax) * 0.5f;
            var size   = config.boundsMax - config.boundsMin;
            Gizmos.DrawWireCube(center, size);
        }

        // ── Mesh fallback ──────────────────────────────────────────────────────

        // Creates a unit octahedron (8 flat triangles) — the lowest-poly sphere-like primitive.
        private static Mesh CreateOctahedronMesh()
        {
            // Unique vertex per triangle face gives flat shading (low-low-poly look).
            Vector3[] v = {
                new( 0,  1,  0), new( 1,  0,  0), new( 0,  0,  1),
                new( 0,  1,  0), new( 0,  0,  1), new(-1,  0,  0),
                new( 0,  1,  0), new(-1,  0,  0), new( 0,  0, -1),
                new( 0,  1,  0), new( 0,  0, -1), new( 1,  0,  0),
                new( 0, -1,  0), new( 0,  0,  1), new( 1,  0,  0),
                new( 0, -1,  0), new(-1,  0,  0), new( 0,  0,  1),
                new( 0, -1,  0), new( 0,  0, -1), new(-1,  0,  0),
                new( 0, -1,  0), new( 1,  0,  0), new( 0,  0, -1),
            };
            int[] t = new int[v.Length];
            for (int i = 0; i < t.Length; i++) t[i] = i;

            var mesh = new Mesh { name = "Octahedron" };
            mesh.vertices  = v;
            mesh.triangles = t;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
