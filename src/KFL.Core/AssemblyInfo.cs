using System.Runtime.CompilerServices;

// 004-save-system（R-03）给 KFL.Core 开的**唯一**程序集级面。
//
// 存档复原需要 Core 的既有 internal 写入通道：
//   - Person.SetGeneration / SetSpouse / AddFormerSpouse（001 起即为 internal）
//   - Family.CaptureOutsiderBookkeeping / RestoreOutsiderBookkeeping（004 新增）
//   - FamilyEconomy.RestoreFamine（004 新增）
// 这些簿记决定**后续**推演（外来者辈分还能不能再变、饥馑已持续月数），不落盘即静默失真。
//
// MUST NOT 在此放行任何其它程序集；特别是 tests/KFL.Tests——测试经公开 API 与
// ISaveService 往返观察这些面，不经反射、也不直连 Core 的 internal。
[assembly: InternalsVisibleTo("KFL.Infrastructure")]
