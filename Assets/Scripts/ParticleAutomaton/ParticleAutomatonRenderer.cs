using System;
using UnityEngine;

namespace ParticleAutomaton
{
    public class ParticleAutomatonRenderer : IDisposable
    {
        private readonly Mesh     _mesh;
        private readonly Material _material;

        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _classColors;

        private readonly uint[] _args = new uint[5];

        public ParticleAutomatonRenderer(Mesh mesh, Material material, ParticleAutomatonConfig cfg)
        {
            _mesh     = mesh;
            _material = material;

            _argsBuffer = new ComputeBuffer(1, _args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            _args[0] = mesh.GetIndexCount(0);
            _args[1] = (uint)cfg.particleCount;
            _args[2] = mesh.GetIndexStart(0);
            _args[3] = mesh.GetBaseVertex(0);
            _args[4] = 0;
            _argsBuffer.SetData(_args);

            _classColors = new ComputeBuffer(Mathf.Max(1, cfg.classes.Length), sizeof(float) * 4);
            UploadClassColors(cfg);
        }

        public void UpdateClassColors(ParticleAutomatonConfig cfg)
        {
            if (_classColors == null || _classColors.count < cfg.classes.Length)
            {
                _classColors?.Release();
                _classColors = new ComputeBuffer(Mathf.Max(1, cfg.classes.Length), sizeof(float) * 4);
            }
            UploadClassColors(cfg);
        }

        private void UploadClassColors(ParticleAutomatonConfig cfg)
        {
            var colors = new Vector4[cfg.classes.Length];
            for (int i = 0; i < cfg.classes.Length; i++)
                colors[i] = (Vector4)cfg.classes[i].color;
            _classColors.SetData(colors);
        }

        public void Draw(ComputeBuffer particlesRead, ParticleAutomatonConfig cfg, Bounds bounds)
        {
            _material.SetBuffer("_Particles",     particlesRead);
            _material.SetBuffer("_ClassColors",   _classColors);
            _material.SetFloat( "_ParticleRadius", cfg.particleVisualRadius);
            _material.SetInt(   "_ClassCount",     cfg.classes.Length);

            Graphics.DrawMeshInstancedIndirect(_mesh, 0, _material, bounds, _argsBuffer);
        }

        public void Dispose()
        {
            _argsBuffer?.Release();
            _classColors?.Release();
        }
    }
}
