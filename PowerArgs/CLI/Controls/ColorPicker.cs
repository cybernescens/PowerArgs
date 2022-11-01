namespace PowerArgs.Cli;

public class ColorPicker : ProtectedConsolePanel
{
    public ColorPicker()
    {
        var dropdown = ProtectedPanel
            .Add(
                new Dropdown(
                    Enums.GetEnumValues<ConsoleColor>().Select(
                        c => new DialogOption(c.ToString(), (RGB)c, c.ToString().ToConsoleString()))))
            .Fill();

        dropdown.SubscribeForLifetime(this, nameof(dropdown.Value), () => Value = (RGB)dropdown.Value!.Value);

        SubscribeForLifetime(this, nameof(Value), () => dropdown.Value = dropdown.Options.Single(o => o.Value.Equals(Value)));
    }

    public RGB Value
    {
        get => Get<RGB>();
        set => Set(value);
    }
}