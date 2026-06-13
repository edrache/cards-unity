using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//   ___
//  |[_]| 
//  |+ ;|  Goodies!
//  `---'


namespace Lux.Goodies 
{
    public class RotateThis : MonoBehaviour
    {
        [SerializeField] float rotationSpeed;

        void Update(){
            this.transform.Rotate(0, rotationSpeed*Time.deltaTime, 0);
        }
    }
}
