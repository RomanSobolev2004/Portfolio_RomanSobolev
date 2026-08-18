public class ZenjectWrappedParamers : WrappedParameters, IZenjectWrappedGlobal
{
    public ZenjectWrappedParamers(DiContainer container, IZenjectWrappedGlobal global)
    {
        Container = container;
        Global = global;
    }

    public bool Contains<ObjectType>() => base.Contains<ObjectType>(false);
    public void Bind(params object[] parameters) => WrappNew(parameters);

    public virtual ObjectType Unwrapp<ObjectType>(int index = 0, bool includeGlobal = true)
    {
        var obj = base.UnwrappAs<ObjectType>(index);

        if (obj == null && includeGlobal) obj = Global.Unwrapp<ObjectType>(index, false);

        if (obj == null) Debug.LogError($"Failed to unwrapp {typeof(ObjectType)} within {this}");

        return obj;
    }

    public DiContainer Container { get; private set; }

    public IZenjectWrappedGlobal Global { get; private set; }
}

public class WrappedParameters
{
    [ES3Serializable] protected List<object> _defaultParameters = new();
    [ES3Serializable] protected Dictionary<Type, List<object>> _wrappedParameters = new();
    [ES3Serializable] protected Dictionary<string, (Type, int)> _wrappedKeys = new();

    public WrappedParameters(params object[] parameters)
    {
        WrappObjects(parameters);
    }

    public virtual void WrappObjects(params object[] parameters)
    {
        _defaultParameters.Clear();
        _wrappedParameters.Clear();

        WrappNew(parameters);
    }
    public virtual void WrappNew(params object[] parameters)
    {
        foreach (var parameter in parameters)
        {
            _defaultParameters.Add(parameter);

            object obj = parameter;
            Type type = obj.GetType();
            string wrappedKey = null;

            CovertIfCertainTypePair(ref obj, ref type, ref wrappedKey);

            if (_wrappedParameters.ContainsKey(type))
            {
                _wrappedParameters[type].Add(obj);
            }
            else
            {
                _wrappedParameters.Add(type, new List<object>() { obj });
            }

            if (wrappedKey != null)
            {
                _wrappedKeys.Add(wrappedKey, (type, _wrappedParameters[type].Count - 1));
            }
        }
    }
    public virtual void Append(WrappedParameters other)
    {
        _defaultParameters.AddRange(other._defaultParameters);
        _wrappedParameters.AddRange(other._wrappedParameters);
    }

    public virtual IReadOnlyList<Type> Types => _wrappedParameters.Keys.ToList();
    public virtual IReadOnlyCollection<object> Defaults => _defaultParameters;

    public virtual int Count => Defaults.Count;
    public virtual int CountOfType<ObjectType>(bool includeInheritance = false) => CountOfType(typeof(ObjectType), includeInheritance);
    public virtual int CountOfType(Type type, bool includeInheritance = false)
    {
        if (_wrappedParameters.ContainsKey(type)) return _wrappedParameters[type].Count;
        else if (includeInheritance)
        {
            int count = 0;
            foreach (var wrapped in _wrappedParameters)
            {
                if (Utils.AreTypesRelated(wrapped.Key, type)) count += wrapped.Value.Count;
            }
            return count;
        }
        else return 0;
    }

    public virtual bool Contains<ObjectType>(bool includeInheritance = false) => Contains(typeof(ObjectType), includeInheritance);
    public virtual bool Contains(Type type, bool includeInheritance = false)
    {
        return CountOfType(type, includeInheritance) > 0;
    }


    public virtual ObjectType Unwrapp<ObjectType>(int index = 0)
    {
        Type type = typeof(ObjectType);

        return (ObjectType)Unwrapp(type, index);
    }
    public virtual ObjectType UnwrappAs<ObjectType>(int index = 0)
    {
        Type type = typeof(ObjectType);

        return (ObjectType)Unwrapp(type, index, true);
    }
    public virtual ObjectType UnwrappAt<ObjectType>(int index)
    {
        return (ObjectType)_defaultParameters[index];
    }
    public virtual ObjectType UnwrappFromKey<ObjectType>(string key, int keyFailIndex = 0)
    {
        if (!_wrappedKeys.ContainsKey(key)) return UnwrappAs<ObjectType>(keyFailIndex);

        var typeId = _wrappedKeys[key];
        var list = _wrappedParameters[typeId.Item1];

        return (ObjectType)_wrappedParameters[typeId.Item1][typeId.Item2];
    }

    public virtual object Unwrapp(Type type, int index = 0, bool includeInheritance = false)
    {
        if (!_wrappedParameters.ContainsKey(type))
        {
            return includeInheritance ? UnwrappInheritance(type, index) : null;
        }
        else
        {
            var list = _wrappedParameters[type];

            return _wrappedParameters[type][index];
        }
    }

    protected virtual object UnwrappInheritance(Type type, int index)
    {
        foreach (var item in _wrappedParameters)
        {
            var iType = item.Key;

            if (Utils.AreTypesRelated(iType, type))
            {
                var list = item.Value;

                if (index >= list.Count) index -= list.Count;
                else return list[index];
            }
        }

        return null;
    }

    protected virtual void CovertIfCertainTypePair(ref object obj, ref Type type, ref string key)
    {
        if (type == typeof(WrappedObjectType))
        {
            var wrapped = (WrappedObjectType)obj;

            obj = wrapped.Value;
            if (wrapped.Type != null) type = wrapped.Type;
            else key = wrapped.Key;
        }
    }
}

public struct WrappedObjectType
{
    public Type Type;
    public string Key;
    public object Value;

    public WrappedObjectType(object obj, Type type)
    {
        Type = type;
        Value = obj;
        Key = null;
    }
    public WrappedObjectType(object obj, string key)
    {
        Type = null;
        Value = obj;
        Key = key;
    }
}

public static class WrappedObjectTypeExtensions
{
    public static WrappedObjectType WrappTo<ObjectType>(this object obj)
    {
        if (obj == null) return default;

        var type = typeof(ObjectType);
        if (!Utils.AreTypesRelated(obj.GetType(), type)) type = obj.GetType();

        return new WrappedObjectType(obj, type);
    }
    public static WrappedObjectType WrappTo(this object obj, string key)
    {
        if (obj == null) return default;

        return new WrappedObjectType(obj, key);
    }
}