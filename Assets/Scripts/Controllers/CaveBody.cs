using UnityEngine;

namespace CardsUnity.Controllers
{
    /// <summary>Static fallen knight with deterministic proportions and an independent torch.</summary>
    public sealed class CaveBody : MonoBehaviour
    {
        [SerializeField] private ShadowKnightProportions body;
        [SerializeField] private Transform torch;
        [SerializeField] private int seed = 173;
        public int Seed => seed;

        public void Configure(int value)
        {
            seed = value;
            if (body == null) return;
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.RandomizeBody(seed);
            var random = new System.Random(seed);
            foreach (var joint in body.GetComponentsInChildren<Transform>(true))
            {
                if (joint.name == "Left Forearm" || joint.name == "Right Forearm")
                    joint.localRotation = Quaternion.Euler(0f, 0f, (float)random.NextDouble() * 24f - 12f);
                if (joint.name == "Left Leg" || joint.name == "Right Leg")
                    joint.localRotation = Quaternion.Euler(0f, 0f, joint.name == "Left Leg" ? -9f : 14f);
                if (joint.name == "Left Calf" || joint.name == "Right Calf") joint.localRotation = Quaternion.identity;
                if (joint.name == "Head") joint.localRotation = Quaternion.Euler(0f, 0f, 18f);
            }
            body.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            // Fit actual mesh vertices, avoiding rotated renderer AABB overestimates.
            Vector3 min = Vector3.one * float.PositiveInfinity, max = Vector3.one * float.NegativeInfinity;
            foreach (var mesh in body.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.sharedMesh == null || !mesh.gameObject.activeInHierarchy) continue;
                foreach (var vertex in mesh.sharedMesh.vertices)
                {
                    Vector3 p = transform.InverseTransformPoint(mesh.transform.TransformPoint(vertex));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
            }
            if (!float.IsInfinity(min.y))
                body.transform.localPosition = new Vector3(-(min.x + max.x) * 0.5f, -min.y + 0.015f, -(min.z + max.z) * 0.5f);
            if (torch != null)
            {
                torch.localPosition = new Vector3(0.9f, 0.13f, -0.3f);
                torch.localRotation = Quaternion.Euler(88f, 25f, 0f);
            }
        }
    }
}
