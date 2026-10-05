using System.Windows;

namespace KFL.App;

/// <summary>
/// 空壳主窗口。001 只要求 KFL.App 存在且可构建启动（FR-001）；
/// 卡片墙与「下月 / 快进」属规格书 §16 阶段③，本阶段 MUST NOT 出现业务逻辑或规则数值。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
