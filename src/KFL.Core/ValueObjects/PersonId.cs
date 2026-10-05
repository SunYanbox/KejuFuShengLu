namespace KFL.Core.ValueObjects;

/// <summary>
/// 成员的强类型标识。<c>Person</c> 之间的全部引用都用它，杜绝裸传 <c>Guid</c>。
/// </summary>
public readonly record struct PersonId
{
    /// <summary>构造并校验。</summary>
    /// <param name="value">非空 <see cref="Guid"/>。</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> 为 <see cref="Guid.Empty"/>。</exception>
    public PersonId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("PersonId 不允许 Guid.Empty。", nameof(value));
        }

        Value = value;
    }

    /// <summary>底层标识。</summary>
    public Guid Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
