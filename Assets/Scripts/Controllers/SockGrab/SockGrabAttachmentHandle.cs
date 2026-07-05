using Obi;
using UnityEngine;

namespace CardsUnity
{
    public sealed class SockGrabAttachmentHandle
    {
        public ObiParticleAttachment Attachment { get; }

        public SockGrabAttachmentHandle(ObiParticleAttachment attachment)
        {
            Attachment = attachment;
        }

        public Vector3 GetWorldPosition()
        {
            if (Attachment == null)
                return Vector3.zero;

            ObiActor actor = Attachment.actor;
            ObiParticleGroup group = Attachment.particleGroup;

            if (actor == null || actor.solver == null || actor.solverIndices == null || group == null || group.Count <= 0)
                return Attachment.transform.position;

            int localParticleIndex = group.particleIndices[0];
            if (localParticleIndex < 0 || localParticleIndex >= actor.solverIndices.count)
                return Attachment.transform.position;

            int solverIndex = actor.solverIndices[localParticleIndex];
            if (solverIndex < 0 || solverIndex >= actor.solver.positions.count)
                return Attachment.transform.position;

            Vector3 solverLocalPosition = actor.solver.positions[solverIndex];
            return actor.solver.transform.TransformPoint(solverLocalPosition);
        }
    }
}
