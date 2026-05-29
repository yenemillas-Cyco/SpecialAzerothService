namespace SpecialAzerothService.Core.Services;

/// <summary>État d'initialisation Carto — une seule passe, jamais de rebouclage.</summary>
public enum CartoInitPhase
{
    NotStarted = 0,
    Running = 1,
    Complete = 2
}

/// <summary>Verrou simple : Start() une fois, Complete() une fois.</summary>
public sealed class CartoInitGate
{
    private CartoInitPhase _phase = CartoInitPhase.NotStarted;

    public CartoInitPhase Phase => _phase;

    public bool IsComplete => _phase == CartoInitPhase.Complete;

    /// <returns>True si cette invocation démarre la session.</returns>
    public bool TryBegin()
    {
        if (_phase != CartoInitPhase.NotStarted)
            return false;
        _phase = CartoInitPhase.Running;
        return true;
    }

    public void Complete()
    {
        if (_phase == CartoInitPhase.Running)
            _phase = CartoInitPhase.Complete;
    }

    public void Reset()
    {
        _phase = CartoInitPhase.NotStarted;
    }
}
