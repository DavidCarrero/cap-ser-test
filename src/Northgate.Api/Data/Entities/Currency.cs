using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Northgate.Api.Data.Entities;

/// <summary>ISO 4217 lookup. Maps <c>public.currencies</c>.</summary>
[Table("currencies")]
public sealed class Currency
{
    [Key]
    [Column("code", TypeName = "char(3)")]
    [StringLength(3, MinimumLength = 3)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Decimal places the currency subdivides into; JPY is 0.</summary>
    [Column("minor_unit")]
    public short MinorUnit { get; set; }

    [InverseProperty(nameof(Transaction.Currency))]
    public ICollection<Transaction> Transactions { get; } = new List<Transaction>();
}
