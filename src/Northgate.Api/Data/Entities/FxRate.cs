using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Northgate.Api.Data.Entities;

/// <summary>
/// Historical FX quotes. Maps <c>public.fx_rates</c>. Rates are kept per
/// <c>as_of</c> instant rather than overwritten, so a settled transaction can
/// point at the exact quote that was applied.
/// </summary>
[Table("fx_rates")]
[Index(nameof(BaseCode), nameof(QuoteCode), nameof(AsOf), IsUnique = true, Name = "fx_rates_unique_quote")]
[Index(nameof(QuoteCode), Name = "fx_rates_quote_code")]
public sealed class FxRate
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    [Column("base_code", TypeName = "char(3)")]
    [StringLength(3, MinimumLength = 3)]
    public string BaseCode { get; set; } = string.Empty;

    [Required]
    [Column("quote_code", TypeName = "char(3)")]
    [StringLength(3, MinimumLength = 3)]
    public string QuoteCode { get; set; } = string.Empty;

    [Column("rate")]
    [Precision(18, 8)]
    public decimal Rate { get; set; }

    [Column("as_of")]
    public DateTime AsOf { get; set; }

    [ForeignKey(nameof(BaseCode))]
    public Currency? BaseCurrency { get; set; }

    [ForeignKey(nameof(QuoteCode))]
    public Currency? QuoteCurrency { get; set; }
}
