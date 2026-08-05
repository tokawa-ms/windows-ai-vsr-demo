using System.Windows.Input;

namespace WindowsAiVsrCpuDemo.Infrastructure;

public sealed class AsyncCommand : ICommand, IDisposable
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;
    private bool _isDisposed;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isDisposed && !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isExecuting = true;
        NotifyCanExecuteChanged();

        try
        {
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            if (!_isDisposed)
            {
                NotifyCanExecuteChanged();
            }
        }
    }

    public void NotifyCanExecuteChanged()
    {
        if (!_isDisposed)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        CanExecuteChanged = null;
    }
}

public sealed class RelayCommand : ICommand, IDisposable
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isDisposed;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) =>
        !_isDisposed && (_canExecute?.Invoke() ?? true);

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute();
        }
    }

    public void NotifyCanExecuteChanged()
    {
        if (!_isDisposed)
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        CanExecuteChanged = null;
    }
}
