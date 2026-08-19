[Serializable]
public class BaseNode : Node
{
    public virtual void Execute()
    {

    }

    public override object GetValue(NodePort port)
    {
        string fieldName = port.fieldName;

        FieldInfo field = GetType().GetField(fieldName,
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (field != null)
        {
            if (port.IsOutput) return field.GetValue(this);
            else if (port.IsInput) return GetInputValue(fieldName, field.GetValue(this));
        }

        return base.GetValue(port);
    }
}

public class InputOutputNode : BaseNode
{
    [Input, SerializeField] private int entry;
    [Output, SerializeField] private int exit;
}

public abstract class StoryActionNode : InputOutputNode, IStoryActionNode
{
    [field: SerializeField] public StoryActionCallTypes CallType { get; private set; }
    [field: SerializeField] public StoryLogSaveMethod SaveToLog { get; private set; }

    public abstract StoryActionTypes NodeType { get; }
}