# Unity Project Setup

## 1. Create the project

Open Unity Hub → New project → **Universal 3D** template → **Unity 6000.x**.
Name it `3d_automaton` and point its root at this directory so Unity adopts the
existing `Assets/` and `Packages/` folders.

---

## 2. Create the material

1. In the Project window: `Assets/Materials/` → right-click → **Create → Material**.
2. Name it `ParticleMaterial`.
3. In the Inspector, set **Shader** to `ParticleAutomaton/ParticleInstanced`.

---

## 3. Set up the scene — `ParticleAutomatonDemo`

### Camera
- Position `(80, 80, -120)`, rotate so it looks at `(0, 0, 0)`.
- Add component: `OrbitCamera`. Set **Target** to the `ParticleAutomaton` GameObject.

### Lighting
- Window → Rendering → Lighting → set **Ambient Mode** to **Color**, pick a soft
  dark grey (e.g. `#303030`).
- Add a `Directional Light` at intensity `0.4`, rotated `(50, -30, 0)`.

### ParticleAutomaton GameObject
1. Create an empty GameObject, name it `ParticleAutomaton`.
2. Add component: `ParticleAutomatonController`.
3. In the Inspector, assign:
   - **Compute Shader** → `Assets/Shaders/ParticleAutomaton.compute`
   - **Particle Mesh** → leave empty (auto-creates an octahedron fallback) **or**
     assign any low-poly sphere mesh you have.
   - **Particle Material** → `Assets/Materials/ParticleMaterial`

### Volume wireframe (optional runtime)
Add a child Cube to `ParticleAutomaton`:
- Scale `(100, 100, 100)`.
- Assign a transparent material with `Rendering Mode = Transparent`, alpha ≈ 5%.
- Or simply rely on the editor Gizmo drawn by `OnDrawGizmos`.

---

## 4. Runtime controls

Right-click `ParticleAutomatonController` in the Inspector → context menu:

| Button | Effect |
|---|---|
| Reset Particles | Scatter all particles randomly inside the volume |
| Randomize Matrix | Random interaction matrix (–20 … +20) |
| Randomize Classes | Random HSV colors and weights for each class |

---

## 5. Inspector tweaks to try

| Parameter | Safe range |
|---|---|
| `maxParticlesPerCell` | 32 – 256 (64 default, ≥500 wastes GPU memory) |
| `interactionRadius` | equal to or less than `cellSize` for correctness |
| `gravity` | 0 (weightless) to 20 |
| `damping` | 0.95 – 0.999 |

---

## 6. Expected GPU memory (default config)

| Buffer | Size |
|---|---|
| Particle ping-pong (2×) | 10 000 × 32 × 2 = ~625 KiB |
| Cell counts | 15 625 × 4 = ~61 KiB |
| Cell particle IDs | 15 625 × 64 × 4 = ~3.81 MiB |
| **Total** | **~4.5 MiB** |

With `maxParticlesPerCell = 500`: ~30.5 MiB.
