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
    public float floorAngleAverageRaycastWidth = 0.2f;


    // Internals
    private bool buttonX;
    private bool buttonA;


    private XRController activeHand = null;
    private Vector3 handReferencePosition;
    private Vector3 rigReferencePosition;
    private Vector3 newPlayerPosition;
    private Transform head;
    private Vector3 footPosition;

    // So here's what this is meant to do
    // 
    // The VR playing area (XR Rig, the object this script is attached to) is a box
    // Inside that, the head is represented by the main camera.
    // We set the Y of the XR Rig to represent the floor level underneath the camera at all times
    // When we're moving or walking into an obstacle, the XR Rig moves in the scene to accomodate that
    //
    // Movement works by holding down the move button on the controller which then locks that hand to that point in space
    // Meaning that you can then drag yourself along the world until you let go
    // First button pressed overrides the other, so if you press left hand, hold and press hold right hand the right hand doesn't do anything until you let go of left hand
    // If all buttons are let go, there should be a little bit of velocity to carry you through (with exceptions for steps/drops)
    //
    // Ground colissions are currently fucked.
    // Intended function:
    // - Allow moving up slopes up to x degrees
    // - Allow steps up to x step height (fast lerp between the heights to avoid brain murder)
    // - Allow for gravity and falling
    // - Kinda want to also experiment with jumping by swinging both arms backwards
    // That's basically it. I want to add a crawl function at some point but I'll deal with putting your head through the ceiling later.
    // If you try to put your head through a wall you'll get pushed back.
    // 
    // I (badly) stole most of the logic from this https://www.patreon.com/posts/astro-kat-moving-35207209 

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

        // If currently moving, calculate new position otherwise use existing.
        Vector2 newCoords;
        if (activeHand != null)
        {
            // Only use new position if position is valid
            Vector2 targetCoords = DetermineTargetCoords();
            bool canMove = CanMoveTo(targetCoords);
            newCoords = canMove ? targetCoords : new Vector2(transform.position.x, transform.position.z);
        }
        else
        {
            newCoords = new Vector2(transform.position.x, transform.position.z);
        }

        Vector2 finalCoord = newCoords + HeadProtectionOffset();
        if (activeHand == null){
            //get velocity by comparing position button was pressed and button was released and do a small amount of overshoot
        
        }
        transform.position = new Vector3(finalCoord.x, SlopeHeight(), finalCoord.y);
        
    }

    private float nearestDistance;
    private Collider[] sphereHits;
    private Vector3 safeHeadOffset;
    private Vector3 nearestHit;
    private Vector2 HeadProtectionOffset()
    {
        // =================== GET NEAREST COLLISION
        nearestDistance = 1;
        sphereHits = Physics.OverlapSphere(head.position, 0.3f, -1, QueryTriggerInteraction.Ignore);
        if (sphereHits.Length > 0)
        {
            foreach (Collider col in sphereHits)
            {
                Vector3 closestPoint = Physics.ClosestPoint(head.position, col, col.transform.position, col.transform.rotation);
                if (Vector3.Distance(head.position, closestPoint) < nearestDistance)
                {
                    nearestDistance = Vector3.Distance(head.position, closestPoint);
                    nearestHit = closestPoint;
                }
            }

            // ================== WORK OUT WHERE TO PUT PLAYER
            safeHeadOffset = head.position - nearestHit;
            Vector2 safeHeadOffsetCoords = new Vector2(safeHeadOffset.x, safeHeadOffset.z);
            return safeHeadOffsetCoords.normalized * (0.3f - nearestDistance);
        }
        return Vector2.zero;
    }

    void SwitchActiveHand(XRController hand)
    {
        activeHand = hand;
        handReferencePosition = activeHand.transform.localPosition;
        rigReferencePosition = transform.position;
    }

    private Vector3 stepOffsetOrigin;
    private Vector3 stepOffsetTarget;
    private Vector3 castDirection;
    private float chestHeight;
    private bool CanMoveTo(Vector3 targetPosition)
    {
        //============== CHECK IF VALID MOVEMENT
        // Cast ray from max step height to new position
        stepOffsetOrigin = new Vector3(head.position.x, transform.position.y + maxStepHeight, head.position.z);
        stepOffsetTarget = new Vector3(newPlayerPosition.x, newPlayerPosition.y + maxStepHeight, newPlayerPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;

        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Cast new position bottom to head height
        // TODO: Will need to account for slopes
        stepOffsetOrigin = new Vector3(newPlayerPosition.x, newPlayerPosition.y + maxStepHeight, newPlayerPosition.z);
        stepOffsetTarget = new Vector3(newPlayerPosition.x, head.position.y + maxStepHeight, newPlayerPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;
        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Cast box from chest to midpoint of new position
        chestHeight = (head.position.y - transform.position.y) / 2;
        stepOffsetOrigin = new Vector3(head.position.x, transform.position.y + chestHeight, head.position.z);
        stepOffsetTarget = new Vector3(newPlayerPosition.x, transform.position.y + chestHeight, newPlayerPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;
        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // If we get here, the path is clear. Apply changes.
        return true;
    }

    private Vector2 DetermineTargetCoords()
    {
        Vector3 handCurrentPosition = activeHand.transform.localPosition;
        // Compare current position with reference position, get difference
        Vector3 handPositionDifference = handReferencePosition - handCurrentPosition;
        handPositionDifference.y = 0;

        // apply difference to xr rig
        Vector3 newRigPosition = rigReferencePosition + handPositionDifference;
        newPlayerPosition = newRigPosition + head.transform.localPosition;
        newRigPosition = rigReferencePosition + handPositionDifference;
        return new Vector2( newRigPosition.x, newRigPosition.z);
    }

    private float sumOfFloorPoints;
    private int pointsToCount;
    RaycastHit floorHit;
    private float averageHeight;
    private float yRef;
    private float SlopeHeight()
    {
        // Reset counts
        sumOfFloorPoints = 0;
        pointsToCount = 0;

        // Get foot position
        footPosition = new Vector3(head.position.x, transform.position.y, head.position.z);

        // raycast around with floor +- step height
        for (int i = -1; i < 2; i++)
        {
            for (int j = 0; j < 2; j++)
            {
                //Checking from way above to see if we're actually above step height, carry on if not. This is fucking shit
                if (!Physics.Raycast(footPosition + new Vector3(i, maxStepHeight + 1, j), Vector3.down, 1 - maxStepHeight, -1, QueryTriggerInteraction.Ignore))
                {
                    // Check height at point around player and if it hits anything (and is valid!) we add it to average
                    if (Physics.Raycast(footPosition + new Vector3(i, maxStepHeight, j), Vector3.down, out floorHit, maxStepHeight * 2, -1, QueryTriggerInteraction.Ignore))
                    {
                        sumOfFloorPoints += floorHit.point.y;
                        pointsToCount++;
                    }
                }
            }
        }

        // get average height
        averageHeight = sumOfFloorPoints / pointsToCount;

        // apply that as y (smoothed)
        return Mathf.SmoothDamp(transform.position.y, averageHeight, ref yRef, 0.01f);
    }
}
