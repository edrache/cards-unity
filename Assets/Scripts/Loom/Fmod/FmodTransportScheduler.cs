using System;
using Loom.Core;

namespace Loom.Fmod
{
    /// <summary>
    /// Drives a Core transport from the synth-bus DSP clock and dispatches bounded lookahead batches.
    /// </summary>
    public sealed class FmodTransportScheduler : IDisposable
    {
        public const int DefaultLookaheadMilliseconds = 200;
        public const int DefaultSchedulingCapacity = 256;
        public const int DefaultMaxDispatchEventsPerPump = 64;

        private readonly FMOD.System coreSystem;
        private readonly FmodOscillatorInstrument instrument;
        private readonly TempoMap tempoMap;
        private readonly Transport transport;
        private readonly int maxDispatchEventsPerPump;
        private readonly ulong anchorLeadSampleFrames;
        private readonly int runtimeSampleRate;
        private readonly ulong lookaheadSampleFrames;

        private FmodTickDspClockMapper clockMapper;
        private FmodScheduledNoteDispatcher dispatcher;
        private ulong lastObservedDspClock;
        private bool hasObservedDspClock;
        private bool isDisposed;

        public FmodTransportScheduler(
            FMOD.System coreSystem,
            FmodOscillatorInstrument instrument,
            TempoMap tempoMap,
            StepPattern pattern,
            ulong seed,
            ulong streamId,
            int lookaheadMilliseconds = DefaultLookaheadMilliseconds,
            int schedulingCapacity = DefaultSchedulingCapacity,
            int maxDispatchEventsPerPump = DefaultMaxDispatchEventsPerPump)
        {
            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            this.instrument = instrument
                ?? throw new ArgumentNullException(nameof(instrument));
            this.tempoMap = tempoMap
                ?? throw new ArgumentNullException(nameof(tempoMap));
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            if (lookaheadMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lookaheadMilliseconds),
                    lookaheadMilliseconds,
                    "Scheduling lookahead must be positive.");
            }

