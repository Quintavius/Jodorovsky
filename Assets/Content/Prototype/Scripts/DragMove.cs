using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class DragMove : MonoBehaviour
{
    [Header("Hand References")]
    public XRController leftHand;
    public XRController rightHand;

    [Header("Settings")]
    public float maxStepHeight = 0.3f;


    // Internals
    private bool buttonX;
    private bool buttonA;


    private XRController activeHand = null;
    private Vector3 handReferencePosition;
    private Vector3 handCurrentPosition;
    private Vector3 handPositionDifference;
    private Vector3 rigReferencePosition;
    private Vector3 newPosition;
    private Transform head;
    

    void Start()
    {
head = Camera.main.transform;

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
           TryMove();
        }
    }

    private Ray ray;
    private Vector3 stepOffsetOrigin;
    private Vector3 stepOffsetTarget;
    private Vector3 castDirection;
    private float chestHeight;
    void TryMove(){
        //============== DETERMINE DISTANCE TO MOVE
        handCurrentPosition = activeHand.transform.localPosition;
        // Compare current position with reference position, get difference
        handPositionDifference = handReferencePosition - handCurrentPosition;
        handPositionDifference.y = 0;
        // apply difference to xr rig
        newPosition = rigReferencePosition + handPositionDifference;

        //============== CHECK IF VALID MOVEMENT
        // Cast ray from max step height to new position
        stepOffsetOrigin = new Vector3(head.position.x, transform.position.y + maxStepHeight, head.position.z);
        stepOffsetTarget = new Vector3(newPosition.x, newPosition.y + maxStepHeight, newPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;

        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore )){
            return;
        }

        // Cast new position bottom to head height
        // TODO: Will need to account for slopes
        stepOffsetOrigin = new Vector3(newPosition.x, newPosition.y + maxStepHeight, newPosition.z);
        stepOffsetTarget = new Vector3(newPosition.x, head.position.y + maxStepHeight, newPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;
        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore )){
            return;
        }

        // Cast box from chest to midpoint of new position
        chestHeight = (head.position.y - transform.position.y) / 2;
        stepOffsetOrigin = new Vector3(head.position.x, transform.position.y + chestHeight, head.position.z);
        stepOffsetTarget = new Vector3(newPosition.x, transform.position.y + chestHeight, newPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;
        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore )){
            return;
        }

        // If we get here, the path is clear. Apply changes.
        transform.position = newPosition;
        
    }

    void SwitchActiveHand(XRController hand)
    {
        activeHand = hand;
        handReferencePosition = activeHand.transform.localPosition;
        rigReferencePosition = transform.position;
    }
}
