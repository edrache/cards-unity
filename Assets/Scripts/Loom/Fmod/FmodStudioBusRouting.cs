using System;

namespace Loom.Fmod
{
    internal sealed class FmodStudioBusRouting : IDisposable
    {
        private readonly FMOD.Studio.System studioSystem;
        private readonly string busPath;
        private FMOD.Studio.Bus cachedBus;
        private FMOD.ChannelGroup cachedChannelGroup;
        private bool isChannelGroupLocked;
        private bool isDisposed;

        public FmodStudioBusRouting(
            FMOD.Studio.System studioSystem,
            string busPath)
        {
            if (string.IsNullOrWhiteSpace(busPath))
            {
                throw new ArgumentException(
                    "FMOD Studio bus path must not be empty.",
                    nameof(busPath));
            }

            if (!studioSystem.isValid())
            {
                throw new ArgumentException(
                    "FMOD Studio system must have a valid handle.",
                    nameof(studioSystem));
            }

            this.studioSystem = studioSystem;
            this.busPath = busPath;
        }

        public string BusPath => busPath;

        public FMOD.ChannelGroup GetChannelGroup()
        {
            ThrowIfDisposed();
            if (IsCachedChannelGroupValid())
            {
                return cachedChannelGroup;
            }

            ReleaseChannelGroup();
            AcquireChannelGroup();
            return cachedChannelGroup;
        }

        public void PrepareForBankReload()
        {
            ThrowIfDisposed();
            ReleaseChannelGroup();
        }

        public void RestoreAfterBankReload()
        {
            ThrowIfDisposed();
            GetChannelGroup();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            ReleaseChannelGroup();
            isDisposed = true;
        }

        private void AcquireChannelGroup()
        {
            EnsureBusResult(
                studioSystem.getBus(busPath, out cachedBus),
                "FMOD.Studio.System.getBus");
            if (!cachedBus.isValid())
            {
                cachedBus.clearHandle();
                throw new InvalidOperationException(
                    $"FMOD Studio bus '{busPath}' resolved to an invalid handle. Ensure its bank is loaded before creating or restoring the instrument.");
            }

            EnsureBusResult(
                cachedBus.lockChannelGroup(),
                "FMOD.Studio.Bus.lockChannelGroup");
            isChannelGroupLocked = true;

            try
            {
                EnsureBusResult(
                    studioSystem.flushCommands(),
                    "FMOD.Studio.System.flushCommands");
                EnsureBusResult(
                    cachedBus.getChannelGroup(out cachedChannelGroup),
                    "FMOD.Studio.Bus.getChannelGroup");
                if (!cachedChannelGroup.hasHandle())
                {
                    throw new InvalidOperationException(
                        $"FMOD Studio bus '{busPath}' returned an invalid ChannelGroup handle.");
                }

                EnsureBusResult(
                    cachedChannelGroup.getNumChannels(out _),
                    "FMOD.ChannelGroup.getNumChannels");
            }
            catch (Exception acquisitionException)
            {
                Exception cleanupException = TryReleaseChannelGroup();
                if (cleanupException != null)
                {
                    throw new AggregateException(
                        acquisitionException,
                        cleanupException);
                }

                throw;
            }
        }

        private bool IsCachedChannelGroupValid()
        {
            if (!isChannelGroupLocked
                || !cachedBus.isValid()
                || !cachedChannelGroup.hasHandle())
            {
                return false;
            }

            FMOD.RESULT result = cachedChannelGroup.getNumChannels(out _);
            if (result == FMOD.RESULT.OK)
            {
                return true;
            }

            if (result == FMOD.RESULT.ERR_INVALID_HANDLE
                || result == FMOD.RESULT.ERR_STUDIO_NOT_LOADED)
            {
                return false;
            }

            EnsureBusResult(result, "FMOD.ChannelGroup.getNumChannels");
            return false;
        }

        private void ReleaseChannelGroup()
        {
            if (!isChannelGroupLocked)
            {
                cachedChannelGroup.clearHandle();
                cachedBus.clearHandle();
                return;
            }

            if (!cachedBus.isValid())
            {
                isChannelGroupLocked = false;
                cachedChannelGroup.clearHandle();
                cachedBus.clearHandle();
                return;
            }

            FMOD.RESULT result = cachedBus.unlockChannelGroup();
            EnsureBusResult(
                result,
                "FMOD.Studio.Bus.unlockChannelGroup");
            isChannelGroupLocked = false;
            cachedChannelGroup.clearHandle();
            cachedBus.clearHandle();
        }

        private Exception TryReleaseChannelGroup()
        {
            try
            {
                ReleaseChannelGroup();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private void EnsureBusResult(FMOD.RESULT result, string operation)
        {
            if (result != FMOD.RESULT.OK)
            {
                throw new FmodOperationException(
                    $"{operation} for '{busPath}'",
                    result);
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(FmodStudioBusRouting));
            }
        }
    }
}
