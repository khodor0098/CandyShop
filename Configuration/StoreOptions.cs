namespace CandyShop.Configuration;

/// <summary>
/// Store details printed on the invoice, bound from the "Store" configuration section.
/// </summary>
public class StoreOptions
{
    public const string SectionName = "Store";

    public string Name { get; set; } = "CANDY VAN";

    /// <summary>Optional second line under the store name (phone number, licence, slogan).</summary>
    public string? Subtitle { get; set; }

    /// <summary>Optional closing line printed at the bottom of the receipt.</summary>
    public string FooterMessage { get; set; } = "Thank you!";
}
