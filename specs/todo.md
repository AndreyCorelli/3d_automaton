# Unity GPU Particle Automaton — Confirmed TODO

## Confirmed Decisions

```text
Unity version: Unity 6 / 6000.x
Render pipeline: URP
Simulation: 3D from the start
Rendering: instanced low-low-poly spheres
Particle state: GPU buffers, not GameObjects
Spatial partitioning: fixed-size cell buckets for v1
Particle collisions: no hard collisions
Wall collisions: yes, particles bounce and never penetrate walls
Gravity: scalar, downward, default 10.0
Gravity formula: acceleration = gravity * classWeight
Interaction matrix semantic: interactionMatrix[current.classId, neighbor.classId]
Positive force: attraction
Negative force: repulsion
Force falloff: matrixValue * (1 - distance / interactionRadius)
Zero-distance handling: clamp distance with epsilon
Inspector controls: yes
Runtime buttons: Reset Particles, Randomize Matrix, Randomize Classes
```

---

# TODO

## 1. Project Bootstrap

- [ ] Create Unity 6 / 6000.x project.
- [ ] Use URP.
- [ ] Create scene: `ParticleAutomatonDemo`.
- [ ] Add `Main Camera`.
- [ ] Add soft ambient lighting.
- [ ] Add optional weak directional light.
- [ ] Add empty GameObject: `ParticleAutomaton`.
- [ ] Add `ParticleAutomatonController` component.
- [ ] Add simple orbit camera controller.

---

## 2. Scene Defaults

- [ ] Set simulation volume to:

```text
100 × 100 × 100
```

- [ ] Use bounds:

```text
min = (-50, -50, -50)
max = ( 50,  50,  50)
```

- [ ] Set default camera:

```text
position = (80, 80, -120)
lookAt = (0, 0, 0)
```

- [ ] Add visible volume bounds helper:

```text
wireframe cube / transparent cube
```

---

## 3. Particle Class Config

- [ ] Define class config:

```csharp
[Serializable]
public struct ParticleClassConfig
{
    public Color color;
    public float weight;
}
```

- [ ] Use 3 default classes.
- [ ] Default colors:

```text
class 0 = red
class 1 = green
class 2 = blue
```

- [ ] Default weights:

```text
class 0 = 1.0
class 1 = 1.2
class 2 = 0.8
```

---

## 4. Interaction Matrix

- [ ] Store matrix as flat row-major `float[]`.

```text
index = currentClass * classCount + neighborClass
```

- [ ] Use this semantic everywhere:

```text
interactionMatrix[current.classId, neighbor.classId]
```

- [ ] Positive value means attraction.
- [ ] Negative value means repulsion.
- [ ] Default matrix:

```text
[
  [-10, 0, 10],
  [ 10, 0,  0],
  [ -5, 0,  5]
]
```

- [ ] Flattened default:

```text
-10, 0, 10,
 10, 0,  0,
 -5, 0,  5
```

- [ ] Add code comments explaining this exact semantic.

---

## 5. Particle Data Layout

- [ ] Use GPU particle struct:

```hlsl
struct Particle
{
    float3 position;
    float  weight;

    float3 velocity;
    uint   classId;
};
```

- [ ] Match it with C# struct.
- [ ] Ensure stride is 32 bytes.
- [ ] Use ping-pong buffers:

```text
_ParticlesRead
_ParticlesWrite
```

---

## 6. Simulation Defaults

- [ ] Use default particle count:

```text
10,000
```

- [ ] Use default gravity:

```text
10.0
```

- [ ] Gravity direction:

```text
(0, -1, 0)
```

- [ ] Gravity acceleration:

```hlsl
float3 gravityAcceleration = float3(0, -_Gravity * p.weight, 0);
```

- [ ] Use default damping:

```text
0.99
```

- [ ] Use default wall bounce:

```text
0.8
```

- [ ] Use default interaction radius:

```text
4.0
```

- [ ] Use default max velocity:

```text
100.0
```

---

## 7. Spatial Grid

- [ ] Use uniform 3D grid.
- [ ] Default cell size:

```text
4 × 4 × 4
```

- [ ] For volume `100 × 100 × 100`, grid size is:

```text
25 × 25 × 25
```

- [ ] Total cells:

```text
15,625
```

- [ ] Use fixed-size cell buckets in v1.
- [ ] Default `maxParticlesPerCell`:

```text
64
```

- [ ] Keep it configurable.
- [ ] Allow experiments with:

```text
500
```

- [ ] Warn that 500 wastes memory for 10,000 particles.

---

## 8. Spatial Grid Buffers

- [ ] Create:

```text
_CellCounts
_CellParticleIds
_OverflowCount
```

- [ ] `_CellCounts` size:

```text
totalCells
```

- [ ] `_CellParticleIds` size:

```text
totalCells * maxParticlesPerCell
```

