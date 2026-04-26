using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Runtime.Village
{
    internal sealed class BuildingLevelMeshCombiner
    {
        public void Combine(GameObject[] levelRoots, GeneratedMeshSet generatedMeshes)
        {
            if (levelRoots == null || generatedMeshes == null)
            {
                return;
            }

            int i;
            for (i = 0; i < levelRoots.Length; i++)
            {
                GameObject levelRoot = levelRoots[i];
                if (levelRoot == null)
                {
                    continue;
                }

                CombineLevel(levelRoot, generatedMeshes);
            }
        }

        private static void CombineLevel(GameObject levelRoot, GeneratedMeshSet generatedMeshes)
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

                CreateCombinedRenderer(combinedTransform, group, generatedMeshes);
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

        private static void CreateCombinedRenderer(
            Transform parent,
            MaterialCombineGroup group,
            GeneratedMeshSet generatedMeshes)
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
