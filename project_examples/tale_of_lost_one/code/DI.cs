public sealed class DiContainer : IContainer, IDisposable
{
    const BindingFlags MemberFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    sealed class ParamPlan
    {
        public readonly Type[] Types;
        public readonly string[] Keys;
        public readonly string[] Names;

        public ParamPlan(ParameterInfo[] ps)
        {
            Types = new Type[ps.Length];
            Keys = new string[ps.Length];
            Names = new string[ps.Length];

            for (int i = 0; i < ps.Length; i++)
            {
                Types[i] = ps[i].ParameterType;
                Keys[i] = ps[i].GetCustomAttribute<KeyAttribute>(false)?.Key;
                Names[i] = ps[i].Name;
            }
        }
    }

    sealed class CtorPlan
    {
        public ConstructorInfo Ctor;
        public ParamPlan Params;
    }

    sealed class MethodPlan
    {
        public MethodInfo Method;
        public ParamPlan Params;
    }

    sealed class InjectInfo
    {
        public FieldInfo[] Fields;
        public string[] FieldKeys;
        public PropertyInfo[] Properties;
        public string[] PropertyKeys;
        public MethodPlan[] Methods;
    }

    sealed class ReferenceComparer : IEqualityComparer<object>
    {
        bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
        int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    static readonly Dictionary<Type, InjectInfo> InjectCache = new();
    static readonly Dictionary<Type, CtorPlan> CtorCache = new();


    readonly Dictionary<Type, ServiceEntry> _map;
    readonly List<Registration> _all;
    readonly Dictionary<Registration, object> _singletons = new();
    readonly List<IDisposable> _disposables = new();
    readonly Stack<Registration> _path = new();
    readonly HashSet<object> _injected = new HashSet<object>(new ReferenceComparer());

    GameObject _holder;

    internal DiContainer(Dictionary<Type, ServiceEntry> map, List<Registration> all)
    {
        _map = map;
        _all = all;
    }


    public T Resolve<T>(string key = null) => (T)Resolve(typeof(T), key);

    public object Resolve(Type type, string key = null)
    {
        if (TryResolveCore(type, key, out var value, out var miss)) return value;
        throw new InvalidOperationException(MissMessage(miss, type, key));
    }

    public bool TryResolve<T>(out T value, string key = null)
    {
        if (TryResolveCore(typeof(T), key, out var obj, out _))
        {
            value = (T)obj;
            return true;
        }

        value = default;
        return false;
    }

    public bool TryResolve(Type type, out object value, string key = null) =>
        TryResolveCore(type, key, out value, out _);

    enum Miss { None, NotRegistered, KeyNotFound, Ambiguous }

    static bool IsSelfType(Type type) =>
        type == typeof(IContainerResolve) || type == typeof(IContainer) || type == typeof(DiContainer);

    Miss Find(Type type, string key, out Registration reg)
    {
        reg = null;

        if (!_map.TryGetValue(type, out var entry)) return Miss.NotRegistered;

        if (key != null)
            return entry.Keyed.TryGetValue(key, out reg) ? Miss.None : Miss.KeyNotFound;

        if (entry.Default != null)
        {
            reg = entry.Default;
            return Miss.None;
        }

        if (entry.Keyed.Count == 1)
        {
            reg = entry.First;
            return Miss.None;
        }

        return Miss.Ambiguous;
    }

    bool TryResolveCore(Type type, string key, out object value, out Miss miss)
    {
        value = null;

        if (IsSelfType(type))
        {
            miss = Miss.None;
            value = this;
            return true;
        }

        miss = Find(type, key, out var reg);
        if (miss != Miss.None) return false;

        value = Get(reg);
        return true;
    }

    string MissMessage(Miss miss, Type type, string key)
    {
        switch (miss)
        {
            case Miss.KeyNotFound:
                return $"{type.Name}: there is no instance with the key \"{key}\". Available keys: {KeysOf(type)}";
            case Miss.Ambiguous:
                return $"{type.Name}: there is no registration without a key, but there are several with keys ({KeysOf(type)}). Specify the key";
            default:
                return $"Does not registred: {type}";
        }
    }

    string KeysOf(Type type)
    {
        var keys = _map[type].Keyed;
        return keys.Count == 0 ? "—" : string.Join(", ", keys.Keys);
    }


    public T Instantiate<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component =>
        InstantiateInjected(prefab, position, rotation, parent);

    public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) =>
        InstantiateInjected(prefab, position, rotation, parent);


    T InstantiateInjected<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent)
        where T : UnityEngine.Object
    {
        if (prefab == null) throw new ArgumentNullException(nameof(prefab));

        var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, GetHolder().transform);
        var go = GetGameObject(instance);

        try
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) Inject(mb);

            var t = go.transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(position, rotation);

            if (parent == null)
            {
                var active = SceneManager.GetActiveScene();
                if (go.scene != active) SceneManager.MoveGameObjectToScene(go, active);
            }

            return instance;
        }
        catch
        {
            UnityEngine.Object.Destroy(go);
            throw;
        }
    }

    GameObject GetHolder()
    {
        if (_holder == null)
        {
            _holder = new GameObject("[Injecting]") { hideFlags = HideFlags.HideInHierarchy };
            _holder.SetActive(false);
        }
        return _holder;
    }

    static GameObject GetGameObject(UnityEngine.Object obj)
    {
        if (obj is GameObject go) return go;
        if (obj is Component c) return c.gameObject;
        throw new ArgumentException($"Counldn't to instantiate {obj.GetType().Name}: an GameObject or Component is needed");
    }


    object Get(Registration reg)
    {
        if (reg.Instance != null)
        {
            InjectOnce(reg.Instance);
            return reg.Instance;
        }

        if (reg.Lifetime == Lifetime.Transient) return Create(reg);

        if (_singletons.TryGetValue(reg, out var cached)) return cached;

        var obj = Create(reg);
        _singletons[reg] = obj;
        if (obj is IDisposable d) _disposables.Add(d);
        return obj;
    }

    internal void InjectOnce(object target)
    {
        if (_injected.Add(target)) Inject(target);
    }

    public void CreateNonLazy()
    {
        foreach (var r in _all)
            if (r.NonLazy && r.Instance == null && r.Lifetime == Lifetime.Singleton)
                Get(r);
    }

    object Create(Registration reg)
    {
        if (_path.Contains(reg))
            throw new InvalidOperationException("Circular dependency: " + PathString(reg));

        _path.Push(reg);
        try
        {
            var type = reg.Implementation;
            var plan = GetCtorPlan(type);
            var args = ResolveAll(plan.Params, type);

            object obj;
            try { obj = plan.Ctor.Invoke(args); }
            catch (TargetInvocationException e)
            {
                throw new InvalidOperationException($"Constructor {type.Name} threw an exception", e.InnerException);
            }

            Inject(obj);
            return obj;
        }
        finally { _path.Pop(); }
    }

    static CtorPlan GetCtorPlan(Type type)
    {
        if (CtorCache.TryGetValue(type, out var cached)) return cached;

        ConstructorInfo best = null;
        var bestInject = false;
        var bestCount = -1;

        foreach (var c in type.GetConstructors())
        {
            var hasInject = c.IsDefined(typeof(InjectAttribute), false);
            var count = c.GetParameters().Length;

            if (best == null
                || (hasInject && !bestInject)
                || (hasInject == bestInject && count > bestCount))
            {
                best = c;
                bestInject = hasInject;
                bestCount = count;
            }
        }

        if (best == null)
            throw new InvalidOperationException($"No public constructor: {type}");

        var plan = new CtorPlan { Ctor = best, Params = new ParamPlan(best.GetParameters()) };
        CtorCache[type] = plan;
        return plan;
    }

    public void Inject(object target)
    {
        var type = target.GetType();
        var info = GetInjectInfo(type);

        for (int i = 0; i < info.Fields.Length; i++)
        {
            var field = info.Fields[i];
            field.SetValue(target, ResolveMember(field.FieldType, info.FieldKeys[i], field, type));
        }

        for (int i = 0; i < info.Properties.Length; i++)
        {
            var property = info.Properties[i];
            var value = ResolveMember(property.PropertyType, info.PropertyKeys[i], property, type);
            try { property.SetValue(target, value); }
            catch (TargetInvocationException e)
            {
                throw new InvalidOperationException(
                    $"Сеттер {property.DeclaringType.Name}.{property.Name} threw an exception", e.InnerException);
            }
        }

        for (int i = 0; i < info.Methods.Length; i++)
        {
            var method = info.Methods[i].Method;
            var args = ResolveAll(info.Methods[i].Params, type);

            try { method.Invoke(target, args); }
            catch (TargetInvocationException e)
            {
                throw new InvalidOperationException(
                    $"[Inject] {method.DeclaringType.Name}.{method.Name} threw an exception", e.InnerException);
            }
        }
    }

    static bool IsRoot(Type t) =>
        t == typeof(object) || t == typeof(MonoBehaviour) || t == typeof(ScriptableObject);

    static InjectInfo GetInjectInfo(Type type)
    {
        if (InjectCache.TryGetValue(type, out var cached)) return cached;

        var chain = new Stack<Type>();
        for (var t = type; t != null && !IsRoot(t); t = t.BaseType) chain.Push(t);

        var fields = new List<FieldInfo>();
        var fieldKeys = new List<string>();
        var properties = new List<PropertyInfo>();
        var propertyKeys = new List<string>();
        var methods = new List<MethodPlan>();

        foreach (var t in chain)
        {
            foreach (var f in t.GetFields(MemberFlags))
            {
                if (!f.IsDefined(typeof(InjectAttribute), false)) continue;

                fields.Add(f);
                fieldKeys.Add(f.GetCustomAttribute<KeyAttribute>(false)?.Key);
            }

            foreach (var p in t.GetProperties(MemberFlags))
            {
                if (!p.IsDefined(typeof(InjectAttribute), false)) continue;

                if (p.GetSetMethod(true) == null)
                    throw new InvalidOperationException(
                        $"{t.Name}.{p.Name}: [Inject] on a property without a set. Add a set (maybe private).");
                if (p.GetIndexParameters().Length != 0)
                    throw new InvalidOperationException($"{t.Name}.{p.Name}: [Inject] is not supported on the indexer");

                properties.Add(p);
                propertyKeys.Add(p.GetCustomAttribute<KeyAttribute>(false)?.Key);
            }

            foreach (var m in t.GetMethods(MemberFlags))
            {
                if (!m.IsDefined(typeof(InjectAttribute), false)) continue;

                methods.Add(new MethodPlan { Method = m, Params = new ParamPlan(m.GetParameters()) });
            }
        }

        var result = new InjectInfo
        {
            Fields = fields.ToArray(),
            FieldKeys = fieldKeys.ToArray(),
            Properties = properties.ToArray(),
            PropertyKeys = propertyKeys.ToArray(),
            Methods = methods.ToArray()
        };
        InjectCache[type] = result;
        return result;
    }

    object ResolveMember(Type memberType, string key, MemberInfo member, Type owner)
    {
        if (!TryResolveCore(memberType, key, out var value, out var miss))
            throw new InvalidOperationException(
                $"{owner.Name}: failed to inject {member.DeclaringType.Name}.{member.Name} " +
                $"({memberType.Name}{(key != null ? $", key \"{key}\"" : "")}). {MissMessage(miss, memberType, key)}");

        return value;
    }

    object[] ResolveAll(ParamPlan plan, Type owner)
    {
        var count = plan.Types.Length;
        if (count == 0) return Array.Empty<object>();

        var args = new object[count];

        for (int i = 0; i < count; i++)
        {
            var key = plan.Keys[i];

            if (!TryResolveCore(plan.Types[i], key, out var value, out var miss))
                throw new InvalidOperationException(
                    $"{owner.Name}: couldn't get parameter \"{plan.Names[i]}\" " +
                    $"({plan.Types[i].Name}{(key != null ? $", key \"{key}\"" : "")}). {MissMessage(miss, plan.Types[i], key)}");

            args[i] = value;
        }

        return args;
    }

    string PathString(Registration last) =>
        string.Join(" -> ", _path.Reverse().Select(r => r.Implementation.Name).Append(last.Implementation.Name));


    readonly struct Dep
    {
        public readonly Type Type;
        public readonly string Key;
        public readonly string Where;

        public Dep(Type type, string key, string where)
        {
            Type = type;
            Key = key;
            Where = where;
        }
    }

    internal void Validate(IEnumerable<object> extraTargets = null)
    {
        var errors = new List<string>();

        foreach (var reg in _all)
            CheckType(reg.Implementation, reg.Instance == null, errors);

        if (extraTargets != null)
        {
            var seen = new HashSet<Type>();
            foreach (var target in extraTargets)
                if (target != null && seen.Add(target.GetType()))
                    CheckType(target.GetType(), false, errors);
        }

        DetectCycles(errors);

        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"[DI] Problems in the dependency graph ({errors.Count}):\n - " + string.Join("\n - ", errors));
    }

    void CheckType(Type type, bool includeCtor, List<string> errors)
    {
        var deps = new List<Dep>();
        try { CollectDeps(type, includeCtor, deps); }
        catch (InvalidOperationException e)
        {
            AddError(errors, e.Message);
            return;
        }

        foreach (var d in deps)
        {
            if (IsSelfType(d.Type)) continue;

            var miss = Find(d.Type, d.Key, out _);
            if (miss != Miss.None)
                AddError(errors, $"{type.Name}: {d.Where} — {MissMessage(miss, d.Type, d.Key)}");
        }
    }

    static void CollectDeps(Type type, bool includeCtor, List<Dep> deps)
    {
        if (includeCtor)
            AddParams(GetCtorPlan(type).Params, "constructor", deps);

        var info = GetInjectInfo(type);

        for (int i = 0; i < info.Fields.Length; i++)
        {
            var f = info.Fields[i];
            deps.Add(new Dep(f.FieldType, info.FieldKeys[i], $"field {f.DeclaringType.Name}.{f.Name}"));
        }

        for (int i = 0; i < info.Properties.Length; i++)
        {
            var p = info.Properties[i];
            deps.Add(new Dep(p.PropertyType, info.PropertyKeys[i], $"property {p.DeclaringType.Name}.{p.Name}"));
        }

        for (int i = 0; i < info.Methods.Length; i++)
        {
            var m = info.Methods[i].Method;
            AddParams(info.Methods[i].Params, $"метод {m.DeclaringType.Name}.{m.Name}", deps);
        }
    }

    static void AddParams(ParamPlan plan, string where, List<Dep> deps)
    {
        for (int i = 0; i < plan.Types.Length; i++)
            deps.Add(new Dep(plan.Types[i], plan.Keys[i], $"{where}, parameter \"{plan.Names[i]}\""));
    }

    static void AddError(List<string> errors, string message)
    {
        if (!errors.Contains(message)) errors.Add(message);
    }

    void DetectCycles(List<string> errors)
    {
        var state = new Dictionary<Registration, bool>();
        var stack = new List<Registration>();

        foreach (var r in _all)
            Visit(r, state, stack, errors);
    }

    void Visit(Registration r, Dictionary<Registration, bool> state, List<Registration> stack, List<string> errors)
    {
        if (r.Instance != null) return;

        if (state.TryGetValue(r, out var done))
        {
            if (!done)
            {
                var chain = stack.Skip(stack.IndexOf(r)).Select(x => x.Implementation.Name)
                    .Append(r.Implementation.Name);
                AddError(errors, "Circular dependency: " + string.Join(" -> ", chain));
            }
            return;
        }

        state[r] = false;
        stack.Add(r);

        var deps = new List<Dep>();
        try { CollectDeps(r.Implementation, true, deps); }
        catch (InvalidOperationException) { deps.Clear(); }

        foreach (var d in deps)
        {
            if (IsSelfType(d.Type)) continue;
            if (Find(d.Type, d.Key, out var target) == Miss.None)
                Visit(target, state, stack, errors);
        }

        stack.RemoveAt(stack.Count - 1);
        state[r] = true;
    }


    public void Dispose()
    {
        for (int i = _disposables.Count - 1; i >= 0; i--) _disposables[i].Dispose();
        _disposables.Clear();
        _singletons.Clear();
        _injected.Clear();

        if (_holder != null) UnityEngine.Object.Destroy(_holder);
        _holder = null;
    }
}