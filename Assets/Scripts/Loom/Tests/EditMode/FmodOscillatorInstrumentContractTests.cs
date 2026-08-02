using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Loom.Tests.EditMode
{
    public sealed class FmodOscillatorInstrumentContractTests
    {
        [Test]
        public void PublicConstructorRejectsAnInvalidCoreSystem()
        {
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodOscillatorInstrument(
                    default(FMOD.System),
                    default(FMOD.Studio.System)));
        }

        [Test]
        public void ConstructorRequiresAPositiveCapacityAndVoiceFactory()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Fmod.FmodOscillatorInstrument(0, CreateVoice));
            Assert.Throws<ArgumentNullException>(
                () => new Fmod.FmodOscillatorInstrument(8, null));
        }

        [Test]
        public void InstrumentUsesEightVoicesByDefaultAndImplementsCoreContract()
        {
            Assert.That(
                Fmod.FmodOscillatorInstrument.DefaultVoiceCapacity,
                Is.EqualTo(8));
            Assert.That(
                typeof(Core.IInstrument).IsAssignableFrom(
                    typeof(Fmod.FmodOscillatorInstrument)),
                Is.True);
            Assert.That(
                Fmod.FmodOscillatorInstrument.SynthBusPath,
                Is.EqualTo("bus:/MUS_Synth"));
            using (var instrument = new Fmod.FmodOscillatorInstrument(1, CreateVoice))
            {
                Assert.That(instrument.BusPath, Is.EqualTo(Fmod.FmodTrackBusPaths.Synth));
            }
        }

        [Test]
        public void StudioBusRoutingRejectsInvalidConstructionInputs()
        {
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodStudioBusRouting(default, string.Empty));
            Assert.Throws<ArgumentException>(
                () => new Fmod.FmodStudioBusRouting(
                    default,
                    Fmod.FmodOscillatorInstrument.SynthBusPath));
        }

        [Test]
        public void NoteOnReturnsStableHandlesAndPreservesVoiceInputs()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(2, voices))
            {
                Core.VoiceHandle first = instrument.NoteOn(new Core.Note(60), 80);
                Core.VoiceHandle second = instrument.NoteOn(new Core.Note(64), 100);

                Assert.That(first.IsValid, Is.True);
                Assert.That(second.IsValid, Is.True);
                Assert.That(second, Is.Not.EqualTo(first));
                Assert.That(voices[0].Note, Is.EqualTo(new Core.Note(60)));
                Assert.That(voices[0].Velocity, Is.EqualTo(80));
                Assert.That(voices[1].Note, Is.EqualTo(new Core.Note(64)));
                Assert.That(voices[1].Velocity, Is.EqualTo(100));
                Assert.That(voices[0].StartCount, Is.EqualTo(1));
                Assert.That(voices[1].StartCount, Is.EqualTo(1));
                Assert.That(instrument.OwnedVoiceCount, Is.EqualTo(2));
                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(2));
                Assert.That(instrument.CreatedVoiceCount, Is.EqualTo(2));
            }
        }

        [TestCase(0)]
        [TestCase(128)]
        public void NoteOnRejectsVelocityOutsideThePlayableRange(byte velocity)
        {
            using (var instrument = new Fmod.FmodOscillatorInstrument(
                1,
                CreateVoice))
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => instrument.NoteOn(new Core.Note(60), velocity));
                Assert.That(instrument.ActiveVoiceCount, Is.Zero);
                Assert.That(instrument.CreatedVoiceCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ScheduleNotePreservesTheLogicalEventAndExactDspClocks()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(1, voices))
            {
                var noteEvent = new Core.NoteEvent(
                    new Core.Note(60),
                    81,
                    960,
                    240);

                Core.VoiceHandle handle = instrument.ScheduleNote(
                    noteEvent,
                    1_000_000UL,
                    1_006_000UL);

                Assert.That(handle.IsValid, Is.True);
                Assert.That(voices[0].Note, Is.EqualTo(noteEvent.Note));
                Assert.That(voices[0].Velocity, Is.EqualTo(noteEvent.Velocity));
                Assert.That(voices[0].StartScheduledCount, Is.EqualTo(1));
                Assert.That(voices[0].ScheduledStartDspClock, Is.EqualTo(1_000_000UL));
                Assert.That(
                    voices[0].ScheduledReleaseStartDspClock,
                    Is.EqualTo(1_006_000UL));
                Assert.That(instrument.OwnedVoiceCount, Is.EqualTo(1));
            }
        }

        [TestCase(0UL, 1UL)]
        [TestCase(100UL, 100UL)]
        [TestCase(100UL, 99UL)]
        public void ScheduleNoteRejectsInvalidClockOrderBeforePreparingAVoice(
            ulong startDspClock,
            ulong releaseStartDspClock)
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(1, voices))
            {
                var noteEvent = new Core.NoteEvent(new Core.Note(60), 100, 0, 1);

                Assert.Throws<ArgumentOutOfRangeException>(
                    () => instrument.ScheduleNote(
                        noteEvent,
                        startDspClock,
                        releaseStartDspClock));
                Assert.That(voices[0].PrepareCount, Is.Zero);
                Assert.That(voices[0].StartScheduledCount, Is.Zero);
            }
        }

        [Test]
        public void HandleFromAnotherInstrumentCannotReleaseALocalVoice()
        {
            using (var firstInstrument = new Fmod.FmodOscillatorInstrument(
                1,
                CreateVoice))
            using (var secondInstrument = new Fmod.FmodOscillatorInstrument(
                1,
                CreateVoice))
            {
                Core.VoiceHandle foreignHandle = firstInstrument.NoteOn(
                    new Core.Note(60),
                    100);
                Core.VoiceHandle localHandle = secondInstrument.NoteOn(
                    new Core.Note(64),
                    100);

                Assert.That(secondInstrument.NoteOff(foreignHandle), Is.False);
                Assert.That(secondInstrument.NoteOff(localHandle), Is.True);
            }
        }

        [Test]
        public void NoteOffTargetsAndConsumesOnlyTheExactHandle()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(2, voices))
            {
                Core.VoiceHandle first = instrument.NoteOn(new Core.Note(60), 100);
                Core.VoiceHandle second = instrument.NoteOn(new Core.Note(64), 100);

                Assert.That(instrument.NoteOff(first), Is.True);
                Assert.That(instrument.NoteOff(first), Is.False);
                Assert.That(instrument.NoteOff(default), Is.False);
                Assert.That(voices[0].BeginReleaseCount, Is.EqualTo(1));
                Assert.That(voices[1].BeginReleaseCount, Is.Zero);
                Assert.That(instrument.OwnedVoiceCount, Is.EqualTo(1));
                Assert.That(instrument.NoteOff(second), Is.True);
            }
        }

        [Test]
        public void ScheduledHandlesRequireHardCancellation()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(1, voices))
            {
                var noteEvent = new Core.NoteEvent(new Core.Note(60), 100, 0, 1);
                Core.VoiceHandle handle = instrument.ScheduleNote(
                    noteEvent,
                    100UL,
                    101UL);

                Assert.That(instrument.NoteOff(handle), Is.False);
                Assert.That(instrument.CancelScheduledNote(default), Is.False);
                Assert.That(instrument.CancelScheduledNote(handle), Is.True);
                Assert.That(instrument.CancelScheduledNote(handle), Is.False);
                Assert.That(voices[0].BeginReleaseCount, Is.Zero);
                Assert.That(voices[0].StopCount, Is.EqualTo(1));
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);
            }
        }

        [Test]
        public void CancelAllScheduledNotesLeavesKeyboardVoicesOwned()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(2, voices))
            {
                Core.VoiceHandle keyboardHandle = instrument.NoteOn(
                    new Core.Note(60),
                    100);
                var noteEvent = new Core.NoteEvent(new Core.Note(64), 90, 240, 120);
                Core.VoiceHandle scheduledHandle = instrument.ScheduleNote(
                    noteEvent,
                    1_000UL,
                    1_100UL);

                instrument.CancelAllScheduledNotes();
                instrument.CancelAllScheduledNotes();

                Assert.That(voices[0].StopCount, Is.Zero);
                Assert.That(voices[0].BeginReleaseCount, Is.Zero);
                Assert.That(voices[1].StopCount, Is.EqualTo(1));
                Assert.That(voices[1].BeginReleaseCount, Is.Zero);
                Assert.That(instrument.CancelScheduledNote(scheduledHandle), Is.False);
                Assert.That(instrument.OwnedVoiceCount, Is.EqualTo(1));

                instrument.AllNotesOff();

                Assert.That(voices[0].BeginReleaseCount, Is.EqualTo(1));
                Assert.That(instrument.NoteOff(keyboardHandle), Is.False);
            }
        }

        [Test]
        public void AllNotesOffReleasesKeyboardAndStopsScheduledVoices()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(2, voices))
            {
                instrument.NoteOn(new Core.Note(60), 100);
                instrument.ScheduleNote(
                    new Core.NoteEvent(new Core.Note(64), 100, 0, 1),
                    100UL,
                    101UL);

                instrument.AllNotesOff();

                Assert.That(voices[0].BeginReleaseCount, Is.EqualTo(1));
                Assert.That(voices[0].StopCount, Is.Zero);
                Assert.That(voices[1].BeginReleaseCount, Is.Zero);
                Assert.That(voices[1].StopCount, Is.EqualTo(1));
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);
            }
        }

        [Test]
        public void NinthNoteDeterministicallyStealsTheOldestVoice()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(8, voices))
            {
                var handles = new Core.VoiceHandle[8];
                for (int i = 0; i < handles.Length; i++)
                {
                    handles[i] = instrument.NoteOn(new Core.Note(60 + i), 100);
                }

                Core.VoiceHandle ninth = instrument.NoteOn(new Core.Note(72), 100);

                Assert.That(voices[0].PrepareCount, Is.EqualTo(2));
                Assert.That(voices[0].StopCount, Is.EqualTo(1));
                Assert.That(voices[0].DisposeCount, Is.Zero);
                for (int i = 1; i < 8; i++)
                {
                    Assert.That(voices[i].PrepareCount, Is.EqualTo(1));
                    Assert.That(voices[i].DisposeCount, Is.Zero);
                }

                Assert.That(instrument.NoteOff(handles[0]), Is.False);
                Assert.That(instrument.NoteOff(handles[1]), Is.True);
                Assert.That(instrument.NoteOff(ninth), Is.True);
                Assert.That(instrument.ActiveVoiceCount, Is.EqualTo(8));
                Assert.That(instrument.CreatedVoiceCount, Is.EqualTo(8));
            }
        }

        [Test]
        public void CompletedVoiceIsReclaimedBeforeAnActiveVoiceIsStolen()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(2, voices))
            {
                instrument.NoteOn(new Core.Note(60), 100);
                Core.VoiceHandle second = instrument.NoteOn(new Core.Note(64), 100);
                voices[0].Complete();

                instrument.NoteOn(new Core.Note(67), 100);

                Assert.That(voices[0].PrepareCount, Is.EqualTo(2));
                Assert.That(voices[0].DisposeCount, Is.Zero);
                Assert.That(voices[1].PrepareCount, Is.EqualTo(1));
                Assert.That(voices[1].DisposeCount, Is.Zero);
                Assert.That(instrument.NoteOff(second), Is.True);
            }
        }

        [Test]
        public void AllNotesOffInvalidatesEveryHandleAndReleasesEachOwnedVoice()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(3, voices))
            {
                Core.VoiceHandle first = instrument.NoteOn(new Core.Note(60), 100);
                Core.VoiceHandle second = instrument.NoteOn(new Core.Note(64), 100);
                Core.VoiceHandle third = instrument.NoteOn(new Core.Note(67), 100);

                instrument.AllNotesOff();

                Assert.That(instrument.NoteOff(first), Is.False);
                Assert.That(instrument.NoteOff(second), Is.False);
                Assert.That(instrument.NoteOff(third), Is.False);
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);
                Assert.That(voices[0].BeginReleaseCount, Is.EqualTo(1));
                Assert.That(voices[1].BeginReleaseCount, Is.EqualTo(1));
                Assert.That(voices[2].BeginReleaseCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void SynthControlsReachEveryPreallocatedVoice()
        {
            var voices = new List<FakeVoice>();
            using (var instrument = CreateInstrument(3, voices))
            {
                var envelope = new Fmod.FmodAdsrEnvelope(
                    0.02d,
                    0.15d,
                    0.6f,
                    0.4d);

                instrument.SetEnvelope(envelope);
                instrument.SetWaveform(Fmod.FmodOscillatorWaveform.Triangle);
                instrument.SetGain(0.25f);
                instrument.SetOctave(-1);
                instrument.SetCutoffHz(3200f);
                instrument.SetResonance(1.5f);

                Assert.That(instrument.Envelope, Is.SameAs(envelope));
                Assert.That(
                    instrument.Waveform,
                    Is.EqualTo(Fmod.FmodOscillatorWaveform.Triangle));
                Assert.That(instrument.Gain, Is.EqualTo(0.25f));
                Assert.That(instrument.Octave, Is.EqualTo(-1));
                Assert.That(instrument.CutoffHz, Is.EqualTo(3200f));
                Assert.That(instrument.Resonance, Is.EqualTo(1.5f));

                foreach (FakeVoice voice in voices)
                {
                    Assert.That(voice.Envelope, Is.SameAs(envelope));
                    Assert.That(
                        voice.Waveform,
                        Is.EqualTo(Fmod.FmodOscillatorWaveform.Triangle));
                    Assert.That(voice.MaximumGain, Is.EqualTo(0.25f));
                    Assert.That(voice.Octave, Is.EqualTo(-1));
                    Assert.That(voice.CutoffHz, Is.EqualTo(3200f));
                    Assert.That(voice.Resonance, Is.EqualTo(1.5f));
                }
            }
        }

        [Test]
        public void DisposeImmediatelyCleansEveryVoiceAndIsIdempotent()
        {
            var voices = new List<FakeVoice>();
            var instrument = CreateInstrument(3, voices);
            Core.VoiceHandle first = instrument.NoteOn(new Core.Note(60), 100);
            instrument.NoteOn(new Core.Note(64), 100);
            instrument.NoteOn(new Core.Note(67), 100);

            instrument.Dispose();
            instrument.Dispose();

            Assert.That(instrument.NoteOff(first), Is.False);
            Assert.That(instrument.ActiveVoiceCount, Is.Zero);
            Assert.That(instrument.CreatedVoiceCount, Is.Zero);
            Assert.That(instrument.OwnedVoiceCount, Is.Zero);
            Assert.That(voices[0].DisposeCount, Is.EqualTo(1));
            Assert.That(voices[1].DisposeCount, Is.EqualTo(1));
            Assert.That(voices[2].DisposeCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(
                () => instrument.NoteOn(new Core.Note(72), 100));
        }

        [Test]
        public void FailedVoiceStartDisposesTheUnownedVoice()
        {
            var voice = new FakeVoice
            {
                StartFailure = new InvalidOperationException("Start failed.")
            };
            using (var instrument = new Fmod.FmodOscillatorInstrument(
                1,
                () => voice))
            {
                Assert.Throws<InvalidOperationException>(
                    () => instrument.NoteOn(new Core.Note(60), 100));

                Assert.That(voice.DisposeCount, Is.EqualTo(1));
                Assert.That(instrument.ActiveVoiceCount, Is.Zero);
                Assert.That(instrument.CreatedVoiceCount, Is.Zero);
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);
            }
        }

        [Test]
        public void FailedScheduledVoiceStartDisposesTheUnownedVoice()
        {
            var voice = new FakeVoice
            {
                StartFailure = new InvalidOperationException("Scheduled start failed.")
            };
            using (var instrument = new Fmod.FmodOscillatorInstrument(
                1,
                () => voice))
            {
                var noteEvent = new Core.NoteEvent(new Core.Note(60), 100, 0, 1);

                Assert.Throws<InvalidOperationException>(
                    () => instrument.ScheduleNote(noteEvent, 100UL, 101UL));

                Assert.That(voice.DisposeCount, Is.EqualTo(1));
                Assert.That(instrument.ActiveVoiceCount, Is.Zero);
                Assert.That(instrument.CreatedVoiceCount, Is.Zero);
                Assert.That(instrument.OwnedVoiceCount, Is.Zero);
            }
        }

        private static Fmod.FmodOscillatorInstrument CreateInstrument(
            int capacity,
            ICollection<FakeVoice> voices)
        {
            return new Fmod.FmodOscillatorInstrument(
                capacity,
                () =>
                {
                    var voice = new FakeVoice();
                    voices.Add(voice);
                    return voice;
                });
        }

        private static Fmod.IOscillatorInstrumentVoice CreateVoice()
        {
            return new FakeVoice();
        }

        private sealed class FakeVoice : Fmod.IOscillatorInstrumentVoice
        {
            private bool isStarted;

            public Core.Note Note { get; private set; }

            public byte Velocity { get; private set; }

            public int PrepareCount { get; private set; }

            public int StartCount { get; private set; }

            public int StartScheduledCount { get; private set; }

            public ulong ScheduledStartDspClock { get; private set; }

            public ulong ScheduledReleaseStartDspClock { get; private set; }

            public int BeginReleaseCount { get; private set; }

            public int StopCount { get; private set; }

            public int DisposeCount { get; private set; }

            public Exception StartFailure { get; set; }

            public Fmod.FmodAdsrEnvelope Envelope { get; private set; }

            public Fmod.FmodOscillatorWaveform Waveform { get; private set; }

            public float MaximumGain { get; private set; }

            public int Octave { get; private set; }

            public float CutoffHz { get; private set; }

            public float Resonance { get; private set; }

            public bool IsStarted => isStarted;

            public void SetEnvelope(Fmod.FmodAdsrEnvelope envelope)
            {
                Envelope = envelope;
            }

            public void SetWaveform(Fmod.FmodOscillatorWaveform waveform)
            {
                Waveform = waveform;
            }

            public void SetMaximumGain(float gain)
            {
                MaximumGain = gain;
            }

            public void SetOctave(int octave)
            {
                Octave = octave;
            }

            public void SetCutoffHz(float cutoffHz)
            {
                CutoffHz = cutoffHz;
            }

            public void SetResonance(float resonance)
            {
                Resonance = resonance;
            }

            public void Prepare(Core.Note note, byte velocity)
            {
                Note = note;
                Velocity = velocity;
                PrepareCount++;
            }

            public void Start()
            {
                StartCount++;
                if (StartFailure != null)
                {
                    throw StartFailure;
                }

                isStarted = true;
            }

            public void BeginRelease()
            {
                BeginReleaseCount++;
            }

            public bool StartScheduled(
                ulong startDspClock,
                ulong releaseStartDspClock)
            {
                StartScheduledCount++;
                ScheduledStartDspClock = startDspClock;
                ScheduledReleaseStartDspClock = releaseStartDspClock;
                if (StartFailure != null)
                {
                    throw StartFailure;
                }

                isStarted = true;
                return false;
            }

            public void Complete()
            {
                isStarted = false;
            }

            public void Stop()
            {
                StopCount++;
                isStarted = false;
            }

            public void Dispose()
            {
                DisposeCount++;
                isStarted = false;
            }
        }
    }
}
