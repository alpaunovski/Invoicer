namespace InvoiceDesk.Helpers;

/// <summary>
/// Controls how currencies are shown during and after the euro transition.
/// dualCurrencyEnabled keeps EUR as a secondary, display-only currency alongside BGN.
/// eurOnlyMode is reserved for the future switch; do not activate now.
/// </summary>
public class CurrencyDisplayOptions
{
    public bool DualCurrencyEnabled { get; set; } = true;

    // Future-proof flag: once the transition ends, EUR may become primary-only.
    public bool EurOnlyMode { get; set; } = false;
}
