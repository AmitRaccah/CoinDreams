using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class BuildingVisualController : MonoBehaviour
    {
        private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        [SerializeField] private BuildingDefinitionSO buildingDefinition;
        [SerializeField] private BuildingPartsRegistry partsRegistry;
        [SerializeField] private bool applyLevelZeroOnAwake = true;

        private StepActivationOp[][] activationOpsByStep = Array.Empty<StepActivationOp[]>();
        private StepTextureOp[][] textureOpsByStep = Array.Empty<StepTextureOp[]>();
        private GameObject[] partObjects = Array.Empty<GameObject>();
        private Renderer[] partRenderers = Array.Empty<Renderer>();
        private TexturePropertyState[] textureStatesByPart = Array.Empty<TexturePropertyState>();
        private MaterialPropertyBlock reusablePropertyBlock;
        private bool cacheInitialized;
        private bool valid;

        public BuildingDefinitionSO Definition
        {
            get { return buildingDefinition; }
        }

        public string BuildingId
        {
            get
            {
                if (buildingDefinition == null)
                {
                    return string.Empty;
                }

                return buildingDefinition.BuildingID;
            }
        }

        public int MaxLevel
        {
            get
            {
                EnsureCache();
                return activationOpsByStep.Length;
            }
        }

        private void Awake()
        {
            EnsureCache();

            if (applyLevelZeroOnAwake)
            {
                ApplyLevel(0);
            }
        }

        public bool ApplyLevel(int level)
        {
            EnsureCache();

            if (!valid)
            {
                return false;
            }

            int clampedLevel = level;
            if (clampedLevel < 0)
            {
                clampedLevel = 0;
            }

            if (clampedLevel > activationOpsByStep.Length)
            {
                clampedLevel = activationOpsByStep.Length;
            }

            ApplyLevelZeroState();

            int stepIndex;
            for (stepIndex = 0; stepIndex < clampedLevel; stepIndex++)
            {
                ApplyStep(stepIndex);
            }

            return true;
        }

        private void EnsureCache()
        {
            if (cacheInitialized)
            {
                return;
            }

            cacheInitialized = true;
            reusablePropertyBlock = new MaterialPropertyBlock();
            valid = BuildCache();
        }

        private bool BuildCache()
        {
            if (buildingDefinition == null)
            {
                Debug.LogError("[BuildingVisualController] Missing BuildingDefinitionSO.", this);
                return false;
            }

            BuildingPartsRegistry registry = partsRegistry;
            if (registry == null)
            {
                registry = GetComponent<BuildingPartsRegistry>();
                partsRegistry = registry;
            }

            if (registry == null)
            {
                Debug.LogError("[BuildingVisualController] Missing BuildingPartsRegistry on root " + name + ".", this);
                return false;
            }

            if (!registry.IsValid)
            {
                Debug.LogError("[BuildingVisualController] BuildingPartsRegistry invalid on root " + name + ".", this);
                return false;
            }

            int partCount = registry.PartCount;
            partObjects = new GameObject[partCount];
            partRenderers = new Renderer[partCount];
            textureStatesByPart = new TexturePropertyState[partCount];

            int partIndex;
            for (partIndex = 0; partIndex < partCount; partIndex++)
            {
                if (!registry.TryGetPartObjectByIndex(partIndex, out GameObject partObject))
                {
                    continue;
                }

                partObjects[partIndex] = partObject;

                if (registry.TryGetPartRendererByIndex(partIndex, out Renderer partRenderer))
                {
                    partRenderers[partIndex] = partRenderer;
                }

                textureStatesByPart[partIndex] = BuildTexturePropertyState(partRenderers[partIndex]);
            }

            List<BuildingUpgradeStepConfig> upgradeSteps = buildingDefinition.upgradeSteps;
            if (upgradeSteps == null)
            {
                upgradeSteps = new List<BuildingUpgradeStepConfig>(0);
            }

            int stepCount = upgradeSteps.Count;
            activationOpsByStep = new StepActivationOp[stepCount][];
            textureOpsByStep = new StepTextureOp[stepCount][];

            int stepIndex;
            for (stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                BuildingUpgradeStepConfig step = upgradeSteps[stepIndex];

                if (step == null || step.nextLevelPartStates == null || step.nextLevelPartStates.Count == 0)
                {
                    activationOpsByStep[stepIndex] = Array.Empty<StepActivationOp>();
                    textureOpsByStep[stepIndex] = Array.Empty<StepTextureOp>();
                    continue;
                }

                BuildStepOps(stepIndex, step.nextLevelPartStates, registry, out StepActivationOp[] activationOps, out StepTextureOp[] textureOps);
                activationOpsByStep[stepIndex] = activationOps;
                textureOpsByStep[stepIndex] = textureOps;
            }

            return true;
        }

        private TexturePropertyState BuildTexturePropertyState(Renderer renderer)
        {
            TexturePropertyState state;
            state.propertyId = 0;
            state.defaultTexture = null;
            state.hasDefaultTexture = false;

            if (renderer == null)
            {
                return state;
            }

            Material material = renderer.sharedMaterial;
            if (material == null)
            {
                return state;
            }

            int propertyId = 0;
            if (material.HasProperty(BaseMapPropertyId))
            {
                propertyId = BaseMapPropertyId;
            }
            else if (material.HasProperty(MainTexPropertyId))
            {
                propertyId = MainTexPropertyId;
            }

            if (propertyId == 0)
            {
                return state;
            }

            state.propertyId = propertyId;
            state.defaultTexture = material.GetTexture(propertyId);
            state.hasDefaultTexture = state.defaultTexture != null;
            return state;
        }

        private void BuildStepOps(
            int stepIndex,
            List<BuildingPartVisualStateConfig> partStates,
            BuildingPartsRegistry registry,
            out StepActivationOp[] activationOps,
            out StepTextureOp[] textureOps)
        {
            List<StepActivationOp> activationBuilder = new List<StepActivationOp>(partStates.Count);
            List<StepTextureOp> textureBuilder = new List<StepTextureOp>(partStates.Count);

            int i;
            for (i = 0; i < partStates.Count; i++)
            {
                BuildingPartVisualStateConfig stateConfig = partStates[i];
                if (stateConfig == null)
                {
                    continue;
                }

                int partIndex;
                if (!registry.TryGetPartIndex(stateConfig.partId, out partIndex))
                {
                    Debug.LogWarning("[BuildingVisualController] Part ID "
                        + stateConfig.partId
                        + " not found in root registry for building "
                        + BuildingId
                        + " at upgrade step "
                        + stepIndex
                        + ".",
                        this);
                    continue;
                }

                StepActivationOp activationOp;
                activationOp.partIndex = partIndex;
                activationOp.isActive = stateConfig.isActive;
                activationBuilder.Add(activationOp);

                if (stateConfig.texture == null)
                {
                    continue;
                }

                TexturePropertyState textureState = textureStatesByPart[partIndex];
                if (textureState.propertyId == 0)
                {
                    Debug.LogWarning("[BuildingVisualController] Part ID "
                        + stateConfig.partId
                        + " has no supported texture property (_BaseMap/_MainTex).",
                        this);
                    continue;
                }

                StepTextureOp textureOp;
                textureOp.partIndex = partIndex;
                textureOp.texture = stateConfig.texture;
                textureBuilder.Add(textureOp);
            }

            if (activationBuilder.Count == 0)
            {
                activationOps = Array.Empty<StepActivationOp>();
            }
            else
            {
                activationOps = activationBuilder.ToArray();
            }

            if (textureBuilder.Count == 0)
            {
                textureOps = Array.Empty<StepTextureOp>();
            }
            else
            {
                textureOps = textureBuilder.ToArray();
            }
        }

        private void ApplyLevelZeroState()
        {
            int partIndex;
            for (partIndex = 0; partIndex < partObjects.Length; partIndex++)
            {
                GameObject partObject = partObjects[partIndex];
                if (partObject == null)
                {
                    continue;
                }

                if (partObject.activeSelf)
                {
                    partObject.SetActive(false);
                }

                Renderer partRenderer = partRenderers[partIndex];
                TexturePropertyState textureState = textureStatesByPart[partIndex];

                if (partRenderer == null || textureState.propertyId == 0)
                {
                    continue;
                }

                if (!textureState.hasDefaultTexture)
                {
                    // No default texture exists on the material, so clear overrides.
                    partRenderer.SetPropertyBlock(null);
                    continue;
                }

                partRenderer.GetPropertyBlock(reusablePropertyBlock);
                reusablePropertyBlock.SetTexture(textureState.propertyId, textureState.defaultTexture);
                partRenderer.SetPropertyBlock(reusablePropertyBlock);
            }
        }

        private void ApplyStep(int stepIndex)
        {
            StepActivationOp[] activationOps = activationOpsByStep[stepIndex];
            StepTextureOp[] textureOps = textureOpsByStep[stepIndex];

            int i;
            for (i = 0; i < activationOps.Length; i++)
            {
                StepActivationOp op = activationOps[i];
                if (op.partIndex < 0 || op.partIndex >= partObjects.Length)
                {
                    continue;
                }

                GameObject partObject = partObjects[op.partIndex];
                if (partObject == null)
                {
                    continue;
                }

                if (partObject.activeSelf != op.isActive)
                {
                    partObject.SetActive(op.isActive);
                }
            }

            for (i = 0; i < textureOps.Length; i++)
            {
                StepTextureOp op = textureOps[i];
                if (op.partIndex < 0 || op.partIndex >= partRenderers.Length)
                {
                    continue;
                }

                Renderer partRenderer = partRenderers[op.partIndex];
                if (partRenderer == null)
                {
                    continue;
                }

                TexturePropertyState textureState = textureStatesByPart[op.partIndex];
                if (textureState.propertyId == 0)
                {
                    continue;
                }

                if (op.texture == null)
                {
                    partRenderer.SetPropertyBlock(null);
                    continue;
                }

                partRenderer.GetPropertyBlock(reusablePropertyBlock);
                reusablePropertyBlock.SetTexture(textureState.propertyId, op.texture);
                partRenderer.SetPropertyBlock(reusablePropertyBlock);
            }
        }

        private struct StepActivationOp
        {
            public int partIndex;
            public bool isActive;
        }

        private struct StepTextureOp
        {
            public int partIndex;
            public Texture texture;
        }

        private struct TexturePropertyState
        {
            public int propertyId;
            public Texture defaultTexture;
            public bool hasDefaultTexture;
        }
    }
}
