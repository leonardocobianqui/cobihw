namespace CobiHw.App;

public sealed record PercentText(string Number, string Sign)
{
    public string Text => Number + Sign;
}
