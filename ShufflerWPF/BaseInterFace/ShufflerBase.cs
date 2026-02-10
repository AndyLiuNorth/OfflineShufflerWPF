using ShufflerWPF.Model;
using ShufflerWPF.Pages;

namespace ShufflerWPF.BaseInterFace;

public abstract class ShufflerBase: IDisposable
{
    public abstract bool ConnectStatus { get; }
    public abstract Task<bool> ConnectShuffler();
    public abstract Task LockTouch(string shufflerip);
    public abstract Task UnLockTouch(string shufflerip);
    public abstract Task DoShuffle(string shufflerip);
    public abstract Task StopShuffle(string shufflerip);
    public abstract List<ShufflerStatus> GetShufflerStatusList();
    public abstract void CloseShuffler();
    public virtual void Dispose(){}
    
    public event Action<List<ShufflerStatus>>? ShufflerStatusChangeAction;

    protected void OnShufflerStatusChange(List<ShufflerStatus> list)
    {
        ShufflerStatusChangeAction?.Invoke(list);
    }
    
}