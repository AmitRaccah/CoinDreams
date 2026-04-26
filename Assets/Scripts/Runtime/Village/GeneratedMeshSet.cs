using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Village
{
    internal sealed class GeneratedMeshSet
    {
        private readonly List<Mesh> meshes = new List<Mesh>();

        public void Add(Mesh mesh)
        {
            if (mesh != null)
            {
                meshes.Add(mesh);
            }
        }

        public void DestroyAll()
        {
            int i;
            for (i = 0; i < meshes.Count; i++)
            {
                Mesh mesh = meshes[i];
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }

            meshes.Clear();
        }
    }
}
