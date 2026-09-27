using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace SmartFoldering.Controls;

public class AccessibleTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<TextBlock>("PART_Watermark") is { } watermark)
            watermark.Opacity = 1;
    }
}
