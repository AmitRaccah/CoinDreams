using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime.Village
{
    [DisallowMultipleComponent]
    public sealed class BuildingVisualController : MonoBehaviour
    {
        private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");

        [SerializeField] private BuildingDefinitionSO buildingDefinition;
        [SerializeField] private GameObject[] partObjects = new GameObject[0];
        [SerializeField] private BuildingLevelVisual[] levelVisuals = Array.Empty<BuildingLevelVisual>();
        [SerializeField] private bool usePartObjectsAsLevelVisuals = true;
        [SerializeField] private bool combineLevelMeshesOnAwake;
        [SerializeField] private bool autoCollectChildrenWhenEmpty = true;
        [SerializeField] private bool applyLevelZeroOnAwake = true;

        private StepActivationOp[][] activationOpsByStep = Array.Empty<StepActivationOp[]>();
        private StepTextureOp[][] textureOpsByStep = Array.Empty<StepTextureOp[]>();
        private GameObject[] levelVisualRoots = Array.Empty<GameObject>();
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private Renderer[] partRenderers = Array.Empty<Renderer>();
        private TexturePropertyState[] textureStatesByPart = Array.Empty<TexturePropertyState>();
        private MaterialPropertyBlock reusablePropertyBlock;
        private bool cacheInitialized;
        private bool valid;
        private bool useLevelVisualMode;

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
                return GetMaxCachedLevel();
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

            if (useLevelVisualMode)
            {
                ApplyLevelVisual(clampedLevel);
                return true;
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

            GameObject[] resolvedPartObjects = BuildPartObjects();
            if (resolvedPartObjects.Length == 0)
            {
                Debug.LogError("[BuildingVisualController] No part objects configured on root " + name + ".", this);
                return false;
            }

            partObjects = resolvedPartObjects;
            BuildLevelVisualRoots();

            int partCount = partObjects.Length;
            partRenderers = new Renderer[partCount];
            textureStatesByPart = new TexturePropertyState[partCount];

            int partIndex;
            for (partIndex = 0; partIndex < partCount; partIndex++)
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

                BuildStepOps(stepIndex, step.nextLevelPartStates, out StepActivationOp[] activationOps, out StepTextureOp[] textureOps);
                activationOpsByStep[stepIndex] = activationOps;
                textureOpsByStep[stepIndex] = textureOps;
            }

            if (useLevelVisualMode && combineLevelMeshesOnAwake)
            {
                CombineLevelVisualMeshes();
            }

            return true;
        }

        private void OnDestroy()
        {
            DestroyGeneratedMeshes();
        }

        private GameObject[] BuildPartObjects()
        {
            GameObject[] configuredParts = partObjects;
            if (configuredParts != null && configuredParts.Length > 0)
            {
                return configuredParts;
            }

            if (!autoCollectChildrenWhenEmpty)
            {
                return Array.Empty<GameObject>();
            }

            int childCount = transform.childCount;
            if (childCount == 0)
            {
                return Array.Empty<GameObject>();
            }

            GameObject[] collected = new GameObject[childCount];
            int i;
            for (i = 0; i < childCount; i++)
            {
                Transform child = transform.GetChild(i);
                collected[i] = child != null ? child.gameObject : null;
            }

            return collected;
        }

        private void BuildLevelVisualRoots()
        {
            levelVisualRoots = Array.Empty<GameObject>();
            useLevelVisualMode = false;

            int configuredCount = levelVisuals != null ? levelVisuals.Length : 0;
            if (configuredCount > 0)
            {
                levelVisualRoots = new GameObject[configuredCount];

                int i;
                for (i = 0; i < configuredCount; i++)
                {
                    BuildingLevelVisual visual = levelVisuals[i];
                    levelVisualRoots[i] = visual != null ? visual.root : null;
                    if (levelVisualRoots[i] != null)
                    {
                        useLevelVisualMode = true;
                    }
                }

                return;
            }

            if (!usePartObjectsAsLevelVisuals || partObjects == null || partObjects.Length == 0)
            {
                return;
            }

            levelVisualRoots = new GameObject[partObjects.Length + 1];

            int partIndex;
            for (partIndex = 0; partIndex < partObjects.Length; partIndex++)
            {
                GameObject levelRoot = partObjects[partIndex];
                levelVisualRoots[partIndex + 1] = levelRoot;
                if (levelRoot != null)
                {
                    useLevelVisualMode = true;
                }
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

                int partIndex = stateConfig.partIndex;
                if (partIndex < 0 || partIndex >= partObjects.Length)
                {
                    Debug.LogWarning(
                        "[BuildingVisualController] Invalid part index "
                        + partIndex
                        + " for building "
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
                    Debug.LogWarning(
                        "[BuildingVisualController] Part index "
                        + partIndex
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

                ClearTextureOverride(partRenderer);
            }
        }

        private void ApplyLevelVisual(int level)
        {
            int activeIndex = level;
            if (activeIndex < 0)
            {
                activeIndex = 0;
            }

            if (activeIndex >= levelVisualRoots.Length)
            {
                activeIndex = levelVisualRoots.Length - 1;
            }

            int i;
            for (i = 0; i < levelVisualRoots.Length; i++)
            {
                GameObject visualRoot = levelVisualRoots[i];
                if (visualRoot == null)
                {
                    continue;
                }

                bool shouldBeActive = i == activeIndex;
                if (visualRoot.activeSelf != shouldBeActive)
                {
                    visualRoot.SetActive(shouldBeActive);
                }
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
            targetRenderer.GetPropertyBlock(reusablePropertyBlock);
            reusablePropertyBlock.SetTexture(propertyId, texture);
            targetRenderer.SetPropertyBlock(reusablePropertyBlock);
        }

        private int GetMaxCachedLevel()
        {
            if (useLevelVisualMode && levelVisualRoots.Length > 0)
            {
                return levelVisualRoots.Length - 1;
            }

            return activationOpsByStep.Length;
        }

        private void CombineLevelVisualMeshes()
        {
            int i;
            for (i = 0; i < levelVisualRoots.Length; i++)
            {
                GameObject levelRoot = levelVisualRoots[i];
                if (levelRoot == null)
                {
                    continue;
                }

                CombineLevelVisualMesh(levelRoot);
            }
        }

        private void CombineLevelVisualMesh(GameObject levelRoot)
        {
            MeshFilter[] meshFilters = levelRoot.GetComponentsInChildren<MeshFilter>(true);
            if (meshFilters == null || meshFilters.Length <= 1)
            {
                return;
            }

            List<MaterialCombineGroup> materialGroups = new List<MaterialCombineGroup>();
            int sourceRendererCount = 0;

            int filterIndex;
            for (filterIndex = 0; filterIndex < meshFilters.Length; filterIndex++)
            {
                MeshFilter meshFilter = meshFilters[filterIndex];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer == null || meshRenderer.sharedMaterials == null)
                {
                    continue;
                }

                AddMeshToMaterialGroups(
                    materialGroups,
                    levelRoot.transform,
                    meshFilter,
                    meshRenderer);
                sourceRendererCount++;
            }

            if (sourceRendererCount <= 1 || materialGroups.Count == 0)
            {
                return;
            }

            GameObject combinedRoot = new GameObject(levelRoot.name + "_CombinedRenderers");
            Transform combinedTransform = combinedRoot.transform;
            combinedTransform.SetParent(levelRoot.transform, false);
            combinedTransform.localPosition = Vector3.zero;
            combinedTransform.localRotation = Quaternion.identity;
            combinedTransform.localScale = Vector3.one;

            int groupIndex;
            for (groupIndex = 0; groupIndex < materialGroups.Count; groupIndex++)
            {
                MaterialCombineGroup group = materialGroups[groupIndex];
                if (group.material == null || group.combineInstances.Count == 0)
                {
                    continue;
                }

                CreateCombinedRenderer(combinedTransform, group);
            }

            DisableSourceRenderers(meshFilters);
        }

        private static void AddMeshToMaterialGroups(
            List<MaterialCombineGroup> materialGroups,
            Transform rootTransform,
            MeshFilter meshFilter,
            MeshRenderer meshRenderer)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Material[] materials = meshRenderer.sharedMaterials;
            int subMeshCount = mesh.subMeshCount;
            if (subMeshCount > materials.Length)
            {
                subMeshCount = materials.Length;
            }

            Matrix4x4 rootSpaceMatrix = rootTransform.worldToLocalMatrix * meshFilter.transform.localToWorldMatrix;

            int subMeshIndex;
            for (subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                Material material = materials[subMeshIndex];
                if (material == null)
                {
                    continue;
                }

                MaterialCombineGroup group = GetOrCreateMaterialGroup(materialGroups, material);
                CombineInstance combineInstance = new CombineInstance();
                combineInstance.mesh = mesh;
                combineInstance.subMeshIndex = subMeshIndex;
                combineInstance.transform = rootSpaceMatrix;
                combineInstance.lightmapScaleOffset = Vector4.zero;
                combineInstance.realtimeLightmapScaleOffset = Vector4.zero;
                group.combineInstances.Add(combineInstance);
            }
        }

        private static MaterialCombineGroup GetOrCreateMaterialGroup(
            List<MaterialCombineGroup> materialGroups,
            Material material)
        {
            int i;
            for (i = 0; i < materialGroups.Count; i++)
            {
                MaterialCombineGroup existing = materialGroups[i];
                if (existing.material == material)
                {
                    return existing;
                }
            }

            MaterialCombineGroup group = new MaterialCombineGroup(material);
            materialGroups.Add(group);
            return group;
        }

        private void CreateCombinedRenderer(Transform parent, MaterialCombineGroup group)
        {
            GameObject combinedObject = new GameObject(group.material.name + "_Combined");
            Transform combinedTransform = combinedObject.transform;
            combinedTransform.SetParent(parent, false);
            combinedTransform.localPosition = Vector3.zero;
            combinedTransform.localRotation = Quaternion.identity;
            combinedTransform.localScale = Vector3.one;

            Mesh combinedMesh = new Mesh();
            combinedMesh.name = parent.name + "_" + group.material.name + "_CombinedMesh";
            combinedMesh.indexFormat = IndexFormat.UInt32;
            combinedMesh.CombineMeshes(group.combineInstances.ToArray(), true, true, false);
            combinedMesh.RecalculateBounds();
            generatedMeshes.Add(combinedMesh);

            MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = combinedMesh;

            MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = group.material;
        }

        private static void DisableSourceRenderers(MeshFilter[] meshFilters)
        {
            int i;
            for (i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null)
                {
                    continue;
                }

                MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.enabled = false;
                }
            }
        }

        private void DestroyGeneratedMeshes()
        {
            int i;
            for (i = 0; i < generatedMeshes.Count; i++)
            {
                Mesh mesh = generatedMeshes[i];
                if (mesh != null)
                {
                    Destroy(mesh);
                }
            }

            generatedMeshes.Clear();
        }

        [Serializable]
        public sealed class BuildingLevelVisual
        {
            public GameObject root;
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

        private sealed class MaterialCombineGroup
        {
            public readonly Material material;
            public readonly List<CombineInstance> combineInstances;

            public MaterialCombineGroup(Material material)
            {
                this.material = material;
                combineInstances = new List<CombineInstance>();
            }
        }
    }
}
