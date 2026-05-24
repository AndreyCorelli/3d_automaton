using UnityEngine;

namespace ParticleAutomaton
{
    public static class BuiltinPresets
    {
        public static readonly string[] Names = { "default", "3 bodies", "Bob Marley Drink" };

        public static ParticleAutomatonConfig Get(string name) => name switch
        {
            "default"           => Default(),
            "3 bodies"          => ThreeBodies(),
            "Bob Marley Drink"  => BobMarleyDrink(),
            _                   => null,
        };

        static ParticleAutomatonConfig Default() => new();

        static ParticleAutomatonConfig BobMarleyDrink() => new()
        {
            particleCount       = 40000,
            repulsionStrength   = 50f,
            repulsionRadius     = 5f,
            classes = new[]
            {
                new ParticleClassConfig { color = Color.red,   weight = 4f },
                new ParticleClassConfig { color = Color.green, weight = 2f },
                new ParticleClassConfig { color = Color.blue,  weight = 1f },
            },
            interactionMatrix = new float[]
            {
                3f, 0f, 0f,
                0f, 3f, 0f,
                0f, 0f, 3f,
            },
        };

        static ParticleAutomatonConfig ThreeBodies() => new()
        {
            particleCount        = 3,
            timeScale            = 2f,
            gravity              = 0f,
            damping              = 0.99f,
            wallBounce           = 0.8f,
            interactionRadius    = 100f,
            maxVelocity          = 70f,
            repulsionStrength    = 50f,
            repulsionRadius      = 5f,
            boundsMin            = new Vector3(-50f, -50f, -50f),
            boundsMax            = new Vector3( 50f,  50f,  50f),
            cellSize             = 100f,
            maxParticlesPerCell  = 3,
            particleVisualRadius = 3.15f,
            classes = new[]
            {
                new ParticleClassConfig { color = Color.red,   weight = 1f },
                new ParticleClassConfig { color = Color.green, weight = 1f },
                new ParticleClassConfig { color = Color.blue,  weight = 1f },
            },
            interactionMatrix = new float[]
            {
                3f, 3f, 3f,
                3f, 3f, 3f,
                3f, 3f, 3f,
            },
        };
    }
}
