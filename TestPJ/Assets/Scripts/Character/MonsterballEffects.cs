using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Yeolha.BeltScroll
{
    /// <summary>
    /// Particle System effects for Monsterball.
    /// Controls WHEN and WHERE effects play; visual parameters are authored
    /// on Unity ParticleSystem components and materials.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterballEffects : MonoBehaviour
    {
        [SerializeField] private MonsterballRedStats stats;
        [SerializeField] private Transform visualRoot;

        [Header("Horn Fire")]
        [SerializeField] private ParticleSystem hornFire;
        [SerializeField] private ParticleSystem hornEmbers;

        [Header("Move Trail")]
        [SerializeField] private ParticleSystem darkSmoke;
        [SerializeField] private ParticleSystem whiteSmoke;
        [SerializeField] private ParticleSystem moveTrailSparks;

        [Header("Impact")]
        [SerializeField] private ParticleSystem impactFlash;
        [SerializeField] private ParticleSystem impactFlame;
        [SerializeField] private ParticleSystem impactSparks;
        [SerializeField] private ParticleSystem impactSmoke;

        [Header("Ground Burst")]
        [SerializeField] private ParticleSystem groundBurstDust;
        [SerializeField] private ParticleSystem groundBurstDebris;
        [SerializeField] private ParticleSystem groundBurstScuff;

        [Header("Death")]
        [SerializeField] private ParticleSystem deathSmoke;
        [SerializeField] private ParticleSystem deathFlame;
        [SerializeField] private ParticleSystem deathGlow;
        [SerializeField] private ParticleSystem deathSparks;

        [Header("Horn flames")]
        [Tooltip("Fallback radius when the model has no renderer bounds at runtime.")]
        [SerializeField, Min(0.1f)] private float bodyRadius = 1.65f;
        [SerializeField, Range(4f, 20f)] private float flamesPerHornPerSecond = 11f;
        [SerializeField, Range(0.3f, 1.2f)] private float hornFlameSize = 0.85f;
        [SerializeField] private Vector3 flameCenterOffset = Vector3.zero;

        // Public properties for external inspection / test probes
        public ParticleSystem DarkSmoke => darkSmoke;
        public ParticleSystem WhiteSmoke => whiteSmoke;
        public ParticleSystem HornFire => hornFire;
        public ParticleSystem HornEmbers => hornEmbers;
        public ParticleSystem MoveTrailSparks => moveTrailSparks;
        public ParticleSystem ImpactFlash => impactFlash;
        public ParticleSystem ImpactFlame => impactFlame;
        public ParticleSystem ImpactSparks => impactSparks;
        public ParticleSystem ImpactSmoke => impactSmoke;
        public ParticleSystem GroundBurstDust => groundBurstDust;
        public ParticleSystem GroundBurstDebris => groundBurstDebris;
        public ParticleSystem GroundBurstScuff => groundBurstScuff;
        public ParticleSystem DeathSmoke => deathSmoke;
        public ParticleSystem DeathFlame => deathFlame;
        public ParticleSystem DeathGlow => deathGlow;
        public ParticleSystem DeathSparks => deathSparks;

        private float previousHP;
        private float nextTrailTime;
        private float lastRollTrailSample;
        private Vector3 previousRollPosition;
        private float nextMotionSmokeTime;
        private float emberAccumulator;
        private readonly List<Transform> horns = new List<Transform>(18);
        private float[] hornEmissionAccumulators = Array.Empty<float>();
        private int emergePulse;
        private float effectScale = 1f;
        private float groundOffset = 0.29f;
        private float groundY;
        private Vector3 modelCenterLocal = new Vector3(0f, 1f, 0f);
        private Vector3 previousCenter;
        private bool burning;
        private bool ready;

        private void Awake()
        {
            if (stats == null) stats = GetComponent<MonsterballRedStats>();
            if (visualRoot == null) visualRoot = transform.Find("Group");
            if (stats != null) previousHP = stats.CurrentHP;
            CharacterController body = GetComponent<CharacterController>();
            if (body != null) groundOffset = Mathf.Max(0f,
                (body.center.y - body.height * 0.5f) * transform.lossyScale.y);
            groundY = transform.position.y + groundOffset;
            CalibrateToModel();
            CacheHorns();
            ResolveReferences();

            if (hornFire != null)
                hornFire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ready = true;
            previousCenter = FireCenter();
        }

        public void ResolveReferences()
        {
            Transform vfx = transform.Find("VFX");
            if (vfx == null) return;

            if (hornFire == null) hornFire = vfx.Find("HornFire/Flame")?.GetComponent<ParticleSystem>();
            if (hornEmbers == null) hornEmbers = vfx.Find("HornFire/Embers")?.GetComponent<ParticleSystem>();

            if (darkSmoke == null) darkSmoke = vfx.Find("MoveTrail/DarkSmoke")?.GetComponent<ParticleSystem>();
            if (whiteSmoke == null) whiteSmoke = vfx.Find("MoveTrail/WhiteSmoke")?.GetComponent<ParticleSystem>();
            if (moveTrailSparks == null) moveTrailSparks = vfx.Find("MoveTrail/Sparks")?.GetComponent<ParticleSystem>();

            if (impactFlash == null) impactFlash = vfx.Find("Impact/Flash")?.GetComponent<ParticleSystem>();
            if (impactFlame == null) impactFlame = vfx.Find("Impact/Flame")?.GetComponent<ParticleSystem>();
            if (impactSparks == null) impactSparks = vfx.Find("Impact/Sparks")?.GetComponent<ParticleSystem>();
            if (impactSmoke == null) impactSmoke = vfx.Find("Impact/Smoke")?.GetComponent<ParticleSystem>();

            if (groundBurstDust == null) groundBurstDust = vfx.Find("GroundBurst/Dust")?.GetComponent<ParticleSystem>();
            if (groundBurstDebris == null) groundBurstDebris = vfx.Find("GroundBurst/Debris")?.GetComponent<ParticleSystem>();
            if (groundBurstScuff == null) groundBurstScuff = vfx.Find("GroundBurst/GroundScuff")?.GetComponent<ParticleSystem>();

            if (deathSmoke == null) deathSmoke = vfx.Find("Death/Smoke")?.GetComponent<ParticleSystem>();
            if (deathFlame == null) deathFlame = vfx.Find("Death/Flame")?.GetComponent<ParticleSystem>();
            if (deathGlow == null) deathGlow = vfx.Find("Death/Glow")?.GetComponent<ParticleSystem>();
            if (deathSparks == null) deathSparks = vfx.Find("Death/Sparks")?.GetComponent<ParticleSystem>();
        }

        private void CalibrateToModel()
        {
            if (visualRoot == null) return;
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (!found) return;
            float measuredRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            if (measuredRadius < 0.1f) return;
            CharacterController body = GetComponent<CharacterController>();
            float bodyScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            bodyRadius = body != null
                ? Mathf.Clamp(measuredRadius * 1.04f, body.radius * bodyScale * 0.85f,
                    body.radius * bodyScale * 1.1f)
                : measuredRadius * 1.04f;
            modelCenterLocal = visualRoot.InverseTransformPoint(bounds.center);
            effectScale = Mathf.Clamp(bodyRadius / 1.65f, 0.5f, 3f);
        }

        private void CacheHorns()
        {
            if (visualRoot == null) return;
            foreach (Transform bone in visualRoot.GetComponentsInChildren<Transform>(true))
            {
                string name = bone.name;
                if (!name.StartsWith("Horn", StringComparison.Ordinal) || name.Length < 7 ||
                    (name[name.Length - 2] != '_' ||
                     (name[name.Length - 1] != 'L' && name[name.Length - 1] != 'R')))
                    continue;
                if (int.TryParse(name.Substring(4, name.Length - 6), out int number) &&
                    number >= 1 && number <= 9)
                    horns.Add(bone);
            }
            hornEmissionAccumulators = new float[horns.Count];
            for (int i = 0; i < horns.Count; i++)
                hornEmissionAccumulators[i] = Random.value;
            if (horns.Count == 0)
                Debug.LogWarning("Monsterball horn bones were not found; horn fire is disabled.", this);
        }

        private void LateUpdate()
        {
            Vector3 center = FireCenter();
            Vector3 movement = center - previousCenter;
            previousCenter = center;
            if (!ready || !burning) return;
            EmitMotionSmoke(center, movement);
            EmitHornFlames(Time.deltaTime);

            emberAccumulator += Time.deltaTime * 7f;
            if (emberAccumulator < 1f) return;
            emberAccumulator -= 1f;
            if (horns.Count > 0 && hornEmbers != null)
            {
                Vector3 emberSource = horns[Random.Range(0, horns.Count)].position;
                EmitAt(hornEmbers, emberSource, 1);
            }
        }

        private Vector3 FireCenter()
        {
            return (visualRoot != null ? visualRoot.TransformPoint(modelCenterLocal) : transform.position)
                + flameCenterOffset;
        }

        private void EmitMotionSmoke(Vector3 center, Vector3 movement)
        {
            if (Time.deltaTime <= 0f || Time.time < nextMotionSmokeTime) return;
            movement.y = 0f;
            float speed = movement.magnitude / Time.deltaTime;
            if (speed < 0.35f * effectScale) return;
            nextMotionSmokeTime = Time.time + 0.055f;
            Vector3 direction = movement.normalized;
            Vector3 source = center - direction * bodyRadius * 0.7f
                - Vector3.up * bodyRadius * 0.35f;
            Vector3 drift = -direction + Vector3.up * 0.3f;
            DirectionalEmit(darkSmoke, source, drift, 2, 0.8f);
            DirectionalEmit(whiteSmoke, source + Vector3.up * bodyRadius * 0.08f, drift, 1, 0.8f);
        }

        private void EmitHornFlames(float dt)
        {
            if (hornFire == null) return;
            Vector3 center = FireCenter();
            for (int i = 0; i < horns.Count; i++)
            {
                Transform horn = horns[i];
                if (horn == null || !horn.gameObject.activeInHierarchy) continue;
                hornEmissionAccumulators[i] += dt * flamesPerHornPerSecond;
                int count = Mathf.Min(2, Mathf.FloorToInt(hornEmissionAccumulators[i]));
                hornEmissionAccumulators[i] -= count;
                for (int j = 0; j < count; j++)
                {
                    Vector3 radial = horn.position - center;
                    Vector3 outward = radial.sqrMagnitude > 0.001f
                        ? radial.normalized : horn.up;
                    // Follow animated horn sockets. A capsule radius is not a mesh radius;
                    // clamping to it can bury the flames inside the animated model.
                    Vector3 source = horn.position + outward * (0.04f * effectScale);
                    Vector3 velocity = Vector3.up * Random.Range(0.1f, 0.35f)
                        + outward * Random.Range(0.06f, 0.16f);
                    var flame = new ParticleSystem.EmitParams
                    {
                        position = source,
                        velocity = velocity,
                        applyShapeToPosition = false
                    };
                    hornFire.Emit(flame, 1);
                }
            }
        }

        private void OnEnable()
        {
            if (stats == null) stats = GetComponent<MonsterballRedStats>();
            if (stats != null) stats.OnHealthChanged += OnHealthChanged;
        }

        private void OnDisable()
        {
            if (stats != null) stats.OnHealthChanged -= OnHealthChanged;
        }

        private void EmitAt(ParticleSystem system, Vector3 position, int count, float radius = 0f)
        {
            if (system == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = position;
                if (radius > 0.001f)
                    spawnPos += Random.insideUnitSphere * (radius * effectScale);
                var ep = new ParticleSystem.EmitParams
                {
                    position = spawnPos,
                    applyShapeToPosition = false
                };
                system.Emit(ep, 1);
            }
        }

        private void DirectionalEmit(ParticleSystem system, Vector3 position, Vector3 direction, int count, float speedScale = 1f)
        {
            if (system == null || count <= 0) return;
            var main = system.main;
            float baseSpeed = main.startSpeed.mode == ParticleSystemCurveMode.TwoConstants
                ? (main.startSpeed.constantMin + main.startSpeed.constantMax) * 0.5f
                : (main.startSpeed.mode == ParticleSystemCurveMode.Constant ? main.startSpeed.constant : 1f);
            if (baseSpeed <= 0.001f) baseSpeed = 1f;
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = Vector3.Slerp(direction, Random.onUnitSphere, 0.35f).normalized
                    * (baseSpeed * speedScale * effectScale * Random.Range(0.6f, 1.25f));
                var ep = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = velocity,
                    applyShapeToPosition = false
                };
                system.Emit(ep, 1);
            }
        }

        private void GroundScuff(Vector3 position, int count)
        {
            if (groundBurstScuff == null || count <= 0) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 scuffPos = new Vector3(
                    position.x + Random.Range(-0.55f, 0.55f) * effectScale,
                    groundY + 0.03f * effectScale,
                    position.z + Random.Range(-0.55f, 0.55f) * effectScale);
                var ep = new ParticleSystem.EmitParams
                {
                    position = scuffPos,
                    rotation = Random.Range(0f, 360f),
                    applyShapeToPosition = false
                };
                groundBurstScuff.Emit(ep, 1);
            }
        }

        public void Emerge()
        {
            if (!ready) return;
            emergePulse = 0;
            EmitGroundBurst(0.8f);
        }

        public void EmergeProgress(float progress)
        {
            if (!ready) return;
            int pulse = Mathf.Clamp(Mathf.FloorToInt(progress * 4f), 0, 4);
            while (emergePulse < pulse)
            {
                emergePulse++;
                EmitGroundBurst(emergePulse == 2 ? 0.65f : 0.4f);
                if (emergePulse == 2)
                {
                    Vector3 vent = new Vector3(transform.position.x, groundY + 0.12f, transform.position.z);
                    DirectionalEmit(darkSmoke, vent, Vector3.up, 18, 2.3f);
                    DirectionalEmit(groundBurstDebris, vent, Vector3.up, 10, 2.8f);
                }
            }
        }

        private void EmitGroundBurst(float strength)
        {
            Vector3 ground = new Vector3(transform.position.x, groundY, transform.position.z);
            int count = Mathf.RoundToInt(12f * strength);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.Range(-0.15f, 0.15f)) * Mathf.PI * 2f / count;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 source = ground + radial * bodyRadius * Random.Range(0.58f, 0.88f) + Vector3.up * 0.04f;
                Vector3 vel = radial * Random.Range(0.35f, 0.8f) + Vector3.up * Random.Range(0.1f, 0.32f);

                if (groundBurstDust != null)
                {
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = source,
                        velocity = vel,
                        applyShapeToPosition = false
                    };
                    groundBurstDust.Emit(ep, 1);
                }

                if (i % 2 == 0 && groundBurstDebris != null)
                {
                    Vector3 debrisVel = radial * Random.Range(0.8f, 1.6f) + Vector3.up * Random.Range(0.45f, 0.95f);
                    var ep = new ParticleSystem.EmitParams
                    {
                        position = source,
                        velocity = debrisVel,
                        applyShapeToPosition = false
                    };
                    groundBurstDebris.Emit(ep, 1);
                }
            }
            GroundScuff(ground, Mathf.CeilToInt(5f * strength));
        }

        public void SetBurning(bool burning)
        {
            if (!ready || this.burning == burning) return;
            this.burning = burning;
            if (burning)
            {
                if (hornFire != null) hornFire.Play();
            }
            else
            {
                if (hornFire != null)
                    hornFire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                emberAccumulator = 0f;
                for (int i = 0; i < hornEmissionAccumulators.Length; i++)
                    hornEmissionAccumulators[i] = Random.value;
            }
        }

        public void RollTrail()
        {
            if (!ready || Time.time < nextTrailTime) return;
            nextTrailTime = Time.time + 0.045f;
            Vector3 position = transform.position;
            if (Time.time - lastRollTrailSample > 0.35f)
            {
                previousRollPosition = position;
                lastRollTrailSample = Time.time;
                return;
            }
            Vector3 travel = position - previousRollPosition;
            previousRollPosition = position;
            lastRollTrailSample = Time.time;
            travel.y = 0f;
            if (travel.sqrMagnitude < 0.0004f * effectScale * effectScale) return;
            Vector3 direction = travel.normalized;
            Vector3 behind = position - direction * (bodyRadius * 0.6f);
            behind.y = groundY + 0.1f * effectScale;

            DirectionalEmit(darkSmoke, behind, -direction + Vector3.up * 0.15f, 2, 0.8f);
            DirectionalEmit(whiteSmoke, behind + Vector3.up * 0.08f * effectScale, -direction + Vector3.up * 0.15f, 1, 0.7f);
            // Rolling uses friction sparks; reserve the large flash for an impact.
            EmitAt(moveTrailSparks, behind, 1);
            GroundScuff(behind, 1);
        }

        public void Land()
        {
            if (!ready) return;
            EmitGroundBurst(0.7f);
        }

        public void Impact(Vector3 position)
        {
            if (!ready) return;
            EmitAt(impactFlash, position, 4, 0.2f);
            EmitAt(impactFlame, position, 5, 0.2f);
            EmitAt(impactSparks, position, 12, 0.2f);
            EmitAt(impactSmoke, position, 3, 0.2f);
        }

        public void Detonate()
        {
            if (!ready) return;
            Vector3 core = new Vector3(transform.position.x, groundY + bodyRadius * 0.55f, transform.position.z);
            EmitAt(impactFlash, core, 8, 0.3f);
            DirectionalEmit(impactFlame, core, Vector3.up, 36, 1.8f);
            DirectionalEmit(deathSmoke != null ? deathSmoke : darkSmoke, core, Vector3.up, 18, 1.5f);
            DirectionalEmit(impactSparks, core, Vector3.up, 24, 2f);
            EmitGroundBurst(0.9f);
        }

        private void OnHealthChanged(float current, float maximum)
        {
            if (current < previousHP && current > 0f && ready)
            {
                Vector3 surface = FireCenter() + transform.forward * bodyRadius * 0.7f;
                EmitAt(impactSparks, surface, 8, 0.15f);
                EmitAt(impactFlash, surface, 2, 0.15f);
            }
            previousHP = current;
        }

        public void Death()
        {
            if (!ready) return;
            SetBurning(false);
            Vector3 center = FireCenter();
            EmitAt(deathSmoke != null ? deathSmoke : darkSmoke, center, 14, 0.35f);
            EmitAt(deathFlame != null ? deathFlame : impactFlame, center, 12, 0.35f);
            EmitAt(deathGlow != null ? deathGlow : impactFlash, center, 8, 0.35f);
            EmitAt(deathSparks != null ? deathSparks : impactSparks, center, 18, 0.35f);
            EmitGroundBurst(0.6f);
        }
    }
}
