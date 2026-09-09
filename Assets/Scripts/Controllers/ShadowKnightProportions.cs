using UnityEngine;

namespace CardsUnity.Controllers
{
    [ExecuteAlways, DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ShadowKnightProportions : MonoBehaviour
    {
        [Header("Lengths in metres")]
        [SerializeField, Range(0.2f, 0.9f)] private float upperArmLength = 0.43f;
        [SerializeField, Range(0.2f, 0.9f)] private float forearmLength = 0.42f;
        [SerializeField, Range(0.25f, 0.9f)] private float thighLength = 0.38f;
        [SerializeField, Range(0.25f, 0.9f)] private float calfLength = 0.38f;
        [Header("Body")]
        [SerializeField, Range(0.45f, 1.5f)] private float torsoHeight = 0.9f;
        [SerializeField, Range(0.4f, 1.2f)] private float torsoWidth = 0.78f;
        [SerializeField, Range(0.25f, 0.8f)] private float torsoDepth = 0.44f;
        [SerializeField, Range(0.25f, 0.9f)] private float headSize = 0.56f;
        [Header("Arm posture")]
        [Tooltip("Base angle away from the torso: 0 hangs down, 90 extends sideways. Walking adds its own arm motion.")]
        [SerializeField, Range(0f, 90f)] private float armOutwardAngle = 8f;
        [Header("Accessory")]
        [SerializeField] private bool helmetVisible = true;
        [SerializeField, HideInInspector] private Transform[] parts;
        private bool pending = true;

        private void OnEnable() => pending = true;
        private void OnValidate() => pending = true;
        private void Update()
        {
            if (pending) ApplyProportions();
        }

        private Transform Part(string partName)
        {
            foreach (var part in parts)
                if (part != null && part.name == partName) return part;
            return null;
        }

        private void Shape(string partName, Vector3 position, Vector3 scale)
        {
            var part = Part(partName);
            if (part == null) return;
            part.localPosition = position;
            part.localScale = scale;
        }

        /// <summary>Deterministic corpse proportions without touching Unity's global random state.</summary>
        public void RandomizeBody(int seed)
        {
            var random = new System.Random(seed);
            float Sample(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
            upperArmLength = Sample(0.34f, 0.54f);
            forearmLength = Sample(0.32f, 0.52f);
            thighLength = Sample(0.36f, 0.58f);
            calfLength = Sample(0.36f, 0.58f);
            torsoHeight = Sample(0.65f, 1.05f);
            torsoWidth = Sample(0.52f, 0.95f);
            torsoDepth = Sample(0.3f, 0.52f);
            headSize = Sample(0.4f, 0.65f);
            armOutwardAngle = Sample(12f, 32f);
            ApplyProportions();
        }

        [ContextMenu("Apply Proportions")]
        public void ApplyProportions()
        {
            pending = false;
            parts = GetComponentsInChildren<Transform>(true);
            upperArmLength = Mathf.Clamp(upperArmLength, 0.2f, 0.9f);
            forearmLength = Mathf.Clamp(forearmLength, 0.2f, 0.9f);
            thighLength = Mathf.Clamp(thighLength, 0.25f, 0.9f);
            calfLength = Mathf.Clamp(calfLength, 0.25f, 0.9f);
            torsoHeight = Mathf.Clamp(torsoHeight, 0.45f, 1.5f);
            torsoWidth = Mathf.Clamp(torsoWidth, 0.4f, 1.2f);
            torsoDepth = Mathf.Clamp(torsoDepth, 0.25f, 0.8f);
            headSize = Mathf.Clamp(headSize, 0.25f, 0.9f);
            armOutwardAngle = Mathf.Clamp(armOutwardAngle, 0f, 90f);
            float hip = thighLength + calfLength + 0.04f;
            float halfWidth = torsoWidth * 0.32f;
            Vector3 origin = Vector3.up * (hip + 0.13f);
            Shape("Body Pivot", origin, Vector3.one);
            Shape("Torso", new Vector3(0, torsoHeight * 0.5f - 0.13f, 0), new Vector3(torsoWidth, torsoHeight, torsoDepth));
            Shape("Pelvis", new Vector3(0, -0.08f, 0), new Vector3(torsoWidth * 0.9f, 0.22f, torsoDepth * 0.9f));
            var spine = GetComponent<ProceduralSpine>();
            bool articulated = spine != null;
            Shape("Head", new Vector3(0, (articulated ? 0f : torsoHeight - 0.13f) + headSize * 0.3f, 0), Vector3.one * headSize);
            foreach (string side in new[] { "Left", "Right" })
            {
                float sign = side == "Left" ? -1 : 1;
                Shape(side + " Arm", new Vector3(sign * (torsoWidth * 0.5f + 0.035f), (articulated ? torsoHeight * 0.3f - 0.09f : torsoHeight - 0.22f), 0), Vector3.one);
                var arm = Part(side + " Arm");
                if (arm != null) arm.localRotation = Quaternion.Euler(0f, 0f, sign * armOutwardAngle);
                Shape(side + " Upper Arm", Vector3.down * upperArmLength * 0.5f, new Vector3(0.18f, upperArmLength + 0.05f, 0.19f));
                Shape(side + " Forearm", Vector3.down * upperArmLength, Vector3.one);
                Shape(side + " Forearm Mesh", Vector3.down * forearmLength * 0.5f, new Vector3(0.13f, forearmLength + 0.03f, 0.15f));
                Shape(side + " Hand", Vector3.down * forearmLength, new Vector3(0.18f, 0.19f, 0.18f));
                Shape(side + " Leg", new Vector3(sign * halfWidth, hip, 0), Vector3.one);
                Shape(side + " Thigh Mesh", Vector3.down * thighLength * 0.5f, new Vector3(0.19f, thighLength + 0.04f, 0.22f));
                Shape(side + " Calf", Vector3.down * thighLength, Vector3.one);
                Shape(side + " Calf Mesh", Vector3.down * calfLength * 0.5f, new Vector3(0.14f, calfLength + 0.03f, 0.17f));
                Shape(side + " Foot", Vector3.down * calfLength, Vector3.one);
            }
            if (spine != null) spine.Configure(torsoHeight);
            var helmet = Part("Helmet");
            if (helmet != null && helmet.gameObject.activeSelf != helmetVisible) helmet.gameObject.SetActive(helmetVisible);
            var gait = GetComponent<CartoonCharacterGait>();
            if (gait != null)
            {
                gait.SetRigDimensions(thighLength, calfLength, hip, halfWidth, origin);
                gait.SetArmOutwardAngle(armOutwardAngle);
            }
            var torch = GetComponentInChildren<HandheldTorch>(true);
            if (torch != null)
            {
                torch.SetGripOffset(Vector3.down * forearmLength);
                torch.SetArmOutwardAngle(armOutwardAngle);
            }
            var controller = GetComponent<CharacterController>();
            if (controller != null)
            {
                float height = hip + torsoHeight + headSize * 0.8f;
                controller.height = height;
                controller.stepOffset = calfLength;
                controller.center = Vector3.up * height * 0.5f;
                controller.radius = Mathf.Clamp(torsoWidth * 0.42f, 0.18f, 0.45f);
            }
        }
    }
}
