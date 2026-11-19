using System.Collections;
using UnityEngine;

public class TestMove : MonoBehaviour
{
    public RobotProgram program;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(2f);
        Debug.Log("TestMove: lancio MoveJ");
        program.MoveJToLiveTarget();
    }
}
