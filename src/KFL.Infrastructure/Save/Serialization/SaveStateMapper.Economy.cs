// SaveStateMapper 的经济部分：FamilyEconomy ↔ FamilyEconomyDto。

using KFL.Core.Entities;
using KFL.Core.ValueObjects;

namespace KFL.Infrastructure.Save.Serialization;

public static partial class SaveStateMapper
{
    private static FamilyEconomy BuildEconomy(FamilyEconomyDto dto)
    {
        var treasuryDto = Required(dto.Treasury, "treasury");
        var loanDto = Required(treasuryDto.Loan, "loan");

        var loan = new Loan
        {
            Principal = Money.FromWen(loanDto.Principal),
            MonthsSinceInterest = loanDto.MonthsSinceInterest,
        };

        // 欠息只增不减：AccrueInterest 是唯一入口，故此处按「补记」复原。
        if (loanDto.AccruedInterest != 0m)
        {
            loan.AccrueInterest(Money.FromWen(loanDto.AccruedInterest));
        }

        // 储蓄利率与其年份互为条件：年份 MUST 先落，否则年份为 null 时利率写不进去。
        var treasury = new Treasury(
            Money.FromWen(treasuryDto.Cash),
            Money.FromWen(treasuryDto.Savings),
            Money.FromWen(treasuryDto.MerchantCapital))
        {
            Loan = loan,
            SavingsRateYear = treasuryDto.SavingsRateYear,
            SavingsRate = treasuryDto.SavingsRate,
        };

        var holdingsDto = Required(dto.Holdings, "holdings");
        var holdings = new Holdings
        {
            FarmlandMu = holdingsDto.FarmlandMu,
            RuralHouses = holdingsDto.RuralHouses,
            UrbanHouses = holdingsDto.UrbanHouses,
            Shops = holdingsDto.Shops,
        };

        var ledger = new Ledger();
        foreach (var entry in Required(dto.Ledger, "ledger"))
        {
            ledger.Append(new LedgerEntry(
                new GameDate(entry.Year, entry.Month),
                entry.PersonId is { } personId ? new PersonId(personId) : null,
                entry.Category,
                Money.FromWen(entry.AmountWen)));
        }

        var economy = new FamilyEconomy(
            dto.LivingStandard,
            new GrainPriceIndex(dto.GrainPriceIndex),
            treasury,
            holdings,
            ledger)
        {
            PendingLivingStandard = dto.PendingLivingStandard,
        };

        var famineDto = Required(dto.Famine, "famine");
        economy.RestoreFamine(famineDto.Stage, famineDto.ElapsedMonths);

        return economy;
    }

    private static FamilyEconomyDto ToDto(FamilyEconomy economy) => new()
    {
        LivingStandard = economy.LivingStandard,
        PendingLivingStandard = economy.PendingLivingStandard,
        GrainPriceIndex = economy.GrainPriceIndex.Value,
        Treasury = new TreasuryDto
        {
            Cash = economy.Treasury.Cash.Wen,
            Savings = economy.Treasury.Savings.Wen,
            MerchantCapital = economy.Treasury.MerchantCapital.Wen,
            Loan = new LoanDto
            {
                Principal = economy.Treasury.Loan.Principal.Wen,
                AccruedInterest = economy.Treasury.Loan.AccruedInterest.Wen,
                MonthsSinceInterest = economy.Treasury.Loan.MonthsSinceInterest,
            },
            SavingsRate = economy.Treasury.SavingsRate,
            SavingsRateYear = economy.Treasury.SavingsRateYear,
        },
        Holdings = new HoldingsDto
        {
            FarmlandMu = economy.Holdings.FarmlandMu,
            RuralHouses = economy.Holdings.RuralHouses,
            UrbanHouses = economy.Holdings.UrbanHouses,
            Shops = economy.Holdings.Shops,
        },
        Ledger = [.. economy.Ledger.Entries.Select(entry => new LedgerEntryDto
        {
            Year = entry.Date.Year,
            Month = entry.Date.Month,
            PersonId = entry.PersonId?.Value,
            Category = entry.Category,
            AmountWen = entry.Amount.Wen,
        })],
        Famine = new FamineDto
        {
            Stage = economy.Famine.Stage,
            ElapsedMonths = economy.Famine.ElapsedMonths,
        },
    };
}
