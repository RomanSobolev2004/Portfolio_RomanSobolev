public class ThreadGenerator : MonoBehaviour
{
    [SerializeField] protected int iterations;
    [SerializeField] protected bool central_generation;

    [Space]
    [SerializeField] protected DungeonRoom startRoom;
    [SerializeField] protected DungeonRoom emptyRoom;
    [SerializeField] protected DungeonRoom corridorRoom;
    [SerializeField] protected SerializableDictionary<DungeonRoom, RoomsSpawnOptions> roomsForSpawn;

    [SerializeField] protected Transform dungeonParent;
    [SerializeField] protected NavMeshSurface dungeonNavMeshSurface;
    [SerializeField] protected bool spawnMobs;

    [SerializeField, HideInInspector] protected SerializableDictionary<Vector2Int, DungeonRoom> _spawned;

    private RoomsGenerationJob _generatrionJob;
    private JobHandle _generationJobHandle;

    [Inject] private DiContainer _container;

    private void Start()
    {
        dungeonNavMeshSurface.BuildNavMesh();

        if (spawnMobs && dungeonParent)
        {
            foreach (var room in _spawned.Values)
            {
                room.SpawnMobsInRoom();
            }
        }
    }

    [ContextMenu("Generate with Threads In Game")]
    public void InGameGenerate()
    {
        Generate(true);

        dungeonNavMeshSurface.BuildNavMesh();

        if (spawnMobs && dungeonParent)
        {
            foreach (var room in _spawned.Values)
            {
                room.SpawnMobsInRoom();
            }
        }
    }
    [ContextMenu("Generate with Threads")]
    public void IneditorGenerate()
    {
        Generate();
    }

    public void Generate(bool inGame = false)
    {
        Profiler.BeginSample("Thread gen");
        var stopwatch = Stopwatch.StartNew();

        Clear();

        _generatrionJob = new RoomsGenerationJob()
        {
            startRoom = FromDungeonRoom(startRoom),
            emptyRoom = FromDungeonRoom(emptyRoom),
            corridorRoom = FromDungeonRoom(corridorRoom),
            rooms = StructsFromDictionary(roomsForSpawn),

            central_generation = central_generation,
            iterations = iterations,
            Rand = new Unity.Mathematics.Random((uint)Random.Range(0, 99999)),

            Spawned = new(roomsForSpawn.Count * 3, Allocator.Persistent),
        };

        _generationJobHandle = _generatrionJob.Schedule();

        _generationJobHandle.Complete();

        SpawnRooms(_generatrionJob.Spawned, inGame);

        _generatrionJob.Spawned.Dispose();
        _generatrionJob.rooms.Dispose();

        Profiler.EndSample();

        stopwatch.Stop();
        Debug.Log($"Code run time {stopwatch.ElapsedMilliseconds}");
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        if (_generatrionJob.Equals(null)) return;

        if (_generatrionJob.rooms.IsCreated) _generatrionJob.rooms.Dispose();
        if (_generatrionJob.Spawned.IsCreated) _generatrionJob.Spawned.Dispose();

        DispawnRooms();
        //dungeonNavMeshSurface
    }

    private RoomStruct FromDungeonRoom(DungeonRoom room)
    {
        return new RoomStruct()
        {
            roomType = room.RoomType,
            roomTrasnform = default,
            doors = DoorsFromDungeonDoors(room.Doors)
        };
    }
    private FixedList512Bytes<DoorStruct> DoorsFromDungeonDoors(IReadOnlyList<DungeonDoor> doors)
    {
        FixedList512Bytes<DoorStruct> structs = new();

        for (int i = 0; i < doors.Count; i++)
        {
            var door = new DoorStruct()
            {
                Direction = doors[i].DoorDirection.Selected,
                DoorTransform = FromTransform(doors[i].DoorTransform),
                EntryPoint = FromTransform(doors[i].DoorEntryPoint)
            };

            structs.Add(door);
        }

        return structs;
    }
    private NativeParallelHashMap<RoomStruct, RoomsSpawnOptions> StructsFromDictionary(IReadOnlyDictionary<DungeonRoom, RoomsSpawnOptions> rooms)
    {
        NativeParallelHashMap<RoomStruct, RoomsSpawnOptions> map = new(rooms.Count, Allocator.TempJob);

        foreach (var room in rooms)
        {
            map.Add(FromDungeonRoom(room.Key), room.Value);
        }

        return map;
    }

    private NativeTransformStruct FromTransform(Transform transform)
    {
        return new()
        {
            LocalPosition = transform.localPosition,
            LocalRotation = transform.localRotation
        };
    }

    private void DispawnRooms()
    {
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif

        if (dungeonParent == null) return;

        _spawned.Clear();
        DestroyImmediate(dungeonParent.gameObject);
        dungeonParent = null;

    }
    private void SpawnRooms(NativeParallelHashMap<Vector2Int, RoomStruct> rooms, bool inGame)
    {
        DispawnRooms();
        dungeonParent = new GameObject("Parent").transform;

        Dictionary<RoomType, DungeonRoom> spawnRooms = TypesFromDooms();
        _spawned = new();

        foreach (var roomS in rooms)
        {
            DungeonRoom room;

            if (inGame)
            {
                room = _container.InstantiatePrefabForComponent<DungeonRoom>(spawnRooms[roomS.Value.roomType], dungeonParent);
            }
            else
            {
                room = Instantiate(spawnRooms[roomS.Value.roomType], dungeonParent);
            }

            room.transform.localPosition = roomS.Value.roomTrasnform.LocalPosition;
            room.transform.rotation = roomS.Value.roomTrasnform.LocalRotation;
            room.RotateDoors(roomS.Value.DoorsRotation);

            _spawned.Add(roomS.Key, room);
        }

        SetRoomDoors(_spawned);
    }

    private void SetRoomDoors(IReadOnlyDictionary<Vector2Int, DungeonRoom> spawned)
    {
        foreach (var room in spawned)
        {
            foreach (var door in room.Value.Doors)
            {
                if (spawned.ContainsKey(room.Key + door.DoorDirection.Selected))
                {
                    door.DoorActive(true);
                }
            }
        }
    }

    private Dictionary<RoomType, DungeonRoom> TypesFromDooms()
    {
        Dictionary<RoomType, DungeonRoom> rooms = new();

        foreach (var room in roomsForSpawn)
        {
            rooms.Add(room.Key.RoomType, room.Key);
        }

        if (!rooms.ContainsKey(RoomType.StartRoom)) rooms.Add(RoomType.StartRoom, startRoom);
        if (!rooms.ContainsKey(RoomType.EmptyRoom)) rooms.Add(RoomType.EmptyRoom, emptyRoom);
        if (!rooms.ContainsKey(RoomType.Hallway)) rooms.Add(RoomType.Hallway, corridorRoom);

        return rooms;
    }
}



