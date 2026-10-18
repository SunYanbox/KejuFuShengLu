using System.Windows.Input;

namespace KFL.Presentation.Mvvm;

/// <summary>
/// 单一令牌命令：UI 用一个共享命令 + <see cref="ICommand"/> 参数令牌（如 <c>bribe:JieShi</c>）
/// 派发全部动作，避免为每个按钮声明一个命令属性。
/// </summary>
public sealed class ActionCommand : ICommand
{
    private readonly Action<string?> _execute;
    private readonly Predicate<string?>? _canExecute;

    public ActionCommand(Action<string?> execute, Predicate<string?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter as string) ?? true;

    public void Execute(object? parameter) => _execute(parameter as string);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
