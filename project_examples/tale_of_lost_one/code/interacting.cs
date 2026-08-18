[Serializable]
public class InteractingSystem
{
    [SerializeField] private bool canInteracting = true;
    [AllowNesting, EnableIf("canInteracting"), SerializeField] private Transform raySubject;
    [AllowNesting, EnableIf("canInteracting"), SerializeField] private float interactingDistance;
    [AllowNesting, EnableIf("canInteracting"), SerializeField] private LayerMask interactingMask;
    [AllowNesting, EnableIf("canInteracting"), SerializeField] private LayerMask obstacleMask;
    [AllowNesting, EnableIf("canInteracting"), SerializeField] private TextMeshProUGUI cursorText;
    [AllowNesting, EnableIf("canInteracting"), SerializeField] private bool autoClearCursorText = true;

    [Space]
    [SerializeField] private GameObject interacting_crosshair;

    [Space]
    [SerializeField] private bool drawGizmos;

    public event Action<InteractableObject, float> OnObjectAim;
    public event Action<InteractableObject> OnObjectAway;

    private Transform _aimingHit;
    private InteractableObject _aimingObject;
    private IDisposable _aimingUpdate;

    private HidingController m_hidingController;
    private ItemTakerDropper m_itemTakerDropper;
    private IKeyInputSystem m_inputSystem;

    public bool CanInteracting
    {
        get => canInteracting;
        set
        {
            canInteracting = value;
            if (!canInteracting) ResetAimingObect(true);
        }
    }
    public bool AutoClearCursorText { get => autoClearCursorText; set => autoClearCursorText = value; }
    public TextMeshProUGUI CursorText => cursorText;


    public void BindInteractincSystem(HidingController hidingCon, ItemTakerDropper itemTakerDropper, IKeyInputSystem inputSystem)
    {
        m_itemTakerDropper = itemTakerDropper;
        m_hidingController = hidingCon;
        m_inputSystem = inputSystem;
    }

    public void InteractingUpdate()
    {
        if (!CanInteracting) return;

        if (Physics.Raycast(raySubject.position, raySubject.forward, out var hit, interactingDistance, interactingMask + obstacleMask))
        {
            if (_aimingHit != hit.transform)
            {
                OnObjectAway?.Invoke(_aimingObject);
                _aimingObject?.OnCursorAway(this);

                _aimingHit = hit.transform;

                if (_aimingHit.TryGetComponent<InteractableObject>(out var component) && !obstacleMask.Contains(hit.transform.gameObject.layer))
                {
                    cursorText.text = string.Empty;

                    _aimingObject = component;
                    OnObjectAim?.Invoke(_aimingObject, Vector3.Distance(_aimingHit.position, raySubject.position));
                    _aimingObject.OnCursorAiming(this);
                    SetCursortextFromObject(_aimingObject);
                    if (_aimingObject.CanInteract()) interacting_crosshair.SetActive(true);

                    _aimingUpdate?.Dispose();
                    _aimingUpdate = Observable.EveryUpdate().Subscribe(_ => AimingUpdate()).AddTo(raySubject);
                }
                else
                {
                    ResetAimingObect(true);
                }
            }
        }
        else
        {
            ResetAimingObect(true);
        }
    }

    public void ResetAimingObect(bool callObjectNonAim = true)
    {
        if (callObjectNonAim)
        {
            _aimingObject?.OnCursorAway(this);
            OnObjectAway?.Invoke(_aimingObject);
        }

        _aimingHit = null;
        _aimingObject = null;
        if (autoClearCursorText) cursorText.text = string.Empty;
        interacting_crosshair.SetActive(false);
        _aimingUpdate?.Dispose();
    }

    public void UpdateCursorAimingText()
    {
        if (!_aimingObject) return;

        SetCursortextFromObject(_aimingObject);
    }

    private void SetCursortextFromObject(InteractableObject obj)
    {
        if (obj == null) return;

        bool canInteract = obj.CanInteract();
        bool showName = canInteract || obj.AlwaysNameShowCursorText;
        bool showDesc = canInteract || obj.AlwaysShowCursorText;

        if (!showDesc && !showName) return;

        cursorText.text = string.Empty;


        if (showName) cursorText.text = $"{obj.ObjectName}";
        if (showDesc)
        {
            cursorText.text += ((obj.CursorInteractingText != null) ? ":" : "") + "\n";
            cursorText.text += $"{m_inputSystem.KeyOf(InputKey.Intercat)} - {CurorActionTextFromObjectType(obj)}";
        }
    }
    private void AimingUpdate()
    {
        if (_aimingObject == null || !_aimingObject.CanInteract()) return;

        if (Input.GetKeyDown(m_inputSystem.KeyOf(InputKey.Intercat)))
        {
            _aimingObject.Interact(this);
            ObjectTypeExtraAction(_aimingObject);
            if (_aimingObject && _aimingObject.ResetCursorTextAfterInteract) UpdateCursorAimingText();
            if (_aimingObject && _aimingObject.CallNonAimingAfterInteract) ResetAimingObect(true);
        }
    }

    public void DrawGizmos()
    {
        if (!drawGizmos) return;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(raySubject.position, interactingDistance);
        Gizmos.DrawRay(raySubject.position, raySubject.forward * interactingDistance);
    }

    private void ObjectTypeExtraAction(InteractableObject obj)
    {
        if (obj is HidingObject)
        {
            m_hidingController.HideOrEscape();
        }
        else if (obj is ITakeItem)
        {
            m_itemTakerDropper.TryTakeItem(obj);
        }
    }
    private string CurorActionTextFromObjectType(InteractableObject obj)
    {
        string text = obj.CursorInteractingText;

        if (obj is HidingObject)
        {
            text = m_hidingController.GetHideTextForCursor();
        }
        else if (obj is ITakeItem)
        {
            text = m_itemTakerDropper.TakeCursortext;
        }

        return text;
    }
}