public struct RoomsGenerationJob : IJob
{
    public NativeParallelHashMap<RoomStruct, RoomsSpawnOptions> rooms;
    public RoomStruct startRoom;
    public RoomStruct emptyRoom;
    public RoomStruct corridorRoom;
    public int iterations;
    public bool central_generation;
    public Random Rand;

    public NativeParallelHashMap<Vector2Int, RoomStruct> Spawned;

    public void Execute()
    {
        int count = RoomsCount();

        Vector2Int pos = Vector2Int.zero;

        AddRoom(pos, ref startRoom);

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            for (int length = 0; length < count; length++)
            {
                var lastRoom = Spawned[pos];
                var lastDoor = RandomRoomDoor(lastRoom);
                pos += lastDoor.Direction;

                if (Spawned.ContainsKey(pos + lastDoor.Direction))
                {
                    length--;
                    pos += lastDoor.Direction;
                    continue;
                }

                RoomStruct room = RoomFromWeigth();

                if (room.Equals(default))
                {
                    return;
                }

                AddNextRoom(ref pos, corridorRoom, ref lastDoor, ref lastRoom);

                if (room.doors.Length == 1)
                {
                    AddNextRoom(ref pos, emptyRoom, ref lastDoor, ref lastRoom);

                    if (!Spawned.ContainsKey(pos + lastDoor.Direction))
                    {
                        AddNextRoom(ref pos, corridorRoom, ref lastDoor, ref lastRoom);
                    }
                    else
                    {
                        length--;
                        pos -= lastDoor.Direction;
                        continue;
                    }
                }

                AddRoom(pos, ref room, lastDoor, lastRoom);
            }

            if (central_generation) pos = Vector2Int.up;
        }
    }

    private void AddRoom(Vector2Int pos, ref RoomStruct room, DoorStruct lastDoor = default, RoomStruct lastRoom = default)
    {
        room = new(room);

        if (!lastDoor.Equals(default))
        {
            SetRoomPositionAndRotation(lastRoom, lastDoor, ref room);
        }

        //Debug.Log($"Spawned {room.roomType} in {pos} | last door {lastDoor.Direction}");

        Spawned.Add(pos, room);
    }
    private void AddNextRoom(ref Vector2Int pos, RoomStruct room, ref DoorStruct lastDoor, ref RoomStruct lastRoom)
    {
        AddRoom(pos, ref room, lastDoor, lastRoom);
        lastDoor = DoorFromRoomAsSequence(lastDoor, room);
        lastRoom = room;
        pos += lastDoor.Direction;
    }

    private void SetRoomPositionAndRotation(RoomStruct lastRoom, DoorStruct lastDoor, ref RoomStruct room)
    {
        var choosen = RandomRoomDoor(room);

        float lastAngle = RotationFromDoor(lastDoor.Direction, false);
        float angle = RotationFromDoor(choosen.Direction);

        float doorsRot = -(angle + lastAngle);
        room.RotateDoors(doorsRot);
        room.DoorsRotation = doorsRot;
        Quaternion roomRot = Quaternion.Euler(0, angle + lastAngle + 90, 0);
        room.roomTrasnform.LocalRotation = roomRot;

        var entryPos = lastRoom.roomTrasnform.TransformPoint(lastDoor.DoorTransform.TransformPoint(lastDoor.EntryPoint.LocalPosition));
        var doorPos = room.roomTrasnform.TransformPoint(choosen.DoorTransform.LocalPosition + choosen.DoorTransform.TransformDirection(DungeonDoor.DoorHalfWidth));

        var dir = entryPos - doorPos;
        room.roomTrasnform.LocalPosition += dir;
    }

    private DoorStruct RandomRoomDoor(RoomStruct room)
    {
        return room.doors[Rand.NextInt(0, room.doors.Length)];
    }

    private float RotationFromDoor(Vector2Int door, bool forward180 = true)
    {
        float angle = 0;
        angle = forward180 ? 0 : 180;

        if (door == EntriesDroopdown.ForwardDoor)
        {
            angle = forward180 ? 180 : 0;
        }
        else if (door == EntriesDroopdown.RightDoor)
        {
            angle = 90;
        }
        else if (door == EntriesDroopdown.LeftDoor)
        {
            angle = -90;
        }

        return angle;
    }

    private DoorStruct DoorFromRoomAsSequence(DoorStruct lastDoor, RoomStruct room)
    {
        var dir = lastDoor.Direction;

        foreach (var door in room.doors)
        {
            if (door.Direction == dir)
            {
                return door;
            }
        }

        return default;
    }

    private int RoomsCount()
    {
        int count = 0;

        foreach (var rooms in rooms)
        {
            count += rooms.Value.SpawnCount;
        }

        return count;
    }

    private RoomStruct RoomFromWeigth()
    {
        float summ = 0;
        foreach (var room in rooms) summ += room.Value.SpawnWeigth;

        float random = Rand.NextFloat(0, summ);

        foreach (var room in rooms)
        {
            if (room.Value.SpawnCount == 0) continue;

            random -= room.Value.SpawnWeigth;

            if (random <= 0)
            {
                room.Value.MinusCount();

                var result = room.Key;

                if (room.Value.SpawnCount <= 0)
                {
                    rooms.Remove(result);
                }

                return result;
            }
        }

        return default;
    }
}