- [ ] `_OverflowCount` size:

```text
1
```

- [ ] Use atomic increment during grid build:

```hlsl
uint slot;
InterlockedAdd(_CellCounts[cellId], 1, slot);

if (slot < _MaxParticlesPerCell)
{
    _CellParticleIds[cellId * _MaxParticlesPerCell + slot] = particleId;
}
else
{
    InterlockedAdd(_OverflowCount[0], 1);
}
```

---

## 9. Compute Shader Kernels

Create:

```text
Assets/Shaders/ParticleAutomaton.compute
```

Kernels:

```hlsl
#pragma kernel ClearGrid
#pragma kernel BuildGrid
#pragma kernel Simulate
```

Optional later:

```hlsl
#pragma kernel InitParticles
```

Use:

```hlsl
[numthreads(256, 1, 1)]
```

---

## 10. ClearGrid Kernel

- [ ] Clear all cell counts.
- [ ] Clear overflow counter.
- [ ] Dispatch over `totalCells`.
- [ ] Make sure overflow counter is cleared exactly once.

---

## 11. BuildGrid Kernel

For each particle:

- [ ] Read particle position.
- [ ] Convert position to cell coordinate.
- [ ] Clamp cell coordinate to grid.
- [ ] Flatten cell coordinate.
- [ ] Atomically allocate slot.
- [ ] Write particle ID into bucket if slot is available.
- [ ] Increment overflow counter if bucket is full.

Cell coordinate:

```hlsl
float3 local = particle.position - _BoundsMin;
int3 cell = (int3)floor(local / _CellSize);
cell = clamp(cell, int3(0, 0, 0), _GridSize - 1);
```

Flatten:

```hlsl
uint cellId =
    cell.x +
    cell.y * _GridSize.x +
    cell.z * _GridSize.x * _GridSize.y;
```

---

## 12. Simulate Kernel

For each particle:

- [ ] Read current particle.
- [ ] Find current cell.
- [ ] Inspect 27 neighboring cells:

```text
dx = -1..1
dy = -1..1
dz = -1..1
```

- [ ] Skip cells outside grid.
- [ ] Read cell count.
- [ ] Clamp count to `maxParticlesPerCell`.
- [ ] Iterate particle IDs in the cell.
- [ ] Skip self.
- [ ] Read neighbor particle.
- [ ] Calculate distance.
- [ ] Clamp distance with epsilon.
- [ ] Skip if distance > interaction radius.
- [ ] Read matrix value:

```hlsl
float matrixValue =
    _InteractionMatrix[current.classId * _ClassCount + neighbor.classId];
```

- [ ] Calculate falloff:

```hlsl
float falloff = 1.0 - distance / _InteractionRadius;
```

- [ ] Calculate force magnitude:

```hlsl
float forceMagnitude = matrixValue * falloff;
```

- [ ] Calculate direction:

```hlsl
float3 direction = delta / max(distance, _DistanceEpsilon);
```

- [ ] Accumulate force:

```hlsl
force += direction * forceMagnitude;
```

- [ ] Apply gravity:

```hlsl
force += float3(0, -_Gravity * current.weight, 0);
```

- [ ] Integrate velocity:

```hlsl
current.velocity += force * _DeltaTime;
```

- [ ] Apply damping:

```hlsl
current.velocity *= _Damping;
```

- [ ] Clamp max velocity.
- [ ] Integrate position:

```hlsl
current.position += current.velocity * _DeltaTime;
```

- [ ] Resolve wall bounce.
- [ ] Write to output buffer.
- [ ] Swap particle buffers after dispatch.

---

## 13. Wall Bounce

- [ ] Clamp position inside bounds.
- [ ] Reflect velocity component.
- [ ] Multiply reflected component by `_WallBounce`.
- [ ] Apply independently for X/Y/Z.

Example:

```hlsl
if (p.position.x < _BoundsMin.x)
{
    p.position.x = _BoundsMin.x;
    p.velocity.x = abs(p.velocity.x) * _WallBounce;
}

if (p.position.x > _BoundsMax.x)
{
    p.position.x = _BoundsMax.x;
    p.velocity.x = -abs(p.velocity.x) * _WallBounce;
}
```

---

## 14. Rendering

- [ ] Render particles as instanced low-low-poly spheres.
- [ ] Do not use one GameObject per particle.
- [ ] Use:

```text
Graphics.DrawMeshInstancedIndirect
```

- [ ] Create particle instancing shader.
- [ ] Read particle by `SV_InstanceID`.
- [ ] Read class color from `_ClassColors`.
- [ ] Position sphere at particle position.
- [ ] Scale by configurable particle visual radius.
- [ ] Default visual radius:

```text
0.15
```

---

## 15. Renderer Buffers

- [ ] Create class color buffer:

```text
_ClassColors
```

