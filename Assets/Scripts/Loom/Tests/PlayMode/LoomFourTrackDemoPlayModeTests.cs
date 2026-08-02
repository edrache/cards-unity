using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Loom.Tests.PlayMode
{
    public sealed class LoomFourTrackDemoPlayModeTests
    {
        [UnityTest]
        public IEnumerator DemoRunsFourRoutedTracksInFixedOrder()
        {
            yield return SceneManager.LoadSceneAsync("scn_loom", LoadSceneMode.Single);
            Demo.LoomSynthDemoController synthController =
                Object.FindFirstObjectByType<Demo.LoomSynthDemoController>();
            if (synthController != null)
            {
                synthController.enabled = false;
            }

            Demo.LoomFourTrackDemoController controller =
                Object.FindFirstObjectByType<Demo.LoomFourTrackDemoController>(
                    FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            controller.gameObject.SetActive(true);

            try
            {
                yield return new WaitForSecondsRealtime(0.35f);

                Assert.That(controller.IsRunning, Is.True);
                Assert.That(
                    controller.ActiveSchedulerCount,
                    Is.EqualTo(Demo.LoomFourTrackDemoController.TrackCount));
                Assert.That(
                    controller.GetBusPath(0),
                    Is.EqualTo(Fmod.FmodTrackBusPaths.Drums));
                Assert.That(
                    controller.GetBusPath(1),
                    Is.EqualTo(Fmod.FmodTrackBusPaths.Bass));
                Assert.That(
                    controller.GetBusPath(2),
                    Is.EqualTo(Fmod.FmodTrackBusPaths.Lead));
                Assert.That(
                    controller.GetBusPath(3),
                    Is.EqualTo(Fmod.FmodTrackBusPaths.Pad));

                for (int index = 0;
                    index < Demo.LoomFourTrackDemoController.TrackCount;
                    index++)
                {
                    Assert.That(
                        controller.GetDispatchedEventCount(index),
                        Is.GreaterThan(0L),
                        $"Track index {index} did not dispatch an event.");
                }
            }
            finally
            {
                controller.gameObject.SetActive(false);
            }

            yield return null;
        }
    }
}
