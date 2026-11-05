using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Net.Sockets;

public class AppManager : MonoBehaviour
{
    public static AppManager Instance { get; private set; }

    [Header("Network Configuration")]
    public string RobotIP   = "192.168.0.11";
    public int    RobotPort = 10000;

    [Header("UI References")]
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TMP_InputField portInputField;
    [SerializeField] private Button connectButton;
    [SerializeField] private TextMeshProUGUI connectionStatusLabel;

    [Header("Panels")]
    [SerializeField] private GameObject rosConnectionPanel;

    private const float CONNECTION_TIMEOUT = 5f;
    private bool _isConnecting = false;

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (ipInputField       != null) ipInputField.text   = RobotIP;
        if (portInputField     != null) portInputField.text = RobotPort.ToString();
        if (connectButton      != null) connectButton.onClick.AddListener(OnConnectButtonPressed);
        if (rosConnectionPanel != null) rosConnectionPanel.SetActive(true);

        SetStatusLabel("Enter ROS IP and port, then press Connect.", Color.white);
    }

    // =========================================================================
    // ROS Connection
    // =========================================================================

    public void OnConnectButtonPressed()
    {
        if (_isConnecting) return;

        string ip   = ipInputField   != null ? ipInputField.text.Trim()  : RobotIP;
        string port = portInputField != null ? portInputField.text.Trim() : RobotPort.ToString();

        if (string.IsNullOrEmpty(ip))
        {
            SetStatusLabel("Please enter a valid IP address.", Color.red); return;
        }
        if (!int.TryParse(port, out int portNum) || portNum < 1 || portNum > 65535)
        {
            SetStatusLabel("Please enter a valid port (1-65535).", Color.red); return;
        }

        RobotIP   = ip;
        RobotPort = portNum;

        if (connectButton != null) connectButton.interactable = false;
        SetStatusLabel("Connecting...", Color.yellow);
        _isConnecting = true;

        Debug.Log($"[AppManager] Attempting ROS connection → {RobotIP}:{RobotPort}");
        StartCoroutine(AttemptROSConnection(RobotIP, RobotPort));
    }

    private IEnumerator AttemptROSConnection(string ip, int port)
    {
        bool connected = false;
        string errorMsg = "";
        bool finished = false;

        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var result   = client.BeginConnect(ip, port, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(
                        System.TimeSpan.FromSeconds(CONNECTION_TIMEOUT));

                    if (success && client.Connected)
                    {
                        client.EndConnect(result);
                        connected = true;
                    }
                    else
                    {
                        errorMsg = $"Timed out after {CONNECTION_TIMEOUT}s. Is ros_tcp_endpoint running?";
                    }
                }
            }
            catch (System.Exception ex) { errorMsg = ex.Message; }
            finally { finished = true; }
        });

        thread.IsBackground = true;
        thread.Start();
        while (!finished) yield return null;

        _isConnecting = false;

        if (connected)
        {
            SetStatusLabel("Connected! Loading...", Color.green);
            Debug.Log("[AppManager] ROS reachable. Loading next scene.");
            yield return new WaitForSeconds(0.6f);

            if (rosConnectionPanel != null) rosConnectionPanel.SetActive(false);

            int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
            AsyncOperation op = SceneManager.LoadSceneAsync(nextIndex);
            while (op != null && !op.isDone) yield return null;
        }
        else
        {
            SetStatusLabel($"Failed: {errorMsg}", Color.red);
            Debug.LogWarning($"[AppManager] Connection failed → {errorMsg}");
            if (connectButton != null) connectButton.interactable = true;
        }
    }

    private void SetStatusLabel(string msg, Color color)
    {
        if (connectionStatusLabel == null) return;
        connectionStatusLabel.text  = msg;
        connectionStatusLabel.color = color;
    }
}