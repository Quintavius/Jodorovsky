using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class DragMoveColin : MonoBehaviour
{
    [Header("Hand References")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Settings")]
    public float maxStepHeight = 0.3f;
    public float floorAngleAverageRaycastWidth = 0.2f;
    public float slopeLimit = 30f;

    // Internals
    private bool buttonX;
    private bool buttonA;


    private Transform activeHand = null;
    private Vector3 handReferencePosition;
    private Vector3 rigReferencePosition;
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



    //Basically : Implemented floor raycast and slope handling of MinionsArt : it replaces SlopeHeight in the Y calculation
    //Also added one floor check in CanMove
    //The raycast values in ground floor checking are based on StepHeight (wild guesses) so feel free to change it
    //Also added a Slope float value in order to handle the slopes (wow)
    void Start()
    {
        head = Camera.main.transform;
    }

    void Update()
    {
        buttonX = Input.GetButton("VR_Primary_Left") || Input.GetMouseButton(0);
        buttonA = Input.GetButton("VR_Primary_Right") || Input.GetMouseButton(1);

        if ((Input.GetButtonDown("VR_Primary_Left") || Input.GetMouseButtonDown(0)) && !buttonA)
        {
            SwitchActiveHand(leftHand);
        }
        if ((Input.GetButtonDown("VR_Primary_Right") || Input.GetMouseButtonDown(1)) && !buttonX)
        {
            SwitchActiveHand(rightHand);
        }
        if (!buttonA && !buttonX)
        {
            activeHand = null;
        }

        //if(activeHand != null)
        //{
        //    Vector2 targetCoords = DetermineTargetCoords();
        //}

       

        // If currently moving, calculate new position otherwise use existing.
        Vector2 newCoords;
        if (activeHand != null)
        {
            Vector2 targetCoords = DetermineTargetCoords();
            bool canMove = CanMoveTo(new Vector3(targetCoords.x, transform.position.y, targetCoords.y));

            // Only use new position if position is valid
            newCoords = canMove ? targetCoords : new Vector2(transform.position.x, transform.position.z);
        }
        else
        {
            newCoords = new Vector2(transform.position.x, transform.position.z);
        }

        Vector2 finalCoord = newCoords + HeadProtectionOffset();
        if (activeHand == null)
        {
            //get velocity by comparing position button was pressed and button was released and do a small amount of overshoot

        }
        transform.position = new Vector3(finalCoord.x, FindFloor().y, finalCoord.y);

    }


    private Vector2 HeadProtectionOffset()
    {
        float nearestDistance;
        Collider[] sphereHits;
        Vector3 safeHeadOffset;
        Vector3 nearestHit = Vector3.zero;
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

    void SwitchActiveHand(Transform hand)
    {
        activeHand = hand;
        handReferencePosition = activeHand.transform.localPosition;
        rigReferencePosition = transform.position;
    }
    Vector3 newPlayerPosition = Vector3.zero;
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(newPlayerPosition, 0.2f);
    }

    private bool CanMoveTo(Vector3 targetPosition)
    {

        newPlayerPosition = targetPosition + head.transform.localPosition;

        Vector3 stepOffsetOrigin;
        Vector3 stepOffsetTarget;
        Vector3 castDirection;
        float chestHeight;
        //============== CHECK IF VALID MOVEMENT
        // Cast ray from max step height to new position
        Vector3 offset = (targetPosition - transform.position).normalized*0.2f;
        if(FloorRaycasts(offset.x, offset.z, maxStepHeight * 2f) == Vector3.zero)
        {
            return false;
        }


        stepOffsetOrigin = new Vector3(head.position.x, transform.position.y + maxStepHeight, head.position.z);
        stepOffsetTarget = new Vector3(newPlayerPosition.x, newPlayerPosition.y + maxStepHeight, newPlayerPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;

        //Debug.DrawRay(stepOffsetOrigin, castDirection*10f, Color.red, 5f);


        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Cast new position bottom to head height
        // TODO: Will need to account for slopes
        stepOffsetOrigin = new Vector3(newPlayerPosition.x, newPlayerPosition.y + maxStepHeight, newPlayerPosition.z);
        stepOffsetTarget = new Vector3(newPlayerPosition.x, head.position.y + maxStepHeight, newPlayerPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;
        //Debug.DrawRay(stepOffsetOrigin, castDirection * 10f, Color.green, 5f);

        if (Physics.Raycast(stepOffsetOrigin, castDirection, castDirection.magnitude, -1, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Cast box from chest to midpoint of new position
        chestHeight = (head.position.y - transform.position.y) / 2;
        stepOffsetOrigin = new Vector3(head.position.x, transform.position.y + chestHeight, head.position.z);
        stepOffsetTarget = new Vector3(newPlayerPosition.x, transform.position.y + chestHeight, newPlayerPosition.z);
        castDirection = stepOffsetTarget - stepOffsetOrigin;
        //Debug.DrawRay(stepOffsetOrigin, castDirection * 10f, Color.blue, 5f);

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
        //Vector3 handCurrentPosition = rightHand.transform.localPosition;

        // Compare current position with reference position, get difference
        Vector3 handPositionDifference = handReferencePosition - handCurrentPosition;
        handPositionDifference.y = 0;

        // apply difference to xr rig
        Vector3 newRigPosition = rigReferencePosition + handPositionDifference;
        //newRigPosition = rigReferencePosition + handPositionDifference;
        return new Vector2( newRigPosition.x, newRigPosition.z);
    }
    
    float yRef;
    //private float SlopeHeight(Vector2 finalCoord)
    //{
    //    float sumOfFloorPoints = 0f;
    //    int pointsToCount = 0;
    //    RaycastHit floorHit;
    //    float averageHeight;
    //    // Get foot position
    //    footPosition = new Vector3(finalCoord.x, transform.position.y, finalCoord.y);

    //    // raycast around with floor +- step height
    //    for (int i = -1; i < 2; i++)
    //    {
    //        for (int j = 0; j < 2; j++)
    //        {
    //            //Checking from way above to see if we're actually above step height, carry on if not. This is fucking shit
    //            if (!Physics.Raycast(footPosition + new Vector3(i, maxStepHeight + 1, j), Vector3.down, 1 - maxStepHeight, -1, QueryTriggerInteraction.Ignore))
    //            {
    //                // Check height at point around player and if it hits anything (and is valid!) we add it to average
    //                if (Physics.Raycast(footPosition + new Vector3(i, maxStepHeight, j), Vector3.down, out floorHit, maxStepHeight * 2, -1, QueryTriggerInteraction.Ignore))
    //                {
    //                    sumOfFloorPoints += floorHit.point.y;
    //                    pointsToCount++;
    //                }
    //            }
    //        }
    //    }

    //    // get average height
    //    averageHeight = sumOfFloorPoints / pointsToCount;

    //    // apply that as y (smoothed)
    //    return Mathf.SmoothDamp(transform.position.y, averageHeight, ref yRef, 0.01f);
    //}

    Vector3 FindFloor()
    {
        // width of raycasts around the centre of your character
        float raycastWidth = 0.25f;
        // check floor on 5 raycasts   , get the average when not Vector3.zero  
        int floorAverage = 1;

        CombinedRaycast = FloorRaycasts(0, 0, maxStepHeight*2f);
        floorAverage += (getFloorAverage(raycastWidth, 0) + getFloorAverage(-raycastWidth, 0) + getFloorAverage(0, raycastWidth) + getFloorAverage(0, -raycastWidth));

        return CombinedRaycast / floorAverage;
    }

    // only add to average floor position if its not Vector3.zero
    int getFloorAverage(float offsetx, float offsetz)
    {

        if (FloorRaycasts(offsetx, offsetz, maxStepHeight*2f) != Vector3.zero)
        {
            CombinedRaycast += FloorRaycasts(offsetx, offsetz, 1.6f);
            return 1;
        }
        else { return 0; }
    }

    Vector3 raycastFloorPos;

    Vector3 CombinedRaycast;
    Vector3 FloorRaycasts(float offsetx, float offsetz, float raycastLength)
    {
        RaycastHit hit;
        // move raycast
        raycastFloorPos = transform.TransformPoint(0 + offsetx, 0 + maxStepHeight, 0 + offsetz);

        Debug.DrawRay(raycastFloorPos, Vector3.down, Color.magenta);
        if (Physics.Raycast(raycastFloorPos, -Vector3.up, out hit, raycastLength))
        {
            Vector3 floorNormal = hit.normal;

            if (Vector3.Angle(floorNormal, Vector3.up) < slopeLimit)
            {
                return hit.point;
            }
            else return Vector3.zero;
        }
        else return Vector3.zero;
    }
}
