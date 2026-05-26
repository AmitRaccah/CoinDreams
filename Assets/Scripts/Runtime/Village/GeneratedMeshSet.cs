#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class GeneratedMeshSet
    {
        private const int InitialCapacity = 8;

        private readonly List<Mesh> meshes = new List<Mesh>(InitialCapacity);
        private readonly List<GameObject> hostGameObjects = new List<GameObject>(InitialCapacity);

        public void Add(Mesh mesh)
        {
            if (mesh != null)
            {
                meshes.Add(mesh);
            }
        }

        public void RegisterGameObject(GameObject host)
        {
            if (host != null)
            {
                hostGameObjects.Add(host);
            }
        }

        public void DestroyAll()
        {
            int meshIndex;
            for (meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
            {
                Mesh mesh = meshes[meshIndex];
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            meshes.Clear();

            int hostIndex;
            for (hostIndex = 0; hostIndex < hostGameObjects.Count; hostIndex++)
            {
                GameObject host = hostGameObjects[hostIndex];
                if (host != null)
                {
                    Object.Destroy(host);
                }
            }

            hostGameObjects.Clear();
        }
    }
}
