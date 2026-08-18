public partial class EnemySystem : OptimizedMonoBehaviour, ISaveable
{
    [SerializeField] protected float spawnTick;
    [field: SerializeField] public bool EnableSave { get; protected set; }
    [field: SerializeField] public string SaveID { get; protected set; }

    protected Dictionary<Type, Enemy> _enemies = new();
    protected List<Type> _spawnedEnemies = new();

    protected IDisposable _update;
    protected float _tickTimer;

    public event Action OnEnemySpawn;
    public event Action OnEnemyDespawn;

    protected override void Construct(IZenjectWrapped zenject)
    {
        base.Construct(zenject);

        SaveLoadRegistry.Register(this);
    }

    protected override void Start()
    {
        ActiveSystem();
    }

    public virtual void ActiveSystem()
    {
        _update = Observable.EveryUpdate().Subscribe(_ => SystemUpdate()).AddTo(this);
    }
    public virtual void DeaciveSystem()
    {
        _update?.Dispose();
        foreach (var enemy in _enemies.Values)
        {
            enemy.enemyStateMashine.EnterIn(typeof(EnemyActiveState), false, new object[0]);
            enemy.enemyStateMashine.ExitStateMachine();
        }
    }

    public virtual void CreateEnemy(Type enemyType, EnemySpawnConditions conditions, IStateMachine enemy)
    {
        _enemies.Add(enemyType, new(enemy, conditions));
    }
    public virtual void RemoveEnemy(Type enemyType)
    {
        if (_spawnedEnemies.Contains(enemyType))
        {
            _enemies[enemyType].enemyStateMashine.EnterIn(typeof(EnemyActiveState), false, new object[0]);
            _enemies[enemyType].enemyStateMashine.ExitStateMachine();
            _spawnedEnemies.Remove(enemyType);
        }

        _enemies.Remove(enemyType);
    }
    public virtual IEnemy GetEnemy(Type enemyType)
    {
        if (_enemies.TryGetValue(enemyType, out Enemy enemy)) return enemy;
        return null;
    }
    protected virtual void SystemUpdate()
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer <= 0)
        {
            TryActiveEnemies();
            _tickTimer = spawnTick;
        }

        TryDeactivateEnemies();
    }
}


public class EnemyBootstrapper<SpawnConditions> : OptimizedMonoBehaviour, ISaveable where SpawnConditions : EnemySpawnConditions
{
    [SerializeField] protected SpawnConditions spawnConditions;
    [SerializeField] protected OptimizedMonoBehaviour enemyObject;

    [field: SerializeField] public bool EnableSave { get; protected set; }
    [field: SerializeField] public string SaveID { get; protected set; }

    protected EnemySystem _enemySystem;
    protected IEditStateMachine _enemyStateMachine;


    protected override void Construct(IZenjectWrapped zenject)
    {
        base.Construct(zenject);

        SaveLoadRegistry.Register(this);

        _enemySystem = zenject.Unwrapp<EnemySystem>();

        BindEnemyStateMachine(zenject);

    }

    protected override void Awake()
    {
        base.Awake();

        if (enabled)
        {
            _enemySystem.CreateEnemy(GetType(), spawnConditions, _enemyStateMachine);
        }
    }
    protected override void Start()
    {
        base.Start();

        if (enabled)
        {
            spawnConditions.StartCondotion();
        }
    }

    protected virtual void BindStates(IZenjectWrapped zenject) { }
    protected virtual void BindEnemyStateMachine(IZenjectWrapped zenject)
    {
        _enemyStateMachine = new StateMachine();
        _enemyStateMachine.InitStateMachine();
        _enemyStateMachine.CanEnterTheSameState = true;

        BindStates(zenject);
    }

    public virtual ExpData PackData()
    {
        var data = new EnemyBootstrapperExpData();

        WriteData(data);

        return data;
    }
    protected virtual void WriteData(EnemyBootstrapperExpData data)
    {
        data.CurrentStateSaveID = (_enemyStateMachine.CurrentState as EnemyState)?.SaveID;
        data.EnemyObjectExpData = new(enemyObject);
    }

    public virtual void LoadData(ExpData data)
    {
        var enemyBootData = data as EnemyBootstrapperExpData;

        if (enemyBootData.CurrentStateSaveID != null)
        {
            _enemyStateMachine.CurrentState = SaveLoadRegistry.GetRegistred<EnemyState>(enemyBootData.CurrentStateSaveID);
        }
        enemyBootData.EnemyObjectExpData.WriteToExp(enemyObject);
    }
}