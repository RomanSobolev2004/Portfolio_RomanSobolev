public class StateMachineGraphEditorWindow : EditorWindow
{
    private StateMachinePortable _sourceSM;
    private Vector2 _gridOffcet;

    [NonSerialized] private PortableState _selectedNode;
    [NonSerialized] private PortableTransition _selectedConnection;

    [NonSerialized] private PortableState selectedOutNode = null;
    private bool _isCreatingConnection = false;
    private bool _statesTransitionsMenueOpen = false;

    private SerializedObject _serializedSMportable;
    private SerializedProperty _nodesProperty;
    private SerializedProperty _connectionsProperty;

    private SerializedProperty _sm_autoStart;
    private SerializedProperty _sm_active;
    private SerializedProperty _sm_updating;
    private SerializedProperty _sm_checkTransitions;
    private SerializedProperty _sm_checkAnyStateTransitions;

    Rect _sideMenueRect = new(0, 0, 200, -1);
    Rect _statesTransitionsMenueRect = new(-1, 0, 300, -1);

    private Vector2 WindowCenter => new Vector2(position.width / 2, position.height / 2);

    private PortableState SelectedState
    {
        get => _selectedNode;
        set
        {
            _selectedNode = value;
            GUI.FocusControl(null);
        }
    }

    private void OnEnable()
    {
        _selectedConnection = null;
        _selectedNode = null;
        _gridOffcet = Vector2.zero;

        if (_sourceSM == null) return;

        _serializedSMportable = new SerializedObject(_sourceSM);

        _sm_autoStart = _serializedSMportable.FindProperty("autoStart");
        _sm_active = _serializedSMportable.FindProperty("isActive");
        _sm_updating = _serializedSMportable.FindProperty("updating");
        _sm_checkTransitions = _serializedSMportable.FindProperty("checkStateTransitions");
        _sm_checkAnyStateTransitions = _serializedSMportable.FindProperty("checkAnyStateTransitions");

        if (_serializedSMportable.FindProperty("isNew").boolValue)
        {
            _serializedSMportable.FindProperty("isNew").boolValue = false;
            _serializedSMportable.ApplyModifiedProperties();

            AddNode(typeof(EntryPoint), WindowCenter - new Vector2(400, 0));
            AddNode(typeof(AnyState), WindowCenter);
            AddNode(typeof(ExitPoint), WindowCenter + new Vector2(400, 0));

            EditorUtility.SetDirty(_sourceSM);
            _serializedSMportable.ApplyModifiedProperties();
        }
    }

    public static void ShowWindow(StateMachinePortable target)
    {
        var window = GetWindow<StateMachineGraphEditorWindow>();
        window._sourceSM = target;
        window.titleContent = new GUIContent("State Machine Graph");

        window.OnEnable();
    }

    private void OnGUI()
    {
        DrawGrid(20, 0.2f, Color.gray);
        DrawGrid(100, 0.4f, Color.gray);

        if (_sourceSM == null) return;

        SetPropsAndRects();

        DrawConnections();
        DrawNodes();

        DrawSideMenu();
        DrawStateAndTransitionMenue();

        ProcessEvents(Event.current);
    }

    private void SetPropsAndRects()
    {
        _serializedSMportable.Update();
        _nodesProperty = _serializedSMportable.FindProperty("_nodes");
        _connectionsProperty = _serializedSMportable.FindProperty("_connections");

        _sideMenueRect.height = position.height;

        _statesTransitionsMenueRect.height = position.height;
        _statesTransitionsMenueRect.x = position.width - 300;
    }

    //...

    void DrawNode(SerializedProperty node)
    {
        var state = node.managedReferenceValue as PortableState;
        state.ApplyGridOffcet(_gridOffcet);

        bool isSelected = _selectedNode == state;

        var serializedObject = node.serializedObject;

        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float titleHeight = EditorGUIUtility.singleLineHeight + spacing;
        float totalHeight = titleHeight;

        GUILayout.BeginArea(state.rect, GetStyle(state, isSelected));

        GUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(state.NodeTitle, EditorStyles.boldLabel);

        if (state.ShowNodeParametersMenue)
        {
            SerializedProperty showProp = node.FindPropertyRelative("showNodeParameters");
            if (showProp.boolValue)
            {
                if (GUILayout.Button("_")) showProp.boolValue = !showProp.boolValue;
                GUILayout.EndHorizontal();

                SerializedProperty property = node;
                SerializedProperty iterator = property.Copy();

                SerializedProperty end = iterator.GetEndProperty();
                if (iterator.Next(true))
                {
                    while (iterator.NextVisible(false) && !SerializedProperty.EqualContents(iterator, end))
                    {
                        totalHeight += EditorGUI.GetPropertyHeight(iterator) + spacing;
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("□")) showProp.boolValue = !showProp.boolValue;
                GUILayout.EndHorizontal();
            }
        }
        else GUILayout.EndHorizontal();

        state.rect.height = totalHeight + titleHeight + spacing;

        serializedObject.ApplyModifiedProperties();
        GUILayout.EndArea();
    }

    //...
}