            if (maxDispatchEventsPerPump <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxDispatchEventsPerPump),
                    maxDispatchEventsPerPump,
                    "Per-pump dispatch limit must be positive.");
            }

            FmodResult.Ensure(
                coreSystem.getDSPBufferSize(out uint dspBufferLength, out _),
                "FMOD.System.getDSPBufferSize(transport scheduler)");
            if (dspBufferLength == 0U)
            {
                throw new InvalidOperationException(
                    "FMOD returned a zero DSP buffer length.");
            }

            FmodResult.Ensure(
                coreSystem.getSoftwareFormat(out int sampleRate, out _, out _),
                "FMOD.System.getSoftwareFormat(transport scheduler)");
            if (sampleRate <= 0)
            {
                throw new InvalidOperationException(
                    $"FMOD returned an invalid runtime sample rate: {sampleRate}.");
            }

            this.coreSystem = coreSystem;
            this.maxDispatchEventsPerPump = maxDispatchEventsPerPump;
            runtimeSampleRate = sampleRate;
            lookaheadSampleFrames = ResolveLookaheadSampleFrames(
                runtimeSampleRate,
                lookaheadMilliseconds);
            anchorLeadSampleFrames = Math.Max(
                lookaheadSampleFrames,
                dspBufferLength);
            transport = new Transport(
                new StepSequencer(pattern, seed, streamId),
                schedulingCapacity);
        }

        public TransportState State => transport.State;

        public long CurrentTick => transport.CurrentTick;

        public long ScheduledThroughTick => transport.ScheduledThroughTick;

        public int PendingEventCount => transport.PendingEventCount;

        public ulong LookaheadSampleFrames => lookaheadSampleFrames;

        public int RuntimeSampleRate => runtimeSampleRate;

        public long DispatchedEventCount { get; private set; }

        public long LateEventCount { get; private set; }

        public long PlateauDurationCount { get; private set; }

        public long UnderrunPumpCount { get; private set; }

        public long BackpressurePumpCount { get; private set; }

        /// <summary>
        /// Gets the number of runtime DSP-clock domain regressions recovered since start.
        /// </summary>
        public long ClockRegressionRecoveryCount { get; private set; }

        public void Start(long startTick = 0)
        {
            ThrowIfDisposed();
            if (transport.State != TransportState.Stopped)
            {
                throw new InvalidOperationException(
                    "Transport scheduler can start only while stopped.");
            }

            if (startTick < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startTick),
                    startTick,
                    "Transport start tick cannot be negative.");
            }

            instrument.CancelAllScheduledNotes();
            try
            {
                Reanchor(startTick);
                transport.Start(startTick);
                ResetTelemetry();
            }
            catch
            {
                ClearAnchor();
                throw;
            }
        }

        /// <summary>
        /// Generates and submits one bounded batch using the current synth-bus clock.
        /// </summary>
        public TransportUpdateStatus Pump()
        {
            ThrowIfDisposed();
            if (transport.State != TransportState.Playing
                || clockMapper == null
                || dispatcher == null)
            {
                throw new InvalidOperationException(
                    "Transport scheduler can pump only while playing and anchored.");
            }

            try
            {
                ulong currentDspClock = clockMapper.GetCurrentDspClock();
                return PumpAtDspClock(currentDspClock);
            }
            catch (Exception pumpFailure)
            {
                FailClosed(pumpFailure);
                throw;
            }
        }

        internal TransportUpdateStatus PumpAtDspClock(ulong currentDspClock)
        {
            if (transport.State != TransportState.Playing
                || clockMapper == null
                || dispatcher == null)
            {
                throw new InvalidOperationException(
                    "Transport scheduler can pump only while playing and anchored.");
            }

            if (hasObservedDspClock && currentDspClock < lastObservedDspClock)
            {
                RecoverFromClockRegression();
                return TransportUpdateStatus.Completed;
            }

            lastObservedDspClock = currentDspClock;
            hasObservedDspClock = true;
            ResolveSchedulingWindow(
                currentDspClock,
                out long currentTick,
                out long targetTickExclusive);
            if (currentTick < transport.CurrentTick)
            {
                RecoverFromClockRegression();
                return TransportUpdateStatus.Completed;
            }

            TransportUpdateStatus status = transport.Update(
                currentTick,
                targetTickExclusive);
            if ((status & TransportUpdateStatus.Underrun) != 0)
            {
                UnderrunPumpCount++;
            }

            if ((status & TransportUpdateStatus.BufferFull) != 0)
            {
                BackpressurePumpCount++;
            }

            int dispatchedCount = dispatcher.DispatchAvailable(
                transport,
                maxDispatchEventsPerPump,
                currentDspClock,
                out int lateEventCount,
                out int plateauDurationCount);
            DispatchedEventCount += dispatchedCount;
            LateEventCount += lateEventCount;
            PlateauDurationCount += plateauDurationCount;
            return status;
        }

        public void Pause()
        {
            ThrowIfDisposed();
            if (transport.State != TransportState.Playing || clockMapper == null)
            {
                throw new InvalidOperationException(
                    "Transport scheduler can pause only while playing and anchored.");
            }

            long pauseTick = ResolveCurrentTick();
            instrument.CancelAllScheduledNotes();
            transport.Pause(pauseTick);
            ClearAnchor();
        }

        public void Resume()
        {
            ThrowIfDisposed();
            if (transport.State != TransportState.Paused)
            {
                throw new InvalidOperationException(
                    "Transport scheduler can resume only while paused.");
            }

            try
            {
                Reanchor(transport.CurrentTick);
                transport.Resume();
            }
            catch
            {
                ClearAnchor();
                throw;
            }
        }

        public void Panic()
        {
            ThrowIfDisposed();
            long panicTick = transport.CurrentTick;
            if (transport.State == TransportState.Playing)
            {
                if (clockMapper == null)
                {
                    throw new InvalidOperationException(
                        "Playing transport scheduler has no DSP clock anchor.");
                }

                panicTick = ResolveCurrentTick();
            }

            instrument.AllNotesOff();
            transport.Panic(panicTick);

            if (transport.State != TransportState.Playing)
            {
                ClearAnchor();
                return;
            }

            try
            {
                Reanchor(panicTick);
            }
            catch
            {
                transport.Pause(panicTick);
                ClearAnchor();
                throw;
            }
        }

        public void Stop()
        {
            if (isDisposed)
            {
                return;
            }

            Exception cancellationFailure = null;
            try
            {
                instrument.CancelAllScheduledNotes();
            }
            catch (Exception exception)
            {
                cancellationFailure = exception;
            }

            transport.Stop();
            ClearAnchor();

            if (cancellationFailure != null)
            {
                throw cancellationFailure;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            try
            {
                Stop();
            }
            finally
            {
                isDisposed = true;
            }
        }

        private void Reanchor(long anchorTick)
        {
            clockMapper = instrument.CreateTickDspClockMapper(
                tempoMap,
                anchorTick,
                anchorLeadSampleFrames);
            dispatcher = new FmodScheduledNoteDispatcher(
                coreSystem,
                clockMapper,
                instrument);
            if (clockMapper.SampleRate != runtimeSampleRate)
            {
                ClearAnchor();
                throw new InvalidOperationException(
                    "FMOD runtime sample rate changed during the scheduler lifetime.");
            }

            lastObservedDspClock = checked(
                clockMapper.AnchorDspClock - anchorLeadSampleFrames);
            hasObservedDspClock = true;
        }

        private long ResolveCurrentTick()
        {
            ulong currentDspClock = clockMapper.GetCurrentDspClock();
            long currentTick = clockMapper.TryToTickAtOrBeforeDspClock(
                currentDspClock,
                out long resolvedCurrentTick)
                    ? resolvedCurrentTick
                    : transport.CurrentTick;
            return Math.Max(currentTick, transport.CurrentTick);
        }

        private void ResolveSchedulingWindow(
            ulong currentDspClock,
            out long currentTick,
            out long targetTickExclusive)
        {
            currentTick = clockMapper.TryToTickAtOrBeforeDspClock(
                currentDspClock,
                out long resolvedCurrentTick)
                    ? resolvedCurrentTick
                    : transport.CurrentTick;

            ulong targetDspClock = checked(
                currentDspClock + lookaheadSampleFrames);
            long targetTick = clockMapper.ToTickAtOrBeforeDspClock(targetDspClock);
            targetTickExclusive = targetTick == long.MaxValue
                ? long.MaxValue
                : targetTick + 1L;
            if (targetTickExclusive < currentTick)
            {
                targetTickExclusive = currentTick;
            }
        }

        private void RecoverFromClockRegression()
        {
            long recoveryTick = transport.CurrentTick;
            instrument.CancelAllScheduledNotes();
            transport.Panic(recoveryTick);
            ClearAnchor();
            Reanchor(recoveryTick);
            ClockRegressionRecoveryCount++;
        }

        private static ulong ResolveLookaheadSampleFrames(
            int sampleRate,
            int milliseconds)
        {
            ulong scaledFrames = checked((ulong)sampleRate * (ulong)milliseconds);
            return checked((scaledFrames + 999UL) / 1_000UL);
        }

        private void ResetTelemetry()
        {
            DispatchedEventCount = 0L;
            LateEventCount = 0L;
            PlateauDurationCount = 0L;
            UnderrunPumpCount = 0L;
            BackpressurePumpCount = 0L;
            ClockRegressionRecoveryCount = 0L;
        }

        private void ClearAnchor()
        {
            clockMapper = null;
            dispatcher = null;
            lastObservedDspClock = 0UL;
            hasObservedDspClock = false;
        }

        private void FailClosed(Exception pumpFailure)
        {
            Exception cancellationFailure = null;
            try
            {
                instrument.CancelAllScheduledNotes();
            }
            catch (Exception exception)
            {
                cancellationFailure = exception;
            }

            transport.Stop();
            ClearAnchor();
            if (cancellationFailure != null)
            {
                throw new AggregateException(pumpFailure, cancellationFailure);
            }
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(FmodTransportScheduler));
            }
        }
    }
}
