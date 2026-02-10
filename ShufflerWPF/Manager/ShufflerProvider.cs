using ShufflerWPF.BaseInterFace;

namespace ShufflerWPF.Manager;
public enum ShufflerType
{
    Legacy,
    Clark
}
public sealed class ShufflerProvider
{
    private static readonly Lazy<ShufflerProvider> _instance = new  Lazy<ShufflerProvider>(() => new ShufflerProvider());
    
    public static ShufflerProvider Instance => _instance.Value;

    private Lazy<ShufflerBase> _current;

    private ShufflerProvider()
    {
        //set default shuffler
        _current = new Lazy<ShufflerBase>(() => ShufflerManager.Instance);
    }

    public ShufflerBase Current => _current.Value;

    public void UseLegacy()
    {
        _current = new Lazy<ShufflerBase>(()=> ShufflerManager.Instance);
    }

    // public void UseClark()
    // {
    //     _current = new Lazy<ShufflerBase>(() => ClarkShufflerManager.Instance);
    // }
    
}