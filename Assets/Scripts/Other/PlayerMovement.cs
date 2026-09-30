
//#if UNITY_EDITOR
//using UnityEditor.Callbacks;
//using UnityEditor.Rendering.LookDev;
//using UnityEngine;
//using UnityEngine.InputSystem;
//#endif

//public class PlayerMovement : MonoBehaviour
//{
    //[Header("Movement")]
    //[SerializeField] private float speed = 5f;
    //private Vector3 movementDirection;
    //private  Vector3 currentInput;
    //private Rigidbody Rigid;

    //[Header("Animation")]
    //[SerializeField] private Animator anim;
    //private string lastDirection; 

        // Start is called once before the first execution of Update after the MonoBehaviour is created
    //void Start()
    //{
        //Rigid = GetComponent<Rigidbody>();
    //}

    //private void FixedUpdate()
    //{
        ////Rigid.linearVelocity  = movementDirection * speed;
    //} 

    //public Vector3 GetDirection(Vector3 input)
    //{
            //Vector3 finalDirection = Vector3.zero;
            //if (input.y > 0.01f)
            //{
                //lastDirection = "Up";
                //finalDirection = new Vector3(0, 1);
            //}
            //else if (input.y < -0.01f)
            //{
                //lastDirection = "Down";
                //finalDirection = new Vector3(0, -1);
            //}
            //else if (input.x > 0.01f)
            //{
                //lastDirection = "Right";
                //finalDirection = new Vector3(1, 0);
            //}
            //else if (input.x < -0.01f)
            //{
                //lastDirection = "Left";
                //finalDirection = new Vector3(-1, 0);
            //}
            //else
                //finalDirection = Vector3.zero;
            //return finalDirection;
        //}

    //public void OnMove(InputValue value)
    //{
        //currentInput = value.Get<Vector3>().normalized;
        //movementDirection = GetDirection(currentInput);
    //}

    // Update is called once per frame
    //public void Testing()
    //{
        //Debug.Log("Test");
    //}
//}
