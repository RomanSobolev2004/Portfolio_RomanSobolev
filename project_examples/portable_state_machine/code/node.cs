[Serializable]
public abstract class PortableState : IState
{
    [SerializeField] protected string stateName;
    [SerializeField] protected Color nodeColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    [SerializeField, HideInInspector] public Rect rect;
    [SerializeField, HideInInspector] private Vector2 _localPos;

    [SerializeField, HideInInspector] private string _guidID;
    [SerializeField, HideInInspector] private bool showNodeParameters = false;

    private bool _isDragged;

    [SerializeField, HideInInspector] protected StateMachinePortable _thisStateMachine;

    public void ApplyNode(Rect rect, string title, StateMachinePortable thisMachine)
    {
        _guidID = Guid.NewGuid().ToString();
        stateName = title;

        _thisStateMachine = thisMachine;

        this.rect = rect;
        _localPos = rect.position;
    }

    public void ChangeLocalPosition(Vector2 delta)
    {
        _localPos += delta;
    }
    public void ApplyGridOffcet(Vector2 offcet)
    {
        rect.position = _localPos + offcet;
    }
}