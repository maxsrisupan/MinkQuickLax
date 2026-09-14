using System.Windows.Data;
using System.Windows.Markup;

namespace MinkQuickLax.Services;

/// <summary>XAML shorthand for a live-updating localized string: <c>{services:Text Scanner_Title}</c>.</summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TextExtension(string key) : MarkupExtension
{
    public TextExtension()
        : this("")
    {
    }

    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]") { Source = Localizer.Instance, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
