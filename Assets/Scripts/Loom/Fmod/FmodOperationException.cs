using System;

namespace Loom.Fmod
{
    public sealed class FmodOperationException : InvalidOperationException
    {
        public FmodOperationException(string operation, FMOD.RESULT result)
            : base($"FMOD operation '{operation}' failed with {result}: {FMOD.Error.String(result)}")
        {
            Operation = operation;
            Result = result;
        }

        public string Operation { get; }

        public FMOD.RESULT Result { get; }
    }

    internal static class FmodResult
    {
        public static void Ensure(FMOD.RESULT result, string operation)
        {
            if (result != FMOD.RESULT.OK)
            {
                throw new FmodOperationException(operation, result);
            }
        }
    }
}
