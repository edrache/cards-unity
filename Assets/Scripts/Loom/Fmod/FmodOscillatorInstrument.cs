using System;
using System.Collections.Generic;
using System.Threading;
using Loom.Core;

namespace Loom.Fmod
{
    public sealed class FmodOscillatorInstrument : IInstrument
    {
        public const int DefaultVoiceCapacity = 8;
        public const string SynthBusPath = "bus:/MUS_Synth";

        private static long lastIssuedVoiceId;

        private readonly FmodStudioBusRouting busRouting;
        private readonly VoiceSlot[] voiceSlots;
        private ulong nextStartOrder = 1UL;
        private bool isDisposed;

        public FmodOscillatorInstrument(
            FMOD.System coreSystem,
            FMOD.Studio.System studioSystem)
            : this(
                coreSystem,
                studioSystem,
                FmodAdsrEnvelope.Default,
                FmodOscillatorSettings.Default,
                DefaultVoiceCapacity)
        {
        }

        public FmodOscillatorInstrument(
            FMOD.System coreSystem,
            FMOD.Studio.System studioSystem,
            FmodAdsrEnvelope envelope,
            FmodOscillatorSettings settings,
            int voiceCapacity = DefaultVoiceCapacity)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!coreSystem.hasHandle())
            {
                throw new ArgumentException(
                    "FMOD Core system must have a valid handle.",
                    nameof(coreSystem));
            }

