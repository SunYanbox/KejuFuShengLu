using System.Collections.ObjectModel;
using KFL.Infrastructure.Abstractions;
using KFL.Presentation.Mvvm;

namespace KFL.Presentation.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>统计页图表（按月聚合自族流水账，可左右滚动）。</summary>
    public ObservableCollection<ChartFrame> Charts { get; } = [];

    /// <summary>成员详情里的关系超链接（需求 7：父母 / 配偶 / 子女 / 兄弟姐妹点击跳转）。</summary>
    public ObservableCollection<RelationLink> RelationLinks { get; } = [];

    /// <summary>收支与分类两组图取累计口径（需求 11 的切换开关）。</summary>
    public bool IsMoneyChartCumulative
    {
        get => _moneyChartCumulative;
        set
        {
            if (Set(ref _moneyChartCumulative, value))
            {
                Raise(nameof(IsMoneyChartMonthly));
                RefreshStatistics();
            }
        }
    }

    /// <summary>人口与功名图取累计口径（需求 10 的切换开关）。</summary>
    public bool IsPopulationChartCumulative
    {
        get => _populationChartCumulative;
        set
        {
            if (Set(ref _populationChartCumulative, value))
            {
                Raise(nameof(IsPopulationChartMonthly));
                RefreshStatistics();
            }
        }
    }

    /// <summary>人口与功名图取当月口径（与 <see cref="IsPopulationChartCumulative"/> 互斥）。</summary>
    public bool IsPopulationChartMonthly
    {
        get => !_populationChartCumulative;
        set
        {
            if (value)
            {
                IsPopulationChartCumulative = false;
            }
        }
    }

    /// <summary>收支与分类两组图取当月口径（与 <see cref="IsMoneyChartCumulative"/> 互斥）。</summary>
    public bool IsMoneyChartMonthly
    {
        get => !_moneyChartCumulative;
        set
        {
            if (value)
            {
                IsMoneyChartCumulative = false;
            }
        }
    }

    /// <summary>统计页「算好的数据」（需求 16）：月均收支、结构占比、现金可支撑月数等。</summary>
    public string StatsInsights
    {
        get => _statsInsights;
        private set => Set(ref _statsInsights, value);
    }

    /// <summary>自由金额输入框（需求 13：储蓄 / 还贷 / 商本不再只能整存整取）。</summary>
    public string MoneyAmountText
    {
        get => _moneyAmountText;
        set => Set(ref _moneyAmountText, value);
    }

    /// <summary>家族成员筛选（已归档 / 已死亡 / 已外嫁 / 服刑中 / 在册…）。</summary>
    public Choice<string> SelectedMemberFilter
    {
        get => _selectedMemberFilter;
        set
        {
            if (Set(ref _selectedMemberFilter, value))
            {
                RefreshMembers();
            }
        }
    }

    /// <summary>存档概览筛选后的成员计数文案。</summary>
    public string MemberCountText
    {
        get => _memberCountText;
        private set => Set(ref _memberCountText, value);
    }

    /// <summary>当前设置下的每月收支摘要（需求 7）；点击弹出明细。</summary>
    public string MonthlyBalanceText
    {
        get => _monthlyBalanceText;
        private set => Set(ref _monthlyBalanceText, value);
    }

    /// <summary>收支明细悬浮窗是否可见（需求 7）。</summary>
    public bool IsBreakdownVisible
    {
        get => _isBreakdownVisible;
        set => Set(ref _isBreakdownVisible, value);
    }

    /// <summary>收支明细悬浮窗正文（按人、按来源）。</summary>
    public string MonthlyBreakdownText
    {
        get => _monthlyBreakdownText;
        private set => Set(ref _monthlyBreakdownText, value);
    }

    /// <summary>自动治疗开关（需求 10）：开启后每次结算完自动为患病成员开始治疗。</summary>
    public bool AutoTreat
    {
        get => _autoTreat;
        set
        {
            if (Set(ref _autoTreat, value))
            {
                RefreshMonthlyBalance();
            }
        }
    }

    /// <summary>选中的备份压缩包（需求 15）。</summary>
    public Choice<SaveSlot>? SelectedBackup
    {
        get => _selectedBackup;
        set => Set(ref _selectedBackup, value);
    }

    /// <summary>主菜单里选中的已有存档（需求 1）。</summary>
    public Choice<SaveSlot>? SelectedMenuSave
    {
        get => _selectedMenuSave;
        set
        {
            if (Set(ref _selectedMenuSave, value))
            {
                RefreshMenuSaveOverview();
            }
        }
    }

    /// <summary>主菜单里选中存档的概览。</summary>
    public string MenuSaveOverviewText
    {
        get => _menuSaveOverviewText;
        private set => Set(ref _menuSaveOverviewText, value);
    }

    /// <summary>存档写入格式（存档 / 另存为 / 自动存档 均遵循；切换后立即对新格式生效）。</summary>
    public Choice<SaveFormat> SelectedSaveFormat
    {
        get => _selectedSaveFormat;
        set
        {
            if (!Set(ref _selectedSaveFormat, value) || _session is null)
            {
                return;
            }

            var error = _session.SetSaveFormat(value.Value);
            if (error.Length > 0)
            {
                _session.Note(error);
            }

            RefreshLog();
            RefreshAll();
        }
    }

    /// <summary>选中存档的概览（成员、资产、负债、功名分布、最后游玩时间）。</summary>
    public string SaveOverviewText
    {
        get => _saveOverviewText;
        private set => Set(ref _saveOverviewText, value);
    }

    public bool ConsoleVisible
    {
        get => _consoleVisible;
        set => Set(ref _consoleVisible, value);
    }

    public string ConsoleInput
    {
        get => _consoleInput;
        set => Set(ref _consoleInput, value);
    }

    public string ConsoleOutput
    {
        get => _consoleOutput;
        private set => Set(ref _consoleOutput, value);
    }
}
