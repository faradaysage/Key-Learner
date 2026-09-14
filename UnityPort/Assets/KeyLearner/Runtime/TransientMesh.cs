using UnityEngine;
namespace KeyLearner.Unity
{
    // Generated terrain meshes have the same bounded lifetime as their segment.
    public sealed class TransientMesh : MonoBehaviour
    {
        public Mesh Mesh;
        void OnDestroy()
        {
            if (Mesh)
                Destroy(Mesh);
        }
    }
}
