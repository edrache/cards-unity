using UnityEngine;
using Rewired;
using Unity.Cinemachine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Characters")]
    [SerializeField] GameObject policjant;
    [SerializeField] GameObject zlodziej;

    [Header("Settings")]
    [SerializeField] float catchDistance = 0.8f;

    [Header("Rewired Actions")]
    [SerializeField] string actionPlayAsPolicjant = "PlayAsPolicjant";
    [SerializeField] string actionPlayAsZlodziej = "PlayAsZlodziej";
    [SerializeField] string actionSimulation = "Simulation";
    [SerializeField] int playerId = 0;

    [Header("Spawn Points")]
    [SerializeField] Transform policjantSpawn;
    [SerializeField] Transform zlodziejSpawn;

    [Header("Caught Popup")]
    [SerializeField] GameObject caughtPopup;

    [Header("Camera Target Group")]
    [SerializeField] CinemachineTargetGroup targetGroup;
    [SerializeField] float weightPlayer = 1f;
    [SerializeField] float weightOpponent = 0.3f;

    Player _player;
    bool _gameStarted;
    bool _caught;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        _player = ReInput.players.GetPlayer(playerId);
        SetComponentsActive(false);
        if (caughtPopup != null) caughtPopup.SetActive(true);
    }

    void Update()
    {
        if (!_gameStarted)
        {
            if (_player.GetButtonDown(actionPlayAsPolicjant)) ChoosePolicjant(true);
            else if (_player.GetButtonDown(actionPlayAsZlodziej)) ChooseZlodziej(true);
            else if (_player.GetButtonDown(actionSimulation)) StartSimulation(true);
            return;
        }

        if (_caught) return;

        if (Vector3.Distance(policjant.transform.position, zlodziej.transform.position) <= catchDistance)
            TriggerCaught();
    }

    // Called by Toggle.onValueChanged — only act when toggle is turned ON
    public void ChoosePolicjant(bool isOn)
    {
        if (!isOn) return;
        ResetGame();
        policjant.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        policjant.GetComponent<CharacterMover>().enabled = true;
        zlodziej.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = true;
        zlodziej.GetComponent<ZlodziejFlee>().enabled = true;
        SetCameraWeights(policjantWeight: weightPlayer, zlodziejWeight: weightOpponent);
        _gameStarted = true;
    }

    public void ChooseZlodziej(bool isOn)
    {
        if (!isOn) return;
        ResetGame();
        zlodziej.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        zlodziej.GetComponent<CharacterMover>().enabled = true;
        policjant.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = true;
        policjant.GetComponent<PolicjantChase>().enabled = true;
        SetCameraWeights(policjantWeight: weightOpponent, zlodziejWeight: weightPlayer);
        _gameStarted = true;
    }

    public void StartSimulation(bool isOn)
    {
        if (!isOn) return;
        ResetGame();
        policjant.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = true;
        policjant.GetComponent<PolicjantChase>().enabled = true;
        zlodziej.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = true;
        zlodziej.GetComponent<ZlodziejFlee>().enabled = true;
        SetCameraWeights(policjantWeight: 1f, zlodziejWeight: 1f);
        _gameStarted = true;
    }

    // Button-friendly overloads (no bool parameter)
    public void ChoosePolicjant() => ChoosePolicjant(true);
    public void ChooseZlodziej() => ChooseZlodziej(true);
    public void StartSimulation() => StartSimulation(true);

    void SetCameraWeights(float policjantWeight, float zlodziejWeight)
    {
        if (targetGroup == null) return;

        for (int i = 0; i < targetGroup.Targets.Count; i++)
        {
            var t = targetGroup.Targets[i];
            if (t.Object == policjant.transform) t.Weight = policjantWeight;
            else if (t.Object == zlodziej.transform) t.Weight = zlodziejWeight;
            targetGroup.Targets[i] = t;
        }
    }

    void ResetGame()
    {
        _caught = false;
        _gameStarted = false;

        if (caughtPopup != null) caughtPopup.SetActive(false);

        // Disable agents before teleporting to avoid NavMesh errors
        policjant.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;
        zlodziej.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled = false;

        if (policjantSpawn != null) policjant.transform.position = policjantSpawn.position;
        if (zlodziejSpawn != null) zlodziej.transform.position = zlodziejSpawn.position;

        zlodziej.GetComponent<ZlodziejFlee>().OnUncaught();
        policjant.GetComponent<PolicjantChase>().OnUncaught();

        SetComponentsActive(false);
    }

    void TriggerCaught()
    {
        _caught = true;

        ZlodziejFlee flee = zlodziej.GetComponent<ZlodziejFlee>();
        if (flee.enabled) flee.OnCaught();

        PolicjantChase chase = policjant.GetComponent<PolicjantChase>();
        if (chase.enabled) chase.OnCaught();

        if (caughtPopup != null) caughtPopup.SetActive(true);
    }

    void SetComponentsActive(bool active)
    {
        policjant.GetComponent<CharacterMover>().enabled = active;
        policjant.GetComponent<PolicjantChase>().enabled = active;
        zlodziej.GetComponent<CharacterMover>().enabled = active;
        zlodziej.GetComponent<ZlodziejFlee>().enabled = active;
    }
}
