using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class DragMove : MonoBehaviour
{
    public XRController leftHand;
    public XRController rightHand;


    private bool buttonX;
    private bool buttonA;


    private XRController activeHand = null;
    private Vector3 handReferencePosition;
    private Vector3 handCurrentPosition;
    private Vector3 handPositionDifference;
    private Vector3 rigReferencePosition;


    

    // Start is called before the first frame update
    void Start()
    {


    }

    void Update()
    {
        buttonX = Input.GetButton("VR_Primary_Left");
        buttonA = Input.GetButton("VR_Primary_Right");
        
        if (Input.GetButtonDown("VR_Primary_Left") && !buttonA)
            {
                SwitchActiveHand(leftHand);
            }
        if (Input.GetButtonDown("VR_Primary_Right") && !buttonX)
        {
            SwitchActiveHand(rightHand);
        }
        if (!buttonA && !buttonX)
        {
            activeHand = null;
        }

        if (activeHand != null)
        {
            handCurrentPosition = activeHand.transform.localPosition;
            // Compare current position with reference position, get difference
            handPositionDifference = handReferencePosition - handCurrentPosition;
            handPositionDifference.y = 0;
            // apply difference to xr rig
            transform.position = rigReferencePosition + handPositionDifference;
        }
    }

    void SwitchActiveHand(XRController hand)
    {
        activeHand = hand;
        handReferencePosition = activeHand.transform.localPosition;
        rigReferencePosition = transform.position;
    }
}
