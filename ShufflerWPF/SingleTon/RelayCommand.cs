using System.Windows.Input;

namespace ShufflerWPF.SingleTon;

/// <summary>
/// A reusable command implementation for MVVM pattern
/// </summary>
public class RelayCommand:ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    ///  Constructor for synchronous commands
    /// </summary>
    /// <param name="execute"></param>
    /// <param name="canExecute"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// Determines whether the command can execute
    /// </summary>
    /// <param name="parameter"></param>
    /// <returns></returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }

    /// <summary>
    /// Executes the command
    /// </summary>
    /// <param name="parameter"></param>
    public void Execute(object? parameter)
    {
        _execute();
    }
    
    /// <summary>
    /// Event that is raised when CanExecute changes
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add {CommandManager.RequerySuggested += value;}
        remove {CommandManager.RequerySuggested -= value;}
    }

    /// <summary>
    /// Manually raise CanExecuteChanged
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    public bool CanExecute(object? parameter)
    {
        return !_isExecuting && (_canExecute == null || _canExecute());
    }

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isExecuting = true;
        RaiseCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }
    
    public event EventHandler? CanExecuteChanged
    {
        add {CommandManager.RequerySuggested += value;}
        remove {CommandManager.RequerySuggested -= value;}
    }
    
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }

}