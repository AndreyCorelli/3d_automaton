using System;
using UnityEngine;

namespace ParticleAutomaton
{
    [Serializable]
    public class ParticleAutomatonConfig
    {
        [Header("Simulation")]
        public int   particleCount     = 10000;
        public float timeScale         = 2f;
        public float gravity           = 10f;
        public float damping           = 0.99f;
        public float wallBounce        = 0.8f;
        public float interactionRadius  = 5f;
        public float maxVelocity        = 70f;
        public float repulsionStrength  = 100f;
        public float repulsionRadius    = 2.0f;

        [Header("Volume")]
        public Vector3 boundsMin = new(-50f, -50f, -50f);
        public Vector3 boundsMax = new( 50f,  50f,  50f);

        [Header("Spatial Grid")]
        public float cellSize            = 5f;
        [Tooltip("128 is efficient for 10k particles. Values ≥ 500 waste significant GPU memory.")]
        public int   maxParticlesPerCell = 128;

        [Header("Rendering")]
        public float particleVisualRadius = 1.0f;

        [Header("Classes")]
        public ParticleClassConfig[] classes = new ParticleClassConfig[]
        {
            new() { color = Color.red,   weight = 1.0f },
            new() { color = Color.green, weight = 1.2f },
            new() { color = Color.blue,  weight = 0.8f },
        };

        // Row-major flat array: index = currentClass * classCount + neighborClass
        // Positive value = attraction, Negative value = repulsion
        // interactionMatrix[current.classId, neighbor.classId]
        public float[] interactionMatrix = new float[]
        {
            -10f,  0f, 10f,
             10f,  0f,  0f,
             -5f,  0f,  5f,
        };
    }
}
