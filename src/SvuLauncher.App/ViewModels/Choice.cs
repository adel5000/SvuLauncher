namespace SvuLauncher.App.ViewModels;

/// <summary>A selectable value with its localized label, for combo boxes.</summary>
public sealed record Choice<T>(T Value, string Label)
{
    public override string ToString() => Label;
}
