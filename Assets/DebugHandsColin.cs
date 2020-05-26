using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugHandsColin : MonoBehaviour
{
    public Transform leftHand;
    public Transform rightHand;
    public Transform head;
    Camera camera;
    Vector3 defaultHeadPosition;
    // Start is called before the first frame update
    void Start()
    {
        camera = GetComponent<Camera>();
        defaultHeadPosition = head.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 mousePosition = camera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 10f));
        if(Input.GetKey(KeyCode.LeftShift))
            leftHand.position = new Vector3(mousePosition.x, leftHand.position.y, mousePosition.z);

        if (Input.GetKey(KeyCode.LeftControl))
            rightHand.position = new Vector3(mousePosition.x, rightHand.position.y, mousePosition.z);

        if (Input.GetKey(KeyCode.LeftAlt))
        {
           head.position = new Vector3(mousePosition.x, head.position.y, mousePosition.z);
        }
        else
        {
            head.localPosition = defaultHeadPosition;
        }
    }
}
