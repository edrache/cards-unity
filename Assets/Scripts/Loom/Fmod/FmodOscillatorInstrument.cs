using System;
using System.Collections.Generic;
using System.Threading;
using Loom.Core;

namespace Loom.Fmod
{
    public sealed class FmodOscillatorInstrument
        : IInstrument, IFmodScheduledInstrument
    {
        public const int DefaultVoiceCapacity = 8;
        public const string SynthBusPath = FmodTrackBusPaths.Synth;

        private static long lastIssuedVoiceId;

        private readonly FMOD.System coreSystem;
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
                DefaultVoiceCapacity,
                SynthBusPath)
        {
        }

        public FmodOscillatorInstrument(
            FMOD.System coreSystem,
            FMOD.Studio.System studioSystem,
            FmodAdsrEnvelope envelope,
            FmodOscillatorSettings settings,
            int voiceCapacity = DefaultVoiceCapacity,
            string busPath = SynthBusPath)
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
            Envelope = envelope;
            Waveform = settings.Waveform;
            Gain = settings.Gain;
            Octave = settings.Octave;
            CutoffHz = settings.CutoffHz;
            Resonance = settings.Resonance;
            this.coreSystem = coreSystem;
            busRouting = new FmodStudioBusRouting(
                studioSystem,
                busPath);
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
            Envelope = FmodAdsrEnvelope.Default;
            Waveform = FmodOscillatorSettings.Default.Waveform;
            Gain = FmodOscillatorSettings.Default.Gain;
            Octave = FmodOscillatorSettings.Default.Octave;
            CutoffHz = FmodOscillatorSettings.Default.CutoffHz;
            Resonance = FmodOscillatorSettings.Default.Resonance;
            voiceSlots = CreateVoiceSlots(voiceCapacity, voiceFactory);
        }

        public FmodAdsrEnvelope Envelope { get; private set; }

        public FmodOscillatorWaveform Waveform { get; private set; }

        public float Gain { get; private set; }

        public int Octave { get; private set; }

        public float CutoffHz { get; private set; }

        public float Resonance { get; private set; }

        public int VoiceCapacity => voiceSlots.Length;

        public string BusPath => busRouting?.BusPath ?? SynthBusPath;

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

        /// <summary>
        /// Creates a clock mapper in the exact synth-bus domain used by scheduled voices.
        /// </summary>
        public FmodTickDspClockMapper CreateTickDspClockMapper(
            TempoMap tempoMap,
            long anchorTick,
            ulong anchorLeadSampleFrames = 0UL)
        {
            ThrowIfDisposed();
            if (busRouting == null)
            {
                throw new InvalidOperationException(
                    "A native synth bus route is required to create a DSP clock mapper.");
            }

            return new FmodTickDspClockMapper(
                coreSystem,
                busRouting.GetChannelGroup(),
                tempoMap,
                anchorTick,
                anchorLeadSampleFrames);
        }

        public void SetEnvelope(FmodAdsrEnvelope envelope)
        {
            ThrowIfDisposed();
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].Voice?.SetEnvelope(envelope);
            }

            Envelope = envelope;
        }

        public void SetWaveform(FmodOscillatorWaveform waveform)
        {
            ThrowIfDisposed();
            FmodOscillatorSettings.ValidateWaveform(waveform);
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].Voice?.SetWaveform(waveform);
            }

            Waveform = waveform;
        }

        public void SetGain(float gain)
        {
            ThrowIfDisposed();
            FmodOscillatorSettings.ValidateGain(gain);
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].Voice?.SetMaximumGain(gain);
            }

            Gain = gain;
        }

        public void SetOctave(int octave)
        {
            ThrowIfDisposed();
            FmodOscillatorSettings.ValidateOctave(octave);
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].Voice?.SetOctave(octave);
            }

            Octave = octave;
        }

        public void SetCutoffHz(float cutoffHz)
        {
            ThrowIfDisposed();
            FmodOscillatorSettings.ValidateCutoffHz(cutoffHz);
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].Voice?.SetCutoffHz(cutoffHz);
            }

            CutoffHz = cutoffHz;
        }

        public void SetResonance(float resonance)
        {
            ThrowIfDisposed();
            FmodOscillatorSettings.ValidateResonance(resonance);
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                voiceSlots[i].Voice?.SetResonance(resonance);
            }

            Resonance = resonance;
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

            slot.Assign(handle, startOrder, false);
            voiceSlots[slotIndex] = slot;
            return handle;
        }

        /// <summary>
        /// Schedules one resolved logical note at exact clocks in the synth bus domain.
        /// </summary>
        public VoiceHandle ScheduleNote(
            NoteEvent noteEvent,
            ulong startDspClock,
            ulong releaseStartDspClock)
        {
            ThrowIfDisposed();
            if (startDspClock == 0UL)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startDspClock),
                    startDspClock,
                    "Scheduled start DSP clock must be positive.");
            }

            if (releaseStartDspClock <= startDspClock)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(releaseStartDspClock),
                    releaseStartDspClock,
                    "Scheduled release DSP clock must be after the start clock.");
            }

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
                slot.Voice.Prepare(noteEvent.Note, noteEvent.Velocity);
                slot.Voice.StartScheduled(startDspClock, releaseStartDspClock);
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

            slot.Assign(handle, startOrder, true);
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

                if (voiceSlots[i].IsScheduled)
                {
                    return false;
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

        /// <summary>
        /// Hard-stops and consumes one exact scheduled voice handle.
        /// </summary>
        public bool CancelScheduledNote(VoiceHandle voice)
        {
            if (isDisposed || !voice.IsValid)
            {
                return false;
            }

            ReclaimCompletedVoices();
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                if (!voiceSlots[i].IsOwned
                    || !voiceSlots[i].IsScheduled
                    || voiceSlots[i].Handle != voice)
                {
                    continue;
                }

                voiceSlots[i].ClearOwnership();
                try
                {
                    voiceSlots[i].Voice.Stop();
                }
                catch (Exception stopException)
                {
                    Exception cleanupException = TryDisposeSlotVoice(
                        ref voiceSlots[i]);
                    if (cleanupException != null)
                    {
                        throw new AggregateException(
                            stopException,
                            cleanupException);
                    }

                    throw;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Hard-stops every owned scheduled voice without affecting interactive notes.
        /// </summary>
        public void CancelAllScheduledNotes()
        {
            if (isDisposed)
            {
                return;
            }

            ReclaimCompletedVoices();
            List<Exception> failures = null;
            for (int i = 0; i < voiceSlots.Length; i++)
            {
                if (!voiceSlots[i].IsOwned || !voiceSlots[i].IsScheduled)
                {
                    continue;
                }

                voiceSlots[i].ClearOwnership();
                try
                {
                    voiceSlots[i].Voice.Stop();
                }
                catch (Exception stopException)
                {
                    AddFailure(ref failures, stopException);
                    AddFailure(
                        ref failures,
                        TryDisposeSlotVoice(ref voiceSlots[i]));
                }
            }

            ThrowFailures(failures);
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

                bool isScheduled = voiceSlots[i].IsScheduled;
                voiceSlots[i].IsOwned = false;
                try
                {
                    if (isScheduled)
                    {
                        voiceSlots[i].Voice.Stop();
                        voiceSlots[i].ClearOwnership();
                    }
                    else
                    {
                        voiceSlots[i].Voice.BeginRelease();
                    }
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
                IsScheduled = false;
            }

            public IOscillatorInstrumentVoice Voice;

            public VoiceHandle Handle;

            public ulong StartOrder;

            public bool IsOwned;

            public bool IsScheduled;

            public void Assign(
                VoiceHandle handle,
                ulong startOrder,
                bool isScheduled)
            {
                Handle = handle;
                StartOrder = startOrder;
                IsOwned = true;
                IsScheduled = isScheduled;
            }

            public void ClearOwnership()
            {
                Handle = default;
                StartOrder = 0UL;
                IsOwned = false;
                IsScheduled = false;
            }
        }
    }

    internal interface IOscillatorInstrumentVoice : IDisposable
    {
        bool IsStarted { get; }

        void SetEnvelope(FmodAdsrEnvelope envelope);

        void SetWaveform(FmodOscillatorWaveform waveform);

        void SetMaximumGain(float gain);

        void SetOctave(int octave);

        void SetCutoffHz(float cutoffHz);

        void SetResonance(float resonance);

        void Prepare(Note note, byte velocity);

        void Start();

        void StartScheduled(ulong startDspClock, ulong releaseStartDspClock);

        void BeginRelease();

        void Stop();
    }

    internal interface IFmodScheduledInstrument
    {
        VoiceHandle ScheduleNote(
            NoteEvent noteEvent,
            ulong startDspClock,
            ulong releaseStartDspClock);
    }

    internal sealed class FmodOscillatorInstrumentVoice
        : IOscillatorInstrumentVoice
    {
        private readonly FmodOscillatorVoice voice;
        private float maximumGain;
        private readonly FmodStudioBusRouting busRouting;
        private FmodAdsrEnvelope pendingEnvelope;
        private byte currentVelocity;

        public FmodOscillatorInstrumentVoice(
            FmodOscillatorVoice voice,
            float maximumGain,
            FmodStudioBusRouting busRouting)
        {
            this.voice = voice ?? throw new ArgumentNullException(nameof(voice));
            this.maximumGain = maximumGain;
            pendingEnvelope = voice.Envelope;
            this.busRouting = busRouting
                ?? throw new ArgumentNullException(nameof(busRouting));
        }

        public bool IsStarted => voice.IsStarted;

        public void SetEnvelope(FmodAdsrEnvelope envelope)
        {
            pendingEnvelope = envelope
                ?? throw new ArgumentNullException(nameof(envelope));
            if (!voice.IsStarted)
            {
                voice.SetEnvelope(envelope);
            }
        }

        public void SetWaveform(FmodOscillatorWaveform waveform)
        {
            voice.SetWaveform(waveform);
        }

        public void SetMaximumGain(float gain)
        {
            maximumGain = gain;
            if (voice.IsStarted)
            {
                voice.SetGain(
                    maximumGain
                    * (currentVelocity / (float)NoteEvent.MaxVelocity));
            }
            else
            {
                voice.SetGain(maximumGain);
            }
        }

        public void SetOctave(int octave)
        {
            voice.SetOctave(octave);
        }

        public void SetCutoffHz(float cutoffHz)
        {
            voice.SetCutoffHz(cutoffHz);
        }

        public void SetResonance(float resonance)
        {
            voice.SetResonance(resonance);
        }

        public void Prepare(Note note, byte velocity)
        {
            if (!ReferenceEquals(voice.Envelope, pendingEnvelope))
            {
                voice.SetEnvelope(pendingEnvelope);
            }

            currentVelocity = velocity;
            voice.SetNote(note);
            voice.SetGain(
                maximumGain * (velocity / (float)NoteEvent.MaxVelocity));
        }

        public void Start()
        {
            voice.Start(busRouting.GetChannelGroup());
        }

        public void StartScheduled(
            ulong startDspClock,
            ulong releaseStartDspClock)
        {
            voice.StartScheduled(
                busRouting.GetChannelGroup(),
                startDspClock,
                releaseStartDspClock);
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
