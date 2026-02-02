using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordVault.Converters;

public static class BooleanConverters
{
    public static readonly IValueConverter NullOrString =
        new FuncValueConverter<bool?, string?, string?>((value, param) => (value ?? false) ? null : param);

    public static readonly IValueConverter SidebarPadding =
            new FuncValueConverter<bool?, Thickness>(value => (value ?? false) ? new Thickness(16, 8) : new Thickness(8));

    public static readonly IValueConverter SidebarTogglerHorizontalAlignment =
        new FuncValueConverter<bool?, HorizontalAlignment>(value =>
            (value ?? false) ? HorizontalAlignment.Right : HorizontalAlignment.Center);
}