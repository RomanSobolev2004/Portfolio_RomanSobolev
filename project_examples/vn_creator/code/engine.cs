//ENGINE
public class StoryLineEngine : IStoryLineEngine
{
    //Tiled action logistics
    protected virtual void ActionDictionary(DiContainer container)
    {
        _actions = new()
    {
        { StoryActionTypes.None, container.Instantiate<NoneStoryAction>() },
        { StoryActionTypes.Dialog, container.Instantiate<DialogAction>() },
        { StoryActionTypes.Background, container.Instantiate<BackgroundAction>() },
        { StoryActionTypes.Delay, container.Instantiate<DelayAction>() },
        { StoryActionTypes.CharatcerPosition, container.Instantiate<CharacterPositionAction>() },
        { StoryActionTypes.ChatacterPose, container.Instantiate<CharacterPoseAction>() },
        { StoryActionTypes.ChatacterFace, container.Instantiate<CharacterFaceAction>() },
        { StoryActionTypes.Choise, container.Instantiate<ChoiceAction>(new object[]
            {
                (Action)(() => PlayerTapContinue(true, true)),
                (Action)(() => {SkipMode(false); AutoMode(false); })
            })
        },
        { StoryActionTypes.GoToGraph, container.Instantiate<GoToGraphAction>(new object[] {(Action<StoryLineGraph, bool>)ChangeGraph})},
        { StoryActionTypes.SetVariables, container.Instantiate<VariableAction>()},
        { StoryActionTypes.VariablesCondition, container.Instantiate<VariableConditionAction>()},
        { StoryActionTypes.InputMenu, container.Instantiate<InputFieldAction>() },
    };
    }


    protected virtual void Binds()
    {
        _textMenue?.DialogButtons[DialogButtons.Back].AddListener(BackLog);

        _textMenue?.DialogToggles[DialogButtons.Auto].AddListener((bool on) => AutoMode(on));
        _textMenue?.DialogToggles[DialogButtons.Skip].AddListener((bool on) => SkipMode(on));

        _tapTrigger?.AddListener(() => PlayerTapContinue());
    }

    //==========================


    //Engine body
    protected virtual IEnumerator StoryEnumerator()
    {
        IStoryActionNode node = null;

        while (true)
        {
            yield return new WaitForEndOfFrame();

            yield return TryFinishStep(false);

            if (_skipStep)
            {
                _skipStep = false;
                continue;
            }

            if (_skipGoNextNode) _skipGoNextNode = false;
            else currentGraph.GoToNextNode(NextStoryNodePortFromAction);

            node = currentGraph.CurrentStoryNode;
            if (node is null) break;

            IStoryAction action = _actions[node.NodeType];

            action?.StartAction(node);
        }
    }

    protected virtual IEnumerator TryFinishStep(bool forced = false)
    {
        IStoryActionNode nextNode = currentGraph.GetNextNode(NextStoryNodePortFromAction) as IStoryActionNode;

        bool goThrough = forced || _skipMode || nextNode?.CallType == StoryActionCallTypes.With_Previous;

        if (!goThrough)
        {
            yield return new WaitUntil(() => CanContinueStep(nextNode));
        }

        float waitTime = 0.0001f;

        if (_skipMode) waitTime = 0.1f;
        else if (_autoMode && nextNode?.CallType != StoryActionCallTypes.With_Previous) waitTime = 3;

        yield return new WaitForSeconds(waitTime);


        if (waitTime == 3 && !_autoMode) SkipStep();
        else
        {
            if (_skipMode) StopAllActions(false);
            SaveLog();

            _forcedContinue = false;
            _playerContinue = false;
        }
    }

    protected virtual bool CanContinueStep(IStoryActionNode nextNode)
    {
        return _forcedContinue || _actions[currentGraph.CurrentStoryNode.NodeType].CanContinue && (_skipMode || _playerContinue || ((nextNode?.CallType == StoryActionCallTypes.After_Previous || _autoMode) && IsAllFinished()));
    }
}



//ACTION
public class StoryAction<NodeType> : IStoryAction where NodeType : class, IStoryActionNode
{
    protected NodeType _node;
    protected IDisposable _startedAction;

    public virtual bool Finished { get; protected set; } = true;
    public virtual bool CanContinue { get; protected set; } = true;

    public virtual string NextNodePortField { get; set; } = "exit";
    public virtual bool PlayActionOnLogLoad { get; protected set; } = false;

    public virtual void StartAction(IStoryActionNode node)
    {
        _node = node as NodeType;
    }
    protected virtual void StartUpdate()
    {
        _startedAction?.Dispose();
        _startedAction = Observable.FromCoroutine(ActionProcessing).Subscribe();
    }

    public virtual void StopAction()
    {
        _startedAction?.Dispose();
    }

    protected virtual IEnumerator ActionProcessing()
    {
        yield break;
    }
}