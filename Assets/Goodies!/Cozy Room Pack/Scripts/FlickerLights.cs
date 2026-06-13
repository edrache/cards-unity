using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//   ___
//  |[_]| 
//  |+ ;|  Goodies!
//  `---'


namespace Lux.Goodies 
{
    public class FlickerLights : MonoBehaviour
    {
        [SerializeField] List<Light> lightComponent = new List<Light>();
        [SerializeField] List<Renderer> emissiveRenderer = new List<Renderer>();
        [SerializeField] float minBlinkTime = 0.1f;
        [SerializeField] float maxBlinkTime = 1.0f;

        [SerializeField] float timeBetweenFlickers, variance;


        [SerializeField] int minFlickers, maxFlickers;
        List<Material> startMat = new  List<Material>();
        [SerializeField] Material offMat;
        private void Start(){
            for(int i=0; i<emissiveRenderer.Count; i++){
                startMat.Add(emissiveRenderer[i].material);
            }
            StartCoroutine(Blink());
        }

        IEnumerator Blink(){
            while (true){
                yield return new WaitForSeconds(Random.Range(timeBetweenFlickers-variance, timeBetweenFlickers+variance));
            
                int numOfFlickers = Random.Range(minFlickers, maxFlickers);

                for(int j=0; j<numOfFlickers; j++){
                    
                    foreach(Light l in lightComponent){
                        l.enabled = false;
                    }

                    foreach(Renderer r in emissiveRenderer){
                        r.material = offMat;
                    }

                    yield return new WaitForSeconds(Random.Range(minBlinkTime, maxBlinkTime));

                    
                    foreach(Light l in lightComponent){
                        l.enabled = true;
                    }
                    
                    for(int i=0; i<emissiveRenderer.Count;i++){
                        emissiveRenderer[i].material = startMat[i];
                    }
                    
                    yield return new WaitForSeconds(Random.Range(minBlinkTime, maxBlinkTime));

                }

            }
        }
    }

}