using UnityEngine;

namespace CardsUnity.Controllers
{
    [DisallowMultipleComponent]
    public sealed class ProceduralSpine : MonoBehaviour
    {
        [SerializeField] private Transform lower, middle, chest, neck;
        [SerializeField] private MeshFilter torso;
        [Header("Spine motion")]
        [SerializeField, Range(0f, 2f)] private float bendAmount = 1f;
        [SerializeField, Range(0f, 2f)] private float twistAmount = 1f;
        [SerializeField, Range(0f, 2f)] private float sideBendAmount = 1f;
        [SerializeField, Range(0.02f, 0.3f)] private float responseTime = 0.09f;
        [SerializeField, Range(0f, 1f)] private float headStabilization = 0.9f;
        private Mesh source, deformed;
        private Vector3[] vertices, normals, outputVertices, outputNormals;
        private readonly Matrix4x4[] rest = new Matrix4x4[3];
        private readonly Matrix4x4[] skin = new Matrix4x4[3];
        private readonly Matrix4x4[] normalSkin = new Matrix4x4[3];
        private Transform[] bones;
        private bool configured;

        public void Configure(float height)
        {
            if (lower == null || middle == null || chest == null || neck == null || torso == null) return;
            lower.localPosition = new Vector3(0f, -0.13f, 0f);
            middle.localPosition = Vector3.up * height * 0.35f;
            chest.localPosition = Vector3.up * height * 0.35f;
            neck.localPosition = Vector3.up * height * 0.3f;
            bones = new[] { lower, middle, chest };
            Quaternion[] rotations = { lower.localRotation, middle.localRotation, chest.localRotation };
            for (int i = 0; i < 3; i++) bones[i].localRotation = Quaternion.identity;
            for (int i = 0; i < 3; i++) rest[i] = bones[i].worldToLocalMatrix * torso.transform.localToWorldMatrix;
            for (int i = 0; i < 3; i++) bones[i].localRotation = rotations[i];
            configured = true;
        }

        public void Animate(GaitStylePose pose, float activity, float dt)
        {
            if (!configured || !isActiveAndEnabled) return;
            float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.02f, responseTime));
            for (int i = 0; i < 3; i++)
            {
                // Opposing chest/hip rotation; each joint contributes to the curved silhouette.
                float twistShare = i == 0 ? -0.25f : (i == 1 ? 0.45f : 0.8f);
                Vector3 angles = new Vector3(pose.SpinePitch[i] * bendAmount,
                    pose.SpineTwist * twistShare * twistAmount, pose.SpineRoll[i] * sideBendAmount) * activity;
                bones[i].localRotation = Quaternion.Slerp(bones[i].localRotation, Quaternion.Euler(angles), alpha);
            }
            Quaternion level = Quaternion.Inverse(chest.rotation) * transform.rotation;
            neck.localRotation = Quaternion.Slerp(neck.localRotation,
                Quaternion.Slerp(Quaternion.identity, level, headStabilization), alpha);
            if (Application.isPlaying) Deform(bones);
        }

        private void Deform(Transform[] bones)
        {
            if (deformed == null)
            {
                source = torso.sharedMesh;
                if (source == null) return;
                deformed = Instantiate(source);
                deformed.name = source.name + " (Animated)";
                deformed.MarkDynamic();
                vertices = source.vertices;
                normals = source.normals;
                outputVertices = new Vector3[vertices.Length];
                outputNormals = new Vector3[vertices.Length];
                torso.sharedMesh = deformed;
            }
            for (int i = 0; i < 3; i++)
            {
                skin[i] = torso.transform.worldToLocalMatrix * bones[i].localToWorldMatrix * rest[i];
                normalSkin[i] = skin[i].inverse.transpose;
            }
            for (int i = 0; i < vertices.Length; i++)
            {
                float segment = Mathf.Clamp((vertices[i].y + 0.5f) / 0.35f, 0f, 2f);
                int a = Mathf.Min(Mathf.FloorToInt(segment), 1), b = a + 1;
                float weight = segment - a;
                outputVertices[i] = Vector3.Lerp(skin[a].MultiplyPoint3x4(vertices[i]), skin[b].MultiplyPoint3x4(vertices[i]), weight);
                outputNormals[i] = Vector3.Lerp(normalSkin[a].MultiplyVector(normals[i]),
                    normalSkin[b].MultiplyVector(normals[i]), weight).normalized;
            }
            deformed.vertices = outputVertices;
            deformed.normals = outputNormals;
            deformed.RecalculateBounds();
        }

        private void OnDisable()
        {
            if (deformed == null) return;
            if (torso != null) torso.sharedMesh = source;
            Destroy(deformed);
            deformed = null;
        }
    }
}
