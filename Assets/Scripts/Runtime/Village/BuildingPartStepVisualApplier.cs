using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class BuildingPartStepVisualApplier : IBuildingVisualApplier
    {
        private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        private readonly GameObject[] partObjects;
        private readonly Renderer[] partRenderers;
        private readonly TexturePropertyState[] textureStatesByPart;
        private readonly StepActivationOp[][] activationOpsByStep;
        private readonly StepTextureOp[][] textureOpsByStep;
        private readonly string buildingId;
        private readonly UnityEngine.Object logContext;

        private MaterialPropertyBlock reusablePropertyBlock;

        public BuildingPartStepVisualApplier(
            BuildingDefinitionSO buildingDefinition,
            GameObject[] partObjects,
            UnityEngine.Object logContext)
        {
            this.logContext = logContext;
            this.partObjects = partObjects ?? Array.Empty<GameObject>();
            buildingId = buildingDefinition != null ? buildingDefinition.BuildingID : string.Empty;

            int partCount = this.partObjects.Length;
            if (partCount == 0)
            {
                partRenderers = Array.Empty<Renderer>();
                textureStatesByPart = Array.Empty<TexturePropertyState>();
                activationOpsByStep = Array.Empty<StepActivationOp[]>();
                textureOpsByStep = Array.Empty<StepTextureOp[]>();
                IsValid = false;
                return;
            }

            partRenderers = new Renderer[partCount];
            textureStatesByPart = new TexturePropertyState[partCount];
            BuildRendererCache();

            List<BuildingUpgradeStepConfig> upgradeSteps =
                buildingDefinition != null ? buildingDefinition.upgradeSteps : null;
            int stepCount = upgradeSteps != null ? upgradeSteps.Count : 0;

            activationOpsByStep = new StepActivationOp[stepCount][];
            textureOpsByStep = new StepTextureOp[stepCount][];
            BuildStepCaches(upgradeSteps);

            IsValid = true;
        }

        public bool IsValid { get; private set; }

        public int MaxLevel
        {
            get { return activationOpsByStep.Length; }
        }

        public void ApplyLevel(int level)
        {
            if (!IsValid)
            {
                return;
            }

            int clampedLevel = ClampLevel(level);
            ApplyLevelZeroState();

            int stepIndex;
            for (stepIndex = 0; stepIndex < clampedLevel; stepIndex++)
            {
                ApplyStep(stepIndex);
            }
        }

        public void Dispose()
        {
        }

        private int ClampLevel(int level)
        {
            if (level < 0)
            {
                return 0;
            }

            if (level > activationOpsByStep.Length)
            {
                return activationOpsByStep.Length;
            }

            return level;
        }

        private void BuildRendererCache()
        {
            int partIndex;
            for (partIndex = 0; partIndex < partObjects.Length; partIndex++)
            {
                GameObject partObject = partObjects[partIndex];
                if (partObject == null)
                {
                    continue;
                }

                Renderer partRenderer = partObject.GetComponent<Renderer>();
                if (partRenderer == null)
                {
                    partRenderer = partObject.GetComponentInChildren<Renderer>(true);
                }

                partRenderers[partIndex] = partRenderer;
                textureStatesByPart[partIndex] = BuildTexturePropertyState(partRenderer);
            }
        }

        private void BuildStepCaches(List<BuildingUpgradeStepConfig> upgradeSteps)
        {
            int stepIndex;
            for (stepIndex = 0; stepIndex < activationOpsByStep.Length; stepIndex++)
            {
                BuildingUpgradeStepConfig step = upgradeSteps[stepIndex];

                if (step == null || step.nextLevelPartStates == null || step.nextLevelPartStates.Count == 0)
                {
                    activationOpsByStep[stepIndex] = Array.Empty<StepActivationOp>();
                    textureOpsByStep[stepIndex] = Array.Empty<StepTextureOp>();
                    continue;
                }

                BuildStepOps(
                    stepIndex,
                    step.nextLevelPartStates,
                    out StepActivationOp[] activationOps,
                    out StepTextureOp[] textureOps);

                activationOpsByStep[stepIndex] = activationOps;
                textureOpsByStep[stepIndex] = textureOps;
            }
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
            out StepActivationOp[] activationOps,
            out StepTextureOp[] textureOps)
        {
            CountValidStepOps(partStates, out int activationCount, out int textureCount);

            activationOps = activationCount == 0
                ? Array.Empty<StepActivationOp>()
                : new StepActivationOp[activationCount];
            textureOps = textureCount == 0
                ? Array.Empty<StepTextureOp>()
                : new StepTextureOp[textureCount];

            int activationIndex = 0;
            int textureIndex = 0;

            int i;
            for (i = 0; i < partStates.Count; i++)
            {
                BuildingPartVisualStateConfig stateConfig = partStates[i];
                if (stateConfig == null)
                {
                    continue;
                }

                int partIndex = stateConfig.partIndex;
                if (!IsValidPartIndex(partIndex))
                {
                    LogInvalidPartIndex(partIndex, stepIndex);
                    continue;
                }

                StepActivationOp activationOp;
                activationOp.partIndex = partIndex;
                activationOp.isActive = stateConfig.isActive;
                activationOps[activationIndex] = activationOp;
                activationIndex++;

                if (stateConfig.texture == null)
                {
                    continue;
                }

                TexturePropertyState textureState = textureStatesByPart[partIndex];
                if (textureState.propertyId == 0)
                {
                    LogMissingTextureProperty(partIndex);
                    continue;
                }

                StepTextureOp textureOp;
                textureOp.partIndex = partIndex;
                textureOp.texture = stateConfig.texture;
                textureOps[textureIndex] = textureOp;
                textureIndex++;
            }
        }

        private void CountValidStepOps(
            List<BuildingPartVisualStateConfig> partStates,
            out int activationCount,
            out int textureCount)
        {
            activationCount = 0;
            textureCount = 0;

            int i;
            for (i = 0; i < partStates.Count; i++)
            {
                BuildingPartVisualStateConfig stateConfig = partStates[i];
                if (stateConfig == null || !IsValidPartIndex(stateConfig.partIndex))
                {
                    continue;
                }

                activationCount++;

                if (stateConfig.texture == null)
                {
                    continue;
                }

                TexturePropertyState textureState = textureStatesByPart[stateConfig.partIndex];
                if (textureState.propertyId != 0)
                {
                    textureCount++;
                }
            }
        }

        private bool IsValidPartIndex(int partIndex)
        {
            return partIndex >= 0 && partIndex < partObjects.Length;
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

                ClearTextureOverride(partRenderer);
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
                if (!IsValidPartIndex(op.partIndex))
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

                if (op.texture == null
                    || (textureState.hasDefaultTexture && op.texture == textureState.defaultTexture))
                {
                    ClearTextureOverride(partRenderer);
                    continue;
                }

                ApplyTextureOverride(partRenderer, textureState.propertyId, op.texture);
            }
        }

        private static void ClearTextureOverride(Renderer targetRenderer)
        {
            targetRenderer.SetPropertyBlock(null);
        }

        private void ApplyTextureOverride(Renderer targetRenderer, int propertyId, Texture texture)
        {
            if (reusablePropertyBlock == null)
            {
                reusablePropertyBlock = new MaterialPropertyBlock();
            }

            targetRenderer.GetPropertyBlock(reusablePropertyBlock);
            reusablePropertyBlock.SetTexture(propertyId, texture);
            targetRenderer.SetPropertyBlock(reusablePropertyBlock);
        }

        private void LogInvalidPartIndex(int partIndex, int stepIndex)
        {
            Debug.LogWarning(
                "[BuildingVisualController] Invalid part index "
                + partIndex
                + " for building "
                + buildingId
                + " at upgrade step "
                + stepIndex
                + ".",
                logContext);
        }

        private void LogMissingTextureProperty(int partIndex)
        {
            Debug.LogWarning(
                "[BuildingVisualController] Part index "
                + partIndex
                + " has no supported texture property (_BaseMap/_MainTex).",
                logContext);
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
