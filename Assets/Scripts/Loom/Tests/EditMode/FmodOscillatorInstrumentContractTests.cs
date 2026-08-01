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

            public int BeginReleaseCount { get; private set; }

            public int StopCount { get; private set; }

            public int DisposeCount { get; private set; }

            public Exception StartFailure { get; set; }

            public bool IsStarted => isStarted;

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
