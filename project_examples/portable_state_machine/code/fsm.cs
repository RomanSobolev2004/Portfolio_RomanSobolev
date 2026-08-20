public class StateMachinePortable : OptimizedMonoBehaviour
{
    [SerializeField] private bool autoStart = true;
    [SerializeField, OnValueChanged("UpdateBoolParams")] private bool isActive = true;
    [SerializeField, OnValueChanged("UpdateBoolParams")] private bool updating = true;
    [SerializeField, OnValueChanged("UpdateBoolParams")] private bool checkStateTransitions = true;
    [SerializeField, OnValueChanged("UpdateBoolParams")] private bool checkAnyStateTransitions = true;

    [SerializeReference, HideInInspector] private List<PortableState> _nodes = new();
    [SerializeReference, HideInInspector] private List<PortableTransition> _connections = new();
    [SerializeReference, HideInInspector] private bool isNew = true;

    [HideInInspector] private StateMachine _stateMachine;

    protected override void Awake()
    {
        base.Awake();

        _stateMachine = new StateMachine();
        _stateMachine.InitStateMachine();

        FillStateMachine();
    }

    public virtual void StartStateMachine()
    {
        if (!isActive) return;

        _stateMachine.EntryStateMachine(typeof(EntryPoint));
    }


    private void FillStateMachine()
    {
        List<StateTransition> stateTransitions = new();

        foreach (var node in _nodes)
        {
            stateTransitions.Clear();

            foreach (var connection in _connections)
            {
                if (connection.FromNodeID == node.NodeGuidID)
                {
                    StateTransition transition = new StateTransition(StateFromGuidID(connection.ToNodeID).GetType(), connection.IsBindingsSatisfied);
                    stateTransitions.Add(transition);
                }
            }

            if (node.GetType() == typeof(AnyState))
            {
                _stateMachine.AddStateTransitions(null, stateTransitions.ToArray());
            }
            else
            {
                _stateMachine.AddState(node, stateTransitions.ToArray());

            }
        }
    }
}