using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ParticleAutomaton
{
    // Must match HLSL struct — stride is exactly 32 bytes:
    //   float3 position (12) + float weight (4) + float3 velocity (12) + uint classId (4)
    [StructLayout(LayoutKind.Sequential)]
    public struct GpuParticle
    {
        public Vector3 position; // 12
        public float   weight;   //  4
        public Vector3 velocity; // 12
        public uint    classId;  //  4  → total 32
    }

    public class GpuParticleSimulation : IDisposable
    {
        private readonly ComputeShader        _cs;
        private readonly ParticleAutomatonConfig _cfg;

        private ComputeBuffer _particlesRead;
        private ComputeBuffer _particlesWrite;
        private ComputeBuffer _cellCounts;
        private ComputeBuffer _cellParticleIds;
        private ComputeBuffer _overflowCount;
        private ComputeBuffer _interactionMatrix;
        private ComputeBuffer _classWeights;

        private readonly int _kernelClear;
        private readonly int _kernelBuild;
        private readonly int _kernelSim;

        private readonly Vector3Int _gridSize;
        private readonly int        _totalCells;

        private readonly int[] _overflowReadback = new int[1];

        private Vector3 _ballPosition = Vector3.zero;
        private float   _ballRadius   = 0f;
        private float   _ballForceMin = 0f;
        private float   _ballForceMax = 0f;

        public ComputeBuffer ParticlesRead   => _particlesRead;
        public int           ParticleCount   => _cfg.particleCount;
        public int           LastOverflow    { get; private set; }

        public GpuParticleSimulation(ComputeShader cs, ParticleAutomatonConfig cfg)
        {
            _cs  = cs;
            _cfg = cfg;

            _kernelClear = cs.FindKernel("ClearGrid");
            _kernelBuild = cs.FindKernel("BuildGrid");
            _kernelSim   = cs.FindKernel("Simulate");

            Vector3 vol = cfg.boundsMax - cfg.boundsMin;
            _gridSize = new Vector3Int(
                Mathf.Max(1, Mathf.FloorToInt(vol.x / cfg.cellSize)),
                Mathf.Max(1, Mathf.FloorToInt(vol.y / cfg.cellSize)),
                Mathf.Max(1, Mathf.FloorToInt(vol.z / cfg.cellSize))
            );
            _totalCells = _gridSize.x * _gridSize.y * _gridSize.z;

            AllocateBuffers();
            BindStaticBuffers();
            InitParticles();
        }

        private void AllocateBuffers()
        {
            int particleStride = Marshal.SizeOf<GpuParticle>(); // 32
            _particlesRead  = new ComputeBuffer(_cfg.particleCount, particleStride);
            _particlesWrite = new ComputeBuffer(_cfg.particleCount, particleStride);

            _cellCounts      = new ComputeBuffer(_totalCells, sizeof(int));
            _cellParticleIds = new ComputeBuffer(_totalCells * _cfg.maxParticlesPerCell, sizeof(int));
            _overflowCount   = new ComputeBuffer(1, sizeof(int));

            int n = _cfg.classes.Length;
            _interactionMatrix = new ComputeBuffer(n * n, sizeof(float));
            _interactionMatrix.SetData(_cfg.interactionMatrix);

            _classWeights = new ComputeBuffer(n, sizeof(float));
            UploadClassWeights();
        }

        // Bind buffers that never swap between frames — called once at construction.
        private void BindStaticBuffers()
        {
            _cs.SetBuffer(_kernelClear, "_CellCounts",      _cellCounts);
            _cs.SetBuffer(_kernelClear, "_OverflowCount",   _overflowCount);

            _cs.SetBuffer(_kernelBuild, "_CellCounts",      _cellCounts);
            _cs.SetBuffer(_kernelBuild, "_CellParticleIds", _cellParticleIds);
            _cs.SetBuffer(_kernelBuild, "_OverflowCount",   _overflowCount);

            _cs.SetBuffer(_kernelSim, "_CellCounts",        _cellCounts);
            _cs.SetBuffer(_kernelSim, "_CellParticleIds",   _cellParticleIds);
            _cs.SetBuffer(_kernelSim, "_InteractionMatrix", _interactionMatrix);
            _cs.SetBuffer(_kernelSim, "_ClassWeights",      _classWeights);
        }

        public void InitParticles()
        {
            int n = _cfg.classes.Length;
            var data = new GpuParticle[_cfg.particleCount];
            for (int i = 0; i < _cfg.particleCount; i++)
            {
                int cls = i % n;
                data[i] = new GpuParticle
                {
                    position = new Vector3(
                        Random.Range(_cfg.boundsMin.x, _cfg.boundsMax.x),
                        Random.Range(_cfg.boundsMin.y, _cfg.boundsMax.y),
                        Random.Range(_cfg.boundsMin.z, _cfg.boundsMax.z)
                    ),
                    velocity = Vector3.zero,
                    weight   = _cfg.classes[cls].weight,
                    classId  = (uint)cls,
                };
            }
            _particlesRead.SetData(data);
        }

        public void SetBall(Vector3 position, float radius, float forceMin, float forceMax)
        {
            _ballPosition  = position;
            _ballRadius    = radius;
            _ballForceMin  = forceMin;
            _ballForceMax  = forceMax;
        }

        public void UpdateInteractionMatrix() => _interactionMatrix.SetData(_cfg.interactionMatrix);

        public void UpdateClassWeights() => UploadClassWeights();

        void UploadClassWeights()
        {
            var weights = new float[_cfg.classes.Length];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = _cfg.classes[i].weight;
            _classWeights.SetData(weights);
        }

        public void Step(float deltaTime)
        {
            SetUniforms(deltaTime);

            // Ping-pong buffers swap each frame — rebind before every dispatch.
            _cs.SetBuffer(_kernelBuild, "_ParticlesRead",  _particlesRead);
            _cs.SetBuffer(_kernelSim,   "_ParticlesRead",  _particlesRead);
            _cs.SetBuffer(_kernelSim,   "_ParticlesWrite", _particlesWrite);

            Dispatch(_kernelClear, _totalCells);
            Dispatch(_kernelBuild, _cfg.particleCount);
            Dispatch(_kernelSim,   _cfg.particleCount);

            (_particlesRead, _particlesWrite) = (_particlesWrite, _particlesRead);
        }

        private void SetUniforms(float deltaTime)
        {
            _cs.SetFloat("_DeltaTime",         deltaTime);
            _cs.SetFloat("_Gravity",            _cfg.gravity);
            _cs.SetFloat("_Damping",            _cfg.damping);
            _cs.SetFloat("_WallBounce",         _cfg.wallBounce);
            _cs.SetFloat("_InteractionRadius",  _cfg.interactionRadius);
            _cs.SetFloat("_MaxVelocity",        _cfg.maxVelocity);
            _cs.SetFloat("_DistanceEpsilon",      0.0001f);
            _cs.SetFloat("_RepulsionStrength",    _cfg.repulsionStrength);
            _cs.SetFloat("_RepulsionRadius",      _cfg.repulsionRadius);
            _cs.SetInt(  "_UseMidpointForces",    _cfg.midpointForces ? 1 : 0);
            _cs.SetFloat("_CellSize",           _cfg.cellSize);
            _cs.SetInt(  "_ParticleCount",      _cfg.particleCount);
            _cs.SetInt(  "_ClassCount",         _cfg.classes.Length);
            _cs.SetInt(  "_MaxParticlesPerCell",_cfg.maxParticlesPerCell);
            _cs.SetInt(  "_TotalCells",         _totalCells);
            _cs.SetVector("_BoundsMin",    _cfg.boundsMin);
            _cs.SetVector("_BoundsMax",    _cfg.boundsMax);
            _cs.SetInts(  "_GridSize",     _gridSize.x, _gridSize.y, _gridSize.z);
            _cs.SetVector("_BallPosition", _ballPosition);
            _cs.SetFloat( "_BallRadius",   _ballRadius);
            _cs.SetFloat( "_BallForceMin", _ballForceMin);
            _cs.SetFloat( "_BallForceMax", _ballForceMax);
        }

        private void Dispatch(int kernel, int count)
        {
            _cs.Dispatch(kernel, Mathf.CeilToInt(count / 256f), 1, 1);
        }

        public void ReadbackOverflow()
        {
            _overflowCount.GetData(_overflowReadback);
            LastOverflow = _overflowReadback[0];
        }

        public (Vector3Int gridSize, int totalCells) GetGridInfo() => (_gridSize, _totalCells);

        public long EstimateGpuBytes()
        {
            long ps  = Marshal.SizeOf<GpuParticle>();
            long particles = _cfg.particleCount * ps * 2L;
            long cellC     = _totalCells * sizeof(int);
            long cellIds   = (long)_totalCells * _cfg.maxParticlesPerCell * sizeof(int);
            int  n         = _cfg.classes.Length;
            long matrix    = n * n * sizeof(float);
            return particles + cellC + cellIds + matrix + sizeof(int);
        }

        public void Dispose()
        {
            _particlesRead?.Release();
            _particlesWrite?.Release();
            _cellCounts?.Release();
            _cellParticleIds?.Release();
            _overflowCount?.Release();
            _interactionMatrix?.Release();
            _classWeights?.Release();
        }
    }
}
