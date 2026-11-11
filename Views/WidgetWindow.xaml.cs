using System.Windows;
using System.Windows.Input;
using CrealitySparkXWidget.Helpers;
using CrealitySparkXWidget.ViewModels;

namespace CrealitySparkXWidget.Views;

public partial class WidgetWindow : Window
{
    private bool _isExplicitClosing = false;

    public WidgetWindow(WidgetViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Allow dragging from header
        MouseDown += WidgetWindow_MouseDown;
    }

    private void WidgetWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
        {
            // Only drag if not clicking buttons or textboxes
            if (e.OriginalSource is not System.Windows.Controls.Button &&
                e.OriginalSource is not System.Windows.Controls.TextBox &&
                e.OriginalSource is not System.Windows.Controls.ComboBox &&
                e.OriginalSource is not System.Windows.Controls.CheckBox)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }

    public void ShowAboveTaskbar()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        // Reposition right above the taskbar at current cursor / tray position
        UpdateLayout();
        TaskbarPositioner.PositionAboveTaskbar(this);

        Opacity = 0;
        Show();
        Activate();
        Focus();

        var anim = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };
        BeginAnimation(OpacityProperty, anim);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // User requested: "appena il focus esce, la finestra viene automaticamente nascosta"
        if (!_isExplicitClosing && IsVisible)
        {
            Hide();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExplicitClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    public void ForceClose()
    {
        _isExplicitClosing = true;
        Close();
    }
}

