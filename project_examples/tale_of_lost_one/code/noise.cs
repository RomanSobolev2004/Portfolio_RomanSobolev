public class NoiseContoller : OptimizedMonoBehaviour, INoiseContoller
{
    [SerializeField] private float clamNoise;
    [SerializeField] private float maxNoise;
    [SerializeField] private float forcingTime;
    [SerializeField] private float loweringSpeed;

    [Space, SerializeField] private bool useMicrophineInput;
    [ShowIf("useMicrophineInput"), SerializeField] private int microDeviceIndex;
    [ShowIf("useMicrophineInput"), SerializeField] private float microSensivity;

    private MicrophoneInput m_microImput;
    private NoiseMenu m_menue;

    private float _currentNoise;

    private Tween _forsingTween;

    protected override void Construct(IZenjectWrapped wrapped)
    {
        base.Construct(wrapped);

        m_menue = wrapped.Unwrapp<IMenuManager>().GetMenue<NoiseMenu>();
    }

    public bool Enabled { get => enabled; set => enabled = value; }

    public float CurrentNoise
    {
        get => _currentNoise;
        private set
        {
            _currentNoise = Mathf.Clamp(value, clamNoise, maxNoise);
        }
    }
    public bool ApplyingMicrophoneInput
    {
        get => useMicrophineInput;
        set
        {
            useMicrophineInput = value;

            if (useMicrophineInput) m_microImput?.StartWriting();
            else m_microImput?.StopWriting();
        }
    }

    protected override void Awake()
    {
        SetMicrophone();

        CurrentNoise = clamNoise;
        ApplyingMicrophoneInput = useMicrophineInput;

        //GameConsole.ShowLine($"Noise - {m_menue.IsActive}");
    }

    public void EnableNoiseMenu(bool enable = true)
    {
        m_menue.EnableMenu(enable);
    }

    public void ForceNoise(float noise)
    {
        if (!enabled) return;

        SetNoise(CurrentNoise + noise);
    }
    public void SetNoise(float noise, bool louderOnly = true)
    {
        if (!enabled) return;

        if (louderOnly && noise <= CurrentNoise) return;

        _forsingTween?.Kill();

        _forsingTween = DOTween.To(
            () => CurrentNoise, x => CurrentNoise = x,
            noise, forcingTime).OnKill(() => _forsingTween = null);
    }

    protected override void Update()
    {
        if (!enabled) return;

        if (_forsingTween == null) CurrentNoise -= loweringSpeed * Time.deltaTime;

        if (ApplyingMicrophoneInput)
        {
            var vol = m_microImput.GetVolumeFromActiveDevice() * microSensivity;

            if (vol > CurrentNoise)
            {
                //ForceNoise(vol);
                CurrentNoise = vol;
            }
        }

        m_menue.ViewNoise(CurrentNoise);
    }

    private void SetMicrophone()
    {
        m_microImput = new MicrophoneInput();

        m_microImput.SetAciveDevice(microDeviceIndex);
    }
}

public class MicrophoneInput
{
    protected string m_micro;
    protected AudioClip m_clip;

    protected float[] _sampler_buffer = new float[512];

    public string GetActiveDevice() => m_micro;
    public AudioClip GetWrittenClip() => m_clip;

    public virtual string[] FindAwaliableDewices()
    {
        return Microphone.devices;
    }

    public virtual void SetAciveDevice(int index)
    {
        if (index >= Microphone.devices.Length)
        {
            Debug.Log("Not found microphone device! Invalid index");
            return;
        }

        m_micro = Microphone.devices[index];
    }
    public virtual void SetAciveDevice(string name)
    {
        if (!Microphone.devices.Contains(name))
        {
            Debug.Log("Not found microphone device! Invalid name");
            return;
        }

        m_micro = name;
    }

    public virtual void StartWriting()
    {
        m_clip = Microphone.Start(m_micro, true, 1, 44100);
    }
    public virtual void StopWriting()
    {
        if (m_micro == null) return;

        Microphone.End(m_micro);
    }

    public virtual float GetVolumeFromActiveDevice()
    {
        if (!m_clip || m_micro == null) return 0;

        int micPos = Microphone.GetPosition(m_micro) - _sampler_buffer.Length;
        if (micPos < 0) return 0f;

        m_clip.GetData(_sampler_buffer, micPos);

        float sum = 0;
        for (int i = 0; i < _sampler_buffer.Length; i++)
            sum += _sampler_buffer[i] * _sampler_buffer[i];

        float rms = Mathf.Sqrt(sum / _sampler_buffer.Length);
        return rms;
    }
}