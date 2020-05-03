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
                //Checking from way above to see if we're actually above step height, carry on if not
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
