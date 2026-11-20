using UnityEngine;

public class Clamping : MonoBehaviour
{
    [Header("Riferimenti")]
    [SerializeField] private Light sceneLight;
    [SerializeField] private bool canGrabObject = true;
    [Tooltip("Lascia vuoto per afferrare automaticamente qualsiasi oggetto toccato dalla pinza.")]
    public GameObject childObject;

    [Header("Impostazioni Movimento URDF")]
    [SerializeField] private float speed = 0.05f;
    [SerializeField] private bool invertDirection = false;

    [Header("Parametri Motore")]
    [SerializeField] private float motorStiffness = 10000f;
    [SerializeField] private float motorDamping = 100f;
    [SerializeField] private float motorForceLimit = 1000f;

    private enum GripperState { Closing, Opening }
    private GripperState state = GripperState.Opening;

    private ArticulationBody ab;
    private float currentTargetPos;
    private bool isMoving = true;
    private Transform robotRoot;
    private GameObject grabbedObject;

    public bool IsMoving => isMoving;

    private void Awake()
    {
        ab = GetComponent<ArticulationBody>();
        robotRoot = transform.root;
    }

    private void Start()
    {
        if (ab != null)
        {
            currentTargetPos = ab.xDrive.target;
        }
    }

    public void SetLightEnabled(bool value)
    {
        if (sceneLight != null)
            sceneLight.enabled = value;
    }

    public void CloseGripper()
    {
        state = GripperState.Closing;
        isMoving = true;
    }

    public void OpenGripper()
    {
        GameObject objToRelease = grabbedObject != null ? grabbedObject : childObject;

        if (objToRelease != null && objToRelease.transform.parent == transform)
        {
            // Rende l'oggetto completamente indipendente nella scena (nessun genitore)
            objToRelease.transform.SetParent(null, true);

            // Disattiva Is Trigger su tutti i collider dell'oggetto così resta solido sul tavolo
            Collider[] cols = objToRelease.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++)
            {
                cols[i].isTrigger = false;
            }

            Rigidbody rb = objToRelease.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }

        // Azzera i riferimenti per non interferire mai con gli oggetti già rilasciati
        grabbedObject = null;
        childObject = null;

        state = GripperState.Opening;
        isMoving = true;
    }

    private void FixedUpdate()
    {
        if (!isMoving || ab == null) return;

        if (ab.jointType == ArticulationJointType.PrismaticJoint || 
            ab.jointType == ArticulationJointType.RevoluteJoint)
        {
            float dir = (state == GripperState.Closing) ? -1f : 1f;
            if (invertDirection) dir = -dir;

            currentTargetPos += speed * dir * Time.fixedDeltaTime;

            ArticulationDrive drive = ab.xDrive;

            if (drive.lowerLimit < drive.upperLimit)
            {
                if (currentTargetPos <= drive.lowerLimit)
                {
                    currentTargetPos = drive.lowerLimit;
                    isMoving = false;
                }
                else if (currentTargetPos >= drive.upperLimit)
                {
                    currentTargetPos = drive.upperLimit;
                    isMoving = false;
                }
            }

            drive.target = currentTargetPos;

            if (drive.stiffness <= 0f) drive.stiffness = motorStiffness;
            if (drive.damping <= 0f) drive.damping = motorDamping;
            if (drive.forceLimit <= 0f) drive.forceLimit = motorForceLimit;

            ab.xDrive = drive;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleContact(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleContact(other.gameObject);
    }

    private void HandleContact(GameObject hitObject)
    {
        if (!isMoving || state != GripperState.Closing) return;
        if (hitObject.transform.root == robotRoot) return;

        isMoving = false;

        if (!canGrabObject || grabbedObject != null) return;

        // Risale all'oggetto con Rigidbody se il collider colpito è su un figlio
        Rigidbody targetRb = hitObject.GetComponentInParent<Rigidbody>();
        grabbedObject = targetRb != null ? targetRb.gameObject : hitObject;

        // Se l'oggetto è già stato afferrato dall'altra ganascia, non toccarlo
        if (grabbedObject.transform.parent != null && grabbedObject.transform.parent.root == robotRoot)
            return;

        Collider[] cols = grabbedObject.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++)
        {
            cols[i].isTrigger = true;
        }

        if (targetRb != null)
        {
            targetRb.isKinematic = true;
            targetRb.useGravity = false;
        }

        grabbedObject.transform.SetParent(transform, true);
    }
}