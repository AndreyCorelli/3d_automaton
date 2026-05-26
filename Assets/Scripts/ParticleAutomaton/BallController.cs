using System.Collections.Generic;
using UnityEngine;

namespace ParticleAutomaton
{
    [AddComponentMenu("Particle Automaton/Ball Controller")]
    public class BallController : MonoBehaviour
    {
        private static readonly Color ColorDefault  = new(0.55f, 0.27f, 0.07f);
        private static readonly Color ColorCaptured = new(1.00f, 0.50f, 0.00f);

        private ParticleAutomatonConfig _config;
        private Material _mat;
        private bool     _verticalMode;
        private bool     _isDragging;
        private Plane    _dragPlane;
        private Vector3  _dragOffset;

        public Vector3 Position   => transform.position;
        public float   WorldRadius => ComputeRadius();

        public void Init(ParticleAutomatonConfig config)
        {
            _config = config;

            var mf = gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = CreateWireframeIcosphere(1);

            _mat = new Material(Shader.Find("Unlit/Color")) { color = ColorDefault };
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;

            ApplyScale();
            PlaceAboveCube();
        }

        private void Update()
        {
            ApplyScale();

            if (Input.GetKeyDown(KeyCode.Tab))
                _verticalMode = !_verticalMode;

            bool hovering = !_isDragging && IsMouseOverBall();

            if (hovering && Input.GetMouseButtonDown(0))
                BeginDrag();

            if (_isDragging)
            {
                if (Input.GetMouseButton(0))
                    ContinueDrag();
                else
                    EndDrag();
            }

            _mat.color = (_isDragging || hovering) ? ColorCaptured : ColorDefault;
        }

        private void OnGUI()
        {
            string mode  = _verticalMode ? "vertical" : "horizontal";
            var    style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 185, 320, 24), $"Ball: {mode}  [Tab]", style);
            GUI.Label(new Rect(10, 209, 520, 24),
                $"pos={transform.position:F1}  r={ComputeRadius():F1}  f=[{_config?.ballForceMin},{_config?.ballForceMax}]",
                style);
        }

        // ── Drag ──────────────────────────────────────────────────────────────────

        private void BeginDrag()
        {
            _isDragging = true;
            _dragPlane  = BuildDragPlane();
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            _dragOffset = _dragPlane.Raycast(ray, out float enter)
                ? transform.position - ray.GetPoint(enter)
                : Vector3.zero;
        }

        private void ContinueDrag()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (_dragPlane.Raycast(ray, out float enter))
                transform.position = ray.GetPoint(enter) + _dragOffset;
        }

        private void EndDrag() => _isDragging = false;

        private Plane BuildDragPlane() => _verticalMode
            ? new Plane(Camera.main.transform.forward, transform.position)
            : new Plane(Vector3.up, transform.position);

        // ── Hit test ──────────────────────────────────────────────────────────────

        private bool IsMouseOverBall()
        {
            Ray     ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            float   r   = ComputeRadius();
            Vector3 oc  = ray.origin - transform.position;
            float   b   = Vector3.Dot(oc, ray.direction);
            float   c   = Vector3.Dot(oc, oc) - r * r;
            return  b * b - c >= 0f;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private float ComputeRadius()
        {
            if (_config == null) return 1f;
            Vector3 size = _config.boundsMax - _config.boundsMin;
            return _config.ballRadiusFraction * Mathf.Min(size.x, size.y, size.z);
        }

        private void ApplyScale()
        {
            float r = ComputeRadius();
            transform.localScale = new Vector3(r, r, r);
        }

        private void PlaceAboveCube()
        {
            if (_config == null) return;
            Vector3 center = (_config.boundsMin + _config.boundsMax) * 0.5f;
            float   r      = ComputeRadius();
            transform.position = new Vector3(center.x, _config.boundsMax.y + r * 1.5f, center.z);
        }

        // ── Mesh: wireframe (edges only) 1-subdivision icosphere ─────────────────

        private static Mesh CreateWireframeIcosphere(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var pts = new List<Vector3>
            {
                Norm(new(-1,  t,  0)), Norm(new( 1,  t,  0)),
                Norm(new(-1, -t,  0)), Norm(new( 1, -t,  0)),
                Norm(new( 0, -1,  t)), Norm(new( 0,  1,  t)),
                Norm(new( 0, -1, -t)), Norm(new( 0,  1, -t)),
                Norm(new( t,  0, -1)), Norm(new( t,  0,  1)),
                Norm(new(-t,  0, -1)), Norm(new(-t,  0,  1)),
            };

            int[] raw = {
                0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
                1,5,9,   5,11,4,  11,10,2, 10,7,6,  7,1,8,
                3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
                4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1,
            };

            var faces = new List<(int a, int b, int c)>();
            for (int i = 0; i < raw.Length; i += 3)
                faces.Add((raw[i], raw[i + 1], raw[i + 2]));

            var midCache = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<(int, int, int)>(faces.Count * 4);
                foreach (var (a, b, c) in faces)
                {
                    int ab = Mid(a, b, pts, midCache);
                    int bc = Mid(b, c, pts, midCache);
                    int ca = Mid(c, a, pts, midCache);
                    next.Add((a, ab, ca));
                    next.Add((b, bc, ab));
                    next.Add((c, ca, bc));
                    next.Add((ab, bc, ca));
                }
                faces = next;
                midCache.Clear();
            }

            // Extract unique edges and build a Lines index buffer.
            var edgeSet     = new HashSet<long>();
            var edgeIndices = new List<int>(faces.Count * 3 * 2);
            foreach (var (a, b, c) in faces)
            {
                AddEdge(a, b, edgeSet, edgeIndices);
                AddEdge(b, c, edgeSet, edgeIndices);
                AddEdge(c, a, edgeSet, edgeIndices);
            }

            var mesh = new Mesh { name = "WireframeIcosphere" };
            mesh.vertices = pts.ToArray();
            mesh.SetIndices(edgeIndices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddEdge(int a, int b, HashSet<long> seen, List<int> indices)
        {
            long key = a < b
                ? ((long)a << 32 | (uint)b)
                : ((long)b << 32 | (uint)a);
            if (!seen.Add(key)) return;
            indices.Add(a);
            indices.Add(b);
        }

        private static Vector3 Norm(Vector3 v) => v.normalized;

        private static int Mid(int a, int b, List<Vector3> pts, Dictionary<long, int> cache)
        {
            long key = a < b
                ? ((long)a << 32 | (uint)b)
                : ((long)b << 32 | (uint)a);
            if (cache.TryGetValue(key, out int idx)) return idx;
            idx = pts.Count;
            pts.Add((pts[a] + pts[b]).normalized);
            cache[key] = idx;
            return idx;
        }
    }
}
