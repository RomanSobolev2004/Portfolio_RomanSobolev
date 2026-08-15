public interface IStateMachine
{
    public IReadOnlyDictionary<int, IState> States { get; }
    public IState State { get; }
    public int Index { get; }
    public void EnterInWithArgument(int index, object args);
    public void EnterIn(int index);
    public void ExitOutCurrent();
}

public interface ITurnSM : IStateMachine
{
    public void Instance(Player_EnemyEnum[] stateForEnums, GameObject turnButton, GameObject stageButton);
    public void NextTurn();
    public void NextTurnStage();
}

public interface ITurnSystem
{
    public ITurnSM TurnStateMachine { get; }
}

public class TurnSystem : MonoBehaviour, ITurnSystem
{
    [SerializeField] private Player_EnemyEnum[] statesFor;

    [SerializeField] private MenueButton nextTurnButton;
    [SerializeField] private MenueButton nextStageButton;

    private ITurnSM _turnSM;

    [Inject]
    private void Construct(ITurnSM turnSM)
    {
        _turnSM = turnSM;

        _turnSM.Instance(statesFor, nextTurnButton.gameObject, nextStageButton.gameObject);

        BindButtons();
    }

    public ITurnSM TurnStateMachine => _turnSM;

    private void BindButtons()
    {
        nextTurnButton.OnUp(_turnSM.NextTurn);

        nextStageButton.OnUp(_turnSM.NextTurnStage);
    }
}