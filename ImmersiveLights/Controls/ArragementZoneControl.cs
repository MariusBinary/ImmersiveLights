using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ImmersiveLights.Core;
using ImmersiveLights.Helpers;
using ImmersiveLights.Structures;

namespace ImmersiveLights.Controls
{
    public class ArrangementZoneControl : Control
    {
        #region Properties
        public ArrangementSide Zone
        {
            get { return (ArrangementSide)GetValue(ZoneProperty); }
            set { SetValue(ZoneProperty, value); }
        }

        public static readonly DependencyProperty ZoneProperty =
            DependencyProperty.Register("Zone", typeof(ArrangementSide), typeof(ArrangementZoneControl),
                new PropertyMetadata(new ArrangementSide(), null));

        public int RealIndex
        {
            get { return (int)GetValue(RealIndexProperty); }
            set { SetValue(RealIndexProperty, value); }
        }

        public static readonly DependencyProperty RealIndexProperty =
            DependencyProperty.Register("RealIndex", typeof(int), typeof(ArrangementZoneControl),
                new PropertyMetadata(0, null));

        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register("Index", typeof(int), typeof(ArrangementZoneControl),
                new PropertyMetadata(0, new PropertyChangedCallback(OnIndexPropertyChanged)));

        private static void OnIndexPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ArrangementZoneControl control = target as ArrangementZoneControl;
            control.UpdateZone((int)e.NewValue);
        }

        public bool Enabled
        {
            get { return (bool)GetValue(EnabledProperty); }
            set { SetValue(EnabledProperty, value); }
        }

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.Register("Enabled", typeof(bool), typeof(ArrangementZoneControl),
                new PropertyMetadata(false, new PropertyChangedCallback(OnEnabledPropertyChanged)));

        private static void OnEnabledPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ArrangementZoneControl control = target as ArrangementZoneControl;
            control.UpdateEnabled((bool)e.NewValue);
        }

        public bool Highlighted
        {
            get { return (bool)GetValue(HighlightedProperty); }
            set { SetValue(HighlightedProperty, value); }
        }

        public static readonly DependencyProperty HighlightedProperty =
            DependencyProperty.Register("Highlighted", typeof(bool), typeof(ArrangementZoneControl),
                new PropertyMetadata(false, new PropertyChangedCallback(OnHighlightedPropertyChanged)));


        private static void OnHighlightedPropertyChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            ArrangementZoneControl control = target as ArrangementZoneControl;
            control.UpdateHighlight((bool)e.NewValue);
        }

        private static readonly RoutedEvent OnEnabledChangedEvent =
            EventManager.RegisterRoutedEvent("OnEnabledChanged", RoutingStrategy.Bubble,
                typeof(RoutedPropertyChangedEventHandler<int[]>), typeof(ArrangementZoneControl));

        public event RoutedPropertyChangedEventHandler<int[]> OnEnabledChanged
        {
            add { AddHandler(OnEnabledChangedEvent, value); }
            remove { RemoveHandler(OnEnabledChangedEvent, value); }
        }

        #endregion

        #region Variables
        private int ledIndex = 0;
        private bool highlighted = false;
        private bool enabled = false;
        private Border _disabledZone;
        private Path _disabledIcon;
        private TextBlock _ledIndex;
        private CheckBox _switch;
        private bool controlLoaded = false;
        #endregion

        static ArrangementZoneControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ArrangementZoneControl), 
                new FrameworkPropertyMetadata(typeof(ArrangementZoneControl)));
        }

        public override void OnApplyTemplate()
        {
            this._disabledZone = this.GetTemplateChild("PART_DisabledZone") as Border;
            this._disabledIcon = this.GetTemplateChild("PART_DisabledIcon") as Path;
            this._ledIndex = this.GetTemplateChild("PART_LedIndex") as TextBlock;
            this._switch = this.GetTemplateChild("PART_Switch") as CheckBox;
            this.MouseLeftButtonUp += ArrangementZoneControl_MouseLeftButtonUp;
            this._switch.Click += _switch_Click;

            this.controlLoaded = true;

            UpdateZone(ledIndex);
            UpdateHighlight(highlighted);
            UpdateEnabled(enabled);

            base.OnApplyTemplate();
        }

        private void ArrangementZoneControl_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Enabled = !Enabled;

            var value = new int[] { (int)Zone, RealIndex, (Enabled ? 1 : 0) };
            var args = new RoutedPropertyChangedEventArgs<int[]>(value, value);
            args.RoutedEvent = OnEnabledChangedEvent;
            RaiseEvent(args);
        }

        private void _switch_Click(object sender, RoutedEventArgs e)
        {
            Enabled = _switch.IsChecked.Value;

            var value = new int[] { (int)Zone, RealIndex, (Enabled ? 1 : 0) };
            var args = new RoutedPropertyChangedEventArgs<int[]>(value, value);
            args.RoutedEvent = OnEnabledChangedEvent;
            RaiseEvent(args);
        }

        public void UpdateHighlight(bool highlighted)
        {
            this.highlighted = highlighted;
            if (!controlLoaded) return;
            _switch.Visibility = highlighted ? Visibility.Visible : Visibility.Collapsed;

            UpdateBackground();
        }

        public void UpdateEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (!controlLoaded) return;
            _switch.IsChecked = enabled;

            UpdateBackground();
        }

        public void UpdateZone(int ledIndex)
        {
            this.ledIndex = ledIndex;
            if (!controlLoaded) return;
            _ledIndex.Text = ledIndex.ToString();
        }

        private void UpdateBackground()
        {
            if (highlighted)
            {
                this.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#33DC9C17"));
                this.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FFDC9C17"));

                if (enabled)
                {
                    _disabledZone.Background = Brushes.Transparent;
                    _disabledIcon.Visibility = Visibility.Collapsed;
                    _ledIndex.Visibility = Visibility.Visible;
                }
                else
                {
                    var theme1 = (DrawingBrush)FindResource("HatchBrushEnabled");
                    _disabledZone.Background = theme1;
                    _disabledIcon.Visibility = Visibility.Visible;
                    _ledIndex.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                this.Background = (SolidColorBrush)(new BrushConverter().ConvertFrom("#33808080"));
                this.BorderBrush = (SolidColorBrush)(new BrushConverter().ConvertFrom("#FF343434"));

                if (enabled)
                {
                    _disabledZone.Background = Brushes.Transparent;
                    _disabledIcon.Visibility = Visibility.Collapsed;
                    _ledIndex.Visibility = Visibility.Visible;
                }
                else
                {
                    var theme2 = (DrawingBrush)FindResource("HatchBrushDisabled");
                    _disabledZone.Background = theme2;
                    _disabledIcon.Visibility = Visibility.Visible;
                    _ledIndex.Visibility = Visibility.Collapsed;
                }
            }
        }
    }
}
