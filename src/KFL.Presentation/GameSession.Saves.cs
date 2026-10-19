using KFL.Core.Entities;
using KFL.Core.Enums;
using KFL.Core.ValueObjects;
using KFL.Infrastructure.Abstractions;
using KFL.Infrastructure.Save;
using KFL.Rules.Achievements;
using KFL.Rules.Config;
using KFL.Rules.Economy;
using System.IO;

namespace KFL.Presentation;

public sealed partial class GameSession
{
    public static IReadOnlyList<SaveSlot> ListSaves()
    {
        var saves = new SaveService();
        var found = new List<SaveSlot>();
        foreach (var path in SaveCatalog.ListFiles(saves.RootDirectory))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            // 存档根目录里同时躺着成就档（achievements.json）、成就时刻表与本机脏数据；
            // 只有「不以 achievement 开头」的 .dat/.json 才是可读存档。
            if (name.StartsWith("achievement", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var extension = Path.GetExtension(path);
            if (!SaveFileFormat.TryFormatOfExtension(extension, out var format))
            {
                continue;
            }

            try
            {
                found.Add(new SaveSlot(name, format));
            }
            catch (ArgumentException)
            {
                // 档名含非法字符（非本程序写入的文件）⇒ 跳过而不是让整页崩掉。
            }
        }

        return found
            .OrderByDescending(slot => File.Exists(saves.FilePathOf(slot)) ? File.GetLastWriteTimeUtc(saves.FilePathOf(slot)) : DateTime.MinValue)
            .ToList();
    }

    /// <summary>读档预览：加载该档后抽取可比较的概览，供读档对话框筛选（不写入、不推进）。</summary>
    public static string DescribeSave(SaveSlot slot)
    {
        SaveService saves;
        GameState state;
        try
        {
            saves = new SaveService();
            state = saves.Load(slot);
        }
        catch (SaveFileException ex)
        {
            return $"（无法读取：{ex.Message}）";
        }
        catch (IOException ex)
        {
            return $"（无法读取：{ex.Message}）";
        }

        var family = state.Family;
        var economy = state.Economy;
        var registered = family.RegisteredMembers.Count;
        var archived = family.ArchivedMembers.Count;
        var degrees = family.Members
            .GroupBy(p => p.CurrentDegree)
            .OrderByDescending(g => g.Key)
            .Select(g => $"{Display.Degree(g.Key)} {g.Count()}");
        var highest = family.Members.Count == 0 ? DegreeLevel.BaiShen : family.Members.Max(p => p.CurrentDegree);
        var path = saves.FilePathOf(slot);
        var written = File.Exists(path)
            ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
            : "（文件不存在）";

        return string.Join(Environment.NewLine,
            $"{family.Name}家 ｜ 出身 {Display.Origin(state.Origin)} ｜ 难度 {Display.Difficulty(state.Difficulty)}",
            $"进度：{state.CurrentDate} ｜ 在册 {registered} 人 ｜ 归档 {archived} 人 ｜ 最高功名 {Display.Degree(highest)}",
            $"功名分布：{(degrees.Any() ? string.Join('、', degrees) : "（无）")}",
            $"总资产 {Display.Money(TotalAssetsCalculator.Of(economy))} ｜ 现银 {Display.Money(economy.Treasury.Cash)} ｜ 储蓄 {Display.Money(economy.Treasury.Savings)} ｜ 商本 {Display.Money(economy.Treasury.MerchantCapital)}",
            $"负债 {Display.Money(economy.Treasury.Loan.Total)} ｜ 田 {economy.Holdings.FarmlandMu} 亩 ｜ 农舍 {economy.Holdings.RuralHouses} ｜ 城宅 {economy.Holdings.UrbanHouses} ｜ 商铺 {economy.Holdings.Shops}",
            $"成就 {AchievementQuery.CompletedCount(state)}/68 ｜ 米价 {economy.GrainPriceIndex.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}",
            $"存档文件：{path}",
            $"最后写入（本机时间）：{written}");
    }
}
