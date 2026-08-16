[Serializable]
public class StateMachine : IRedactableStateMachine
{
    [SerializeReference] protected Dictionary<IState, List<StateTransition>> _states; //State interface as Key
    [SerializeReference] protected List<StateTransition> _anyStateTransitions;

    [SerializeField] protected IState _currentState;

    [SerializeField] protected IDisposable _updateStateMachine;

    [SerializeField] protected bool isActive;
    [SerializeField] protected bool checkAnyStatetransitions;
    [SerializeField] protected bool checkStateTransitions;
    [SerializeField] protected bool updating;

    public virtual bool IsActive { get => isActive; set => isActive = value; }
    public virtual bool CheckAnyStateTransition { get => checkAnyStatetransitions; set => checkAnyStatetransitions = value; }
    public virtual bool CheckStateTransition { get => checkStateTransitions; set => checkStateTransitions = value; }
    public virtual bool Updatinig { get => updating; set => updating = value; }

    public Dictionary<IState, List<StateTransition>> States => _states;
    public List<StateTransition> AnyStateTransitions => _anyStateTransitions;
    public IState CurrentState { get => _currentState; set => _currentState = value; }


    //Ititialization of State Machine. Most used for additing states
    public virtual void InitStateMachine(params object[] stateParameters)
    {
        _states = new();
        _anyStateTransitions = new();

        Updatinig = true;
        CheckStateTransition = true;
        CheckAnyStateTransition = true;
    }
    public virtual void AddState(IState state, params StateTransition[] transitions)
    {
        if (_states.ContainsKey(state))
        {
            AddStateTransitions(state.GetType(), transitions);
        }
        else
        {
            _states.Add(state, transitions.ToList());
        }
    }

    /// <summary>
    /// Adds a transitions to added state. If stateType is null, so it adds the trabsitions from AnyState
    /// </summary>
    /// <param name="stateType"></param>
    /// <param name="transitions"></param>
    public virtual void AddStateTransitions(Type stateType, params StateTransition[] transitions)
    {
        if (ContainsState(stateType, out var state))
        {
            _states[state].AddRange(transitions);
        }
        else if (stateType is null)
        {
            _anyStateTransitions.AddRange(transitions);
        }
    }

    //Entering the first state, what turn an Updating On too
    public virtual void EntryStateMachine(Type startState)
    {
        IsActive = true;
        EnterIn(startState);

        _updateStateMachine = Observable.EveryUpdate().Subscribe(_ => UpdateStateMachine());
    }
    //Exit and turn od the State Machine
    public virtual void ExitStateMachine()
    {
        _updateStateMachine?.Dispose();
        ExitOutCurrent();
        IsActive = false;
    }

    //A method that checks for the presence of a condition by its type
    public virtual bool ContainsState(Type stateType, out IState outState)
    {
        outState = null;

        foreach (var state in _states.Keys)
        {
            if (state.GetType() == stateType)
            {
                outState = state;
                return true;
            }
        }

        return false;
    }
    //ћетод входа в состо€ние. »спользует тип в качестве параметра, чтобы сторонние скрипты могли вызвать метод не име€ конкретного экземпл€ра состо€ни€.
    public virtual void EnterIn(Type stateType, params object[] args)
    {
        if (!IsActive || stateType == _currentState?.GetType()) return;

        if (ContainsState(stateType, out IState outState))
        {
            ExitOutCurrent();

            _currentState = outState;

            _currentState.Enter(args);
        }
    }
    //Exiting of currnet state
    public virtual void ExitOutCurrent()
    {
        if (!IsActive) return;

        _currentState?.Exit();
        _currentState = null;
    }

    //Just update
    protected virtual void UpdateStateMachine()
    {
        if (!IsActive || !Updatinig) return;

        CheckStateTransitionsUpdate(out bool entered); if (entered) return;
        CheckAnyStateTransitionsUpdate(out bool anyEntered); if (anyEntered) return;

        _currentState?.Update();
    }

    //Checking the transition of current state
    protected virtual void CheckStateTransitionsUpdate(out bool entered)
    {
        entered = false;

        if (!CheckStateTransition || _currentState is null) return;

        if (_states.TryGetValue(_currentState, out var transitions))
        {
            foreach (var transition in transitions)
            {
                if (transition.AllConditionsFulfilled)
                {
                    EnterIn(transition.EndState, transition.EnterArguments);
                    entered = true;
                    return;
                }
            }
        }
    }
    //Checking the transition of any state
    protected virtual void CheckAnyStateTransitionsUpdate(out bool entered)
    {
        entered = false;

        if (!CheckAnyStateTransition) return;

        foreach (var transition in _anyStateTransitions)
        {
            if (transition.AllConditionsFulfilled && transition.EndState != _currentState.GetType())
            {
                EnterIn(transition.EndState, transition.EnterArguments);
                entered = true;
                return;
            }
        }
    }
}



public delegate bool SM_Condition(); //Condition bool delegate
public struct StateTransition
{
    public Type EndState { get; } //Type of state which must me entered if all conditions fulfilled
    public object[] EnterArguments { get; } //Arguments on entering of the end state 

    private SM_Condition _condition; //List of conditions

    //Constructor that becomes an state type, one condition and parametres for state
    public StateTransition(Type endStateKey, SM_Condition condition, params object[] args)
    {
        EndState = endStateKey;
        EnterArguments = args;
        _condition = default;

        AddExtraConditions(condition);
    }

    //Additing of more than one condition
    public void AddExtraConditions(params SM_Condition[] conditions)
    {
        foreach (var condition in conditions)
        {
            _condition += condition;
        }
    }

    public bool AllConditionsFulfilled
    {
        get => _condition.Invoke();
    }
}



public class CallableStateTransition : ICallableStateTransition
{
    private IPermeableStateMachine _stateMachine; //Interface of State Machine for enter
    private StateTransition _transitionToCall; //Reference from transition

    public CallableStateTransition(IPermeableStateMachine targetStateMachine, StateTransition transitionToCall)
    {
        _stateMachine = targetStateMachine;
        _transitionToCall = transitionToCall;
    }

    public bool TransitionConditionsFulfilled => _transitionToCall.AllConditionsFulfilled; //Is condition of transition fulfilled
    public void CallTransition(params object[] args) //Call of transition
    {
        _stateMachine.EnterIn(_transitionToCall.EndState, args);
    }
    public bool TryCallTransition(params object[] args) //Try to call of transition
    {
        if (TransitionConditionsFulfilled)
        {
            CallTransition(args);
            return true;
        }

        return false;
    }
}