- [ ] Create indirect args buffer.
- [ ] Bind current particle read buffer to material.
- [ ] Bind class color buffer to material.
- [ ] Draw all particles in one indirect draw call.

---

## 16. Inspector Controls

Expose:

- [ ] Particle count.
- [ ] Volume size.
- [ ] Cell size.
- [ ] Max particles per cell.
- [ ] Gravity.
- [ ] Damping.
- [ ] Wall bounce.
- [ ] Interaction radius.
- [ ] Max velocity.
- [ ] Particle visual radius.
- [ ] Particle classes.
- [ ] Class colors.
- [ ] Class weights.
- [ ] Interaction matrix.

---

## 17. Runtime Buttons

Add buttons:

- [ ] Reset Particles.
- [ ] Randomize Matrix.
- [ ] Randomize Classes.

If a custom inspector is not implemented immediately, expose these via context menu methods:

```csharp
[ContextMenu("Reset Particles")]
private void ResetParticles() {}

[ContextMenu("Randomize Matrix")]
private void RandomizeMatrix() {}

[ContextMenu("Randomize Classes")]
private void RandomizeClasses() {}
```

---

## 18. Validation

- [ ] Validate `particleCount > 0`.
- [ ] Validate `cellSize > 0`.
- [ ] Validate `maxParticlesPerCell > 0`.
- [ ] Validate `classCount > 0`.
- [ ] Validate interaction matrix size:

```text
classCount × classCount
```

- [ ] Warn if:

```text
interactionRadius > cellSize
```

- [ ] Warn if:

```text
maxParticlesPerCell >= 500
```

- [ ] Warn if grid dimensions are invalid.
- [ ] Warn if estimated memory is excessive.

---

## 19. Debug Info

Show:

- [ ] Particle count.
- [ ] Class count.
- [ ] Grid size.
- [ ] Total cells.
- [ ] Max particles per cell.
- [ ] Estimated grid memory.
- [ ] Estimated total GPU memory.
- [ ] Overflow count.
- [ ] FPS.

Read back overflow count only occasionally, not every frame.

Recommended:

```text
every 30 frames
```

or only in debug mode.

---

## 20. Memory Estimate Display

For default config:

```text
volume = 100³
cellSize = 4
grid = 25³ = 15,625 cells
particleCount = 10,000
maxParticlesPerCell = 64
particle stride = 32 bytes
```

Expected approximate memory:

```text
cell particle IDs:
15,625 × 64 × 4 = 4,000,000 bytes ≈ 3.81 MiB

cell counts:
15,625 × 4 = 62,500 bytes ≈ 61 KiB

particle ping-pong:
10,000 × 32 × 2 = 640,000 bytes ≈ 625 KiB

total:
~4.5 MiB plus small overhead
```

For `maxParticlesPerCell = 500`:

```text
cell particle IDs:
15,625 × 500 × 4 = 31,250,000 bytes ≈ 29.8 MiB

total:
~30.5 MiB plus small overhead
```

---

## 21. Stability

- [ ] Use distance epsilon:

```text
0.0001
```

- [ ] Clamp velocity to max velocity.
- [ ] Apply damping.
- [ ] Prevent particles from escaping bounds.
- [ ] Avoid NaN from zero-distance normalization.
- [ ] Optional: reset invalid particles if NaN is detected.

---

## 22. Deliverables

Create:

```text
Assets/Scripts/ParticleAutomaton/ParticleAutomatonController.cs
Assets/Scripts/ParticleAutomaton/GpuParticleSimulation.cs
Assets/Scripts/ParticleAutomaton/ParticleAutomatonConfig.cs
Assets/Scripts/ParticleAutomaton/ParticleClassConfig.cs
Assets/Scripts/ParticleAutomaton/ParticleAutomatonRenderer.cs
Assets/Scripts/ParticleAutomaton/OrbitCamera.cs

Assets/Shaders/ParticleAutomaton.compute
Assets/Shaders/ParticleInstanced.shader

Assets/Materials/ParticleMaterial.mat
Assets/Scenes/ParticleAutomatonDemo.unity
```

If material and scene assets cannot be generated safely, provide exact manual setup instructions.

---

## 23. Non-Goals for v1

Do not implement:

```text
GPU sorting
hard particle collision
fluid pressure solving
trails
obstacles
multiple volumes
VFX Graph rewrite
ECS/DOTS
networking
save/load presets
complex custom editor
```

---

## 24. Future Improvements

Possible later work:

```text
sorted cell list instead of fixed buckets
class-specific interaction radius
class-specific visual radius
class-specific damping
preset save/load
runtime UI panel
trail rendering
obstacle SDF
GPU-side particle spawning/death
better matrix editor
GPU stats buffer
```

---

# Key Principle

Particles are not Unity objects.

Particles are GPU-buffer records.

Unity provides:

```text
scene
camera
ambient light
inspector config
compute dispatch
instanced rendering
debug UX
```