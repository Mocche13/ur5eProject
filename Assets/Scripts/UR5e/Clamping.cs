using UnityEngine;

public class Clamping : MonoBehaviour
{
    [SerializeField] private Light sceneLight;
    [SerializeField] private float speed = 0.05f; // Ridotto: in metri/s 0.5 è 50 cm/s (molto veloce per una pinza)

    public GameObject childObject;

    private bool isMoving;
    private ArticulationBody ab;
    private float currentTargetPos;

    private void Awake()
    {
        ab = GetComponent<ArticulationBody>();
        if (ab != null && ab.jointPosition.dofCount > 0)
            currentTargetPos = ab.jointPosition[0];
    }

    public void SetLightEnabled(bool value)
    {
        if (sceneLight != null)
            sceneLight.enabled = value;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.G)) isMoving = true;

        // Se NON è un ArticulationBody prismatico, muovi via Transform
        if (isMoving && (ab == null || ab.jointType != ArticulationJointType.PrismaticJoint))
        {
            transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
        }
    }

    private void FixedUpdate()
    {
        // Se È un giunto prismatico URDF, muovi tramite xDrive.target
        if (isMoving && ab != null && ab.jointType == ArticulationJointType.PrismaticJoint)
        {
            currentTargetPos += speed * Time.fixedDeltaTime;
            ArticulationDrive drive = ab.xDrive;
            drive.target = currentTargetPos;
            ab.xDrive = drive;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isMoving) return;
        isMoving = false;

        if (childObject != null)
        {
            // Se l'oggetto afferrato ha un Rigidbody, rendilo kinematico per evitare esplosioni fisiche
            Rigidbody rb = childObject.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            childObject.transform.SetParent(transform, true);
        }
    }
}