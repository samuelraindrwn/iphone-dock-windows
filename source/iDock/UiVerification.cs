using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace iDock;

/// <summary>Hardware-free checks for the real WPF layout and custom control templates.</summary>
internal static class UiVerification
{
    public static void Run(Action<bool, string> check)
    {
        // Preview skips engine startup, logs, timers, and settings persistence. Never invoke
        // session buttons or Reset here: these checks only navigate and inspect the UI.
        var window = new MainWindow(preview: true)
        {
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
        };
        try
        {
            window.Show();
            Settle(window);
            T Find<T>(string name) where T : FrameworkElement => window.FindName(name) as T
                ?? throw new InvalidOperationException($"Missing UI control: {name}");

            var scroll = Find<ScrollViewer>("WorkspaceScrollViewer");
            var slider = Find<Slider>("SensitivitySlider");
            var orientation = Find<ComboBox>("OrientationCombo");
            var logs = Find<TextBox>("LogBox");
            var actionButtons = new[] { "MirrorButton", "ControlButton", "DiagnoseButton",
                "StopButton", "ResetSensitivityButton", "OpenLogsButton" }.Select(Find<Button>).ToArray();
            var footerLinks = new[] { "RepositoryButton", "IssuesButton" }.Select(Find<Button>).ToArray();
            var footer = Find<Border>("AppFooter");
            var sectionNames = new[] { "GuideSection", "OverviewSection", "SettingsSection", "DiagnosticsSection" };
            var sections = sectionNames.Select(Find<FrameworkElement>).ToArray();

            scroll.ApplyTemplate();
            var scrollbar = scroll.Template.FindName("PART_VerticalScrollBar", scroll) as ScrollBar;
            scrollbar?.ApplyTemplate();
            var scrollTrack = scrollbar?.Template.FindName("PART_Track", scrollbar) as Track;
            check(scrollbar is not null && scrollbar.ActualWidth > 0 && scrollbar.ActualWidth <= 10
                && scrollTrack?.Thumb is not null && scrollTrack.Thumb.ActualHeight > 0,
                "Workspace uses a slim native scrollbar with a draggable thumb");
            scroll.ScrollToTop();
            Settle(window);
            ScrollBar.PageDownCommand.Execute(null, scrollbar!);
            Settle(window);
            check(scroll.VerticalOffset > 0,
                "Slim scrollbar preserves the native page-down scrolling command");
            scroll.ScrollToTop();
            Settle(window);

            check(!Find<Button>("StopButton").IsEnabled
                && actionButtons.Where(button => button.Name != "StopButton").All(button => button.IsEnabled),
                "Redesigned UI preserves idle session button availability");
            check(actionButtons.All(button => button.Focusable && button.IsTabStop),
                "Session and settings buttons remain keyboard reachable");
            check(slider.Focusable && slider.IsTabStop && orientation.Focusable && orientation.IsTabStop,
                "Pointer settings remain keyboard reachable");
            check(!string.IsNullOrWhiteSpace(AutomationProperties.GetName(slider))
                && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(orientation)),
                "Custom pointer controls retain their accessibility names");

            slider.ApplyTemplate();
            var track = slider.Template.FindName("PART_Track", slider) as Track;
            check(track is not null && track.Thumb is not null && track.Thumb.ActualWidth > 0,
                "Custom sensitivity template provides a rendered draggable PART_Track thumb");
            check(track?.DecreaseRepeatButton?.Command == Slider.DecreaseLarge
                && track?.IncreaseRepeatButton?.Command == Slider.IncreaseLarge,
                "Sensitivity track retains decrement and increment commands");
            check(slider.Minimum == 0.25 && slider.Maximum == 3 && slider.SmallChange == 0.05
                && slider.LargeChange == 0.25 && slider.IsSnapToTickEnabled && slider.IsMoveToPointEnabled,
                "Custom slider preserves sensitivity limits and interaction increments");
            slider.Value = 1;
            Slider.IncreaseSmall.Execute(null, slider);
            check(Math.Abs(slider.Value - 1.05) < 0.00001,
                "Keyboard sensitivity command still changes the custom slider");

            orientation.ApplyTemplate();
            check(orientation.Template.FindName("PART_Popup", orientation) is Popup,
                "Custom orientation template retains the required popup part");
            check(orientation.SelectedValuePath == "Tag"
                && orientation.Items.Cast<ComboBoxItem>().Select(item => item.Tag as string)
                    .SequenceEqual(new[] { "0", "90", "270", "180" }),
                "Orientation options retain all four backend rotation values");
            orientation.SelectedValue = "270";
            check(orientation.SelectedItem is ComboBoxItem { Tag: "270" },
                "Custom orientation selector changes the selected backend value");
            check(logs.IsReadOnly && logs.IsReadOnlyCaretVisible && logs.Visibility == Visibility.Visible
                && logs.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
                "Diagnostic log remains visible, selectable, read-only, and scrollable");

            check(Find<StackPanel>("PageSections").Children.Cast<FrameworkElement>()
                    .Select(section => section.Name).SequenceEqual(sectionNames)
                && window.FindName("OverviewNav") is null,
                "Single-page layout starts with the guide and has no sidebar navigation");
            check(footerLinks.All(button => button.Focusable && button.IsTabStop)
                && footerLinks.Select(button => button.Tag as string).SequenceEqual(new[] { "repository", "issues" }),
                "Footer project links remain keyboard reachable and map to known destinations");
            check(actionButtons.All(HasFocusCue), "Custom button templates retain a keyboard focus cue");
            check(HasFocusCue(slider), "Custom slider retains a keyboard focus cue");
            check(HasFocusCue(orientation), "Custom orientation selector retains a keyboard focus cue");
            check(footerLinks.All(HasFocusCue), "Footer link buttons retain a keyboard focus cue");

            foreach (var size in new[] { new Size(900, 680), new Size(1100, 820) })
            {
                window.Width = size.Width;
                window.Height = size.Height;
                scroll.ScrollToTop();
                Settle(window);
                var label = $"{size.Width:0}x{size.Height:0}";
                check(scroll.ViewportWidth > 0 && scroll.ViewportHeight > 0
                    && scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
                    && scroll.ExtentWidth <= scroll.ViewportWidth + 1,
                    $"Workspace fits without horizontal scrolling at {label}");
                check(sections.All(section => section.ActualWidth > 0 && section.ActualHeight > 0
                    && FitsHorizontally(section, scroll)),
                    $"All four content sections fit the workspace width at {label}");
                var workspaceControls = actionButtons.Where(button => button.Name != "StopButton")
                    .Cast<FrameworkElement>().Concat(new FrameworkElement[] { slider, orientation, logs });
                check(workspaceControls.All(control => control.ActualWidth > 0 && control.ActualHeight > 0
                    && FitsHorizontally(control, scroll)),
                    $"Action buttons and pointer settings are not horizontally clipped at {label}");
                check(footerLinks.All(button => button.ActualWidth > 0 && button.ActualHeight >= 28)
                    && footer.ActualHeight > 0 && logs.ActualHeight >= 80
                    && FitsHorizontally(Find<TextBlock>("ShortcutHelp"), scroll),
                    $"Footer links, shortcut help, and diagnostic log retain usable dimensions at {label}");

                for (var index = 0; index < sections.Length; index++)
                {
                    sections[index].BringIntoView();
                    Settle(window);
                    var top = sections[index].TransformToAncestor(scroll).Transform(new Point()).Y;
                    var footerTop = footer.TransformToAncestor(window).Transform(new Point()).Y;
                    check(top >= -1 && top < scroll.ViewportHeight
                        && footerTop >= 0 && footerTop + footer.ActualHeight <= window.ActualHeight,
                        $"Scrolling reveals {sectionNames[index]} while the footer stays visible at {label}");
                }
            }

            window.Width = 900;
            window.Height = 680;
            Find<TextBlock>("MirrorStatus").Text = "Penerima AirPlay belum tersambung. Periksa jaringan Wi-Fi dan pilih uxplay-windows pada perangkat, kemudian coba kembali.";
            Find<TextBlock>("ControlStatus").Text = "Pemeriksaan Bluetooth terhalang akses Windows. Lihat diagnostik dan pastikan adapter mendukung koneksi mouse serta keyboard perangkat.";
            Settle(window);
            check(new[] { "MirrorStatus", "ControlStatus" }.Select(Find<TextBlock>)
                .All(text => text.TextWrapping == TextWrapping.Wrap && text.ActualHeight > text.FontSize * 1.5
                    && FitsHorizontally(text, scroll)),
                "Long connection messages wrap instead of clipping at minimum window width");
        }
        finally { window.Close(); }
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        window.UpdateLayout();
    }

    private static bool FitsHorizontally(FrameworkElement element, Visual ancestor)
    {
        var bounds = element.TransformToAncestor(ancestor).TransformBounds(new Rect(element.RenderSize));
        return bounds.Left >= -1 && ancestor is FrameworkElement frame && bounds.Right <= frame.ActualWidth + 1;
    }

    private static bool HasFocusCue(Control control)
    {
        if (control.FocusVisualStyle is not null) return true;
        static bool IsFocus(DependencyProperty property) => property == UIElement.IsKeyboardFocusedProperty
            || property == UIElement.IsKeyboardFocusWithinProperty;
        static bool HasTrigger(IEnumerable<TriggerBase> triggers) => triggers.Any(trigger => trigger switch
        {
            Trigger value => IsFocus(value.Property) && value.Setters.Count > 0,
            MultiTrigger value => value.Conditions.Cast<System.Windows.Condition>().Any(condition => IsFocus(condition.Property))
                && value.Setters.Count > 0,
            _ => false
        });
        if (control.Template is not null && HasTrigger(control.Template.Triggers.Cast<TriggerBase>())) return true;
        for (var style = control.Style; style is not null; style = style.BasedOn)
            if (HasTrigger(style.Triggers.Cast<TriggerBase>())) return true;
        return false;
    }
}
