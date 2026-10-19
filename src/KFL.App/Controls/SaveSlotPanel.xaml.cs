using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KFL.App.Controls;

/// <summary>
/// 存档槽列表面板：本机存档与备份存档两处的共用外形（标题 + 列表 + 读取/删除）。
/// 删除动作在此处做二次确认，再以 <see cref="DeleteToken"/> 交给界面模型执行。
/// </summary>
public partial class SaveSlotPanel : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(SaveSlotPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
        nameof(SelectedItem), typeof(object), typeof(SaveSlotPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty LoadCommandProperty = DependencyProperty.Register(
        nameof(LoadCommand), typeof(ICommand), typeof(SaveSlotPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty LoadTokenProperty = DependencyProperty.Register(
        nameof(LoadToken), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LoadLabelProperty = DependencyProperty.Register(
        nameof(LoadLabel), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata("读取选中存档"));

    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand), typeof(ICommand), typeof(SaveSlotPanel), new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteTokenProperty = DependencyProperty.Register(
        nameof(DeleteToken), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DeleteLabelProperty = DependencyProperty.Register(
        nameof(DeleteLabel), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata("删除选中存档"));

    /// <summary>删除确认里的名词（「存档」/「备份」）。</summary>
    public static readonly DependencyProperty DeleteNounProperty = DependencyProperty.Register(
        nameof(DeleteNoun), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata("存档"));

    /// <summary>删除确认里的影响说明（「备份存档不受影响」/「主存档不受影响」）。</summary>
    public static readonly DependencyProperty DeleteWarningProperty = DependencyProperty.Register(
        nameof(DeleteWarning), typeof(string), typeof(SaveSlotPanel), new PropertyMetadata("备份存档不受影响"));

    public SaveSlotPanel()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ICommand? LoadCommand
    {
        get => (ICommand?)GetValue(LoadCommandProperty);
        set => SetValue(LoadCommandProperty, value);
    }

    public string LoadToken
    {
        get => (string)GetValue(LoadTokenProperty);
        set => SetValue(LoadTokenProperty, value);
    }

    public string LoadLabel
    {
        get => (string)GetValue(LoadLabelProperty);
        set => SetValue(LoadLabelProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public string DeleteToken
    {
        get => (string)GetValue(DeleteTokenProperty);
        set => SetValue(DeleteTokenProperty, value);
    }

    public string DeleteLabel
    {
        get => (string)GetValue(DeleteLabelProperty);
        set => SetValue(DeleteLabelProperty, value);
    }

    public string DeleteNoun
    {
        get => (string)GetValue(DeleteNounProperty);
        set => SetValue(DeleteNounProperty, value);
    }

    public string DeleteWarning
    {
        get => (string)GetValue(DeleteWarningProperty);
        set => SetValue(DeleteWarningProperty, value);
    }

    /// <summary>删除前的二次确认（玩家反馈 9）：取消则不下发删除指令。</summary>
    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (DeleteCommand is not { } command)
        {
            return;
        }

        var name = SelectedItem?.ToString() ?? "（未选中）";
        if (MessageBox.Show(
                $"确定删除{DeleteNoun}「{name}」吗？\n\n删除后无法恢复（{DeleteWarning}）。",
                $"删除{DeleteNoun}",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) != MessageBoxResult.OK)
        {
            return;
        }

        if (command.CanExecute(DeleteToken))
        {
            command.Execute(DeleteToken);
        }
    }
}