            ValidateVoiceCapacity(voiceCapacity);
            busRouting = new FmodStudioBusRouting(
                studioSystem,
                SynthBusPath);
            try
            {
                busRouting.GetChannelGroup();

                voiceSlots = CreateVoiceSlots(
                    voiceCapacity,
                    () =>
                    {
                        var voice = new FmodOscillatorVoice(
                            coreSystem,
                            new Note(Note.ConcertAMidiNumber),
                            envelope,
                            settings);
                        return new FmodOscillatorInstrumentVoice(
                            voice,
                            settings.Gain,
                            busRouting);
                    });
            }
            catch (Exception creationException)
            {
                try
                {
                    busRouting.Dispose();
                }
                catch (Exception cleanupException)
                {
                    throw new AggregateException(
                        creationException,
                        cleanupException);
                }

                throw;
            }
        }

        internal FmodOscillatorInstrument(
            int voiceCapacity,
            Func<IOscillatorInstrumentVoice> voiceFactory)
        {
            ValidateVoiceCapacity(voiceCapacity);
            if (voiceFactory == null)
            {
                throw new ArgumentNullException(nameof(voiceFactory));
            }

            busRouting = null;
            voiceSlots = CreateVoiceSlots(voiceCapacity, voiceFactory);
        }

        public int VoiceCapacity => voiceSlots.Length;

        public int CreatedVoiceCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < voiceSlots.Length; i++)
                {
                    if (voiceSlots[i].Voice != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ActiveVoiceCount
        {
            get
            {
                ReclaimCompletedVoices();
                int count = 0;
                for (int i = 0; i < voiceSlots.Length; i++)
                {
                    IOscillatorInstrumentVoice voice = voiceSlots[i].Voice;
                    if (voice != null && voice.IsStarted)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int OwnedVoiceCount
        {
            get
            {
                ReclaimCompletedVoices();
                int count = 0;
                for (int i = 0; i < voiceSlots.Length; i++)
                {
                    if (voiceSlots[i].IsOwned)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public VoiceHandle NoteOn(Note note, byte velocity)
        {
            ThrowIfDisposed();
            ValidateVelocity(velocity);

            VoiceHandle handle = TakeNextVoiceHandle();
            ulong startOrder = TakeNextStartOrder();
            int slotIndex = FindSlotForNewVoice();
            VoiceSlot slot = voiceSlots[slotIndex];

            try
            {
                if (slot.Voice.IsStarted)
                {
                    slot.Voice.Stop();
                }

                slot.ClearOwnership();
                slot.Voice.Prepare(note, velocity);
                slot.Voice.Start();
            }
            catch (Exception startException)
            {
                slot.ClearOwnership();
                Exception cleanupException = TryDisposeSlotVoice(ref slot);
                voiceSlots[slotIndex] = slot;
                if (cleanupException != null)
                {
                    throw new AggregateException(
                        startException,
                        cleanupException);
                }

                throw;
            }

            slot.Assign(handle, startOrder);
            voiceSlots[slotIndex] = slot;
            return handle;
        }

        public bool NoteOff(VoiceHandle voice)
        {
            if (isDisposed || !voice.IsValid)
            {
                return false;
            }

            ReclaimCompletedVoices();
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                if (!voiceSlots[i].IsOwned || voiceSlots[i].Handle != voice)
                {
                    continue;
                }

                voiceSlots[i].IsOwned = false;
                try
                {
                    voiceSlots[i].Voice.BeginRelease();
                }
                catch (Exception releaseException)
                {
                    Exception cleanupException = TryDisposeSlotVoice(
                        ref voiceSlots[i]);
                    if (cleanupException != null)
                    {
                        throw new AggregateException(
                            releaseException,
                            cleanupException);
                    }

                    throw;
                }

                return true;
            }

            return false;
        }

        public void AllNotesOff()
        {
            if (isDisposed)
            {
                return;
            }

            ReclaimCompletedVoices();
            List<Exception> failures = null;
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                if (!voiceSlots[i].IsOwned)
                {
                    continue;
                }

                voiceSlots[i].IsOwned = false;
                try
                {
                    voiceSlots[i].Voice.BeginRelease();
                }
                catch (Exception releaseException)
                {
                    AddFailure(ref failures, releaseException);
                    AddFailure(
                        ref failures,
                        TryDisposeSlotVoice(ref voiceSlots[i]));
                }
            }

            ThrowFailures(failures);
        }

        public void PrepareForBankReload()
        {
            ThrowIfDisposed();
            ReclaimCompletedVoices();
            if (ActiveVoiceCount != 0)
            {
                throw new InvalidOperationException(
                    "All oscillator voices must finish before FMOD banks can be reloaded.");
            }

            busRouting.PrepareForBankReload();
        }

        public void RestoreAfterBankReload()
        {
            ThrowIfDisposed();
            busRouting.RestoreAfterBankReload();
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            List<Exception> failures = null;
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].ClearOwnership();
                AddFailure(
                    ref failures,
                    TryDisposeSlotVoice(ref voiceSlots[i]));
            }

            if (busRouting != null)
            {
                try
                {
                    busRouting.Dispose();
                }
                catch (Exception routingException)
                {
                    AddFailure(ref failures, routingException);
                }
            }

            if (failures == null)
            {
                isDisposed = true;
                return;
            }

            ThrowFailures(failures);
        }

        private static VoiceSlot[] CreateVoiceSlots(
            int voiceCapacity,
            Func<IOscillatorInstrumentVoice> voiceFactory)
        {
            var slots = new VoiceSlot[voiceCapacity];
            try
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    IOscillatorInstrumentVoice voice = voiceFactory();
                    if (voice == null)
                    {
                        throw new InvalidOperationException(
                            "The oscillator voice factory returned null.");
                    }

                    slots[i] = new VoiceSlot(voice);
                }

                return slots;
            }
            catch (Exception creationException)
            {
                List<Exception> failures = null;
                AddFailure(ref failures, creationException);
                for (int i = 0; i < slots.Length; i++)
                {
                    AddFailure(
                        ref failures,
                        TryDisposeSlotVoice(ref slots[i]));
                }

                if (failures.Count > 1)
                {
                    throw new AggregateException(failures);
                }

                throw;
            }
        }

        private static void ValidateVoiceCapacity(int voiceCapacity)
        {
            if (voiceCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(voiceCapacity),
                    voiceCapacity,
                    "Voice capacity must be greater than zero.");
            }
        }

        private static void ValidateVelocity(byte velocity)
        {
            if (velocity < NoteEvent.MinVelocity
                || velocity > NoteEvent.MaxVelocity)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(velocity),
                    velocity,
                    $"Velocity must be between {NoteEvent.MinVelocity} and {NoteEvent.MaxVelocity}.");
            }
        }

        private static VoiceHandle TakeNextVoiceHandle()
        {
            long issuedId = Interlocked.Increment(ref lastIssuedVoiceId);
            if (issuedId == 0L)
            {
                issuedId = Interlocked.Increment(ref lastIssuedVoiceId);
            }

            return new VoiceHandle(unchecked((ulong)issuedId));
        }

        private ulong TakeNextStartOrder()
        {
            if (nextStartOrder == 0UL)
            {
                throw new InvalidOperationException(
                    "The local voice start order has been exhausted.");
            }

            ulong startOrder = nextStartOrder;
            nextStartOrder = nextStartOrder == ulong.MaxValue
                ? 0UL
                : nextStartOrder + 1UL;
            return startOrder;
        }

        private int FindSlotForNewVoice()
        {
            ReclaimCompletedVoices();

            int oldestSlotIndex = -1;
            ulong oldestStartOrder = ulong.MaxValue;
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                VoiceSlot slot = voiceSlots[i];
                if (slot.Voice == null)
                {
                    continue;
                }

                if (!slot.Handle.IsValid && !slot.Voice.IsStarted)
                {
                    return i;
                }

                if (slot.StartOrder < oldestStartOrder)
                {
                    oldestStartOrder = slot.StartOrder;
                    oldestSlotIndex = i;
                }
            }

            if (oldestSlotIndex < 0)
            {
                throw new InvalidOperationException(
                    "The instrument has no usable oscillator voice slots.");
            }

            return oldestSlotIndex;
        }

        private void ReclaimCompletedVoices()
        {
            if (isDisposed)
            {
                return;
            }

            for (int i = 0; i < voiceSlots.Length; i++)
            {
                VoiceSlot slot = voiceSlots[i];
                if (slot.Voice != null
                    && slot.Handle.IsValid
                    && !slot.Voice.IsStarted)
                {
                    slot.ClearOwnership();
                    voiceSlots[i] = slot;
                }
            }
        }

        private static Exception TryDisposeSlotVoice(ref VoiceSlot slot)
        {
            IOscillatorInstrumentVoice voice = slot.Voice;
            if (voice == null)
            {
                slot.ClearOwnership();
                return null;
            }

            try
            {
                voice.Dispose();
                slot = default;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static void AddFailure(
            ref List<Exception> failures,
            Exception failure)
        {
            if (failure == null)
            {
                return;
            }

            if (failures == null)
            {
                failures = new List<Exception>();
            }

            failures.Add(failure);
        }

        private static void ThrowFailures(List<Exception> failures)
        {
            if (failures == null)
            {
                return;
            }

            if (failures.Count == 1)
            {
                throw failures[0];
            }

            throw new AggregateException(failures);
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(FmodOscillatorInstrument));
            }
        }

        private struct VoiceSlot
        {
            public VoiceSlot(IOscillatorInstrumentVoice voice)
            {
                Voice = voice;
                Handle = default;
                StartOrder = 0UL;
                IsOwned = false;
            }

            public IOscillatorInstrumentVoice Voice;

            public VoiceHandle Handle;

            public ulong StartOrder;

            public bool IsOwned;

            public void Assign(VoiceHandle handle, ulong startOrder)
            {
                Handle = handle;
                StartOrder = startOrder;
                IsOwned = true;
            }

            public void ClearOwnership()
            {
                Handle = default;
                StartOrder = 0UL;
                IsOwned = false;
            }
        }
    }

    internal interface IOscillatorInstrumentVoice : IDisposable
    {
        bool IsStarted { get; }

        void Prepare(Note note, byte velocity);

        void Start();

        void BeginRelease();

        void Stop();
    }

    internal sealed class FmodOscillatorInstrumentVoice
        : IOscillatorInstrumentVoice
    {
        private readonly FmodOscillatorVoice voice;
        private readonly float maximumGain;
        private readonly FmodStudioBusRouting busRouting;

        public FmodOscillatorInstrumentVoice(
            FmodOscillatorVoice voice,
            float maximumGain,
            FmodStudioBusRouting busRouting)
        {
            this.voice = voice ?? throw new ArgumentNullException(nameof(voice));
            this.maximumGain = maximumGain;
            this.busRouting = busRouting
                ?? throw new ArgumentNullException(nameof(busRouting));
        }

        public bool IsStarted => voice.IsStarted;

        public void Prepare(Note note, byte velocity)
        {
            voice.SetNote(note);
            voice.SetGain(
                maximumGain * (velocity / (float)NoteEvent.MaxVelocity));
        }

        public void Start()
        {
            voice.Start(busRouting.GetChannelGroup());
        }

        public void BeginRelease()
        {
            voice.BeginRelease();
        }

        public void Stop()
        {
            voice.Stop();
        }

        public void Dispose()
        {
            voice.Dispose();
        }
    }
}
